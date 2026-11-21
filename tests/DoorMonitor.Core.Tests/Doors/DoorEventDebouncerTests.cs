using DoorMonitor.Core.Doors;
using Xunit;

namespace DoorMonitor.Core.Tests.Doors;

public sealed class DoorEventDebouncerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 21, 5, 45, 10, TimeSpan.Zero);

    [Fact]
    public void Drops_duplicate_state_within_window()
    {
        var debouncer = new DoorEventDebouncer(TimeSpan.FromMilliseconds(500));

        Assert.True(debouncer.ShouldForward("dev", DoorState.Open, isHeartbeat: false, T0));
        Assert.False(debouncer.ShouldForward("dev", DoorState.Open, isHeartbeat: false, T0.AddMilliseconds(200)));
        Assert.True(debouncer.ShouldForward("dev", DoorState.Open, isHeartbeat: false, T0.AddMilliseconds(600)));
    }

    [Fact]
    public void Always_forwards_heartbeat()
    {
        var debouncer = new DoorEventDebouncer(TimeSpan.FromSeconds(5));

        Assert.True(debouncer.ShouldForward("dev", DoorState.Closed, isHeartbeat: false, T0));
        Assert.True(debouncer.ShouldForward("dev", DoorState.Closed, isHeartbeat: true, T0.AddSeconds(1)));
    }

    [Fact]
    public void Forwards_state_change_immediately()
    {
        var debouncer = new DoorEventDebouncer(TimeSpan.FromSeconds(5));

        Assert.True(debouncer.ShouldForward("dev", DoorState.Closed, isHeartbeat: false, T0));
        Assert.True(debouncer.ShouldForward("dev", DoorState.Open, isHeartbeat: false, T0.AddMilliseconds(100)));
    }

    [Fact]
    public void Tracks_devices_independently()
    {
        var debouncer = new DoorEventDebouncer(TimeSpan.FromSeconds(5));

        Assert.True(debouncer.ShouldForward("dev-a", DoorState.Open, isHeartbeat: false, T0));
        Assert.True(debouncer.ShouldForward("dev-b", DoorState.Open, isHeartbeat: false, T0));
    }
}
