using System.Net;
using DoorMonitor.Functions.Dtos;
using DoorMonitor.Functions.Http;
using DoorMonitor.Functions.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace DoorMonitor.Functions.Functions;

/// <summary>
/// GET /api/doors/{deviceId}/history — 子機の開閉履歴を新しい順に返す。
/// GET /api/doors と同じく認証なしで公開する。
/// </summary>
public sealed class HistoryFunction
{
    private const int DefaultLimit = 20;
    private const int MaxLimit = 100;

    private readonly IDoorRepository _repository;

    public HistoryFunction(IDoorRepository repository)
    {
        _repository = repository;
    }

    [Function("DoorHistory")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "doors/{deviceId}/history")] HttpRequestData request,
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

        var limit = DefaultLimit;
        if (int.TryParse(request.Query["limit"], out var requested))
        {
            limit = Math.Clamp(requested, 1, MaxLimit);
        }

        var events = await _repository
            .GetDoorEventsAsync(deviceId, limit, cancellationToken)
            .ConfigureAwait(false);

        var response = new DoorHistoryResponse(
            deviceId,
            events.Select(doorEvent => new DoorEventDto(doorEvent.State, doorEvent.ChangedAt, doorEvent.OpenedAt)).ToArray());

        return await ApiResponses
            .JsonAsync(request, HttpStatusCode.OK, response, cancellationToken)
            .ConfigureAwait(false);
    }
}
