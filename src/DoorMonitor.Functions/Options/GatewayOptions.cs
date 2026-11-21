namespace DoorMonitor.Functions.Options;

public sealed class GatewayOptions
{
    public const string SectionName = "Gateway";

    /// <summary>公開 API が単一ゲートウェイとして返す ID。</summary>
    public string Id { get; set; } = "gw-pc-01";
}
