using Azure;
using Azure.Data.Tables;
using DoorMonitor.Core.Doors;

namespace DoorMonitor.Functions.Storage;

/// <summary>Devices テーブルの行。表示メタデータは JSON 列にまとめてスキーマ変更を避ける。</summary>
public sealed class DoorEntity : ITableEntity
{
    public const string Partition = "door";

    public string PartitionKey { get; set; } = Partition;

    public string RowKey { get; set; } = string.Empty;

    public DateTimeOffset? Timestamp { get; set; }

    public ETag ETag { get; set; }

    public int? LogicalId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string State { get; set; } = "closed";

    public DateTimeOffset? OpenedAt { get; set; }

    public DateTimeOffset LastChangedAt { get; set; }

    public DateTimeOffset LastSeenAt { get; set; }

    public int? BatteryMv { get; set; }

    public int? Lqi { get; set; }

    public string MetaJson { get; set; } = "{}";
}

/// <summary>Gateways テーブルの行。</summary>
public sealed class GatewayEntity : ITableEntity
{
    public const string Partition = "gateway";

    public string PartitionKey { get; set; } = Partition;

    public string RowKey { get; set; } = string.Empty;

    public DateTimeOffset? Timestamp { get; set; }

    public ETag ETag { get; set; }

    public DateTimeOffset LastSeenAt { get; set; }
}

internal static class DoorEntityMapper
{
    public static DoorEntity ToEntity(DoorSnapshot snapshot) => new()
    {
        RowKey = snapshot.DeviceId,
        LogicalId = snapshot.LogicalId,
        Name = snapshot.Name,
        State = snapshot.State == DoorState.Open ? DoorStateJsonConverter.OpenValue : DoorStateJsonConverter.ClosedValue,
        OpenedAt = snapshot.OpenedAt,
        LastChangedAt = snapshot.LastChangedAt,
        LastSeenAt = snapshot.LastSeenAt,
        BatteryMv = snapshot.BatteryMv,
        Lqi = snapshot.Lqi,
        MetaJson = System.Text.Json.JsonSerializer.Serialize(snapshot.Meta),
    };

    public static DoorSnapshot ToSnapshot(DoorEntity entity) => new()
    {
        DeviceId = entity.RowKey,
        LogicalId = entity.LogicalId,
        Name = entity.Name,
        State = entity.State == DoorStateJsonConverter.OpenValue ? DoorState.Open : DoorState.Closed,
        OpenedAt = entity.OpenedAt,
        LastChangedAt = entity.LastChangedAt,
        LastSeenAt = entity.LastSeenAt,
        BatteryMv = entity.BatteryMv,
        Lqi = entity.Lqi,
        Meta = DeserializeMeta(entity.MetaJson),
    };

    private static DoorMeta DeserializeMeta(string json)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<DoorMeta>(json) ?? new DoorMeta();
        }
        catch (System.Text.Json.JsonException)
        {
            return new DoorMeta();
        }
    }
}
