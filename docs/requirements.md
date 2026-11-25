# ドア開閉IoTモニタリングシステム 要件定義書

## 1. プロジェクト概要

### 1.1 背景・目的

学生のIoTプロジェクト（Engineering Design入門）として、建物のドアの開閉状態および開放からの経過時間をリアルタイムに近い形でWebブラウザから確認できるプロトタイプシステムを開発する。
本プロジェクトは子機1台を用いたプロトタイプの完成を目的とし、複数台運用・フロアマップ表示・表示メタデータの管理 API/UI は対象外とする。

### 1.2 システム全体構成

```text
[子機 (1台)]
  TWELITE DIP (MW-R-DP-W) + リードスイッチ (NO: Normally Open) + 電池
   │
   │ 2.4GHz 無線 (IEEE 802.15.4)
   │ ※状態変化時 + 定期ハートビート送信
   ▼
[親機・ゲートウェイ]
  MONOSTICK-R (USB接続) ──> [常駐ソフト (C# / .NET)]
                             Windows: タスクトレイアプリ / Ubuntu: ヘッドレス
                             │
                             │ HTTPS (REST API / JSON)
                             ▼
[クラウド基盤 (Azure - 学生クレジット枠内)]
  ├─ Azure Functions (Consumption Plan)
  │    ├─ HTTP Trigger  : 状態受信API / ゲートウェイ死活受信 / Web向け状態取得API / 開閉履歴API
  │    └─ 死活判定 : GET 時のリクエスト時刻で評価（Timer Trigger は採用しない）
  ├─ Azure Table Storage
  │    ├─ Devices テーブル : 子機の現在状態・最終開時刻・電池電圧
  │    ├─ Gateways テーブル: 親機(PC)の最終通信時刻・ステータス
  │    ├─ DoorEvents テーブル : 開閉イベント履歴
  │    └─ BatterySamples テーブル : 電池電圧の履歴（1時間に1件）
  └─ Azure Static Web Apps (Free Plan)
       └─ 全体公開Webページ (SPA / ポーリング更新)
           │
           │ HTTPS (数秒〜十数秒おきのポーリング)
           ▼
[クライアント]
  PC / スマートフォン Webブラウザ（認証なし・全体公開）
```

---

## 2. 前提条件・制約事項

1. **公開範囲**: Webページは認証なしの全体公開（誰でもURLから閲覧可能）とする。
2. **クラウド予算**: Azure for Students（学生クレジット $100/年）の範囲内で運用可能なサービス・SKU（無料枠中心）を選定する。
3. **子機台数と電源**:
   - プロトタイプ: 1台
   - 電源: 電池駆動（単3電池×2本 または コイン電池を想定）
4. **PCゲートウェイ環境**:
   - OS: Windows 10 / 11、Ubuntu 22.04 以降 (x64)
   - スクリプト実行環境の構築を不要とし、単一バイナリで配布可能な**常駐アプリケーション（C# / .NET 8）**とする。
   - Windows では**タスクトレイ常駐アプリ（WinForms）**、Ubuntu では**ヘッドレスの常駐プロセス（systemd）**として動作する。シリアル受信・再送キュー・API 送信の処理は共通で、ホスト（画面の有無）だけを分ける。
5. **対象外**:
   - 複数台運用、フロアマップ表示、表示メタデータ（部屋名・地図座標・警告閾値）の管理 API/UI は、プロトタイプの対象外とする。

---

## 3. ハードウェア・無線子機要件

### 3.1 構成部品（子機1台あたり）

| 部品         | 型番・仕様                         | 備考                                                  |
| :----------- | :--------------------------------- | :---------------------------------------------------- |
| 無線マイコン | TWELITE DIP (`MW-R-DP-W`)          | RED (高出力タイプ) または BLUE                        |
| センサー     | リードスイッチ (NO: Normally Open) | 磁石接近時(ドア閉)=ON(導通)、離脱時(ドア開)=OFF(開放) |
| 電源         | 電池（単3×2本 等）                 | 2.3V〜3.6V 動作範囲内                                 |
| 親機         | MONOSTICK-R                        | PCのUSBポートに接続                                   |

### 3.2 動作・省電力要件

- **状態検知**:
  - リードスイッチ(NO)をデジタル入力ピン（DI1）およびGND間に接続する。
  - **閉状態（磁石接近）**: DI1 = LOW (論理ON)
  - **開状態（磁石離脱）**: DI1 = HIGH (論理OFF)
- **送信タイミング**:
  1. **状態変化時（イベント送信）**: ドアが開いた瞬間・閉じた瞬間に即時送信する。
  2. **定期送信（ハートビート）**: 開閉がない場合でも、生存確認（電池切れ検知）のため一定間隔（例：数分〜1時間に1回）で現在状態と電源電圧を送信する。
- **ファームウェア段階方針**:
  - **Step 1 (プロトタイプ)**: 標準アプリ（`App_Twelite` または `App_IO`）の間欠動作モード等を利用し、ファームウェア開発なしでエンドツーエンド通信を確立する。
  - **Step 2 (省電力最適化)**: NOスイッチはドア閉鎖中（常時）に導通するため、内蔵プルアップ抵抗（約50kΩ）による待機電流（約60μA）が問題になる場合は、外付け高抵抗プルアップ（1MΩ等）＋スリープ制御に設定変更する。

---

## 4. 機能要件

### 4.1 PCゲートウェイ（常駐ソフト）

- **技術スタック**: C# (.NET 8), `System.IO.Ports.SerialPort`。シリアル受信・再送キュー・API 送信・設定の読み書きは `DoorMonitor.Gateway.Core` に置き、次の 2 つのホストから共有する。
  - Windows: Windows Forms（`NotifyIcon` によるタスクトレイ常駐）
  - Ubuntu: コンソールアプリ（ヘッドレス、systemd で常駐）
- **配布形態**: ランタイム同梱（Self-contained）の単一実行ファイルを ZIP で配布する（Windows は `win-x64`、Ubuntu は `linux-x64`）。
- **リリース配布**: `v*` タグの push で GitHub Actions が両方の実行ファイルをビルドし、GitHub Release に ZIP（実行ファイル + README + LICENSE）と SHA256 チェックサムを添付する。
- **主な機能**:
  1. **常駐と状態表示**:
     - Windows: タスクトレイ常駐。トレイアイコンの色やツールチップで動作状態（正常/シリアル未接続/API通信エラー）を表示する。
     - Ubuntu: バックグラウンドで常駐し、状態の変化と通信ログを標準出力（systemd の journal）へ出力する。
  2. **シリアル通信 (MONOSTICK受信)**:
     - 指定されたシリアルポート（Windows: `COM3` など、Ubuntu: `/dev/ttyUSB0` など。ボーレート 115200bps）を監視し、TWELITEからのASCIIフォーマット電文を受信・パースする。
     - USB抜き差しや一時的な切断時に自動再接続を試みる。
     - Ubuntu では `/dev/ttyUSB0` などのデバイスへアクセスするため、実行ユーザーを `dialout` グループへ追加する。
  3. **クラウド送信 (HTTPS)**:
     - 子機の「開閉状態変化」または「一定時間経過後のハートビート」を検知した際、Azure Functions API へ HTTPS POST で送信する（チャタリング防止のデバウンス処理を含む）。
     - 送信に失敗したテレメトリはローカルへ永続化し、API 復旧後に受信順で再送する（詳細は 8.7）。
     - 子機からの通信がない時間帯でも、PCゲートウェイ自身の生存を知らせるハートビートを定期送信（例：1〜5分おき）する。
  4. **設定画面・ログ確認**:
     - Windows: トレイアイコンの右クリックメニューから、COMポート選択、APIエンドポイントURL、APIキーの設定変更、および直近の通信ログ確認ができる小画面を表示する。
     - Ubuntu: 設定ファイル（`~/.local/share/DoorMonitor/gateway.settings.json`）とコマンドライン引数で設定する。ログは journal に出力する。

### 4.2 クラウドバックエンド（Azure）

- **技術スタック**: Azure Functions (C# / .NET 8 Isolated Worker) + Azure Table Storage
- **主な機能**:
  1. **テレメトリ受信 API (`POST /api/telemetry`)**:
     - PCゲートウェイから送信されたデータ（子機ID、開閉状態、電池電圧、電波強度LQI、ゲートウェイID）を受信する。
     - 前回状態が「閉(`closed`)」で今回「開(`open`)」に変化した場合、`openedAt`（開放開始日時）に現在時刻を記録する。
     - 前回「開(`open`)」で今回「開(`open`)」のまま（定期ハートビート）の場合、`openedAt` は維持し `lastSeenAt`（最終受信時刻）のみ更新する。
     - 前回「開(`open`)」から「閉(`closed`)」に変化した場合、`openedAt` をクリアし、`DoorEvents` テーブルへ開放時刻と閉塞時刻を記録する。
     - 開閉の変化（`closed` ⇔ `open`）は毎回 `DoorEvents` テーブルへ 1 行記録する。定期ハートビートなど状態が変わらない受信は記録しない。
     - ※認証: APIキー（Functions Host Key またはカスタムヘッダ）により第三者からの不正POSTを防止する。
  2. **ゲートウェイ死活受信 API (`POST /api/heartbeat`)**:
     - PCゲートウェイの最終生存時刻 (`gatewayLastSeenAt`) を更新する（テレメトリ受信時に同時更新でも可）。
  3. **状態一覧取得 API (`GET /api/doors`)**:
     - Webフロントエンド向けに、子機の現在状態、開放開始時刻(`openedAt`)、死活ステータスをJSONで返却する。
  4. **開閉履歴取得 API (`GET /api/doors/{deviceId}/history?limit=20`)**:
     - 指定した子機の直近の開閉イベントを新しい順に返却する（既定 20 件、最大 100 件）。
     - 認証なしで公開する（`GET /api/doors` と同じ扱い）。存在しない子機IDには 404 を返す。
  5. **電池電圧履歴取得 API (`GET /api/doors/{deviceId}/battery?hours=168`)**:
     - 指定した子機の電池電圧の履歴を古い順に返却する（既定 168 時間＝7日、最大 720 時間＝30日）。
     - 1 時間に 1 件（同じ 1 時間内の受信は上書き）で保存した値を返す。最新値・最小値・平均値も返し、電池寿命の評価に使う。
     - 認証なしで公開する（`GET /api/doors` と同じ扱い）。存在しない子機IDには 404 を返す。
  6. **死活判定ロジック（GET時評価を採用）**:
     - プロトタイプでは `GET /api/doors` のリクエスト時刻を基準に判定する。常時監視や通知が必要になった段階で Timer Trigger を追加する。
     - **親機（PCゲートウェイ）死活**: 最終通信から閾値（例：5分）以上経過している場合、システム全体を `gateway_offline` と判定する。
     - **子機（TWELITE）死活**: 子機の最終通信から閾値（例：定期送信間隔×2.5倍）以上経過している場合、該当子機を `device_offline`（電池切れ・圏外）と判定する。

### 4.3 公開Webフロントエンド

- **技術スタック**: Azure Static Web Apps (Vite + React + TypeScript)
- **主な機能**:
  1. **ドア状態表示（プロトタイプ基本画面）**:
     - 子機1台の名称、現在の状態（`開` / `閉` / `オフライン`）を視覚的に分かりやすく表示する。
  2. **開放からの経過時間カウント表示**:
     - ドアが「開」状態の場合、サーバーから取得した `openedAt`（開放開始日時）と現在時刻の差分から **「開いてから ○分○秒（または ○時間○分）経過」** を画面上でリアルタイムにカウントアップ表示する。
     - 開いてから5分以上で黄色、30分以上で赤色へ変化させる（閾値はフロントエンドの固定値）。
  3. **死活状態の表示**:
     - 親機（PC）が稼働中か、子機の最終受信がいつかをステータス表示する（電池残量・電波強度の簡易アイコン表示を含む）。
  4. **開閉履歴の表示**:
     - カードの「履歴」を押すと、`GET /api/doors/{deviceId}/history` から直近の開閉履歴を取得し、開閉時刻と開放時間を新しい順に表示する。
  5. **自動更新（ポーリング）**:
     - 数秒〜10秒おきに `GET /api/doors` をポーリングし、リロードなしで状態を最新化する。
  6. **電池電圧の履歴表示**:
     - カードの「電池」を押すと `GET /api/doors/{deviceId}/battery` から履歴を取得し、折れ線グラフ（SVG）と最新・最小・平均を表示する。
     - 表示期間は 24時間 / 7日 / 30日から選べる（既定 7日）。新しいライブラリは追加しない。

---

## 5. データモデル・インターフェース設計

子機から送られるテレメトリを子機IDに紐づけて現在状態として保持する。電池電圧は現在値に加えて、1 時間に 1 件の履歴（`BatterySamples`）として残す。プロトタイプでは、表示名は `Devices` テーブルの `Name` をそのまま返し、未設定の場合は `ドア {logicalId}` とする。

### 5.1 Webフロントエンド向けレスポンス例 (`GET /api/doors`)

```json
{
  "gateway": {
    "id": "gw-pc-01",
    "status": "online",
    "lastSeenAt": "2026-11-21T06:00:00Z"
  },
  "doors": [
    {
      "deviceId": "0x8201015A",
      "logicalId": 1,
      "name": "ドア 1",
      "state": "open",
      "openedAt": "2026-11-21T05:45:10Z",
      "lastChangedAt": "2026-11-21T05:45:10Z",
      "lastSeenAt": "2026-11-21T05:59:50Z",
      "isOnline": true,
      "telemetry": {
        "batteryMv": 2950,
        "lqi": 135
      }
    }
  ]
}
```

- **ポイント**:
  - **経過時間の計算**: サーバー側は `openedAt` (ISO8601 UTCタイムスタンプ) を保持・返却し、ブラウザ側で `現在時刻 - openedAt` を毎秒描画更新することで、APIポーリング間隔（例：5〜10秒）に関わらず秒単位の滑らかな経過時間表示が可能になる。
  - **表示名**: 子機から送られるのはデバイスIDと論理IDだけである。`Name` が空の場合は `ドア {logicalId}` を返す。

### 5.2 開閉履歴のレスポンス例 (`GET /api/doors/{deviceId}/history`)

```json
{
  "deviceId": "0x8201015A",
  "events": [
    {
      "state": "closed",
      "changedAt": "2026-11-21T06:00:00Z",
      "openedAt": "2026-11-21T05:45:10Z"
    },
    {
      "state": "open",
      "changedAt": "2026-11-21T05:45:10Z",
      "openedAt": null
    }
  ]
}
```

- `events` は新しい順。`openedAt` は閉じたイベントのときだけ、対応する開放開始時刻が入る（ブラウザ側で `changedAt - openedAt` を開放時間として表示する）。
- 履歴は「状態が変化した受信」だけを記録し、定期ハートビートは含めない。

### 5.3 電池電圧履歴のレスポンス例 (`GET /api/doors/{deviceId}/battery`)

```json
{
  "deviceId": "0x8201015A",
  "from": "2026-11-18T07:00:00Z",
  "to": "2026-11-25T07:00:00Z",
  "samples": [
    { "sampledAt": "2026-11-18T07:00:00Z", "batteryMv": 3118 },
    { "sampledAt": "2026-11-18T08:00:00Z", "batteryMv": 3112 }
  ],
  "summary": {
    "sampleCount": 168,
    "latestMv": 2950,
    "minMv": 2941,
    "averageMv": 2968
  }
}
```

- `samples` は古い順。`sampledAt` は 1 時間単位に丸めた UTC。
- 同じ 1 時間内に複数の受信があっても 1 行に上書きするため、点は最大でも 1 時間に 1 つになる。
- `summary` は返した `samples` から計算する（`latestMv` は最新のサンプル、`averageMv` は四捨五入した整数 mV）。
- `hours` は 1〜720。範囲外は 400、存在しない子機IDは 404 を返す。

---

## 6. 非機能要件・コスト見積もり（Azure 学生クレジット対応）

| Azure リソース            | 推奨プラン / SKU             | 月額概算コスト      | 備考                                                          |
| :------------------------ | :--------------------------- | :------------------ | :------------------------------------------------------------ |
| **Azure Static Web Apps** | Free プラン                  | **$0**              | SSL証明書無料、グローバル公開、カスタムドメイン可             |
| **Azure Functions**       | Consumption (従量課金)       | **$0**              | 月100万実行まで無料（10秒ポーリングでも月約26万回で無料枠内） |
| **Azure Storage Account** | Standard LRS (Table Storage) | **約 $0.05〜$0.50** | Functions用ストレージと兼用。データ量は数MB以下のためほぼ無料 |
| **Application Insights**  | (任意・1日上限設定)          | **$0**              | 月5GBまで無料                                                 |

- **ポーリング回数と無料枠の試算**:
  - 仮にブラウザ1台が常時 **10秒に1回** `GET /api/doors` を呼び続けた場合：`6回/分 × 60分 × 24時間 × 30日 = 259,200回/月`。
  - Azure Functions の無料枠（月100万回）の約1/4で収まるため、学生クレジット（$100）をほぼ消費せずに1年間運用可能。

---

## 7. 開発ロードマップ（マイルストーン案）

1. **Phase 1: ハードウェア＆シリアル受信確認**
   - TWELITE DIP + リードスイッチ(NO)の配線と、MONOSTICK-Rでのシリアル出力確認。
2. **Phase 2: クラウドAPI＆モックWeb画面構築**
   - Azure Functions + Table Storage の作成と、Static Web Apps での状態・経過時間表示（ダミーデータでの動作確認）。
3. **Phase 3: PCゲートウェイ（C# 常駐アプリ）開発**
   - シリアルデータのパース、チャタリング対策、Azure Functions への HTTPS POST 送信、単一バイナリ（Windows は `.exe`、Ubuntu は実行ファイル）ビルド。
4. **Phase 4: エンドツーエンド結合＆死活監視テスト**
   - 実機1台での開閉テスト、電池抜き／PC停止時のオフライン表示確認。

---

## 8. 実装構成（雛形作成時点の確定事項）

### 8.1 技術スタックの確定

| 領域 | 決定 | 備考 |
| :--- | :--- | :--- |
| クラウドAPI | Azure Functions (C# / .NET 8 Isolated Worker) | ゲートウェイと同じ C# で統一 |
| 共有ロジック | `DoorMonitor.Core` (net8.0) | 状態遷移・TWELITE 電文パースを Gateway / Functions / テストで共有 |
| ゲートウェイ共通 | `DoorMonitor.Gateway.Core` (net8.0) | シリアル受信・再送キュー・API 送信・設定を両ホストで共有 |
| Windows ゲートウェイ | C# / .NET 8 / WinForms タスクトレイ | 単一 exe（self-contained）配布 |
| Ubuntu ゲートウェイ | C# / .NET 8 / コンソール（ヘッドレス） | 単一実行ファイル（linux-x64 / self-contained）配布 |
| Web | Vite + React + TypeScript | Azure Static Web Apps へ `dist/` を配置 |
| テスト | xUnit（Core）/ Vitest（Web） | 状態遷移・パーサ・経過時間表示の純粋関数 |
| ローカル開発 | InMemory リポジトリ（既定） | Azure 未接続で API と Web を結合確認できる |

### 8.2 死活判定の方式

プロトタイプでは `GET /api/doors` のリクエスト時刻を基準に、`lastSeenAt` と閾値の差分で判定する。Timer Trigger は採用しない。理由は次のとおり。

- Web は数秒〜10秒ごとにポーリングするため、取得時評価で表示要件を満たせる。
- Timer と状態更新の競合を避けられ、実装とテストが単純になる。
- Timer の実行回数を削減でき、学生クレジットの範囲に収めやすい。

将来「一定時間開いたままのドアを通知する」などのバックグラウンド処理が必要になった時点で Timer Trigger を追加する。

### 8.3 リポジトリ構成

```text
door-iot/
├─ .github/workflows/ci.yml    # GitHub Actions（ビルド・テスト・Bicep 検証）
├─ docs/
│  ├─ requirements.md          # 本ドキュメント（設計の正本）
│  ├─ development.md           # 開発・実行手順
│  └─ deployment.md            # Azure へのデプロイ手順
├─ infra/
│  └─ main.bicep               # Azure リソース定義（IaC）
├─ src/
│  ├─ DoorMonitor.Core/        # 共有モデル・状態遷移・TWELITE パーサ
│  ├─ DoorMonitor.Gateway.Core/ # ゲートウェイ共通処理（シリアル・再送キュー・API 送信・設定）
│  ├─ DoorMonitor.Gateway/     # Windows 用 WinForms タスクトレイ常駐アプリ
│  ├─ DoorMonitor.Gateway.Cli/ # Ubuntu 用ヘッドレス常駐アプリ
│  └─ DoorMonitor.Functions/   # Azure Functions (C# / .NET 8 Isolated)
├─ tests/
│  ├─ DoorMonitor.Core.Tests/  # xUnit
│  └─ DoorMonitor.Gateway.Tests/ # xUnit（設定・コマンドライン引数）
├─ web/                        # Vite + React + TypeScript
├─ DoorMonitor.sln
└─ Directory.Build.props
```

### 8.4 認証・公開範囲

- `GET /api/doors` は要件どおり認証なしで公開する。
- `POST /api/telemetry` と `POST /api/heartbeat` は `X-Api-Key` ヘッダで保護する。
- `ApiKey` が未設定の場合はローカル開発用として認証を省略する。本番環境では必ず設定する。
- Web 画面から書き込み API を呼ぶ機能は持たせない。

### 8.5 ローカル開発と Azure の切り替え

`Storage__Mode` でリポジトリ実装を切り替える。

- `InMemory`: プロセス内の辞書に保持する。再起動で消えるため、画面確認・API 結合試験用。
- `Table`: Azure Table Storage の `Devices` / `Gateways` テーブルに保持する。

### 8.6 CI

GitHub Actions（`.github/workflows/ci.yml`）で .NET のビルド・テスト、Web のテスト・ビルド・lint、Bicep の構文検証を継続的に確認する。Ubuntu 用ゲートウェイは `ubuntu-latest` でビルドできることも確認する。ゲートウェイの単一実行ファイルの publish は `main` への push 時のみ実行し、PR の待ち時間を抑える。

### 8.7 テレメトリの再送キュー

通信断や Functions の一時的なエラーでテレメトリを失わないため、ゲートウェイは受信したテレメトリをローカルへ永続化してから順に送信する（at-least-once）。

- **保存形式**: `%LOCALAPPDATA%\DoorMonitor\queue\`（Ubuntu では `~/.local/share/DoorMonitor/queue/`）に 1 件 1 ファイルの JSON（連番のファイル名）。一時ファイルへ書いてからリネームし、書きかけのファイルを残さない。
- **順序保証**: 受信順（FIFO）で送信する。`open` → `closed` の順序が入れ替わるとサーバー側の `openedAt` が誤るため、失敗した電文が残っている間は後続を先に送らない。
- **再試行**: 失敗時は指数バックオフ（5 秒 → 最大 60 秒）で再送する。成功したらキューから削除する。
- **再起動**: キューはディスクに残るため、ゲートウェイを再起動しても失われない。起動後は古い電文から順に送信する。
- **重複送信**: 送信直後にゲートウェイが停止した場合、同じ電文が再度送られることがある。開閉状態の遷移は同じ状態の再適用を無視するため、最終状態は変わらない。
- **上限**: 既定 1,000 件。超過時は最も古い電文を破棄し、警告ログを残す（ディスクを無限に使わない）。
- **対象外**: ゲートウェイ自身のハートビートは死活情報のため再送しない。

### 8.8 開閉履歴の記録

- **保存先**: Table Storage の `DoorEvents` テーブル（`Storage__Mode=InMemory` ではメモリ）。
- **キー設計**: `PartitionKey` = 子機ID、`RowKey` = 時刻を反転した連番。RowKey の昇順が新しい順になるため、`limit` 件だけを効率よく取得できる。
- **記録単位**: 開閉が変化した受信のみ。`changedAt` は電文の `observedAt` を使う。
- **再送への耐性**: RowKey は変化時刻から決まるため、再送で同じ電文が届いても同じ行を上書きする（重複しない）。
- **書き込み順**: 履歴を書いてから現在状態を更新する。状態更新後に失敗しても、再送時には状態が変わっていないため履歴が失われない。
- **保持期間**: 自動削除はしない。件数が問題になった時点で、Timer Trigger による古い行の削除を追加する。

### 8.9 電池電圧の履歴

- **保存先**: Table Storage の `BatterySamples` テーブル（`Storage__Mode=InMemory` ではメモリ）。
- **記録単位**: 1 時間に 1 件。`observedAt` を UTC の時間単位へ切り捨てた値を `sampledAt` とし、同じ時間帯の受信は同じ行を上書きする（その時間帯の最後の受信値）。
- **キー設計**: `PartitionKey` = 子機ID、`RowKey` = 時刻を反転した連番。`RowKey` の範囲で期間を絞り、昇順に読むと新しい順になる。
- **保持する値**: `batteryMv` と `lqi`。現在状態（`Devices`）の最新値とは別に、推移を残す。
- **再送への耐性**: 同じ時間帯の再送は同じ行を上書きするため重複しない。順序が前後しても `observedAt` が新しい行を優先する。
- **保持期間**: 自動削除はしない。1 台あたり年間約 8,760 行で、Table Storage の無料枠に収まる。
- **グラフ**: Web は `GET /api/doors/{deviceId}/battery` を使い、24時間 / 7日 / 30日を切り替えて折れ線（SVG）を描画する。

### 8.10 実装状況（2026-11-25 時点）

- 実装済み: `App_Twelite` 0x81 電文パース（LRC8 検証・DI1 の開閉変換）、デバウンス、開閉状態の遷移、API 5 本、Table / InMemory リポジトリ、ポーリング Web 画面、トレイアプリの骨格、Ubuntu 用ヘッドレス版、テレメトリの再送キュー、開閉履歴、電池電圧の履歴とグラフ、Bicep による Azure リソース定義、Azure へのデプロイ、実機（TWELITE + MONOSTICK）での通し確認、GitHub Release 用のリリースワークフロー（2026-11-25）。
- 1台構成のプロトタイプは完成とする（2026-11-23）。残課題はプロトタイプの必須機能ではなく、運用上の改善として扱う。
- プロトタイプの対象外: 複数台運用、フロアマップ表示、表示メタデータ（部屋名・地図座標・警告閾値）の管理 API/UI。
- 残課題: オフライン表示（電池抜き・PC停止）の実機確認、デプロイの自動化（CD）、Timer Trigger による `DoorEvents` の削除とバックグラウンド死活判定。

### 8.11 Azure へのデプロイ（IaC）

リソースの作成は `infra/main.bicep` で行う。手順は `docs/deployment.md` にまとめる。

- **作成するリソース**: Storage Account（Standard LRS。Table Storage と Functions の内部状態を兼用）、Functions（Consumption / Linux / .NET 8 Isolated）、Static Web Apps（Free）、Log Analytics + Application Insights。
- **技術選定の固定**: 8.1 の選定（Functions / Table Storage / Static Web Apps / ポーリング）をそのまま反映する。有料 SKU は使わない。
- **コスト管理**: Application Insights の取り込み量に 0.1 GB/日の上限を設定し、無料枠（月 5 GB）を超えないようにする。
- **リージョン**: サブスクリプションの「許可された場所」ポリシーに従い、許可されたリージョンを `location` で指定する。Static Web Apps は Free 対応リージョン（`centralus` / `eastus2` / `eastasia` / `westeurope` / `westus2`）の中から選ぶ。リソースグループの場所とリソースの場所は一致しなくてよい。
- **シークレット**: API キーは `@secure()` パラメータで受け取り、リポジトリへ保存しない。Bicep 側で 16 文字以上を検証し、空のキーで本番環境が公開されることを防ぐ。
- **責務の分離**: Bicep はリソースの作成だけを担当する。コード配置（`func azure functionapp publish`、Static Web Apps のデプロイトークン）は手動または別ワークフローで行う。
- **検証**: CI で `az bicep build` を実行し、テンプレートの構文を確認する。実際のデプロイは Azure サブスクリプションが必要なため CI では行わない。
- **実績**: 2026-11-23 に `eastasia` でリソースを作成し、Azure へのデプロイと実機での通し確認まで完了した。
