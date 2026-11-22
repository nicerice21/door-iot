using DoorMonitor.Core.Doors;
using DoorMonitor.Core.Telemetry;
using Xunit;

namespace DoorMonitor.Core.Tests.Telemetry;

public sealed class TelemetryOutboxTests : IDisposable
{
    private readonly TemporaryDirectory _temporary = new();

    public void Dispose() => _temporary.Dispose();

    [Fact]
    public void 追加した順に取り出せる()
    {
        var outbox = new TelemetryOutbox(_temporary.Path);
        var first = TelemetryTestData.CreateReport("0x01", DoorState.Open);

        Assert.True(outbox.Enqueue(first).Succeeded);
        Assert.True(outbox.Enqueue(TelemetryTestData.CreateReport("0x02", DoorState.Closed)).Succeeded);
        Assert.Equal(2, outbox.Count);

        Assert.True(outbox.TryPeek(out var entry));
        Assert.NotNull(entry);
        Assert.Equal("0x01", entry!.Report.DeviceId);
        Assert.Equal(DoorState.Open, entry.Report.State);
        Assert.Equal(first.ObservedAt, entry.Report.ObservedAt);

        Assert.True(outbox.Remove(entry));

        Assert.True(outbox.TryPeek(out entry));
        Assert.Equal("0x02", entry!.Report.DeviceId);
        Assert.Equal(1, outbox.Count);
    }

    [Fact]
    public void 新しいインスタンスでも未送信の電文を引き継ぐ()
    {
        var first = new TelemetryOutbox(_temporary.Path);
        Assert.True(first.Enqueue(TelemetryTestData.CreateReport("0x01")).Succeeded);

        var second = new TelemetryOutbox(_temporary.Path);
        Assert.Equal(1, second.Count);
        Assert.True(second.Enqueue(TelemetryTestData.CreateReport("0x02")).Succeeded);

        Assert.True(second.TryPeek(out var entry));
        Assert.Equal("0x01", entry!.Report.DeviceId);

        Assert.True(second.Remove(entry));
        Assert.True(second.TryPeek(out entry));
        Assert.Equal("0x02", entry!.Report.DeviceId);
    }

    [Fact]
    public void 上限を超えると最も古い電文を破棄する()
    {
        var outbox = new TelemetryOutbox(_temporary.Path, capacity: 2);
        Assert.True(outbox.Enqueue(TelemetryTestData.CreateReport("0x01")).Succeeded);
        Assert.True(outbox.Enqueue(TelemetryTestData.CreateReport("0x02")).Succeeded);

        var result = outbox.Enqueue(TelemetryTestData.CreateReport("0x03"));

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.DroppedCount);
        Assert.Equal(2, outbox.Count);
        Assert.True(outbox.TryPeek(out var entry));
        Assert.Equal("0x02", entry!.Report.DeviceId);
    }

    [Fact]
    public void 壊れたファイルは読み飛ばして削除する()
    {
        Directory.CreateDirectory(_temporary.Path);
        var broken = Path.Combine(_temporary.Path, "000000000001.json");
        File.WriteAllText(broken, "{ \"deviceId\": ");

        var outbox = new TelemetryOutbox(_temporary.Path);
        Assert.True(outbox.Enqueue(TelemetryTestData.CreateReport("0x01")).Succeeded);

        Assert.True(outbox.TryPeek(out var entry));
        Assert.Equal("0x01", entry!.Report.DeviceId);
        Assert.False(File.Exists(broken));
    }

    [Fact]
    public void 起動時に書きかけの一時ファイルを削除する()
    {
        Directory.CreateDirectory(_temporary.Path);
        var temporary = Path.Combine(_temporary.Path, "000000000001.json.tmp");
        File.WriteAllText(temporary, "partial");

        _ = new TelemetryOutbox(_temporary.Path);

        Assert.False(File.Exists(temporary));
    }

    [Fact]
    public void 保存先が空なら例外()
    {
        Assert.Throws<ArgumentException>(() => new TelemetryOutbox(" "));
    }

    [Fact]
    public void 上限が1未満なら例外()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TelemetryOutbox(_temporary.Path, capacity: 0));
    }
}
