using System.Net;
using DoorMonitor.Functions.Http;
using DoorMonitor.Functions.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;

namespace DoorMonitor.Functions.Functions;

public sealed record HeartbeatRequest(string? GatewayId, DateTimeOffset? SentAt);

/// <summary>POST /api/heartbeat — PC ゲートウェイ自身の死活通知。</summary>
public sealed class HeartbeatFunction
{
    private readonly IDoorRepository _repository;
    private readonly IConfiguration _configuration;

    public HeartbeatFunction(IDoorRepository repository, IConfiguration configuration)
    {
        _repository = repository;
        _configuration = configuration;
    }

    [Function("Heartbeat")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "heartbeat")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        if (!ApiKeyValidator.IsAuthorized(request, _configuration))
        {
            return await ApiResponses
                .ErrorAsync(request, HttpStatusCode.Unauthorized, "API キーが正しくありません。", cancellationToken)
                .ConfigureAwait(false);
        }

        var body = await ApiResponses.ReadBodyAsync<HeartbeatRequest>(request, cancellationToken).ConfigureAwait(false);
        if (body is null || string.IsNullOrWhiteSpace(body.GatewayId))
        {
            return await ApiResponses
                .ErrorAsync(request, HttpStatusCode.BadRequest, "gatewayId は必須です。", cancellationToken)
                .ConfigureAwait(false);
        }

        var seenAt = body.SentAt ?? DateTimeOffset.UtcNow;
        await _repository.TouchGatewayAsync(body.GatewayId, seenAt, cancellationToken).ConfigureAwait(false);

        return await ApiResponses.JsonAsync(
            request,
            HttpStatusCode.Accepted,
            new { accepted = true, gatewayId = body.GatewayId, lastSeenAt = seenAt },
            cancellationToken).ConfigureAwait(false);
    }
}
