using DoorMonitor.Core.Doors;
using DoorMonitor.Core.Telemetry;
using Xunit;

namespace DoorMonitor.Core.Tests.Doors;

public sealed class DoorStateReducerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 21, 5, 45, 10, TimeSpan.Zero);

    [Fact]
    public void Opening_sets_opened_at()
    {
        var snapshot = DoorStateReducer.Apply(null, Report(DoorState.Open, T0));

        Assert.Equal(DoorState.Open, snapshot.State);
        Assert.Equal(T0, snapshot.OpenedAt);
        Assert.Equal(T0, snapshot.LastChangedAt);
        Assert.Equal(T0, snapshot.LastSeenAt);
    }

    [Fact]
    public void Heartbeat_while_open_keeps_opened_at()
    {
        var opened = DoorStateReducer.Apply(null, Report(DoorState.Open, T0));

        var heartbeat = DoorStateReducer.Apply(opened, Report(DoorState.Open, T0.AddMinutes(5), isHeartbeat: true));

        Assert.Equal(T0, heartbeat.OpenedAt);
        Assert.Equal(T0, heartbeat.LastChangedAt);
        Assert.Equal(T0.AddMinutes(5), heartbeat.LastSeenAt);
    }

    [Fact]
    public void Closing_clears_opened_at()
    {
        var opened = DoorStateReducer.Apply(null, Report(DoorState.Open, T0));

        var closed = DoorStateReducer.Apply(opened, Report(DoorState.Closed, T0.AddSeconds(30)));

        Assert.Equal(DoorState.Closed, closed.State);
        Assert.Null(closed.OpenedAt);
        Assert.Equal(T0.AddSeconds(30), closed.LastChangedAt);
    }

    [Fact]
    public void Closing_while_closed_does_not_change_last_changed()
    {
        var closed = DoorStateReducer.Apply(null, Report(DoorState.Closed, T0));

        var again = DoorStateReducer.Apply(closed, Report(DoorState.Closed, T0.AddMinutes(1)));

        Assert.Null(again.OpenedAt);
        Assert.Equal(T0, again.LastChangedAt);
        Assert.Equal(T0.AddMinutes(1), again.LastSeenAt);
    }

    [Fact]
    public void Preserves_display_name()
    {
        var existing = DoorStateReducer.Apply(null, Report(DoorState.Closed, T0)) with
        {
            Name = "研究棟 3F 北側ドア",
        };

        var updated = DoorStateReducer.Apply(existing, Report(DoorState.Open, T0.AddMinutes(1)));

        Assert.Equal("研究棟 3F 北側ドア", updated.Name);
        Assert.Equal(DoorState.Open, updated.State);
    }

    private static TelemetryReport Report(DoorState state, DateTimeOffset observedAt, bool isHeartbeat = false) => new()
    {
        GatewayId = "gw-pc-01",
        DeviceId = "0x8201015A",
        LogicalId = 1,
        State = state,
        BatteryMv = 2950,
        Lqi = 135,
        IsHeartbeat = isHeartbeat,
        ObservedAt = observedAt,
    };
}
