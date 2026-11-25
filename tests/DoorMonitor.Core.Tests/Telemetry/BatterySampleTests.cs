using DoorMonitor.Core.Doors;
using DoorMonitor.Core.Telemetry;
using Xunit;

namespace DoorMonitor.Core.Tests.Telemetry;

public sealed class BatterySampleTests
{
    [Fact]
    public void 受信時刻をUTCの1時間単位に丸める()
    {
        var observedAt = new DateTimeOffset(2026, 11, 25, 18, 47, 31, TimeSpan.FromHours(9));

        var bucket = BatterySample.BucketStart(observedAt);

        Assert.Equal(new DateTimeOffset(2026, 11, 25, 9, 0, 0, TimeSpan.Zero), bucket);
    }

    [Fact]
    public void 電圧が無い受信からはサンプルを作らない()
    {
        var report = CreateReport(batteryMv: null);

        Assert.Null(BatterySample.FromReport(report));
    }

    [Fact]
    public void 受信から1時間分のサンプルを作る()
    {
        var report = CreateReport(batteryMv: 2980, lqi: 120);

        var sample = BatterySample.FromReport(report);

        Assert.NotNull(sample);
        Assert.Equal(report.DeviceId, sample!.DeviceId);
        Assert.Equal(new DateTimeOffset(2026, 11, 25, 9, 0, 0, TimeSpan.Zero), sample.SampledAt);
        Assert.Equal(report.ObservedAt, sample.ObservedAt);
        Assert.Equal(2980, sample.BatteryMv);
        Assert.Equal(120, sample.Lqi);
    }

    [Fact]
    public void サンプルから最新最小平均を計算する()
    {
        var samples = new[]
        {
            CreateSample(3100, 9),
            CreateSample(2950, 10),
            CreateSample(3000, 11),
        };

        var summary = BatterySummary.From(samples);

        Assert.Equal(3, summary.SampleCount);
        Assert.Equal(3000, summary.LatestMv);
        Assert.Equal(2950, summary.MinMv);
        Assert.Equal(3017, summary.AverageMv);
    }

    [Fact]
    public void サンプルが無ければ要約は空になる()
    {
        var summary = BatterySummary.From(Array.Empty<BatterySample>());

        Assert.Equal(0, summary.SampleCount);
        Assert.Null(summary.LatestMv);
        Assert.Null(summary.MinMv);
        Assert.Null(summary.AverageMv);
    }

    private static TelemetryReport CreateReport(int? batteryMv, int? lqi = null) => new()
    {
        GatewayId = "gw-test",
        DeviceId = "0x8201015A",
        LogicalId = 1,
        State = DoorState.Closed,
        BatteryMv = batteryMv,
        Lqi = lqi,
        IsHeartbeat = true,
        ObservedAt = new DateTimeOffset(2026, 11, 25, 18, 47, 31, TimeSpan.FromHours(9)),
    };

    private static BatterySample CreateSample(int batteryMv, int hour) => new()
    {
        DeviceId = "0x8201015A",
        SampledAt = new DateTimeOffset(2026, 11, 25, hour, 0, 0, TimeSpan.Zero),
        ObservedAt = new DateTimeOffset(2026, 11, 25, hour, 30, 0, TimeSpan.Zero),
        BatteryMv = batteryMv,
    };
}
