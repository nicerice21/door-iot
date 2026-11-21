using System.Text.Json;
using System.Text.Json.Serialization;

namespace DoorMonitor.Core.Doors;

/// <summary>ドアの開閉状態。API 上では "open" / "closed" として表現する。</summary>
[JsonConverter(typeof(DoorStateJsonConverter))]
public enum DoorState
{
    Closed,
    Open,
}

/// <summary>DoorState と API の文字列表現を相互変換する。</summary>
public sealed class DoorStateJsonConverter : JsonConverter<DoorState>
{
    public const string OpenValue = "open";
    public const string ClosedValue = "closed";

    public override DoorState Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return value switch
        {
            OpenValue => DoorState.Open,
            ClosedValue => DoorState.Closed,
            _ => throw new JsonException($"Unknown door state: {value}"),
        };
    }

    public override void Write(Utf8JsonWriter writer, DoorState value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value == DoorState.Open ? OpenValue : ClosedValue);
    }
}
