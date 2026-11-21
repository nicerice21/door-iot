using DoorMonitor.Core.Twelite;
using Xunit;

namespace DoorMonitor.Core.Tests.Twelite;

public sealed class TwelitePacketParserTests
{
    // 公式マニュアルの出力例（DI1 Low / 定期送信）。
    private const string OfficialExample = ":78811501C98201015A000391000C2E00810301FFFFFFFFFB";

    [Fact]
    public void Parses_official_app_twelite_example()
    {
        var parsed = TwelitePacketParser.TryParse(OfficialExample, out var packet, out var error);

        Assert.True(parsed, error);
        Assert.NotNull(packet);
        Assert.Equal(0x78, packet.LogicalDeviceId);
        Assert.Equal(0x8201015Au, packet.SerialId);
        Assert.Equal(0x15, packet.PacketIdentifier);
        Assert.Equal(0xC9, packet.Lqi);
        Assert.Equal(3118, packet.VoltageMv);
        Assert.Equal(0x0391, packet.DeviceTimestamp);
        Assert.False(packet.Di1High);
        Assert.True(packet.Di1Valid);
        Assert.True(packet.IsPeriodic);
        Assert.Equal("0x8201015A", packet.DeviceId);
        Assert.Equal(Core.Doors.DoorState.Closed, packet.DoorState);
    }

    [Fact]
    public void Rejects_invalid_checksum()
    {
        var broken = OfficialExample[..^2] + "00";

        var parsed = TwelitePacketParser.TryParse(broken, out var packet, out var error);

        Assert.False(parsed);
        Assert.Null(packet);
        Assert.Contains("チェックサム", error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("78811501C9")]
    [InlineData("78811501C98201015A000391000C2E00810301FFFFFFFFFB")]
    [InlineData(":Z8811501C98201015A000391000C2E00810301FFFFFFFFFB")]
    public void Rejects_malformed_lines(string line)
    {
        Assert.False(TwelitePacketParser.TryParse(line, out _, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }
}
