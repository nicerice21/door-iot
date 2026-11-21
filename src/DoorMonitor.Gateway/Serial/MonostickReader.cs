using System.IO.Ports;
using System.Runtime.CompilerServices;
using System.Text;

namespace DoorMonitor.Gateway.Serial;

/// <summary>
/// MONOSTICK (USB シリアル) から App_Twelite の ASCII 電文を1行ずつ読み出す。
/// 切断時は呼び出し側が再生成して再接続する。
/// </summary>
public sealed class MonostickReader : IDisposable
{
    private const int LineBufferLimit = 512;

    private readonly SerialPort _port;

    public MonostickReader(string portName, int baudRate)
    {
        _port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
        {
            Handshake = Handshake.None,
            ReadBufferSize = 4096,
            WriteBufferSize = 1024,
        };
    }

    public void Open() => _port.Open();

    public async IAsyncEnumerable<string> ReadLinesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var buffer = new byte[512];
        var line = new StringBuilder(128);

        while (!cancellationToken.IsCancellationRequested)
        {
            int read;
            try
            {
                read = await _port.BaseStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }

            if (read == 0)
            {
                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
                continue;
            }

            for (var i = 0; i < read; i++)
            {
                var ch = (char)buffer[i];
                if (ch == '\n')
                {
                    var text = line.ToString().TrimEnd('\r');
                    line.Clear();
                    if (text.Length > 0)
                    {
                        yield return text;
                    }
                }
                else if (ch != '\r')
                {
                    line.Append(ch);
                    if (line.Length > LineBufferLimit)
                    {
                        // 壊れたデータで行バッファが伸び続けるのを防ぐ。
                        line.Clear();
                    }
                }
            }
        }
    }

    public void Dispose() => _port.Dispose();
}
