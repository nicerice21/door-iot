using DoorMonitor.Core.Doors;

namespace DoorMonitor.Functions.Dtos;

/// <summary>GET /api/doors/{deviceId}/history のレスポンス。</summary>
public sealed record DoorHistoryResponse(string DeviceId, IReadOnlyList<DoorEventDto> Events);

/// <summary>履歴 1 件。closed のときだけ OpenedAt が入る。</summary>
public sealed record DoorEventDto(DoorState State, DateTimeOffset ChangedAt, DateTimeOffset? OpenedAt);
