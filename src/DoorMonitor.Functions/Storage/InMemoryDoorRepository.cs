using System.Collections.Concurrent;
using DoorMonitor.Core.Doors;

namespace DoorMonitor.Functions.Storage;

/// <summary>
/// Azure を用意せずに API と Web を結合テストするためのメモリ実装。
/// Storage__Mode=InMemory（既定）で使う。プロセス再起動で消える。
/// </summary>
public sealed class InMemoryDoorRepository : IDoorRepository
{
    private readonly ConcurrentDictionary<string, DoorSnapshot> _doors = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, GatewayRecord> _gateways = new(StringComparer.Ordinal);

    public Task<IReadOnlyList<DoorSnapshot>> GetDoorsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<DoorSnapshot> doors = _doors.Values
            .OrderBy(door => door.LogicalId ?? int.MaxValue)
            .ThenBy(door => door.DeviceId, StringComparer.Ordinal)
            .ToArray();

        return Task.FromResult(doors);
    }

    public Task<DoorSnapshot?> GetDoorAsync(string deviceId, CancellationToken cancellationToken)
        => Task.FromResult(_doors.GetValueOrDefault(deviceId));

    public Task UpsertDoorAsync(DoorSnapshot snapshot, CancellationToken cancellationToken)
    {
        _doors[snapshot.DeviceId] = snapshot;
        return Task.CompletedTask;
    }

    public Task<GatewayRecord?> GetGatewayAsync(string gatewayId, CancellationToken cancellationToken)
        => Task.FromResult(_gateways.GetValueOrDefault(gatewayId));

    public Task TouchGatewayAsync(string gatewayId, DateTimeOffset lastSeenAt, CancellationToken cancellationToken)
    {
        _gateways.AddOrUpdate(
            gatewayId,
            id => new GatewayRecord(id, lastSeenAt),
            (id, current) => current with { LastSeenAt = Max(current.LastSeenAt, lastSeenAt) });

        return Task.CompletedTask;
    }

    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a >= b ? a : b;
}
