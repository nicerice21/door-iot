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
dotnet test tests\DoorMonitor.Gateway.Tests\DoorMonitor.Gateway.Tests.csproj
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
| `src/DoorMonitor.Gateway.Core` | シリアル受信、クラウド送信、再送キュー、設定の読み書き（両ホストで共有） |
| `src/DoorMonitor.Gateway` | Windows 用のタスクトレイ UI（WinForms） |
| `src/DoorMonitor.Gateway.Cli` | Ubuntu 用のヘッドレス実行ファイル（`doormonitor-gateway`） |
| `src/DoorMonitor.Functions` | HTTP API、Table Storage / メモリリポジトリ |
| `web` | 状態カード表示、ポーリング、毎秒の経過時間更新 |
| `infra` | Azure リソース定義（Bicep） |
| `scripts` | Azure へのデプロイスクリプト（`deploy.ps1`）と Linux の systemd ユニット |
| `tests/DoorMonitor.Core.Tests` | Core の単体テスト |
| `tests/DoorMonitor.Gateway.Tests` | 設定・コマンドライン引数の単体テスト |

Azure へのデプロイ手順は [deployment.md](deployment.md) を参照してください。

### データの流れ

```text
TWELITE (App_Twelite 0x81)
  → MONOSTICK → Gateway (パース + デバウンス)
  → 再送キュー（`%LOCALAPPDATA%\DoorMonitor\queue` / Ubuntu は `~/.local/share/DoorMonitor/queue` に永続化）
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
| `GET` | `/api/doors/{deviceId}/battery?hours=168` | なし（公開） | 電池電圧の履歴（古い順、既定 168 時間・最大 720 時間） |
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

電池電圧の履歴は 1 時間に 1 件です。`samples` は古い順で、`summary` に最新・最小・平均を含みます（[requirements.md 5.3](requirements.md#53-電池電圧履歴のレスポンス例-get-apidoorsdeviceidbattery)）。

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
| `Devices` | `door` | デバイスID | `Name`, `State`, `OpenedAt`, `LastChangedAt`, `LastSeenAt`, `BatteryMv`, `Lqi` |
| `Gateways` | `gateway` | ゲートウェイID | `LastSeenAt` |
| `DoorEvents` | デバイスID | 変化時刻を反転した 19 桁の連番 | `State`, `ChangedAt`, `OpenedAt` |
| `BatterySamples` | デバイスID | 時間帯の開始時刻を反転した 19 桁の連番 | `SampledAt`, `ObservedAt`, `BatteryMv`, `Lqi` |

`DoorEvents` は開閉が変化したときだけ 1 行追加します。RowKey の昇順が新しい順になるため、履歴は `limit` 件だけを効率よく取得できます。RowKey は変化時刻から決まるので、再送が重複しても同じ行を上書きします。

`BatterySamples` は `observedAt` を UTC の毎時 0 分へ丸めた `SampledAt` ごとに 1 行です。同じ 1 時間内の受信は同じ行を上書きするため、行数は最大でも 1 時間に 1 件（1 台あたり年間約 8,760 行）になります。`RowKey` の範囲で期間を絞り、昇順に読むと新しい順になるので、取得後に反転して古い順で返します。

`Name` は表示名です。未設定の場合は API が `ドア {logicalId}` を返します。旧バージョンが使用していた `MetaJson` 列は読み書きしません（次に `Devices` の行を更新したとき、Replace により削除されます）。

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

各カードの「電池」を押すと、`GET /api/doors/{deviceId}/battery` から電圧の履歴を取得し、SVG の折れ線と最新・最小・平均を表示します。表示期間は 24時間 / 7日 / 30日から選べます（既定 7日）。グラフ用のライブラリは追加せず、座標計算は `src/lib/battery.ts` の純粋関数にまとめています。

## 6. PC ゲートウェイ

シリアル受信・再送キュー・API 送信・設定の読み書きは `DoorMonitor.Gateway.Core` にあり、Windows のトレイアプリと Ubuntu のヘッドレス版で共通です。違うのはホスト（画面の有無）だけです。

- 対応電文: `App_Twelite` の 0x81（23 バイト、LRC8 チェックサム）
- シリアル: 115200 bps / 8-N-1
- リードスイッチ(NO): ドア閉 = DI1 Low、ドア開 = DI1 High
- 状態変化イベントは `DebounceMilliseconds`（既定 500ms）で重複を抑止
- シリアル切断時は `ReconnectDelaySeconds`（既定 5秒）後に再接続
- ゲートウェイ自身のハートビートを `HeartbeatIntervalSeconds`（既定 60秒）ごとに送信

### Windows（タスクトレイ）

```powershell
dotnet run --project src\DoorMonitor.Gateway
```

起動するとタスクトレイに常駐します。設定はトレイアイコンの「設定...」から変更でき、`%LOCALAPPDATA%\DoorMonitor\gateway.settings.json` に保存されます。

### Ubuntu（ヘッドレス）

```bash
# 単一実行ファイルを作る（Windows からのクロス publish も可能）
dotnet publish src/DoorMonitor.Gateway.Cli -p:PublishProfile=linux-x64 -p:DebugType=None -p:DebugSymbols=false
# → publish/gateway-linux/doormonitor-gateway

# 実行（設定はコマンドライン引数で上書きできる）
./doormonitor-gateway \
  --com-port /dev/ttyUSB0 \
  --api-base-url https://func-doormon-dev-xxxxxxxxxxxxx.azurewebsites.net \
  --api-key "$API_KEY"
```

| オプション | 内容 |
| :--- | :--- |
| `--settings <path>` | 設定ファイル（既定: `~/.local/share/DoorMonitor/gateway.settings.json`） |
| `--com-port <path>` | シリアルポート（既定: `/dev/ttyUSB0`） |
| `--api-base-url <url>` | Azure Functions のベース URL |
| `--api-key <key>` | 書き込み API のキー |
| `--gateway-id <id>` | ゲートウェイ ID |
| `--list-ports` | 利用可能なシリアルポートを表示して終了 |
| `--print-config` | 有効な設定を表示して終了（API キーは伏せ字） |
| `--version` / `-h`, `--help` | バージョン・ヘルプを表示して終了 |

- 設定ファイルがない場合は既定値で作成し、編集を促して終了コード 2 を返します。コマンドラインで指定した値は保存されません。
- ログは標準出力と標準エラーへ出します。systemd で動かすと journal に残ります。
- `Ctrl+C` と `SIGTERM`（`systemctl stop`）で、未送信キューのファイルを閉じてから終了します。
- シリアルデバイスへアクセスするため、実行ユーザーを `dialout` グループへ追加してください（再ログインが必要）。
- `libicu` が入っていない最小構成の Ubuntu でも動くよう、Linux 版は `InvariantGlobalization` でビルドしています。日時は明示的な書式で出力しているため表示は変わりません。

```bash
sudo usermod -aG dialout "$USER"
```

### テレメトリの再送キュー

受信したテレメトリは送信前にディスクへ保存されるため、API が落ちていても失われません。

- 保存先は既定で `%LOCALAPPDATA%\DoorMonitor\queue`（Ubuntu は `~/.local/share/DoorMonitor/queue`）。1 件 1 ファイルの JSON、連番
- 送信は古い順。失敗した電文が残っている間は後続を送らない（`openedAt` の順序を守るため）
- 失敗時は 5 秒から最大 60 秒の指数バックオフで再試行
- 上限は既定 1,000 件。超過時は古いものから破棄し、ログに警告を残す
- ゲートウェイのハートビートは死活情報のため再送しない

| キー | 既定値 | 内容 |
| :--- | :--- | :--- |
| `TelemetryQueueDirectory` | `%LOCALAPPDATA%\DoorMonitor\queue`（Ubuntu は `~/.local/share/DoorMonitor/queue`） | 再送キューの保存先 |
| `TelemetryQueueCapacity` | `1000` | キューの上限件数 |

### 単一実行ファイルの作成

Windows（単一 exe）:

```powershell
dotnet publish src\DoorMonitor.Gateway -p:PublishProfile=win-x64
```

出力: `publish\gateway\DoorMonitor.Gateway.exe`（self-contained / single file）。`publish/` は Git 管理対象外です。

Ubuntu（linux-x64）:

```bash
dotnet publish src/DoorMonitor.Gateway.Cli -p:PublishProfile=linux-x64 -p:DebugType=None -p:DebugSymbols=false
```

出力: `publish/gateway-linux/doormonitor-gateway`（self-contained / single file）。

### GitHub Release の作成

`v1.0.0` のようなタグを push すると、`.github/workflows/release.yml` が次を実行します。

1. Core とゲートウェイのテスト（Release 構成）
2. Windows 用の単一 exe を publish（`windows-latest`）
3. Ubuntu 用の単一実行ファイルを publish（`ubuntu-latest`）
4. exe・`README.txt`・`LICENSE` を ZIP、Linux 版を tar.gz にまとめる
5. それぞれの SHA256 チェックサムを作成
6. GitHub Release に両方とチェックサムを添付

```powershell
git tag v1.0.0
git push origin v1.0.0
```

タグは `vX.Y.Z` 形式を推奨します。形式に合う場合は実行ファイルのバージョンにも反映されます。タグに `-` を含めるとプレリリースとして作成されます。既存の Release に同じタグで再実行した場合は、資産を上書きします。

### Ubuntu での動作確認（2026-11-23）

WSL2 の Ubuntu 26.04（x64）で、実機の代わりに pty をシリアルポートとして使って確認しました。

- `dotnet publish -p:PublishProfile=linux-x64` で単一実行ファイルができること
- pty へ `App_Twelite` の 0x81 電文を流すと、`POST /api/telemetry` に `deviceId` / `state` / `batteryMv` / `lqi` が送られること
- 起動直後に `POST /api/heartbeat` が送られること
- `SIGTERM` で終了コード 0 のまま停止すること

実際の MONOSTICK と `/dev/ttyUSB0` での確認はまだ行っていません。

## 7. CI とリリース（GitHub Actions）

`.github/workflows/ci.yml` が次のジョブを実行します。

| ジョブ | ランナー | 内容 |
| :--- | :--- | :--- |
| `.NET build & test` | `windows-latest` | `dotnet restore` → `dotnet build DoorMonitor.sln` → Core / Gateway のテスト → `scripts/deploy.ps1` の構文チェック |
| `Gateway (linux-x64)` | `ubuntu-latest` | ヘッドレス版のビルドとテスト（Linux で壊れていないことの確認） |
| `Web test & build` | `ubuntu-latest` | `npm ci` → `npm test` → `npm run build` → `npm run lint` |
| `Bicep build` | `ubuntu-latest` | `az bicep build --file infra/main.bicep` |
| `Gateway publish (win-x64)` | `windows-latest` | `main` への push 時のみ単一 exe を publish |

- `pull_request` と `main` への push で実行されます（`main` 以外への push では実行されません）。
- Windows 用ゲートウェイは WinForms のため、.NET のジョブは Windows ランナー固定です。Ubuntu 用は別ジョブで `ubuntu-latest` によりビルドします。
- ローカルで同じ確認をする場合は、[2 章](#2-ビルドとテスト)のコマンドを実行してください。
- タグ `v*` を push すると `release.yml` が実行され、Windows と Ubuntu のゲートウェイを GitHub Release に添付します。`release.yml` はタグ push だけをトリガーにします。

## 8. 既知の制約

- 再送は at-least-once のため、送信直後にゲートウェイが停止すると同じ電文が再度送られることがあります（状態遷移は冪等なので最終状態は変わりません）。
- キューが上限に達すると古い電文から破棄されます。長時間の障害に備える場合は `TelemetryQueueCapacity` を増やしてください。
- キューのファイルを書き込めない場合（ディスク容量不足など）はログにエラーを記録し、その電文は破棄されます。
- 履歴は開閉が変化した受信だけを記録します。初回受信や定期ハートビートは残りません。
- `DoorEvents` テーブルは自動削除されません。件数が問題になった時点で、Timer Trigger などによる古い行の削除を追加してください。
- `InMemory` モードは Functions のプロセス再起動で状態が消えます。履歴も子機ごとに直近 500 件だけ保持します。
- Timer Trigger によるバックグラウンド死活判定は未実装です。複数台運用とフロアマップ表示はプロトタイプの対象外です。
- Azure へのデプロイは 2026-11-23 に `eastasia` で完了しています（リソース作成、Functions / Web のコード配置、実機での通し確認）。
- 学生サブスクリプションには「許可された場所」のポリシーがあり、`location` に許可リージョンを指定する必要があります（[deployment.md](deployment.md) 3.1）。
- Linux の Consumption プランは 2028-09-30 に提供終了予定です。長期運用する場合は Flex Consumption への移行を検討してください（技術選定の変更になるため、要件定義 8.1 も合わせて更新が必要です）。
- オフライン表示（電池抜き・PC停止）の実機確認と、`DoorEvents` の自動削除は未実施です。

## 9. 実機での通し確認（2026-11-23）

TWELITE 1 台（MONOSTICK 経由）で、次の流れを確認しました。

- リードスイッチの開閉検知
- ゲートウェイアプリの受信と送信
- Web 画面での状態表示
- カードの「履歴」からの開閉履歴表示

### 確認中に分かった表示の仕様

- **表示名**: 子機から送られるのはデバイスIDと論理IDだけです。表示名（`Devices` の `Name`）が未設定の場合、API は `ドア {logicalId}` を返します（`DoorDtoFactory` のフォールバック）。プロトタイプでは名前を設定する API/UI を持たないため、通常は `ドア 1` のまま表示されます。これは実機の論理ID 1 に対する仕様どおりの表示です。
- **カードが出るタイミング**: 最初のテレメトリ（定期ハートビートを含む）で `Devices` の行が作られるため、開閉操作の前でも「閉」のカードが表示されます。死活監視のため、この動作を前提にしています。
- **名前を変える場合**: Storage Explorer などで `Devices` テーブルの `Name` を直接編集すると、次のポーリングから反映されます。テレメトリは `Name` を上書きしません。ただし、プロトタイプのサポート範囲外の操作です。
