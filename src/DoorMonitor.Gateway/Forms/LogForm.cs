using DoorMonitor.Gateway.Logging;

namespace DoorMonitor.Gateway.Forms;

/// <summary>直近の通信ログを表示する。1秒ごとに最新内容へ更新する。</summary>
public sealed class LogForm : Form
{
    private readonly InMemoryLog _log;
    private readonly TextBox _text = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        WordWrap = false,
        Font = new Font("Consolas", 9),
    };

    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1000 };

    public LogForm(InMemoryLog log)
    {
        _log = log;

        Text = "通信ログ";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(760, 420);
        AutoScaleMode = AutoScaleMode.Dpi;

        Controls.Add(_text);
        _timer.Tick += (_, _) => RefreshLog();
        _timer.Start();

        RefreshLog();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _timer.Stop();
        _timer.Dispose();
        base.OnFormClosed(e);
    }

    private void RefreshLog()
    {
        var wasAtEnd = _text.SelectionStart >= _text.TextLength;
        var lines = _log.Snapshot().Reverse().Select(entry => entry.ToString());

        _text.Text = string.Join(Environment.NewLine, lines);

        if (wasAtEnd)
        {
            _text.SelectionStart = _text.TextLength;
            _text.ScrollToCaret();
        }
    }
}
