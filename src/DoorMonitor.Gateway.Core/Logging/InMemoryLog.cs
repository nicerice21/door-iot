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

    /// <summary>追加されたログ。コンソールや journal へ出力するホストが購読する。</summary>
    public event EventHandler<LogEntry>? Appended;

    public void Add(string message, LogLevel level = LogLevel.Info)
    {
        LogEntry entry;
        lock (_gate)
        {
            entry = new LogEntry(DateTimeOffset.UtcNow, level, message);
            _entries.AddFirst(entry);
            while (_entries.Count > MaxEntries)
            {
                _entries.RemoveLast();
            }
        }

        Appended?.Invoke(this, entry);
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
