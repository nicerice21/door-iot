namespace DoorMonitor.Gateway.Services;

public enum GatewayRuntimeStatus
{
    Starting,
    SerialConnected,
    SerialDisconnected,
    ApiUnreachable,
}

public sealed record GatewayStatusChangedEventArgs(GatewayRuntimeStatus Status, string Summary);
