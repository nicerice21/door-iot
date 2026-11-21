namespace DoorMonitor.Core.Doors;

/// <summary>
/// クラウド側に保持する表示用メタデータ。子機ファームウェアを変更せずに
/// 部屋名・フロア・マップ座標・警告閾値を変更できるよう、デバイスIDに紐づける。
/// </summary>
public sealed record DoorMeta
{
    public string? Floor { get; init; }

    public string? MapId { get; init; }

    /// <summary>フロアマップ上の X 座標（%）。</summary>
    public double? MapX { get; init; }

    /// <summary>フロアマップ上の Y 座標（%）。</summary>
    public double? MapY { get; init; }

    /// <summary>警告色に切り替える開放時間（秒）。</summary>
    public int AlertThresholdSec { get; init; } = 300;
}
