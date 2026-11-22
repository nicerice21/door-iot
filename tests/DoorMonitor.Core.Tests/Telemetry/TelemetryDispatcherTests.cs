using System.Collections.Concurrent;
using DoorMonitor.Core.Telemetry;
using Xunit;

namespace DoorMonitor.Core.Tests.Telemetry;

public sealed class TelemetryDispatcherTests : IDisposable
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(10);

    private readonly TemporaryDirectory _temporary = new();

    public void Dispose() => _temporary.Dispose();

    [Fact]
    public async Task キューを古い順に送信して削除する()
    {
        var outbox = new TelemetryOutbox(_temporary.Path);
        var sent = new ConcurrentQueue<string>();

        using var dispatcher = new TelemetryDispatcher(
            outbox,
            (report, _) =>
            {
                sent.Enqueue(report.DeviceId);
                return Task.CompletedTask;
            },
            initialRetryDelay: RetryDelay,
            maxRetryDelay: RetryDelay);

        dispatcher.Enqueue(TelemetryTestData.CreateReport("0x01"));
        dispatcher.Enqueue(TelemetryTestData.CreateReport("0x02"));

        using var cts = new CancellationTokenSource();
        var run = dispatcher.RunAsync(cts.Token);

        await WaitUntilAsync(() => sent.Count == 2 && outbox.Count == 0);

        Assert.Equal(new[] { "0x01", "0x02" }, sent);
        Assert.Equal(0, dispatcher.PendingCount);

        await StopAsync(cts, run);
    }

    [Fact]
    public async Task 送信に失敗したら後続を送らずに再試行する()
    {
        var outbox = new TelemetryOutbox(_temporary.Path);
        var attempts = new ConcurrentQueue<string>();
        var failures = new ConcurrentQueue<int>();
        var recovered = new ConcurrentQueue<int>();
        var failFirst = true;

        using var dispatcher = new TelemetryDispatcher(
            outbox,
            (report, _) =>
            {
                attempts.Enqueue(report.DeviceId);
                if (failFirst)
                {
                    failFirst = false;
                    throw new InvalidOperationException("通信できません");
                }

                return Task.CompletedTask;
            },
            (_, pendingCount) => failures.Enqueue(pendingCount),
            pendingCount => recovered.Enqueue(pendingCount),
            initialRetryDelay: RetryDelay,
            maxRetryDelay: RetryDelay);

        dispatcher.Enqueue(TelemetryTestData.CreateReport("0x01"));
        dispatcher.Enqueue(TelemetryTestData.CreateReport("0x02"));

        using var cts = new CancellationTokenSource();
        var run = dispatcher.RunAsync(cts.Token);

        await WaitUntilAsync(() => recovered.Count == 1 && outbox.Count == 0);

        Assert.Equal(new[] { "0x01", "0x01", "0x02" }, attempts);
        Assert.Equal(new[] { 2 }, failures);
        // 再開した時点では 0x02 がまだキューに残っている。
        Assert.Equal(new[] { 1 }, recovered);

        await StopAsync(cts, run);
    }

    private static async Task StopAsync(CancellationTokenSource cts, Task run)
    {
        await cts.CancelAsync();
        await run.WaitAsync(TestTimeout);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TestTimeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(10);
        }

        Assert.True(condition(), "待機がタイムアウトしました。");
    }
}
