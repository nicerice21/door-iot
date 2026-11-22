using DoorMonitor.Core.Doors;
using DoorMonitor.Functions.Dtos;

namespace DoorMonitor.Functions.Services;

internal static class DoorDtoFactory
{
    public static DoorDto Create(DoorSnapshot door, DateTimeOffset now, int deviceOfflineThresholdSec)
    {
        var isOnline = Liveness.IsOnline(door.LastSeenAt, now, deviceOfflineThresholdSec);

        return new DoorDto(
            DeviceId: door.DeviceId,
            LogicalId: door.LogicalId,
            Name: string.IsNullOrWhiteSpace(door.Name)
                ? $"ドア {door.LogicalId?.ToString() ?? door.DeviceId}"
                : door.Name,
            State: door.State,
            OpenedAt: door.OpenedAt,
            LastChangedAt: door.LastChangedAt,
            LastSeenAt: door.LastSeenAt,
            IsOnline: isOnline,
            Telemetry: door.BatteryMv is null && door.Lqi is null
                ? null
                : new TelemetryDto(door.BatteryMv, door.Lqi));
    }
}
