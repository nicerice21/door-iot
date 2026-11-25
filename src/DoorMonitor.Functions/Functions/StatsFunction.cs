using System.Globalization;
using System.Net;
using DoorMonitor.Core.Doors;
using DoorMonitor.Functions.Dtos;
using DoorMonitor.Functions.Http;
using DoorMonitor.Functions.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace DoorMonitor.Functions.Functions;

/// <summary>
/// GET /api/doors/{deviceId}/stats — 開閉の回数と開放時間を日別・時間帯別に集計する。
/// 日付の境界は tzOffsetMinutes（UTC からの差）で決める。GET /api/doors と同じく認証なしで公開する。
/// </summary>
public sealed class StatsFunction
{
    private const int DefaultDays = 7;
    private const int MaxDays = 90;
    private const int MinTzOffsetMinutes = -720;
    private const int MaxTzOffsetMinutes = 840;

    private readonly IDoorRepository _repository;

    public StatsFunction(IDoorRepository repository)
    {
        _repository = repository;
    }

    [Function("DoorStats")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "doors/{deviceId}/stats")] HttpRequestData request,
        string deviceId,
        CancellationToken cancellationToken)
    {
        var door = await _repository.GetDoorAsync(deviceId, cancellationToken).ConfigureAwait(false);
        if (door is null)
        {
            return await ApiResponses
                .ErrorAsync(request, HttpStatusCode.NotFound, "指定されたドアが見つかりません。", cancellationToken)
                .ConfigureAwait(false);
        }

        if (!TryParseInt(request.Query["days"], DefaultDays, 1, MaxDays, out var days))
        {
            return await ApiResponses
                .ErrorAsync(request, HttpStatusCode.BadRequest, $"days は 1〜{MaxDays} の整数で指定してください。", cancellationToken)
                .ConfigureAwait(false);
        }

        if (!TryParseInt(request.Query["tzOffsetMinutes"], 0, MinTzOffsetMinutes, MaxTzOffsetMinutes, out var tzOffsetMinutes))
        {
            return await ApiResponses
                .ErrorAsync(request, HttpStatusCode.BadRequest, $"tzOffsetMinutes は {MinTzOffsetMinutes}〜{MaxTzOffsetMinutes} の整数で指定してください。", cancellationToken)
                .ConfigureAwait(false);
        }

        var offset = TimeSpan.FromMinutes(tzOffsetMinutes);
        var to = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(to.ToOffset(offset).DateTime);
        var startDate = today.AddDays(-(days - 1));
        var from = new DateTimeOffset(startDate.ToDateTime(TimeOnly.MinValue), offset);

        var events = await _repository
            .GetDoorEventsAsync(deviceId, from, to, cancellationToken)
            .ConfigureAwait(false);

        var stats = DoorUsageStatistics.Build(events, startDate, today, offset);

        var response = new DoorStatsResponse(
            deviceId,
            from,
            to,
            tzOffsetMinutes,
            days,
            stats.OpenCount,
            stats.CloseCount,
            stats.TotalOpenSeconds,
            stats.AverageOpenSeconds,
            stats.MaxOpenSeconds,
            stats.Daily
                .Select(day => new DailyUsageDto(
                    day.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    day.OpenCount,
                    day.OpenSeconds,
                    day.MaxOpenSeconds))
                .ToArray(),
            stats.OpenCountByHour);

        return await ApiResponses
            .JsonAsync(request, HttpStatusCode.OK, response, cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool TryParseInt(string? value, int defaultValue, int min, int max, out int result)
    {
        if (string.IsNullOrEmpty(value))
        {
            result = defaultValue;
            return true;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            && parsed >= min
            && parsed <= max)
        {
            result = parsed;
            return true;
        }

        result = 0;
        return false;
    }
}
