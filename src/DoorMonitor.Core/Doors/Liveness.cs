namespace DoorMonitor.Core.Doors;

/// <summary>最終受信時刻と閾値から死活状態を判定する。</summary>
public static class Liveness
{
    public static bool IsOnline(DateTimeOffset? lastSeenAt, DateTimeOffset now, int thresholdSeconds)
    {
        if (lastSeenAt is null)
        {
            return false;
        }

        var threshold = TimeSpan.FromSeconds(thresholdSeconds);
        var elapsed = now - lastSeenAt.Value;

        // 端末時計のずれで未来時刻になる場合も、受信直後として扱う。
        return elapsed <= threshold;
    }
}
