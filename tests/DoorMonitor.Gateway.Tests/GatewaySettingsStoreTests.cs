using DoorMonitor.Gateway.Configuration;
using DoorMonitor.Gateway.Logging;
using Xunit;

namespace DoorMonitor.Gateway.Tests;

public sealed class GatewaySettingsStoreTests
{
    [Fact]
    public void CreatesDefaultFileWhenMissing()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "nested", "gateway.settings.json");
        var store = new GatewaySettingsStore(path);

        var settings = store.Load();

        Assert.True(File.Exists(path));
        Assert.Equal(GatewaySettings.DefaultComPort, settings.ComPort);
    }

    [Fact]
    public void RoundTripsSettings()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "gateway.settings.json");
        var store = new GatewaySettingsStore(path);

        var settings = store.Load();
        settings.ComPort = "/dev/ttyACM0";
        settings.ApiKey = "secret";
        settings.GatewayId = "gw-ubuntu";
        store.Save(settings);

        var loaded = store.Load();

        Assert.Equal("/dev/ttyACM0", loaded.ComPort);
        Assert.Equal("secret", loaded.ApiKey);
        Assert.Equal("gw-ubuntu", loaded.GatewayId);
    }

    [Fact]
    public void FallsBackToDefaultsWhenFileIsBroken()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "gateway.settings.json");
        File.WriteAllText(path, "{ not json");

        var settings = new GatewaySettingsStore(path).Load();

        Assert.Equal(GatewaySettings.DefaultComPort, settings.ComPort);
    }
}

public sealed class InMemoryLogTests
{
    [Fact]
    public void RaisesAppendedForEachEntry()
    {
        var log = new InMemoryLog();
        var entries = new List<LogEntry>();
        log.Appended += (_, entry) => entries.Add(entry);

        log.Add("hello");
        log.Add("failed", LogLevel.Error);

        Assert.Equal(2, entries.Count);
        Assert.Equal("hello", entries[0].Message);
        Assert.Equal(LogLevel.Error, entries[1].Level);
    }
}

/// <summary>テスト用の一時ディレクトリ。Dispose で削除する。</summary>
internal sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "door-iot-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // テスト結果に影響しないため、削除に失敗しても無視する。
        }
    }
}
