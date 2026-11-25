namespace DoorMonitor.Functions.Dtos;

/// <summary>GET /api/doors/{deviceId}/stats のレスポンス。日付は指定したタイムゾーンのもの。</summary>
public sealed record DoorStatsResponse(
    string DeviceId,
    DateTimeOffset From,
    DateTimeOffset To,
    int TzOffsetMinutes,
    int Days,
    int OpenCount,
    int CloseCount,
    long TotalOpenSeconds,
    int AverageOpenSeconds,
    int MaxOpenSeconds,
    IReadOnlyList<DailyUsageDto> Daily,
    IReadOnlyList<int> OpenCountByHour);

/// <summary>1 日分の利用状況。Date は yyyy-MM-dd。</summary>
public sealed record DailyUsageDto(string Date, int OpenCount, long OpenSeconds, int MaxOpenSeconds);
