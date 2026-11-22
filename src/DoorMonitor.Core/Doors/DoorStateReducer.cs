using DoorMonitor.Core.Telemetry;

namespace DoorMonitor.Core.Doors;

/// <summary>
/// 受信したテレメトリを現在状態へ反映する。開閉の遷移ルールを一箇所に集約し、
/// クラウド側の API とテストから同じロジックを使う。
/// </summary>
public static class DoorStateReducer
{
    /// <summary>
    /// 既存状態とテレメトリから新しい状態を生成する。
    /// - 閉 → 開: openedAt に受信時刻を記録する
    /// - 開 → 開: openedAt を維持する（ハートビートで経過時間をリセットしない）
    /// - 開 → 閉: openedAt をクリアする
    /// </summary>
    public static DoorSnapshot Apply(DoorSnapshot? current, TelemetryReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var changed = current is null || current.State != report.State;
        var observedAt = report.ObservedAt;

        var openedAt = report.State == DoorState.Open
            ? changed ? observedAt : current!.OpenedAt
            : null;

        return new DoorSnapshot
        {
            DeviceId = report.DeviceId,
            LogicalId = report.LogicalId ?? current?.LogicalId,
            Name = current?.Name ?? string.Empty,
            State = report.State,
            OpenedAt = openedAt,
            LastChangedAt = changed ? observedAt : current!.LastChangedAt,
            LastSeenAt = observedAt,
            BatteryMv = report.BatteryMv,
            Lqi = report.Lqi,
            Meta = current?.Meta ?? new DoorMeta(),
        };
    }

    /// <summary>
    /// 状態が変化した場合の履歴イベントを作る。変化がなければ null。
    /// 初回受信は比較対象がないため記録しない。
    /// </summary>
    public static DoorEvent? CreateEvent(DoorSnapshot? current, DoorSnapshot updated)
    {
        ArgumentNullException.ThrowIfNull(updated);

        if (current is null || current.State == updated.State)
        {
            return null;
        }

        return new DoorEvent
        {
            DeviceId = updated.DeviceId,
            State = updated.State,
            ChangedAt = updated.LastChangedAt,
            // 開いたイベントでは開放開始は変化時刻そのものなので持たない。
            OpenedAt = updated.State == DoorState.Closed ? current.OpenedAt : null,
        };
    }
}
