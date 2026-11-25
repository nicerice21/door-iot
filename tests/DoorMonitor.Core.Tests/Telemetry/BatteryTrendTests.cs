using DoorMonitor.Core.Telemetry;
using Xunit;

namespace DoorMonitor.Core.Tests.Telemetry;

public sealed class BatteryTrendTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 20, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void 低下傾向から1日あたりの変化量と残り日数を求める()
    {
        // 1 時間に 1mV ずつ下がる（24mV/日）。48 時間分。
        var samples = Enumerable.Range(0, 49)
            .Select(hour => CreateSample(3000 - hour, hour))
            .ToArray();

        var trend = BatteryTrend.From(samples);

        Assert.Equal(-24.0, trend.MvPerDay);
        Assert.Equal(2300, trend.ThresholdMv);
        // (2952 - 2300) / 24 = 27.17 → 27 日
        Assert.Equal(27, trend.EstimatedDaysRemaining);
    }

    [Fact]
    public void サンプルが少なければ推定しない()
    {
        var samples = Enumerable.Range(0, 10)
            .Select(hour => CreateSample(3000 - hour, hour))
            .ToArray();

        var trend = BatteryTrend.From(samples);

        Assert.Null(trend.MvPerDay);
        Assert.Null(trend.EstimatedDaysRemaining);
    }

    [Fact]
    public void サンプルが24時間分に満たなければ推定しない()
    {
        var samples = Enumerable.Range(0, 24)
            .Select(hour => CreateSample(3000 - hour, hour))
            .ToArray();

        var trend = BatteryTrend.From(samples);

        Assert.Null(trend.EstimatedDaysRemaining);
    }

    [Fact]
    public void 横ばいなら残り日数を出さない()
    {
        var samples = Enumerable.Range(0, 48)
            .Select(hour => CreateSample(3000, hour))
            .ToArray();

        var trend = BatteryTrend.From(samples);

        Assert.Equal(0.0, trend.MvPerDay);
        Assert.Null(trend.EstimatedDaysRemaining);
    }

    [Fact]
    public void 上昇傾向なら残り日数を出さない()
    {
        var samples = Enumerable.Range(0, 48)
            .Select(hour => CreateSample(2900 + hour, hour))
            .ToArray();

        var trend = BatteryTrend.From(samples);

        Assert.Equal(24.0, trend.MvPerDay);
        Assert.Null(trend.EstimatedDaysRemaining);
    }

    [Fact]
    public void しきい値を下回っていれば0日とする()
    {
        var samples = Enumerable.Range(0, 100)
            .Select(hour => CreateSample(2350 - hour, hour))
            .ToArray();

        var trend = BatteryTrend.From(samples);

        Assert.Equal(0, trend.EstimatedDaysRemaining);
    }

    private static BatterySample CreateSample(int batteryMv, int hourOffset) => new()
    {
        DeviceId = "0x8201015A",
        SampledAt = Start.AddHours(hourOffset),
        ObservedAt = Start.AddHours(hourOffset).AddMinutes(30),
        BatteryMv = batteryMv,
    };
}
