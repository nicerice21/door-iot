using DoorMonitor.Core.Doors;
using DoorMonitor.Core.Telemetry;

namespace DoorMonitor.Core.Tests.Telemetry;

/// <summary>テスト用の一時ディレクトリ。各テストの終了時に削除する。</summary>
internal sealed class TemporaryDirectory : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(),
        "door-monitor-tests",
        Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 一時ディレクトリの削除失敗はテスト結果に影響しない。
        }
    }
}

/// <summary>テレメトリのテストデータ。</summary>
internal static class TelemetryTestData
{
    public static TelemetryReport CreateReport(string deviceId, DoorState state = DoorState.Open) => new()
    {
        GatewayId = "gw-test",
        DeviceId = deviceId,
        State = state,
        BatteryMv = 2950,
        Lqi = 135,
        ObservedAt = new DateTimeOffset(2026, 11, 22, 0, 0, 0, TimeSpan.Zero),
    };
}
