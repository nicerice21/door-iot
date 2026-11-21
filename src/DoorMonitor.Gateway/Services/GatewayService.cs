using DoorMonitor.Core.Doors;
using DoorMonitor.Core.Telemetry;
using DoorMonitor.Core.Twelite;
using DoorMonitor.Gateway.Cloud;
using DoorMonitor.Gateway.Configuration;
using DoorMonitor.Gateway.Logging;
using DoorMonitor.Gateway.Serial;

namespace DoorMonitor.Gateway.Services;

/// <summary>
/// シリアル受信 → パース → デバウンス → クラウド送信 を統括する。
/// 異常時は一定間隔でシリアル再接続を試み、状態をトレイアイコンへ通知する。
/// </summary>
public sealed class GatewayService : IAsyncDisposable
{
    private readonly GatewaySettings _settings;
    private readonly InMemoryLog _log;
    private readonly CloudApiClient _api;
    private readonly DoorEventDebouncer _debouncer;

    private CancellationTokenSource? _cts;
    private Task _serialTask = Task.CompletedTask;
    private Task _heartbeatTask = Task.CompletedTask;

    public GatewayService(GatewaySettings settings, InMemoryLog log)
    {
        _settings = settings;
        _log = log;
        _api = new CloudApiClient(settings.ApiBaseUrl, settings.ApiKey);
        _debouncer = new DoorEventDebouncer(TimeSpan.FromMilliseconds(Math.Max(0, settings.DebounceMilliseconds)));
    }

    public event EventHandler<GatewayStatusChangedEventArgs>? StatusChanged;

    public void Start()
    {
        if (_cts is not null)
        {
            throw new InvalidOperationException("GatewayService は既に開始されています。");
        }

        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        _log.Add($"ゲートウェイ {_settings.GatewayId} を開始しました（{_settings.ComPort} → {_settings.ApiBaseUrl}）");
        Notify(GatewayRuntimeStatus.Starting, "起動しました");

        // UI スレッドの SynchronizationContext を引き継がないよう、専用タスクで動かす。
        _serialTask = Task.Run(() => RunSerialLoopAsync(token), CancellationToken.None);
        _heartbeatTask = Task.Run(() => RunHeartbeatLoopAsync(token), CancellationToken.None);
    }

    private async Task RunSerialLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var reader = new MonostickReader(_settings.ComPort, _settings.BaudRate);
                reader.Open();
                _log.Add($"{_settings.ComPort} を開きました（{_settings.BaudRate} bps）");
                Notify(GatewayRuntimeStatus.SerialConnected, $"{_settings.ComPort} 接続中");

                await foreach (var line in reader.ReadLinesAsync(cancellationToken).ConfigureAwait(false))
                {
                    await HandleLineAsync(line, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.Add($"{_settings.ComPort} の受信に失敗しました: {ex.Message}", LogLevel.Warning);
                Notify(GatewayRuntimeStatus.SerialDisconnected, $"{_settings.ComPort} 未接続");

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, _settings.ReconnectDelaySeconds)), cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task HandleLineAsync(string line, CancellationToken cancellationToken)
    {
        if (!TwelitePacketParser.TryParse(line, out var packet, out var error) || packet is null)
        {
            _log.Add($"電文を無視しました: {error}", LogLevel.Warning);
            return;
        }

        if (!packet.Di1Valid)
        {
            _log.Add($"{packet.DeviceId} の DI1 が有効ではありません。配線を確認してください。", LogLevel.Warning);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if (!_debouncer.ShouldForward(packet.DeviceId, packet.DoorState, packet.IsPeriodic, now))
        {
            return;
        }

        var report = new TelemetryReport
        {
            GatewayId = _settings.GatewayId,
            DeviceId = packet.DeviceId,
            LogicalId = packet.LogicalDeviceId,
            State = packet.DoorState,
            BatteryMv = packet.VoltageMv,
            Lqi = packet.Lqi,
            IsHeartbeat = packet.IsPeriodic,
            ObservedAt = now,
        };

        try
        {
            await _api.SendTelemetryAsync(report, cancellationToken).ConfigureAwait(false);
            _log.Add($"{report.DeviceId} {report.State.ToString().ToLowerInvariant()} を送信しました");
            Notify(GatewayRuntimeStatus.SerialConnected, $"{_settings.ComPort} 接続中");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log.Add($"テレメトリ送信に失敗しました: {ex.Message}", LogLevel.Error);
            Notify(GatewayRuntimeStatus.ApiUnreachable, "API 通信エラー");
        }
    }

    private async Task RunHeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(10, _settings.HeartbeatIntervalSeconds)));

        try
        {
            await SendHeartbeatAsync(cancellationToken).ConfigureAwait(false);

            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                await SendHeartbeatAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 終了要求。
        }
    }

    private async Task SendHeartbeatAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _api.SendHeartbeatAsync(new GatewayHeartbeat(_settings.GatewayId, DateTimeOffset.UtcNow), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log.Add($"ハートビート送信に失敗しました: {ex.Message}", LogLevel.Warning);
            Notify(GatewayRuntimeStatus.ApiUnreachable, "API 通信エラー");
        }
    }

    private void Notify(GatewayRuntimeStatus status, string summary)
        => StatusChanged?.Invoke(this, new GatewayStatusChangedEventArgs(status, summary));

    public async Task StopAsync()
    {
        if (_cts is null)
        {
            return;
        }

        await _cts.CancelAsync().ConfigureAwait(false);

        try
        {
            await Task.WhenAll(_serialTask, _heartbeatTask).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 正常終了。
        }

        _cts.Dispose();
        _cts = null;
        _log.Add("ゲートウェイを停止しました");
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _api.Dispose();
    }
}
