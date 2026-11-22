using DoorMonitor.Core.Doors;
using DoorMonitor.Core.Telemetry;
using Xunit;

namespace DoorMonitor.Core.Tests.Doors;

public sealed class DoorEventTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 21, 5, 45, 10, TimeSpan.Zero);

    [Fact]
    public void 初回受信では履歴を作らない()
    {
        var updated = DoorStateReducer.Apply(null, Report(DoorState.Open, T0));

        Assert.Null(DoorStateReducer.CreateEvent(null, updated));
    }

    [Fact]
    public void 状態が変わらない受信では履歴を作らない()
    {
        var closed = DoorStateReducer.Apply(null, Report(DoorState.Closed, T0));
        var stillClosed = DoorStateReducer.Apply(closed, Report(DoorState.Closed, T0.AddMinutes(5)));
        Assert.Null(DoorStateReducer.CreateEvent(closed, stillClosed));

        var opened = DoorStateReducer.Apply(closed, Report(DoorState.Open, T0.AddMinutes(1)));
        var heartbeat = DoorStateReducer.Apply(opened, Report(DoorState.Open, T0.AddMinutes(6), isHeartbeat: true));
        Assert.Null(DoorStateReducer.CreateEvent(opened, heartbeat));
    }

    [Fact]
    public void 開いたときに開イベントを作る()
    {
        var closed = DoorStateReducer.Apply(null, Report(DoorState.Closed, T0));
        var openedAt = T0.AddMinutes(1);
        var updated = DoorStateReducer.Apply(closed, Report(DoorState.Open, openedAt));

        var doorEvent = DoorStateReducer.CreateEvent(closed, updated);

        Assert.NotNull(doorEvent);
        Assert.Equal("0x8201015A", doorEvent!.DeviceId);
        Assert.Equal(DoorState.Open, doorEvent.State);
        Assert.Equal(openedAt, doorEvent.ChangedAt);
        Assert.Null(doorEvent.OpenedAt);
    }

    [Fact]
    public void 閉じたときに開放開始時刻を持つ閉イベントを作る()
    {
        var closed = DoorStateReducer.Apply(null, Report(DoorState.Closed, T0));
        var opened = DoorStateReducer.Apply(closed, Report(DoorState.Open, T0.AddMinutes(1)));
        var closedAt = T0.AddMinutes(6);
        var updated = DoorStateReducer.Apply(opened, Report(DoorState.Closed, closedAt));

        var doorEvent = DoorStateReducer.CreateEvent(opened, updated);

        Assert.NotNull(doorEvent);
        Assert.Equal(DoorState.Closed, doorEvent!.State);
        Assert.Equal(closedAt, doorEvent.ChangedAt);
        Assert.Equal(T0.AddMinutes(1), doorEvent.OpenedAt);
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
