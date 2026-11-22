# 開発・実行手順

## 1. 前提環境

| 用途 | 必要なもの |
| :--- | :--- |
| .NET のビルド・テスト | .NET SDK 8 以降 |
| PC ゲートウェイの実行 | Windows 10/11 + .NET 8 Desktop Runtime |
| Web の開発 | Node.js 20 以降 |
| Azure Functions のローカル実行 | Azure Functions Core Tools v4 |
| Functions ホストのローカルストレージ | Azurite（`AzureWebJobsStorage=UseDevelopmentStorage=true` を使う場合） |
| Table Storage モード | Azure Storage アカウント（または Azurite） |

## 2. ビルドとテスト

```powershell
dotnet build DoorMonitor.sln
dotnet test tests\DoorMonitor.Core.Tests\DoorMonitor.Core.Tests.csproj
```

```powershell
cd web
npm install
npm test
npm run build
npm run lint
```

`DoorMonitor.Core.Tests` は開発機に .NET 8 ランタイムがなくても動くよう `RollForward=LatestMajor` を設定しています。

## 3. 構成と責務

| パス | 責務 |
| :--- | :--- |
| `src/DoorMonitor.Core` | ドメインモデル、開閉状態の遷移、デバウンス、死活判定、TWELITE パーサ |
| `src/DoorMonitor.Gateway` | シリアル受信、クラウド送信、タスクトレイ UI |
| `src/DoorMonitor.Functions` | HTTP API、Table Storage / メモリリポジトリ |
| `web` | 状態カード表示、ポーリング、毎秒の経過時間更新 |
| `infra` | Azure リソース定義（Bicep） |
| `tests/DoorMonitor.Core.Tests` | Core の単体テスト |

Azure へのデプロイ手順は [deployment.md](deployment.md) を参照してください。

### データの流れ

```text
TWELITE (App_Twelite 0x81)
  → MONOSTICK → Gateway (パース + デバウンス)
  → 再送キュー（%LOCALAPPDATA%\DoorMonitor\queue に永続化）
  → POST /api/telemetry（古い電文から順に送信。失敗時は指数バックオフで再試行）
  → DoorEvents（開閉が変化したときだけ履歴を記録）
  → DoorStateReducer（openedAt / lastSeenAt の更新）
  → Devices / Gateways テーブル（またはメモリ）
  → GET /api/doors（ポーリング） → React 画面（毎秒カウント）
  → GET /api/doors/{deviceId}/history（カードの「履歴」を開いたとき）
```

## 4. Azure Functions

### 4.1 ローカル起動

```powershell
Copy-Item src\DoorMonitor.Functions\local.settings.json.example src\DoorMonitor.Functions\local.settings.json
func start --project src\DoorMonitor.Functions
```

`local.settings.json` は Git 管理対象外です。

Table モード（Azurite）で確認する場合は、別ターミナルで Azurite を起動し、`local.settings.json` の `Storage__Mode` を `Table` に変更します。

```powershell
# ホストの AzureWebJobsStorage 健全性チェックも通すため、全サービスを起動する
azurite --location .azurite
func start --project src\DoorMonitor.Functions
```

2026-11-22 に Azure Functions Core Tools 4.17.0 / Azurite 3.38.0 で次を確認済みです。

- Table モードで `GET /api/doors` が応答し、Functions を再起動しても状態と履歴が残る
- `closed → open → open（ハートビート） → closed` の受信で履歴が 2 件（開・閉）になり、ハートビートは記録されない
- `?limit=1` で件数を制限でき、存在しない子機は 404、同じ変化時刻の再送では履歴が重複しない
- `ApiKey` 設定時、`X-Api-Key` なし／不正は 401、正しいキーは 202。`GET /api/doors` は認証なしで 200

### 4.2 API

| メソッド | パス | 認証 | 用途 |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/doors` | なし（公開） | Web 向け状態一覧 |
| `GET` | `/api/doors/{deviceId}/history?limit=20` | なし（公開） | 開閉履歴（新しい順、既定 20 件・最大 100 件） |
| `POST` | `/api/telemetry` | `X-Api-Key`（設定時） | 子機テレメトリ受信 |
| `POST` | `/api/heartbeat` | `X-Api-Key`（設定時） | ゲートウェイ死活通知 |

テレメトリの例:

```json
{
  "gatewayId": "gw-pc-01",
  "deviceId": "0x8201015A",
  "logicalId": 1,
  "state": "open",
  "batteryMv": 2950,
  "lqi": 135,
  "isHeartbeat": false,
  "observedAt": "2026-11-21T05:45:10Z"
}
```

`state` は `open` / `closed` のみです。死活は `isOnline`（子機）と `gateway.status`（親機）で表現します。

### 4.3 設定

| キー | 既定値 | 内容 |
| :--- | :--- | :--- |
| `Storage__Mode` | `InMemory` | `InMemory` または `Table` |
| `TableStorageConnection` | 空 | Table モードの接続文字列（空なら `AzureWebJobsStorage`） |
| `ApiKey` | 空 | `X-Api-Key` に要求する値。空ならローカル開発用に認証を省略 |
| `Gateway__Id` | `gw-pc-01` | `GET /api/doors` が返すゲートウェイ ID |
| `Monitoring__GatewayOfflineThresholdSec` | `300` | 親機のオフライン判定（秒） |
| `Monitoring__DeviceOfflineThresholdSec` | `300` | 子機のオフライン判定（秒） |

### 4.4 Table Storage の構造

| テーブル | PartitionKey | RowKey | 主な列 |
| :--- | :--- | :--- | :--- |
| `Devices` | `door` | デバイスID | `State`, `OpenedAt`, `LastChangedAt`, `LastSeenAt`, `BatteryMv`, `Lqi`, `MetaJson` |
| `Gateways` | `gateway` | ゲートウェイID | `LastSeenAt` |
| `DoorEvents` | デバイスID | 変化時刻を反転した 19 桁の連番 | `State`, `ChangedAt`, `OpenedAt` |

`DoorEvents` は開閉が変化したときだけ 1 行追加します。RowKey の昇順が新しい順になるため、履歴は `limit` 件だけを効率よく取得できます。RowKey は変化時刻から決まるので、再送が重複しても同じ行を上書きします。

`MetaJson` に `floor` / `mapId` / `mapX` / `mapY` / `alertThresholdSec` を JSON で保持します。ファームウェアを変更せずに表示情報を追加できるようにするためです。

## 5. Web フロントエンド

```powershell
cd web
Copy-Item .env.example .env.local
npm run dev
```

| 環境変数 | 既定値 | 内容 |
| :--- | :--- | :--- |
| `VITE_API_BASE_URL` | 空 | Functions のベース URL。空なら同一オリジン |
| `VITE_USE_MOCK` | `false` | `true` でダミーデータ表示 |
| `VITE_POLL_INTERVAL_MS` | `5000` | `GET /api/doors` のポーリング間隔 |

開発サーバーは `/api` を `http://localhost:7071` へ proxy します。Static Web Apps と Functions を別オリジンに置く場合は `VITE_API_BASE_URL` を設定してください（API は `Access-Control-Allow-Origin: *` を返します）。

経過時間はサーバーでは計算しません。`openedAt` をブラウザ側で毎秒差分計算するため、ポーリング間隔に依存しません。

各カードの「履歴」を押すと、直近 20 件の開閉履歴を取得して表示します。履歴の取得は開いたときだけ行い、ポーリングはしません。閉じたイベントには、対応する開放時間（`changedAt - openedAt`）も表示します。

## 6. PC ゲートウェイ

```powershell
dotnet run --project src\DoorMonitor.Gateway
```

- 対応電文: `App_Twelite` の 0x81（23 バイト、LRC8 チェックサム）
- シリアル: 115200 bps / 8-N-1
- リードスイッチ(NO): ドア閉 = DI1 Low、ドア開 = DI1 High
- 状態変化イベントは `DebounceMilliseconds`（既定 500ms）で重複を抑止
- シリアル切断時は `ReconnectDelaySeconds`（既定 5秒）後に再接続
- ゲートウェイ自身のハートビートを `HeartbeatIntervalSeconds`（既定 60秒）ごとに送信

設定ファイル: `%LOCALAPPDATA%\DoorMonitor\gateway.settings.json`

### テレメトリの再送キュー

受信したテレメトリは送信前にディスクへ保存されるため、API が落ちていても失われません。

- 保存先は既定で `%LOCALAPPDATA%\DoorMonitor\queue`（1 件 1 ファイルの JSON、連番）
- 送信は古い順。失敗した電文が残っている間は後続を送らない（`openedAt` の順序を守るため）
- 失敗時は 5 秒から最大 60 秒の指数バックオフで再試行
- 上限は既定 1,000 件。超過時は古いものから破棄し、ログに警告を残す
- ゲートウェイのハートビートは死活情報のため再送しない

| キー | 既定値 | 内容 |
| :--- | :--- | :--- |
| `TelemetryQueueDirectory` | `%LOCALAPPDATA%\DoorMonitor\queue` | 再送キューの保存先 |
| `TelemetryQueueCapacity` | `1000` | キューの上限件数 |

### 単一 exe の作成

```powershell
dotnet publish src\DoorMonitor.Gateway -p:PublishProfile=win-x64
```

出力: `publish\gateway\DoorMonitor.Gateway.exe`（self-contained / single file）。`publish/` は Git 管理対象外です。

## 7. CI（GitHub Actions）

`.github/workflows/ci.yml` が次のジョブを実行します。

| ジョブ | ランナー | 内容 |
| :--- | :--- | :--- |
| `.NET build & test` | `windows-latest` | `dotnet restore` → `dotnet build DoorMonitor.sln` → Core のテスト |
| `Web test & build` | `ubuntu-latest` | `npm ci` → `npm test` → `npm run build` → `npm run lint` |
| `Bicep build` | `ubuntu-latest` | `az bicep build --file infra/main.bicep` |
| `Gateway publish (win-x64)` | `windows-latest` | `main` への push 時のみ単一 exe を publish |

- `pull_request` と `main` への push で実行されます（`main` 以外への push では実行されません）。
- ゲートウェイは WinForms のため、.NET のジョブは Windows ランナー固定です。
- ローカルで同じ確認をする場合は、[2 章](#2-ビルドとテスト)のコマンドを実行してください。

## 8. 既知の制約

- 再送は at-least-once のため、送信直後にゲートウェイが停止すると同じ電文が再度送られることがあります（状態遷移は冪等なので最終状態は変わりません）。
- キューが上限に達すると古い電文から破棄されます。長時間の障害に備える場合は `TelemetryQueueCapacity` を増やしてください。
- キューのファイルを書き込めない場合（ディスク容量不足など）はログにエラーを記録し、その電文は破棄されます。
- 履歴は開閉が変化した受信だけを記録します。初回受信や定期ハートビートは残りません。
- `DoorEvents` テーブルは自動削除されません。件数が問題になった時点で、Timer Trigger などによる古い行の削除を追加してください。
- `InMemory` モードは Functions のプロセス再起動で状態が消えます。履歴も子機ごとに直近 500 件だけ保持します。
- フロアマップ表示、Timer Trigger によるバックグラウンド死活判定は未実装です。
- Azure のリソース定義（`infra/main.bicep`）はありますが、実際の Azure へのデプロイとコード配置はまだ行っていません。
- Linux の Consumption プランは 2028-09-30 に提供終了予定です。長期運用する場合は Flex Consumption への移行を検討してください（技術選定の変更になるため、要件定義 8.1 も合わせて更新が必要です）。
- 実機（TWELITE + MONOSTICK）での通し試験はまだ行っていません。
