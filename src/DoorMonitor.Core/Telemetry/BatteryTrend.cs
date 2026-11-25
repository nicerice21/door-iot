namespace DoorMonitor.Core.Telemetry;

/// <summary>
/// 電池電圧の履歴から求めた 1 日あたりの変化量と、しきい値までの残り日数。
/// サンプルが少ない、または電圧が下がっていない場合は推定しない（null）。
/// </summary>
public sealed record BatteryTrend(double? MvPerDay, int? EstimatedDaysRemaining, int ThresholdMv)
{
    /// <summary>TWELITE の動作下限（2.3V）。docs/requirements.md 3.2 を参照。</summary>
    public const int DefaultThresholdMv = 2300;

    /// <summary>回帰に必要な最低サンプル数（1 時間 1 件で 24 時間分）。</summary>
    public const int MinimumSamples = 24;

    public static BatteryTrend From(IReadOnlyList<BatterySample> samples, int thresholdMv = DefaultThresholdMv)
    {
        ArgumentNullException.ThrowIfNull(samples);

        var unknown = new BatteryTrend(null, null, thresholdMv);
        if (samples.Count < MinimumSamples)
        {
            return unknown;
        }

        var first = samples[0].SampledAt;
        var hours = new double[samples.Count];
        var values = new double[samples.Count];
        var totalHours = 0.0;

        for (var i = 0; i < samples.Count; i++)
        {
            hours[i] = (samples[i].SampledAt - first).TotalHours;
            values[i] = samples[i].BatteryMv;
        }

        totalHours = hours[^1];
        if (totalHours < 24)
        {
            return unknown;
        }

        var meanX = hours.Average();
        var meanY = values.Average();
        var numerator = 0.0;
        var denominator = 0.0;

        for (var i = 0; i < hours.Length; i++)
        {
            var dx = hours[i] - meanX;
            numerator += dx * (values[i] - meanY);
            denominator += dx * dx;
        }

        if (denominator <= 0)
        {
            return unknown;
        }

        var mvPerDay = Math.Round(numerator / denominator * 24, 1);
        if (mvPerDay >= 0)
        {
            // 横ばい・上昇傾向では寿命を推定しない（充電や交換の可能性がある）。
            return new BatteryTrend(mvPerDay, null, thresholdMv);
        }

        var remainingMv = values[^1] - thresholdMv;
        var days = remainingMv <= 0
            ? 0
            : (int)Math.Round(remainingMv / -mvPerDay, MidpointRounding.AwayFromZero);

        return new BatteryTrend(mvPerDay, days, thresholdMv);
    }
}
