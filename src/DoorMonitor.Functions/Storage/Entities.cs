using Azure;
using Azure.Data.Tables;
using DoorMonitor.Core.Doors;
using DoorMonitor.Core.Telemetry;

namespace DoorMonitor.Functions.Storage;

/// <summary>Devices テーブルの行。</summary>
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

/// <summary>DoorEvents テーブルの行。RowKey は時刻を反転した連番で、昇順が新しい順になる。</summary>
public sealed class DoorEventEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;

    public string RowKey { get; set; } = string.Empty;

    public DateTimeOffset? Timestamp { get; set; }

    public ETag ETag { get; set; }

    public string State { get; set; } = DoorStateJsonConverter.ClosedValue;

    public DateTimeOffset ChangedAt { get; set; }

    public DateTimeOffset? OpenedAt { get; set; }
}

/// <summary>BatterySamples テーブルの行。1 時間に 1 行（同じ時間帯は上書き）。</summary>
public sealed class BatterySampleEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;

    public string RowKey { get; set; } = string.Empty;

    public DateTimeOffset? Timestamp { get; set; }

    public ETag ETag { get; set; }

    public DateTimeOffset SampledAt { get; set; }

    public DateTimeOffset ObservedAt { get; set; }

    public int BatteryMv { get; set; }

    public int? Lqi { get; set; }
}

internal static class DoorEventMapper
{
    public static DoorEventEntity ToEntity(DoorEvent doorEvent) => new()
    {
        PartitionKey = doorEvent.DeviceId,
        RowKey = TableRowKeys.Inverted(doorEvent.ChangedAt),
        State = doorEvent.State == DoorState.Open ? DoorStateJsonConverter.OpenValue : DoorStateJsonConverter.ClosedValue,
        ChangedAt = doorEvent.ChangedAt,
        OpenedAt = doorEvent.OpenedAt,
    };

    public static DoorEvent ToEvent(DoorEventEntity entity) => new()
    {
        DeviceId = entity.PartitionKey,
        State = entity.State == DoorStateJsonConverter.OpenValue ? DoorState.Open : DoorState.Closed,
        ChangedAt = entity.ChangedAt,
        OpenedAt = entity.OpenedAt,
    };
}

internal static class BatterySampleMapper
{
    public static BatterySampleEntity ToEntity(BatterySample sample) => new()
    {
        PartitionKey = sample.DeviceId,
        RowKey = TableRowKeys.Inverted(sample.SampledAt),
        SampledAt = sample.SampledAt,
        ObservedAt = sample.ObservedAt,
        BatteryMv = sample.BatteryMv,
        Lqi = sample.Lqi,
    };

    public static BatterySample ToSample(BatterySampleEntity entity) => new()
    {
        DeviceId = entity.PartitionKey,
        SampledAt = entity.SampledAt,
        ObservedAt = entity.ObservedAt,
        BatteryMv = entity.BatteryMv,
        Lqi = entity.Lqi,
    };
}

internal static class TableRowKeys
{
    /// <summary>時刻を反転した 19 桁の行キー。RowKey の昇順が新しい順になる。</summary>
    public static string Inverted(DateTimeOffset at)
        => (DateTimeOffset.MaxValue.UtcTicks - at.UtcTicks)
            .ToString("D19", System.Globalization.CultureInfo.InvariantCulture);
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
    };
}
