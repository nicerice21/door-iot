using System.IO.Ports;
using System.Reflection;

namespace DoorMonitor.Gateway.Cli;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (!CommandLineOptions.TryParse(args, out var options, out var error))
        {
            Console.Error.WriteLine($"引数が正しくありません: {error}");
            Console.Error.WriteLine();
            CommandLineOptions.WriteUsage(Console.Error);
            return 2;
        }

        if (options.ShowHelp)
        {
            CommandLineOptions.WriteUsage(Console.Out);
            return 0;
        }

        if (options.ShowVersion)
        {
            Console.WriteLine($"doormonitor-gateway {GetVersion()}");
            return 0;
        }

        if (options.ListPorts)
        {
            WriteSerialPorts();
            return 0;
        }

        try
        {
            return await GatewayConsoleHost.RunAsync(options);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ゲートウェイを起動できませんでした: {ex.Message}");
            return 1;
        }
    }

    private static string GetVersion()
    {
        var informational = typeof(Program).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        // SourceLink が付ける "+<commit>" は表示しない。
        return informational?.Split('+')[0] ?? "unknown";
    }

    private static void WriteSerialPorts()
    {
        string[] ports;
        try
        {
            ports = SerialPort.GetPortNames();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            Console.Error.WriteLine($"シリアルポートを取得できませんでした: {ex.Message}");
            return;
        }

        if (ports.Length == 0)
        {
            Console.WriteLine("シリアルポートが見つかりません。MONOSTICK が接続されているか確認してください。");
            return;
        }

        foreach (var port in ports.OrderBy(name => name, StringComparer.Ordinal))
        {
            Console.WriteLine(port);
        }
    }
}
