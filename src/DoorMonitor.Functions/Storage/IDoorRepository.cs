using DoorMonitor.Core.Doors;

namespace DoorMonitor.Functions.Storage;

/// <summary>クラウド側で保持するドア状態・ゲートウェイ状態の読み書き。</summary>
public interface IDoorRepository
{
    Task<IReadOnlyList<DoorSnapshot>> GetDoorsAsync(CancellationToken cancellationToken);

    Task<DoorSnapshot?> GetDoorAsync(string deviceId, CancellationToken cancellationToken);

    Task UpsertDoorAsync(DoorSnapshot snapshot, CancellationToken cancellationToken);

    Task<GatewayRecord?> GetGatewayAsync(string gatewayId, CancellationToken cancellationToken);

    Task TouchGatewayAsync(string gatewayId, DateTimeOffset lastSeenAt, CancellationToken cancellationToken);
}

public sealed record GatewayRecord(string Id, DateTimeOffset LastSeenAt);
