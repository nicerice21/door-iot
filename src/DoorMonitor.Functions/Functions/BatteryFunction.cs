using System.Net;
using DoorMonitor.Core.Telemetry;
using DoorMonitor.Functions.Dtos;
using DoorMonitor.Functions.Http;
using DoorMonitor.Functions.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace DoorMonitor.Functions.Functions;

/// <summary>
/// GET /api/doors/{deviceId}/battery — 子機の電池電圧の履歴を古い順に返す。
/// GET /api/doors と同じく認証なしで公開する。
/// </summary>
public sealed class BatteryFunction
{
    private const int DefaultHours = 168;
    private const int MaxHours = 720;

    private readonly IDoorRepository _repository;

    public BatteryFunction(IDoorRepository repository)
    {
        _repository = repository;
    }

    [Function("DoorBattery")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "doors/{deviceId}/battery")] HttpRequestData request,
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

        var hours = DefaultHours;
        var requestedHours = request.Query["hours"];
        if (!string.IsNullOrEmpty(requestedHours))
        {
            if (!int.TryParse(requestedHours, out var parsed) || parsed < 1 || parsed > MaxHours)
            {
                return await ApiResponses
                    .ErrorAsync(request, HttpStatusCode.BadRequest, $"hours は 1〜{MaxHours} の整数で指定してください。", cancellationToken)
                    .ConfigureAwait(false);
            }

            hours = parsed;
        }

        var to = DateTimeOffset.UtcNow;
        // 区切りのよい時間帯から返すため、開始時刻も 1 時間単位に丸める。
        var from = BatterySample.BucketStart(to.AddHours(-hours));

        var samples = await _repository
            .GetBatterySamplesAsync(deviceId, from, to, cancellationToken)
            .ConfigureAwait(false);

        var response = new BatteryHistoryResponse(
            deviceId,
            from,
            to,
            samples.Select(sample => new BatterySampleDto(sample.SampledAt, sample.BatteryMv)).ToArray(),
            BatterySummaryDto.From(BatterySummary.From(samples)),
            BatteryTrendDto.From(BatteryTrend.From(samples)));

        return await ApiResponses
            .JsonAsync(request, HttpStatusCode.OK, response, cancellationToken)
            .ConfigureAwait(false);
    }
}
