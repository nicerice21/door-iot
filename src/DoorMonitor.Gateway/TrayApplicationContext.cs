using DoorMonitor.Gateway.Configuration;
using DoorMonitor.Gateway.Forms;
using DoorMonitor.Gateway.Logging;
using DoorMonitor.Gateway.Services;

namespace DoorMonitor.Gateway;

/// <summary>タスクトレイ常駐アプリ本体。シリアル受信・クラウド送信は GatewayService に委譲する。</summary>
public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly Control _dispatcher = new();
    private readonly InMemoryLog _log = new();
    private readonly GatewaySettingsStore _settingsStore = new();
    private readonly NotifyIcon _notifyIcon;

    private GatewaySettings _settings;
    private GatewayService _service;

    public TrayApplicationContext()
    {
        // バックグラウンドスレッドからの UI 更新に使う隠しコントロール。
        _ = _dispatcher.Handle;

        _settings = _settingsStore.Load();
        _service = CreateService();
        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "DoorMonitor ゲートウェイ: 起動中",
            Visible = true,
            ContextMenuStrip = BuildMenu(),
        };

        _service.Start();
    }

    private GatewayService CreateService()
    {
        var service = new GatewayService(_settings, _log);
        service.StatusChanged += OnStatusChanged;
        return service;
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        var settingsItem = new ToolStripMenuItem("設定...");
        settingsItem.Click += (_, _) => ShowSettings();

        var logItem = new ToolStripMenuItem("ログ...");
        logItem.Click += (_, _) => ShowLog();

        var exitItem = new ToolStripMenuItem("終了");
        exitItem.Click += (_, _) =>
        {
            _notifyIcon.Visible = false;
            ExitThread();
        };

        menu.Items.Add(settingsItem);
        menu.Items.Add(logItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);
        return menu;
    }

    private void OnStatusChanged(object? sender, GatewayStatusChangedEventArgs e)
    {
        if (_dispatcher.IsDisposed)
        {
            return;
        }

        _dispatcher.BeginInvoke(() => ApplyStatus(e));
    }

    private void ApplyStatus(GatewayStatusChangedEventArgs e)
    {
        _notifyIcon.Icon = e.Status switch
        {
            GatewayRuntimeStatus.SerialConnected => SystemIcons.Information,
            GatewayRuntimeStatus.SerialDisconnected => SystemIcons.Warning,
            GatewayRuntimeStatus.ApiUnreachable => SystemIcons.Error,
            _ => SystemIcons.Application,
        };

        var text = $"DoorMonitor: {e.Summary}";
        _notifyIcon.Text = text.Length > 63 ? text[..63] : text;
    }

    private void ShowSettings()
    {
        using var form = new SettingsForm(_settings);
        if (form.ShowDialog() != DialogResult.OK || form.Settings is null)
        {
            return;
        }

        _settingsStore.Save(form.Settings);
        _ = RestartServiceAsync(form.Settings);
    }

    private async Task RestartServiceAsync(GatewaySettings settings)
    {
        try
        {
            _service.StatusChanged -= OnStatusChanged;
            await _service.DisposeAsync();

            _settings = settings;
            _service = CreateService();
            _service.Start();
        }
        catch (Exception ex)
        {
            _log.Add($"設定の反映に失敗しました: {ex.Message}", LogLevel.Error);
        }
    }

    private void ShowLog()
    {
        using var form = new LogForm(_log);
        form.ShowDialog();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                _service.StatusChanged -= OnStatusChanged;
                _service.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _log.Add($"終了処理でエラーが発生しました: {ex.Message}", LogLevel.Warning);
            }

            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _dispatcher.Dispose();
        }

        base.Dispose(disposing);
    }
}
