using System.Net;
using DoorMonitor.Core.Doors;
using DoorMonitor.Functions.Dtos;
using DoorMonitor.Functions.Http;
using DoorMonitor.Functions.Options;
using DoorMonitor.Functions.Services;
using DoorMonitor.Functions.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Options;

namespace DoorMonitor.Functions.Functions;

/// <summary>
/// GET /api/doors — Web フロントエンド向けの状態一覧。
/// 死活判定はリクエスト時点の時刻で評価する（Timer Trigger は使わない）。
/// </summary>
public sealed class DoorsFunction
{
    private readonly IDoorRepository _repository;
    private readonly MonitoringOptions _monitoring;
    private readonly GatewayOptions _gateway;

    public DoorsFunction(
        IDoorRepository repository,
        IOptions<MonitoringOptions> monitoring,
        IOptions<GatewayOptions> gateway)
    {
        _repository = repository;
        _monitoring = monitoring.Value;
        _gateway = gateway.Value;
    }

    [Function("Doors")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "doors")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        var doors = await _repository.GetDoorsAsync(cancellationToken).ConfigureAwait(false);
        var gateway = await _repository.GetGatewayAsync(_gateway.Id, cancellationToken).ConfigureAwait(false);

        var gatewayOnline = gateway is not null
            && Liveness.IsOnline(gateway.LastSeenAt, now, _monitoring.GatewayOfflineThresholdSec);

        var response = new DoorsResponse(
            Gateway: new GatewayDto(
                Id: _gateway.Id,
                Status: gatewayOnline ? "online" : "offline",
                LastSeenAt: gateway?.LastSeenAt),
            Doors: doors
                .Select(door => DoorDtoFactory.Create(door, now, _monitoring.DeviceOfflineThresholdSec))
                .ToArray());

        return await ApiResponses
            .JsonAsync(request, HttpStatusCode.OK, response, cancellationToken)
            .ConfigureAwait(false);
    }
}
