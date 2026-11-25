using System.Net;
using DoorMonitor.Core.Doors;
using DoorMonitor.Core.Telemetry;
using DoorMonitor.Functions.Http;
using DoorMonitor.Functions.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;

namespace DoorMonitor.Functions.Functions;

/// <summary>POST /api/telemetry — PC ゲートウェイからの子機テレメトリ受信。</summary>
public sealed class TelemetryFunction
{
    private readonly IDoorRepository _repository;
    private readonly IConfiguration _configuration;

    public TelemetryFunction(IDoorRepository repository, IConfiguration configuration)
    {
        _repository = repository;
        _configuration = configuration;
    }

    [Function("Telemetry")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "telemetry")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        if (!ApiKeyValidator.IsAuthorized(request, _configuration))
        {
            return await ApiResponses
                .ErrorAsync(request, HttpStatusCode.Unauthorized, "API キーが正しくありません。", cancellationToken)
                .ConfigureAwait(false);
        }

        var report = await ApiResponses.ReadBodyAsync<TelemetryReport>(request, cancellationToken).ConfigureAwait(false);
        if (report is null
            || string.IsNullOrWhiteSpace(report.DeviceId)
            || string.IsNullOrWhiteSpace(report.GatewayId))
        {
            return await ApiResponses
                .ErrorAsync(request, HttpStatusCode.BadRequest, "deviceId と gatewayId は必須です。", cancellationToken)
                .ConfigureAwait(false);
        }

        var existing = await _repository.GetDoorAsync(report.DeviceId, cancellationToken).ConfigureAwait(false);
        var updated = DoorStateReducer.Apply(existing, report);

        // 履歴を先に書く。状態更新だけ成功して履歴が欠けることを避ける。
        // 再送で同じ電文が届いても、変化時刻から決まる行を上書きするだけで重複しない。
        var doorEvent = DoorStateReducer.CreateEvent(existing, updated);
        if (doorEvent is not null)
        {
            await _repository.UpsertDoorEventAsync(doorEvent, cancellationToken).ConfigureAwait(false);
        }

        await _repository.UpsertDoorAsync(updated, cancellationToken).ConfigureAwait(false);

        // 電池電圧は 1 時間に 1 件の履歴として残す（同じ時間帯は上書き）。
        var batterySample = BatterySample.FromReport(report);
        if (batterySample is not null)
        {
            await _repository.UpsertBatterySampleAsync(batterySample, cancellationToken).ConfigureAwait(false);
        }

        await _repository.TouchGatewayAsync(report.GatewayId, report.ObservedAt, cancellationToken).ConfigureAwait(false);

        return await ApiResponses.JsonAsync(
            request,
            HttpStatusCode.Accepted,
            new
            {
                accepted = true,
                deviceId = updated.DeviceId,
                state = updated.State,
                openedAt = updated.OpenedAt,
                lastSeenAt = updated.LastSeenAt,
            },
            cancellationToken).ConfigureAwait(false);
    }
}
