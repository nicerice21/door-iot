using DoorMonitor.Core.Telemetry;

namespace DoorMonitor.Functions.Dtos;

/// <summary>GET /api/doors/{deviceId}/battery のレスポンス。samples は古い順。</summary>
public sealed record BatteryHistoryResponse(
    string DeviceId,
    DateTimeOffset From,
    DateTimeOffset To,
    IReadOnlyList<BatterySampleDto> Samples,
    BatterySummaryDto Summary);

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
