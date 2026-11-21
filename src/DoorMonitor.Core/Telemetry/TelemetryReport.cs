using DoorMonitor.Core.Doors;

namespace DoorMonitor.Core.Telemetry;

/// <summary>PC ゲートウェイからクラウドへ送る子機テレメトリ。</summary>
public sealed record TelemetryReport
{
    public required string GatewayId { get; init; }

    /// <summary>TWELITE のシリアルID（例: "0x8201015A"）。</summary>
    public required string DeviceId { get; init; }

    public int? LogicalId { get; init; }

    public required DoorState State { get; init; }

    public int? BatteryMv { get; init; }

    public int? Lqi { get; init; }

    /// <summary>定期送信（ハートビート）による報告かどうか。</summary>
    public bool IsHeartbeat { get; init; }

    /// <summary>ゲートウェイが子機の電文を受信した時刻（UTC）。</summary>
    public DateTimeOffset ObservedAt { get; init; }
}
