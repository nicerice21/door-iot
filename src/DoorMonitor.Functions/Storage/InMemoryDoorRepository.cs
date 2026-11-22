using System.Collections.Concurrent;
using DoorMonitor.Core.Doors;

namespace DoorMonitor.Functions.Storage;

/// <summary>
/// Azure を用意せずに API と Web を結合テストするためのメモリ実装。
/// Storage__Mode=InMemory（既定）で使う。プロセス再起動で消える。
/// </summary>
public sealed class InMemoryDoorRepository : IDoorRepository
{
    /// <summary>メモリ実装が無限に増えないよう、子機ごとの履歴件数を制限する。</summary>
    private const int MaxEventsPerDevice = 500;

    private readonly ConcurrentDictionary<string, DoorSnapshot> _doors = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, GatewayRecord> _gateways = new(StringComparer.Ordinal);
    private readonly object _eventsGate = new();
    private readonly Dictionary<string, SortedDictionary<long, DoorEvent>> _events = new(StringComparer.Ordinal);

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

    public Task UpsertDoorEventAsync(DoorEvent doorEvent, CancellationToken cancellationToken)
    {
        lock (_eventsGate)
        {
            if (!_events.TryGetValue(doorEvent.DeviceId, out var events))
            {
                events = new SortedDictionary<long, DoorEvent>();
                _events[doorEvent.DeviceId] = events;
            }

            // 同じ変化時刻は上書きする（再送に対して冪等）。
            events[doorEvent.ChangedAt.UtcTicks] = doorEvent;

            while (events.Count > MaxEventsPerDevice)
            {
                events.Remove(events.Keys.First());
            }
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DoorEvent>> GetDoorEventsAsync(string deviceId, int limit, CancellationToken cancellationToken)
    {
        lock (_eventsGate)
        {
            IReadOnlyList<DoorEvent> events = _events.TryGetValue(deviceId, out var stored)
                ? stored.Values.Reverse().Take(limit).ToArray()
                : Array.Empty<DoorEvent>();

            return Task.FromResult(events);
        }
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
