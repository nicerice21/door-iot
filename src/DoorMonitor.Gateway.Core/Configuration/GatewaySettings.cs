namespace DoorMonitor.Gateway.Configuration;

/// <summary>PC ゲートウェイのユーザー設定。Windows は %LOCALAPPDATA%\DoorMonitor\、Ubuntu は ~/.local/share/DoorMonitor/ に保存する。</summary>
public sealed class GatewaySettings
{
    public string GatewayId { get; set; } = "gw-pc-01";

    /// <summary>MONOSTICK のシリアルポート。Windows は COM3、Ubuntu は /dev/ttyUSB0 を既定にする。</summary>
    public string ComPort { get; set; } = DefaultComPort;

    public static string DefaultComPort { get; } = OperatingSystem.IsWindows() ? "COM3" : "/dev/ttyUSB0";

    public int BaudRate { get; set; } = 115200;

    /// <summary>Azure Functions のベース URL（末尾スラッシュ不要）。</summary>
    public string ApiBaseUrl { get; set; } = "http://localhost:7071";

    public string ApiKey { get; set; } = string.Empty;

    public int HeartbeatIntervalSeconds { get; set; } = 60;

    public int DebounceMilliseconds { get; set; } = 500;

    public int ReconnectDelaySeconds { get; set; } = 5;

    /// <summary>再送キュー（未送信テレメトリ）の保存先。</summary>
    public string TelemetryQueueDirectory { get; set; } = DefaultTelemetryQueueDirectory;

    /// <summary>再送キューの上限件数。超過時は古いものから破棄する。</summary>
    public int TelemetryQueueCapacity { get; set; } = 1000;

    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DoorMonitor",
        "gateway.settings.json");

    public static string DefaultTelemetryQueueDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DoorMonitor",
        "queue");
}
