namespace DoorMonitor.Functions.Options;

/// <summary>死活判定の閾値（秒）。</summary>
public sealed class MonitoringOptions
{
    public const string SectionName = "Monitoring";

    public int GatewayOfflineThresholdSec { get; set; } = 300;

    public int DeviceOfflineThresholdSec { get; set; } = 300;
}
