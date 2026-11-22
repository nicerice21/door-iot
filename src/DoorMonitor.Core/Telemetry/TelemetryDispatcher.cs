namespace DoorMonitor.Core.Telemetry;

/// <summary>
/// 再送キューを先頭から順に送るワーカー。
/// 失敗した電文が残っている間は後続を送らず、指数バックオフで再試行する。
/// </summary>
public sealed class TelemetryDispatcher : IDisposable
{
    public static readonly TimeSpan DefaultInitialRetryDelay = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan DefaultMaxRetryDelay = TimeSpan.FromSeconds(60);

    private static readonly TimeSpan IdleWaitTimeout = TimeSpan.FromSeconds(10);

    private readonly TelemetryOutbox _outbox;
    private readonly Func<TelemetryReport, CancellationToken, Task> _send;
    private readonly Action<Exception, int>? _onFailure;
    private readonly Action<int>? _onRecovered;
    private readonly TimeSpan _initialRetryDelay;
    private readonly TimeSpan _maxRetryDelay;
    private readonly SemaphoreSlim _signal = new(0);

    /// <param name="send">1 件を送信する処理。失敗時は例外を投げる。</param>
    /// <param name="onFailure">送信失敗時（例外と再送待ち件数）。</param>
    /// <param name="onRecovered">失敗後に送信が再開できた時（再送待ち件数）。</param>
    public TelemetryDispatcher(
        TelemetryOutbox outbox,
        Func<TelemetryReport, CancellationToken, Task> send,
        Action<Exception, int>? onFailure = null,
        Action<int>? onRecovered = null,
        TimeSpan? initialRetryDelay = null,
        TimeSpan? maxRetryDelay = null)
    {
        _outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
        _send = send ?? throw new ArgumentNullException(nameof(send));
        _onFailure = onFailure;
        _onRecovered = onRecovered;
        _initialRetryDelay = initialRetryDelay ?? DefaultInitialRetryDelay;
        _maxRetryDelay = maxRetryDelay ?? DefaultMaxRetryDelay;

        if (_initialRetryDelay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(initialRetryDelay), "再試行の初回待ち時間は 0 より大きい必要があります。");
        }

        if (_maxRetryDelay < _initialRetryDelay)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRetryDelay), "再試行の最大待ち時間は初回待ち時間以上にしてください。");
        }
    }

    public int PendingCount => _outbox.Count;

    /// <summary>キューへ追加し、送信ワーカーへ通知する。</summary>
    public OutboxEnqueueResult Enqueue(TelemetryReport report)
    {
        var result = _outbox.Enqueue(report);
        if (result.Succeeded)
        {
            _signal.Release();
        }

        return result;
    }

    /// <summary>キューが空になるまで送信を続ける。キャンセルされるまで戻らない。</summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var retryDelay = _initialRetryDelay;
        var failing = false;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (!_outbox.TryPeek(out var entry) || entry is null)
            {
                try
                {
                    // 電文の到着を待つ。読み取りの一時的な失敗に備えて定期的に見直す。
                    await _signal.WaitAsync(IdleWaitTimeout, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                continue;
            }

            try
            {
                await _send(entry.Report, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                failing = true;
                _onFailure?.Invoke(ex, PendingCount);

                try
                {
                    await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                retryDelay = NextRetryDelay(retryDelay);
                continue;
            }

            // 削除に失敗した場合は、同じ電文を再送しないよう削除だけを再試行する。
            while (!_outbox.Remove(entry))
            {
                try
                {
                    await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }

            if (failing)
            {
                failing = false;
                retryDelay = _initialRetryDelay;
                _onRecovered?.Invoke(PendingCount);
            }
        }
    }

    private TimeSpan NextRetryDelay(TimeSpan current)
        => TimeSpan.FromTicks(Math.Min(current.Ticks * 2, _maxRetryDelay.Ticks));

    public void Dispose() => _signal.Dispose();
}
