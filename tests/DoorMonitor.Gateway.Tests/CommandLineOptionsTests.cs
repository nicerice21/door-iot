using DoorMonitor.Gateway.Cli;
using DoorMonitor.Gateway.Configuration;
using Xunit;

namespace DoorMonitor.Gateway.Tests;

public sealed class CommandLineOptionsTests
{
    [Fact]
    public void ParsesValuesSeparatedBySpace()
    {
        var parsed = CommandLineOptions.TryParse(
            ["--com-port", "/dev/ttyUSB0", "--api-base-url", "https://example.com", "--api-key", "secret"],
            out var options,
            out var error);

        Assert.True(parsed, error);
        Assert.Equal("/dev/ttyUSB0", options.ComPort);
        Assert.Equal("https://example.com", options.ApiBaseUrl);
        Assert.Equal("secret", options.ApiKey);
        Assert.True(options.HasOverrides);
    }

    [Fact]
    public void ParsesValuesJoinedByEquals()
    {
        var parsed = CommandLineOptions.TryParse(
            ["--com-port=/dev/ttyACM0", "--gateway-id=gw-ubuntu"],
            out var options,
            out var error);

        Assert.True(parsed, error);
        Assert.Equal("/dev/ttyACM0", options.ComPort);
        Assert.Equal("gw-ubuntu", options.GatewayId);
    }

    [Fact]
    public void ParsesFlags()
    {
        var parsed = CommandLineOptions.TryParse(
            ["--help", "--version", "--list-ports", "--print-config"],
            out var options,
            out var error);

        Assert.True(parsed, error);
        Assert.True(options.ShowHelp);
        Assert.True(options.ShowVersion);
        Assert.True(options.ListPorts);
        Assert.True(options.PrintConfig);
        Assert.False(options.HasOverrides);
    }

    [Fact]
    public void RejectsUnknownOption()
    {
        var parsed = CommandLineOptions.TryParse(["--unknown"], out _, out var error);

        Assert.False(parsed);
        Assert.Contains("--unknown", error);
    }

    [Fact]
    public void RejectsOptionWithoutValue()
    {
        var parsed = CommandLineOptions.TryParse(["--com-port"], out _, out var error);

        Assert.False(parsed);
        Assert.Contains("--com-port", error);
    }

    [Fact]
    public void ApplyToOnlyOverridesSpecifiedValues()
    {
        var settings = new GatewaySettings
        {
            GatewayId = "gw-pc-01",
            ComPort = "COM3",
            ApiBaseUrl = "https://original.example.com",
            ApiKey = "original",
        };

        Assert.True(CommandLineOptions.TryParse(["--com-port", "/dev/ttyUSB0"], out var options, out _));
        options.ApplyTo(settings);

        Assert.Equal("/dev/ttyUSB0", settings.ComPort);
        Assert.Equal("gw-pc-01", settings.GatewayId);
        Assert.Equal("https://original.example.com", settings.ApiBaseUrl);
        Assert.Equal("original", settings.ApiKey);
    }

    [Fact]
    public void DefaultComPortDependsOnPlatform()
    {
        var expected = OperatingSystem.IsWindows() ? "COM3" : "/dev/ttyUSB0";

        Assert.Equal(expected, GatewaySettings.DefaultComPort);
        Assert.Equal(expected, new GatewaySettings().ComPort);
    }
}
