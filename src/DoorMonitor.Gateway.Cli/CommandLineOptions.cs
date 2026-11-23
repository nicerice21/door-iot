using DoorMonitor.Gateway.Configuration;

namespace DoorMonitor.Gateway.Cli;

/// <summary>ヘッドレス版ゲートウェイのコマンドライン引数。指定値は設定ファイルへ保存しない。</summary>
public sealed class CommandLineOptions
{
    public string? SettingsPath { get; private set; }

    public string? ComPort { get; private set; }

    public string? ApiBaseUrl { get; private set; }

    public string? ApiKey { get; private set; }

    public string? GatewayId { get; private set; }

    public bool ShowHelp { get; private set; }

    public bool ShowVersion { get; private set; }

    public bool ListPorts { get; private set; }

    public bool PrintConfig { get; private set; }

    /// <summary>設定ファイルの値を上書きする指定があるか。</summary>
    public bool HasOverrides =>
        ComPort is not null
        || ApiBaseUrl is not null
        || ApiKey is not null
        || GatewayId is not null;

    public static bool TryParse(string[] args, out CommandLineOptions options, out string? error)
    {
        ArgumentNullException.ThrowIfNull(args);

        options = new CommandLineOptions();
        error = null;

        for (var i = 0; i < args.Length; i++)
        {
            SplitArgument(args[i], out var name, out var inlineValue);

            switch (name)
            {
                case "-h":
                case "-?":
                case "--help":
                    options.ShowHelp = true;
                    break;
                case "--version":
                    options.ShowVersion = true;
                    break;
                case "--list-ports":
                    options.ListPorts = true;
                    break;
                case "--print-config":
                    options.PrintConfig = true;
                    break;
                case "--settings":
                    if (!TryTakeValue(args, ref i, name, inlineValue, out var settingsPath, out error))
                    {
                        return false;
                    }

                    options.SettingsPath = ExpandHome(settingsPath);
                    break;
                case "--com-port":
                    if (!TryTakeValue(args, ref i, name, inlineValue, out var comPort, out error))
                    {
                        return false;
                    }

                    options.ComPort = comPort;
                    break;
                case "--api-base-url":
                    if (!TryTakeValue(args, ref i, name, inlineValue, out var apiBaseUrl, out error))
                    {
                        return false;
                    }

                    options.ApiBaseUrl = apiBaseUrl;
                    break;
                case "--api-key":
                    if (!TryTakeValue(args, ref i, name, inlineValue, out var apiKey, out error))
                    {
                        return false;
                    }

                    options.ApiKey = apiKey;
                    break;
                case "--gateway-id":
                    if (!TryTakeValue(args, ref i, name, inlineValue, out var gatewayId, out error))
                    {
                        return false;
                    }

                    options.GatewayId = gatewayId;
                    break;
                default:
                    error = $"不明なオプションです: {args[i]}";
                    return false;
            }
        }

        return true;
    }

    /// <summary>設定ファイルから読み込んだ値へ、コマンドラインの指定を上書きする。</summary>
    public void ApplyTo(GatewaySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (GatewayId is not null)
        {
            settings.GatewayId = GatewayId;
        }

        if (ComPort is not null)
        {
            settings.ComPort = ComPort;
        }

        if (ApiBaseUrl is not null)
        {
            settings.ApiBaseUrl = ApiBaseUrl;
        }

        if (ApiKey is not null)
        {
            settings.ApiKey = ApiKey;
        }
    }

    public static void WriteUsage(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteLine("doormonitor-gateway - DoorMonitor ゲートウェイ（ヘッドレス版）");
        writer.WriteLine();
        writer.WriteLine("使い方:");
        writer.WriteLine("  doormonitor-gateway [オプション]");
        writer.WriteLine();
        writer.WriteLine("オプション:");
        writer.WriteLine($"  --settings <path>      設定ファイル（既定: {GatewaySettings.DefaultPath}）");
        writer.WriteLine("  --com-port <path>      シリアルポート（例: /dev/ttyUSB0）");
        writer.WriteLine("  --api-base-url <url>   Azure Functions のベース URL");
        writer.WriteLine("  --api-key <key>        書き込み API のキー");
        writer.WriteLine("  --gateway-id <id>      ゲートウェイ ID");
        writer.WriteLine("  --list-ports           利用可能なシリアルポートを表示して終了");
        writer.WriteLine("  --print-config         有効な設定を表示して終了（API キーは伏せ字）");
        writer.WriteLine("  --version              バージョンを表示して終了");
        writer.WriteLine("  -h, --help             このヘルプを表示して終了");
        writer.WriteLine();
        writer.WriteLine("コマンドラインで指定した値は設定ファイルへ保存されません。");
        writer.WriteLine("設定ファイルがない場合は既定値で作成し、編集を促して終了します。");
        writer.WriteLine();
        writer.WriteLine("例:");
        writer.WriteLine("  doormonitor-gateway --com-port /dev/ttyUSB0 \\");
        writer.WriteLine("    --api-base-url https://func-example.azurewebsites.net --api-key $API_KEY");
    }

    private static void SplitArgument(string arg, out string name, out string? value)
    {
        name = arg;
        value = null;

        if (!arg.StartsWith("--", StringComparison.Ordinal))
        {
            return;
        }

        var separator = arg.IndexOf('=');
        if (separator > 2)
        {
            name = arg[..separator];
            value = arg[(separator + 1)..];
        }
    }

    private static bool TryTakeValue(
        string[] args,
        ref int index,
        string name,
        string? inlineValue,
        out string value,
        out string? error)
    {
        if (inlineValue is not null)
        {
            value = inlineValue;
            error = null;
            return true;
        }

        if (index + 1 >= args.Length)
        {
            value = string.Empty;
            error = $"{name} の値がありません。";
            return false;
        }

        index++;
        value = args[index];
        error = null;
        return true;
    }

    /// <summary>シェルが展開しなかった先頭の ~ をホームディレクトリへ置き換える。</summary>
    private static string ExpandHome(string path)
    {
        if (path == "~")
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        if (path.StartsWith("~/", StringComparison.Ordinal) || path.StartsWith("~\\", StringComparison.Ordinal))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, path[2..]);
        }

        return path;
    }
}
