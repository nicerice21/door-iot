namespace DoorMonitor.Core.Doors;

/// <summary>開閉状態が変化した履歴 1 件。</summary>
public sealed record DoorEvent
{
    /// <summary>TWELITE のシリアルID（例: "0x8201015A"）。</summary>
    public required string DeviceId { get; init; }

    /// <summary>変化後の状態。</summary>
    public required DoorState State { get; init; }

    /// <summary>状態が変化した時刻（UTC）。</summary>
    public required DateTimeOffset ChangedAt { get; init; }

    /// <summary>閉じたイベントのとき、対応する開放開始時刻。</summary>
    public DateTimeOffset? OpenedAt { get; init; }
}
