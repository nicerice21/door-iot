using DoorMonitor.Core.Doors;
using Xunit;

namespace DoorMonitor.Core.Tests.Doors;

public sealed class LivenessTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 21, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Online_within_threshold()
    {
        Assert.True(Liveness.IsOnline(Now.AddSeconds(-299), Now, 300));
        Assert.True(Liveness.IsOnline(Now, Now, 300));
    }

    [Fact]
    public void Offline_after_threshold()
    {
        Assert.False(Liveness.IsOnline(Now.AddSeconds(-301), Now, 300));
    }

    [Fact]
    public void Offline_when_never_seen()
    {
        Assert.False(Liveness.IsOnline(null, Now, 300));
    }

    [Fact]
    public void Tolerates_clock_skew_into_future()
    {
        Assert.True(Liveness.IsOnline(Now.AddSeconds(10), Now, 300));
    }
}
