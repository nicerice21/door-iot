using DoorMonitor.Core.Doors;
using DoorMonitor.Core.Telemetry;

namespace DoorMonitor.Functions.Storage;

/// <summary>クラウド側で保持するドア状態・ゲートウェイ状態の読み書き。</summary>
public interface IDoorRepository
{
    Task<IReadOnlyList<DoorSnapshot>> GetDoorsAsync(CancellationToken cancellationToken);

    Task<DoorSnapshot?> GetDoorAsync(string deviceId, CancellationToken cancellationToken);

    Task UpsertDoorAsync(DoorSnapshot snapshot, CancellationToken cancellationToken);

    /// <summary>開閉履歴を保存する。同じ変化時刻のイベントは上書きする（再送に対して冪等）。</summary>
    Task UpsertDoorEventAsync(DoorEvent doorEvent, CancellationToken cancellationToken);

    /// <summary>指定した子機の履歴を新しい順に取得する。</summary>
    Task<IReadOnlyList<DoorEvent>> GetDoorEventsAsync(string deviceId, int limit, CancellationToken cancellationToken);

    /// <summary>電池電圧のサンプルを保存する。同じ時間帯のサンプルは上書きする。</summary>
    Task UpsertBatterySampleAsync(BatterySample sample, CancellationToken cancellationToken);

    /// <summary>指定した期間の電池電圧サンプルを古い順に取得する。</summary>
    Task<IReadOnlyList<BatterySample>> GetBatterySamplesAsync(
        string deviceId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken);

    Task<GatewayRecord?> GetGatewayAsync(string gatewayId, CancellationToken cancellationToken);

    Task TouchGatewayAsync(string gatewayId, DateTimeOffset lastSeenAt, CancellationToken cancellationToken);
}

public sealed record GatewayRecord(string Id, DateTimeOffset LastSeenAt);
