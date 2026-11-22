namespace DoorMonitor.Core.Doors;

/// <summary>クラウドが保持する子機1台分の現在状態。</summary>
public sealed record DoorSnapshot
{
    /// <summary>TWELITE のシリアルID（例: "0x8201015A"）。</summary>
    public required string DeviceId { get; init; }

    /// <summary>TWELITE の論理デバイスID（0x01〜0x64）。</summary>
    public int? LogicalId { get; init; }

    public string Name { get; init; } = string.Empty;

    public DoorState State { get; init; } = DoorState.Closed;

    /// <summary>開状態が始まった時刻。閉状態では null。</summary>
    public DateTimeOffset? OpenedAt { get; init; }

    public DateTimeOffset LastChangedAt { get; init; }

    public DateTimeOffset LastSeenAt { get; init; }

    public int? BatteryMv { get; init; }

    public int? Lqi { get; init; }
}
