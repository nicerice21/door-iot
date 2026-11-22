using System.Text.Json;

namespace DoorMonitor.Core.Telemetry;

/// <summary>テレメトリの JSON 表現。クラウド送信と再送キューで同じ設定を使う。</summary>
public static class TelemetryJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web);
}
