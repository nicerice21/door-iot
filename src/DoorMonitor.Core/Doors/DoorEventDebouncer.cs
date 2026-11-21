namespace DoorMonitor.Core.Doors;

/// <summary>
/// 短時間に同じ状態のイベント電文が連続した場合の重複送信を抑止する。
/// 定期送信（ハートビート）は死活監視のために必ず通す。
/// </summary>
public sealed class DoorEventDebouncer
{
    private readonly TimeSpan _window;
    private readonly Dictionary<string, Entry> _lastEvents = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public DoorEventDebouncer(TimeSpan window)
    {
        if (window < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(window));
        }

        _window = window;
    }

    public bool ShouldForward(string deviceId, DoorState state, bool isHeartbeat, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);

        lock (_gate)
        {
            if (!isHeartbeat
                && _lastEvents.TryGetValue(deviceId, out var last)
                && last.State == state
                && now - last.At < _window)
            {
                return false;
            }

            _lastEvents[deviceId] = new Entry(state, now);
            return true;
        }
    }

    private readonly record struct Entry(DoorState State, DateTimeOffset At);
}
