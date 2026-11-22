using System.Globalization;
using System.Text.Json;

namespace DoorMonitor.Core.Telemetry;

/// <summary>再送キューの先頭にあるテレメトリ。Sequence はファイル名の連番。</summary>
public sealed record TelemetryOutboxEntry(long Sequence, TelemetryReport Report);

/// <summary>キューへの追加結果。上限超過で破棄した件数も返す。</summary>
public readonly record struct OutboxEnqueueResult(bool Succeeded, int DroppedCount, string? Error);

/// <summary>
/// 送信待ちのテレメトリをディスクへ保存する FIFO キュー。
/// 1 件 1 ファイル（連番の JSON）で保持し、送信に成功したものから削除する。
/// 再起動しても未送信の電文が残る。
/// </summary>
public sealed class TelemetryOutbox
{
    public const int DefaultCapacity = 1000;

    private const string FileExtension = ".json";
    private const string TemporaryExtension = ".tmp";
    private const string FileNameFormat = "D12";

    private readonly object _gate = new();
    private readonly string _directory;
    private readonly int _capacity;
    private long _nextSequence;

    public TelemetryOutbox(string directory, int capacity = DefaultCapacity)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("キューの保存先ディレクトリを指定してください。", nameof(directory));
        }

        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "キューの上限は 1 以上にしてください。");
        }

        _directory = directory;
        _capacity = capacity;

        Directory.CreateDirectory(_directory);
        RemoveTemporaryFiles();
        _nextSequence = FindNextSequence();
    }

    public string DirectoryPath => _directory;

    public int Capacity => _capacity;

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return CountCore();
            }
        }
    }

    /// <summary>キューへ追加する。上限を超える場合は最も古い電文を破棄する。</summary>
    public OutboxEnqueueResult Enqueue(TelemetryReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        lock (_gate)
        {
            var path = BuildPath(_nextSequence);
            var temporaryPath = path + TemporaryExtension;
            try
            {
                File.WriteAllText(temporaryPath, JsonSerializer.Serialize(report, TelemetryJson.Options));
                File.Move(temporaryPath, path);
                _nextSequence++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                TryDelete(temporaryPath);
                return new OutboxEnqueueResult(false, 0, ex.Message);
            }

            // 追加してから上限を超えた分を古い順に破棄する（新しい電文を優先して残す）。
            return new OutboxEnqueueResult(true, DropOverflowCore(), null);
        }
    }

    /// <summary>最も古い電文を取り出す（削除はしない）。壊れたファイルは読み飛ばす。</summary>
    public bool TryPeek(out TelemetryOutboxEntry? entry)
    {
        lock (_gate)
        {
            entry = ReadOldestEntry();
            return entry is not null;
        }
    }

    /// <summary>送信に成功した電文をキューから削除する。</summary>
    public bool Remove(TelemetryOutboxEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        lock (_gate)
        {
            return TryDelete(BuildPath(entry.Sequence));
        }
    }

    private int DropOverflowCore()
    {
        var overflow = Math.Max(0, CountCore() - _capacity);
        var dropped = 0;

        for (var i = 0; i < overflow; i++)
        {
            var oldest = EnumerateEntryFiles().FirstOrDefault();
            if (oldest is null || !TryDelete(oldest))
            {
                break;
            }

            dropped++;
        }

        return dropped;
    }

    private TelemetryOutboxEntry? ReadOldestEntry()
    {
        foreach (var path in EnumerateEntryFiles())
        {
            if (!TryParseSequence(path, out var sequence))
            {
                TryDelete(path);
                continue;
            }

            try
            {
                var report = JsonSerializer.Deserialize<TelemetryReport>(File.ReadAllText(path), TelemetryJson.Options);
                if (report is not null)
                {
                    return new TelemetryOutboxEntry(sequence, report);
                }
            }
            catch (JsonException)
            {
                // 壊れたファイルは破棄し、次の電文へ進む。
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // 一時的な読み取り失敗は次回の再試行に任せる。
                return null;
            }

            TryDelete(path);
        }

        return null;
    }

    private long FindNextSequence()
    {
        var max = 0L;
        foreach (var path in EnumerateEntryFiles())
        {
            if (TryParseSequence(path, out var sequence) && sequence > max)
            {
                max = sequence;
            }
        }

        return max + 1;
    }

    private void RemoveTemporaryFiles()
    {
        foreach (var path in Directory.EnumerateFiles(_directory, "*" + TemporaryExtension))
        {
            TryDelete(path);
        }
    }

    private int CountCore() => EnumerateEntryFiles().Count();

    private IEnumerable<string> EnumerateEntryFiles()
        => Directory
            .EnumerateFiles(_directory, "*" + FileExtension)
            .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal);

    private string BuildPath(long sequence)
        => Path.Combine(_directory, sequence.ToString(FileNameFormat, CultureInfo.InvariantCulture) + FileExtension);

    private static bool TryParseSequence(string path, out long sequence)
        => long.TryParse(
            Path.GetFileNameWithoutExtension(path),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out sequence);

    private static bool TryDelete(string path)
    {
        try
        {
            File.Delete(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
