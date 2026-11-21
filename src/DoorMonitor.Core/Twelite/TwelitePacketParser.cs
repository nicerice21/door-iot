using System.Buffers.Binary;
using System.Globalization;

namespace DoorMonitor.Core.Twelite;

/// <summary>
/// MONOSTICK が出力する App_Twelite の ASCII 電文をパースする。
/// 形式: ':' + 23バイトのペイロード + チェックサム(LRC8) + CRLF（すべて16進文字列）。
/// </summary>
public static class TwelitePacketParser
{
    /// <summary>0x81 電文のペイロード長（チェックサムを含まない）。</summary>
    public const int PayloadLength = 23;

    private const byte StatusCommand = 0x81;
    private const byte ProtocolVersion = 0x01;

    public static bool TryParse(string? line, out TwelitePacket? packet)
        => TryParse(line, out packet, out _);

    public static bool TryParse(string? line, out TwelitePacket? packet, out string? error)
    {
        packet = null;
        error = null;

        if (string.IsNullOrWhiteSpace(line))
        {
            error = "電文が空です。";
            return false;
        }

        var span = line.AsSpan().Trim();
        if (span[0] != ':')
        {
            error = "先頭が ':' ではありません。";
            return false;
        }

        var hex = span[1..];
        if (hex.Length != (PayloadLength + 1) * 2)
        {
            error = $"0x81 電文の長さが不正です（{hex.Length / 2} バイト）。";
            return false;
        }

        Span<byte> bytes = stackalloc byte[PayloadLength + 1];
        for (var i = 0; i < bytes.Length; i++)
        {
            if (!byte.TryParse(hex.Slice(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out bytes[i]))
            {
                error = $"16進数として解釈できない文字があります（位置 {i}）。";
                return false;
            }
        }

        var payload = bytes[..PayloadLength];
        var checksum = bytes[PayloadLength];

        if (!IsValidLrc8(payload, checksum))
        {
            error = "チェックサムが一致しません。";
            return false;
        }

        if (payload[1] != StatusCommand)
        {
            error = $"未対応のコマンドです（0x{payload[1]:X2}）。";
            return false;
        }

        if (payload[3] != ProtocolVersion)
        {
            error = $"未対応のプロトコルバージョンです（0x{payload[3]:X2}）。";
            return false;
        }

        var serialId = BinaryPrimitives.ReadUInt32BigEndian(payload[5..9]);
        if ((serialId & 0x8000_0000) == 0)
        {
            error = "送信元シリアルIDの最上位ビットが 1 ではありません。";
            return false;
        }

        // デジタル信号: bit0 = DI1。0 が High、1 が Low。
        // リードスイッチ(NO)はドア閉で導通するため、DI1 Low = ドア閉。
        var digitalSignals = payload[16];
        var digitalMask = payload[17];

        packet = new TwelitePacket
        {
            LogicalDeviceId = payload[0],
            PacketIdentifier = payload[2],
            Lqi = payload[4],
            SerialId = serialId,
            DeviceTimestamp = BinaryPrimitives.ReadUInt16BigEndian(payload[10..12]),
            RelayCount = payload[12],
            VoltageMv = BinaryPrimitives.ReadUInt16BigEndian(payload[13..15]),
            Di1High = (digitalSignals & 0x01) == 0,
            Di1Valid = (digitalMask & 0x01) != 0,
            IsPeriodic = (digitalSignals & 0x80) != 0,
        };

        return true;
    }

    /// <summary>LRC8（ペイロード合計の2の補数）を検証する。</summary>
    private static bool IsValidLrc8(ReadOnlySpan<byte> payload, byte checksum)
    {
        var sum = 0;
        foreach (var b in payload)
        {
            sum = (sum + b) & 0xFF;
        }

        return ((sum + checksum) & 0xFF) == 0;
    }
}
