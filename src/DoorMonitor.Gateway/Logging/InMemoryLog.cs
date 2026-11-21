namespace DoorMonitor.Gateway.Logging;

public enum LogLevel
{
    Info,
    Warning,
    Error,
}

public sealed record LogEntry(DateTimeOffset At, LogLevel Level, string Message)
{
    public override string ToString() => $"{At.ToLocalTime():yyyy-MM-dd HH:mm:ss} [{Level}] {Message}";
}

/// <summary>直近の通信ログをメモリに保持し、設定画面から確認できるようにする。</summary>
public sealed class InMemoryLog
{
    private const int MaxEntries = 500;

    private readonly LinkedList<LogEntry> _entries = new();
    private readonly object _gate = new();

    public event EventHandler? Changed;

    public void Add(string message, LogLevel level = LogLevel.Info)
    {
        lock (_gate)
        {
            _entries.AddFirst(new LogEntry(DateTimeOffset.UtcNow, level, message));
            while (_entries.Count > MaxEntries)
            {
                _entries.RemoveLast();
            }
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlyList<LogEntry> Snapshot()
    {
        lock (_gate)
        {
            return _entries.ToArray();
        }
    }
}
