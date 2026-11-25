namespace DoorMonitor.Core.Telemetry;

/// <summary>
/// 1 時間単位に丸めた電池電圧のサンプル。同じ時間帯の受信は同じ行へ上書きする。
/// </summary>
public sealed record BatterySample
{
    public required string DeviceId { get; init; }

    /// <summary>時間帯の開始時刻（UTC の毎時 0 分）。</summary>
    public required DateTimeOffset SampledAt { get; init; }

    /// <summary>この時間帯で最後に観測した時刻。</summary>
    public required DateTimeOffset ObservedAt { get; init; }

    public required int BatteryMv { get; init; }

    public int? Lqi { get; init; }

    /// <summary>受信時刻を UTC の 1 時間単位へ切り捨てる。</summary>
    public static DateTimeOffset BucketStart(DateTimeOffset observedAt)
    {
        var utc = observedAt.ToUniversalTime();
        return new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, TimeSpan.Zero);
    }

    /// <summary>テレメトリからサンプルを作る。電圧が含まれない受信では null を返す。</summary>
    public static BatterySample? FromReport(TelemetryReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        if (report.BatteryMv is not { } batteryMv)
        {
            return null;
        }

        return new BatterySample
        {
            DeviceId = report.DeviceId,
            SampledAt = BucketStart(report.ObservedAt),
            ObservedAt = report.ObservedAt,
            BatteryMv = batteryMv,
            Lqi = report.Lqi,
        };
    }
}

/// <summary>返したサンプルから計算する電池電圧の要約。グラフと電池寿命の評価に使う。</summary>
public sealed record BatterySummary(int SampleCount, int? LatestMv, int? MinMv, int? AverageMv)
{
    public static BatterySummary From(IReadOnlyList<BatterySample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);

        if (samples.Count == 0)
        {
            return new BatterySummary(0, null, null, null);
        }

        var total = 0;
        var min = int.MaxValue;
        var latest = samples[^1];

        foreach (var sample in samples)
        {
            total += sample.BatteryMv;
            min = Math.Min(min, sample.BatteryMv);
        }

        return new BatterySummary(
            samples.Count,
            latest.BatteryMv,
            min,
            (int)Math.Round((double)total / samples.Count, MidpointRounding.AwayFromZero));
    }
}
