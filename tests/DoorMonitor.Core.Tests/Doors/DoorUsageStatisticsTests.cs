using DoorMonitor.Core.Doors;
using System.Globalization;
using Xunit;

namespace DoorMonitor.Core.Tests.Doors;

public sealed class DoorUsageStatisticsTests
{
    private static readonly TimeSpan Jst = TimeSpan.FromHours(9);

    [Fact]
    public void 開閉回数と開放時間を集計する()
    {
        var events = new[]
        {
            Open("2026-11-25T09:00:00Z"),
            Closed("2026-11-25T09:02:00Z", openedAt: "2026-11-25T09:00:00Z"),
        };

        var stats = DoorUsageStatistics.Build(
            events,
            new DateOnly(2026, 11, 25),
            new DateOnly(2026, 11, 25),
            Jst);

        Assert.Equal(1, stats.OpenCount);
        Assert.Equal(1, stats.CloseCount);
        Assert.Equal(120, stats.TotalOpenSeconds);
        Assert.Equal(120, stats.AverageOpenSeconds);
        Assert.Equal(120, stats.MaxOpenSeconds);

        var day = Assert.Single(stats.Daily);
        Assert.Equal(new DateOnly(2026, 11, 25), day.Date);
        Assert.Equal(1, day.OpenCount);
        Assert.Equal(120, day.OpenSeconds);
        Assert.Equal(120, day.MaxOpenSeconds);

        // JST 18 時に開いた。
        Assert.Equal(1, stats.OpenCountByHour[18]);
    }

    [Fact]
    public void 指定した期間のすべての日を含める()
    {
        var stats = DoorUsageStatistics.Build(
            [Open("2026-11-25T09:00:00Z")],
            new DateOnly(2026, 11, 23),
            new DateOnly(2026, 11, 25),
            Jst);

        Assert.Equal(3, stats.Daily.Count);
        Assert.Equal(2, stats.Daily.Count(day => day.OpenCount == 0));
    }

    [Fact]
    public void 日付の境界はタイムゾーンで決める()
    {
        // UTC 15:30 は JST では翌日の 0:30。
        var events = new[] { Open("2026-11-25T15:30:00Z") };

        var sameDay = DoorUsageStatistics.Build(
            events, new DateOnly(2026, 11, 25), new DateOnly(2026, 11, 25), Jst);
        var nextDay = DoorUsageStatistics.Build(
            events, new DateOnly(2026, 11, 26), new DateOnly(2026, 11, 26), Jst);

        Assert.Equal(0, sameDay.OpenCount);
        Assert.Equal(1, nextDay.OpenCount);
        Assert.Equal(1, nextDay.OpenCountByHour[0]);
        Assert.Equal(1, nextDay.Daily[0].OpenCount);
    }

    [Fact]
    public void 閉じていない開放は時間に含めない()
    {
        var events = new[] { Open("2026-11-25T09:00:00Z") };

        var stats = DoorUsageStatistics.Build(
            events, new DateOnly(2026, 11, 25), new DateOnly(2026, 11, 25), Jst);

        Assert.Equal(1, stats.OpenCount);
        Assert.Equal(0, stats.CloseCount);
        Assert.Equal(0, stats.TotalOpenSeconds);
        Assert.Equal(0, stats.AverageOpenSeconds);
    }

    [Fact]
    public void 異常な順序の時刻でも負の時間にしない()
    {
        var events = new[]
        {
            Closed("2026-11-25T09:00:00Z", openedAt: "2026-11-25T09:05:00Z"),
        };

        var stats = DoorUsageStatistics.Build(
            events, new DateOnly(2026, 11, 25), new DateOnly(2026, 11, 25), Jst);

        Assert.Equal(1, stats.CloseCount);
        Assert.Equal(0, stats.TotalOpenSeconds);
        Assert.Equal(0, stats.MaxOpenSeconds);
    }

    private static DoorEvent Open(string changedAt) => new()
    {
        DeviceId = "0x8201015A",
        State = DoorState.Open,
        ChangedAt = Parse(changedAt),
    };

    private static DoorEvent Closed(string changedAt, string openedAt) => new()
    {
        DeviceId = "0x8201015A",
        State = DoorState.Closed,
        ChangedAt = Parse(changedAt),
        OpenedAt = Parse(openedAt),
    };

    private static DateTimeOffset Parse(string value)
        => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
