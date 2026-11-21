using DoorMonitor.Core.Doors;

namespace DoorMonitor.Core.Twelite;

/// <summary>
/// App_Twelite の 0x81（状態通知）電文。
/// 仕様: https://twelite.net/manuals/twelite-stage-sdk/twelite-apps/app-twelite/latest/uart-data.html
/// </summary>
public sealed record TwelitePacket
{
    /// <summary>送信元の論理デバイスID。</summary>
    public required byte LogicalDeviceId { get; init; }

    /// <summary>送信元のシリアルID（例: 0x8201015A）。</summary>
    public required uint SerialId { get; init; }

    public required byte PacketIdentifier { get; init; }

    public required byte Lqi { get; init; }

    public required ushort VoltageMv { get; init; }

    /// <summary>DI1 が High かどうか。リードスイッチ(NO)では High = ドア開。</summary>
    public required bool Di1High { get; init; }

    /// <summary>DI1 が今回の電文で有効かどうか。</summary>
    public required bool Di1Valid { get; init; }

    public required bool IsPeriodic { get; init; }

    public required byte RelayCount { get; init; }

    /// <summary>子機の電源投入からの経過時間（1秒 = 64カウント）。</summary>
    public required ushort DeviceTimestamp { get; init; }

    public string DeviceId => $"0x{SerialId:X8}";

    /// <summary>リードスイッチ(NO): ドア閉 = DI1 Low、ドア開 = DI1 High。</summary>
    public DoorState DoorState => Di1High ? DoorState.Open : DoorState.Closed;
}
