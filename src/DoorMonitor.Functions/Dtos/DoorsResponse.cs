using DoorMonitor.Core.Doors;

namespace DoorMonitor.Functions.Dtos;

/// <summary>GET /api/doors のレスポンス。docs/requirements.md 5.1 の形に合わせる。</summary>
public sealed record DoorsResponse(GatewayDto Gateway, IReadOnlyList<DoorDto> Doors);

public sealed record GatewayDto(string Id, string Status, DateTimeOffset? LastSeenAt);

public sealed record DoorDto(
    string DeviceId,
    int? LogicalId,
    string Name,
    DoorState State,
    DateTimeOffset? OpenedAt,
    DateTimeOffset LastChangedAt,
    DateTimeOffset LastSeenAt,
    bool IsOnline,
    TelemetryDto? Telemetry,
    DoorMeta Meta);

public sealed record TelemetryDto(int? BatteryMv, int? Lqi);
