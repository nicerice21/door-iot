using System.Runtime.InteropServices;
using DoorMonitor.Gateway.Configuration;
using DoorMonitor.Gateway.Logging;
using DoorMonitor.Gateway.Services;

namespace DoorMonitor.Gateway.Cli;

/// <summary>
/// Ubuntu など画面のない環境でゲートウェイを常駐させる。
/// 設定はファイルとコマンドライン引数、ログは標準出力（systemd の journal）へ出す。
/// </summary>
internal static class GatewayConsoleHost
{
    public static async Task<int> RunAsync(CommandLineOptions options)
    {
        var settingsPath = options.SettingsPath ?? GatewaySettings.DefaultPath;
        var store = new GatewaySettingsStore(settingsPath);

        GatewaySettings settings;
        if (File.Exists(settingsPath))
        {
            settings = store.Load();
        }
        else if (options.HasOverrides)
        {
            settings = new GatewaySettings();
        }
        else
        {
            // 初回起動時に編集のとりかかりになるよう、既定値の設定ファイルを作る。
            store.Load();
            Console.Error.WriteLine($"設定ファイルがないため、既定値で作成しました: {settingsPath}");
            Console.Error.WriteLine("値を編集してから再実行してください（--print-config で内容を確認できます）。");
            return 2;
        }

        options.ApplyTo(settings);

        if (!TryValidate(settings, out var validationError))
        {
            Console.Error.WriteLine($"設定を確認してください: {validationError}");
            Console.Error.WriteLine($"設定ファイル: {settingsPath}");
            return 2;
        }

        if (options.PrintConfig)
        {
            WriteSettings(Console.Out, settings, settingsPath);
            return 0;
        }

        WriteSettings(Console.Out, settings, settingsPath);

        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            Console.WriteLine("警告: API キーが未設定です。書き込み API は認証なしで呼び出します。");
        }

        return await RunServiceAsync(settings, settingsPath);
    }

    private static async Task<int> RunServiceAsync(GatewaySettings settings, string settingsPath)
    {
        var log = new InMemoryLog();
        using var sink = new ConsoleLogSink(log);
        await using var service = new GatewayService(settings, log);
        using var shutdown = new CancellationTokenSource();

        service.StatusChanged += (_, e) =>
            Console.WriteLine($"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] 状態: {e.Summary}（{e.Status}）");

        ConsoleCancelEventHandler cancelHandler = (_, e) =>
        {
            e.Cancel = true;
            shutdown.Cancel();
        };
        Console.CancelKeyPress += cancelHandler;

        // systemd の停止要求（SIGTERM）で再送キューのファイルを閉じてから終了する。
        PosixSignalRegistration? sigterm = null;
        if (!OperatingSystem.IsWindows())
        {
            sigterm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
            {
                context.Cancel = true;
                shutdown.Cancel();
            });
        }

        try
        {
            service.Start();
            await Task.Delay(Timeout.InfiniteTimeSpan, shutdown.Token);
        }
        catch (OperationCanceledException)
        {
            // Ctrl+C または SIGTERM による終了要求。
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
            sigterm?.Dispose();
            await service.DisposeAsync();
        }

        Console.WriteLine($"ゲートウェイを停止しました。設定: {settingsPath}");
        return 0;
    }

    private static bool TryValidate(GatewaySettings settings, out string? error)
    {
        if (string.IsNullOrWhiteSpace(settings.GatewayId))
        {
            error = "ゲートウェイ ID が空です。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(settings.ComPort))
        {
            error = "シリアルポートが空です。";
            return false;
        }

        if (!Uri.TryCreate(settings.ApiBaseUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            error = "API ベース URL は http:// または https:// で始まる URL を指定してください。";
            return false;
        }

        error = null;
        return true;
    }

    private static void WriteSettings(TextWriter writer, GatewaySettings settings, string settingsPath)
    {
        writer.WriteLine($"設定ファイル: {settingsPath}");
        writer.WriteLine($"ゲートウェイ ID: {settings.GatewayId}");
        writer.WriteLine($"シリアルポート: {settings.ComPort}（{settings.BaudRate} bps）");
        writer.WriteLine($"API ベース URL: {settings.ApiBaseUrl}");
        writer.WriteLine($"API キー: {MaskApiKey(settings.ApiKey)}");
        writer.WriteLine($"再送キュー: {settings.TelemetryQueueDirectory}（上限 {settings.TelemetryQueueCapacity} 件）");
    }

    private static string MaskApiKey(string apiKey)
        => string.IsNullOrWhiteSpace(apiKey) ? "(未設定)" : "********";
}

/// <summary>InMemoryLog へ追加されたログを標準出力と標準エラーへ流す。</summary>
internal sealed class ConsoleLogSink : IDisposable
{
    private readonly InMemoryLog _log;

    public ConsoleLogSink(InMemoryLog log)
    {
        _log = log;
        _log.Appended += OnAppended;
    }

    private static void OnAppended(object? sender, LogEntry entry)
    {
        var writer = entry.Level == LogLevel.Info ? Console.Out : Console.Error;
        writer.WriteLine(entry);
    }

    public void Dispose() => _log.Appended -= OnAppended;
}
