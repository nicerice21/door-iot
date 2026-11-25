using DoorMonitor.Core.Telemetry;

namespace DoorMonitor.Functions.Dtos;

/// <summary>GET /api/doors/{deviceId}/battery のレスポンス。samples は古い順。</summary>
public sealed record BatteryHistoryResponse(
    string DeviceId,
    DateTimeOffset From,
    DateTimeOffset To,
    IReadOnlyList<BatterySampleDto> Samples,
    BatterySummaryDto Summary,
    BatteryTrendDto Trend);

/// <summary>電池電圧のサンプル 1 件。SampledAt は 1 時間単位に丸めた UTC。</summary>
public sealed record BatterySampleDto(DateTimeOffset SampledAt, int BatteryMv);

/// <summary>返したサンプルから計算した要約。</summary>
public sealed record BatterySummaryDto(int SampleCount, int? LatestMv, int? MinMv, int? AverageMv)
{
    public static BatterySummaryDto From(BatterySummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return new BatterySummaryDto(summary.SampleCount, summary.LatestMv, summary.MinMv, summary.AverageMv);
    }
}

/// <summary>低下傾向と、動作下限までの残り日数の推定。推定できない項目は null。</summary>
public sealed record BatteryTrendDto(double? MvPerDay, int? EstimatedDaysRemaining, int ThresholdMv)
{
    public static BatteryTrendDto From(BatteryTrend trend)
    {
        ArgumentNullException.ThrowIfNull(trend);

        return new BatteryTrendDto(trend.MvPerDay, trend.EstimatedDaysRemaining, trend.ThresholdMv);
    }
}
