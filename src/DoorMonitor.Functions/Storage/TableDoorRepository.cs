using Azure;
using Azure.Data.Tables;
using DoorMonitor.Core.Doors;

namespace DoorMonitor.Functions.Storage;

/// <summary>Azure Table Storage 実装。Devices / Gateways / DoorEvents の3テーブルを使う。</summary>
public sealed class TableDoorRepository : IDoorRepository
{
    private const string DevicesTable = "Devices";
    private const string GatewaysTable = "Gateways";
    private const string DoorEventsTable = "DoorEvents";

    private readonly TableClient _devices;
    private readonly TableClient _gateways;
    private readonly TableClient _doorEvents;
    private readonly SemaphoreSlim _initGate = new(1, 1);
    private volatile bool _initialized;

    public TableDoorRepository(string connectionString)
    {
        var service = new TableServiceClient(connectionString);
        _devices = service.GetTableClient(DevicesTable);
        _gateways = service.GetTableClient(GatewaysTable);
        _doorEvents = service.GetTableClient(DoorEventsTable);
    }

    public async Task<IReadOnlyList<DoorSnapshot>> GetDoorsAsync(CancellationToken cancellationToken)
    {
        await EnsureTablesAsync(cancellationToken).ConfigureAwait(false);

        var doors = new List<DoorSnapshot>();
        await foreach (var entity in _devices.QueryAsync<DoorEntity>(cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            doors.Add(DoorEntityMapper.ToSnapshot(entity));
        }

        return doors
            .OrderBy(door => door.LogicalId ?? int.MaxValue)
            .ThenBy(door => door.DeviceId, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<DoorSnapshot?> GetDoorAsync(string deviceId, CancellationToken cancellationToken)
    {
        await EnsureTablesAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var response = await _devices
                .GetEntityAsync<DoorEntity>(DoorEntity.Partition, deviceId, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return DoorEntityMapper.ToSnapshot(response.Value);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task UpsertDoorAsync(DoorSnapshot snapshot, CancellationToken cancellationToken)
    {
        await EnsureTablesAsync(cancellationToken).ConfigureAwait(false);
        await _devices
            .UpsertEntityAsync(DoorEntityMapper.ToEntity(snapshot), TableUpdateMode.Replace, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task UpsertDoorEventAsync(DoorEvent doorEvent, CancellationToken cancellationToken)
    {
        await EnsureTablesAsync(cancellationToken).ConfigureAwait(false);
        await _doorEvents
            .UpsertEntityAsync(DoorEventMapper.ToEntity(doorEvent), TableUpdateMode.Replace, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<DoorEvent>> GetDoorEventsAsync(string deviceId, int limit, CancellationToken cancellationToken)
    {
        await EnsureTablesAsync(cancellationToken).ConfigureAwait(false);

        var events = new List<DoorEvent>(limit);
        var query = _doorEvents.QueryAsync<DoorEventEntity>(
            entity => entity.PartitionKey == deviceId,
            maxPerPage: limit,
            cancellationToken: cancellationToken);

        // RowKey が時刻の反転値なので、昇順に読むだけで新しい順になる。
        await foreach (var entity in query.ConfigureAwait(false))
        {
            events.Add(DoorEventMapper.ToEvent(entity));
            if (events.Count >= limit)
            {
                break;
            }
        }

        return events;
    }

    public async Task<GatewayRecord?> GetGatewayAsync(string gatewayId, CancellationToken cancellationToken)
    {
        await EnsureTablesAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var response = await _gateways
                .GetEntityAsync<GatewayEntity>(GatewayEntity.Partition, gatewayId, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return new GatewayRecord(response.Value.RowKey, response.Value.LastSeenAt);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task TouchGatewayAsync(string gatewayId, DateTimeOffset lastSeenAt, CancellationToken cancellationToken)
    {
        await EnsureTablesAsync(cancellationToken).ConfigureAwait(false);

        // 到着順が入れ替わっても最終受信時刻を巻き戻さない。
        var existing = await GetGatewayAsync(gatewayId, cancellationToken).ConfigureAwait(false);
        if (existing is not null && existing.LastSeenAt > lastSeenAt)
        {
            lastSeenAt = existing.LastSeenAt;
        }

        var entity = new GatewayEntity
        {
            RowKey = gatewayId,
            LastSeenAt = lastSeenAt,
        };

        await _gateways
            .UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task EnsureTablesAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        await _initGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_initialized)
            {
                return;
            }

            await _devices.CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
            await _gateways.CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
            await _doorEvents.CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
            _initialized = true;
        }
        finally
        {
            _initGate.Release();
        }
    }
}
