using DoorMonitor.Gateway.Configuration;

namespace DoorMonitor.Gateway.Forms;

/// <summary>COM ポート・API エンドポイントなどを編集する小画面。</summary>
public sealed class SettingsForm : Form
{
    private readonly GatewaySettings _current;
    private readonly TextBox _gatewayId = new() { Dock = DockStyle.Fill };
    private readonly TextBox _comPort = new() { Dock = DockStyle.Fill };
    private readonly TextBox _apiBaseUrl = new() { Dock = DockStyle.Fill };
    private readonly TextBox _apiKey = new() { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
    private readonly NumericUpDown _heartbeatSeconds = new() { Dock = DockStyle.Fill, Minimum = 10, Maximum = 3600 };

    public SettingsForm(GatewaySettings current)
    {
        _current = current;
        Text = "ゲートウェイ設定";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(520, 260);

        _gatewayId.Text = current.GatewayId;
        _comPort.Text = current.ComPort;
        _apiBaseUrl.Text = current.ApiBaseUrl;
        _apiKey.Text = current.ApiKey;
        _heartbeatSeconds.Value = Math.Clamp(current.HeartbeatIntervalSeconds, 10, 3600);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 6,
            Padding = new Padding(12),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddRow(layout, 0, "ゲートウェイ ID", _gatewayId);
        AddRow(layout, 1, "COM ポート", _comPort);
        AddRow(layout, 2, "API ベース URL", _apiBaseUrl);
        AddRow(layout, 3, "API キー", _apiKey);
        AddRow(layout, 4, "ハートビート間隔（秒）", _heartbeatSeconds);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
        };

        var cancel = new Button { Text = "キャンセル", DialogResult = DialogResult.Cancel, AutoSize = true };
        var ok = new Button { Text = "保存", AutoSize = true };
        ok.Click += OnSave;
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);
        layout.Controls.Add(buttons, 1, 5);

        Controls.Add(layout);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    public GatewaySettings? Settings { get; private set; }

    private static void AddRow(TableLayoutPanel layout, int row, string label, Control input)
    {
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        layout.Controls.Add(input, 1, row);
    }

    private void OnSave(object? sender, EventArgs e)
    {
        var apiBaseUrl = _apiBaseUrl.Text.Trim();
        if (string.IsNullOrWhiteSpace(_gatewayId.Text) || string.IsNullOrWhiteSpace(_comPort.Text))
        {
            ShowError("ゲートウェイ ID と COM ポートは必須です。");
            return;
        }

        if (!Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            ShowError("API ベース URL は http:// または https:// で始まる URL を指定してください。");
            return;
        }

        Settings = new GatewaySettings
        {
            GatewayId = _gatewayId.Text.Trim(),
            ComPort = _comPort.Text.Trim(),
            BaudRate = _current.BaudRate,
            ApiBaseUrl = apiBaseUrl,
            ApiKey = _apiKey.Text.Trim(),
            HeartbeatIntervalSeconds = (int)_heartbeatSeconds.Value,
            DebounceMilliseconds = _current.DebounceMilliseconds,
            ReconnectDelaySeconds = _current.ReconnectDelaySeconds,
            TelemetryQueueDirectory = _current.TelemetryQueueDirectory,
            TelemetryQueueCapacity = _current.TelemetryQueueCapacity,
        };

        DialogResult = DialogResult.OK;
        Close();
    }

    private void ShowError(string message)
        => MessageBox.Show(this, message, "設定の確認", MessageBoxButtons.OK, MessageBoxIcon.Warning);
}
