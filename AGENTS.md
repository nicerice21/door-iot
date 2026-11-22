# AGENTS.md

## このリポジトリの現状

- 要件定義に加えて、実装雛形（Core / Gateway / Functions / Web / Tests）と Azure リソース定義（`infra/main.bicep`）が存在する。CI は GitHub Actions（`.github/workflows/ci.yml`）で .NET・Web・Bicep・デプロイスクリプトの構文を検証する。
- 構成: `README.md` / `LICENSE` (Apache-2.0) / `AGENTS.md` / `docs/requirements.md` / `docs/development.md` / `docs/deployment.md` / `infra/` / `scripts/` / `src/` / `tests/` / `web/`
- ビルドやテストのコマンドを推測して書かない。以下は動作確認済み（2026-11-22）。
  - `dotnet build DoorMonitor.sln`
  - `dotnet test tests\DoorMonitor.Core.Tests\DoorMonitor.Core.Tests.csproj`
  - `cd web` → `npm install` → `npm test` → `npm run build` → `npm run lint`
  - `dotnet publish src\DoorMonitor.Gateway -p:PublishProfile=win-x64`
  - `azurite --location .azurite` と `func start --project src\DoorMonitor.Functions`（Table モードで API 通し確認済み）
  - `bicep build infra\main.bicep`（Bicep CLI 0.49.2。CI では `az bicep build`）
- Azure へのデプロイ（`eastasia`）と実機（TWELITE + MONOSTICK）での通し確認は 2026-11-23 に完了し、1台構成のプロトタイプは完成。残りはオフライン表示の実機確認、デプロイの自動化（CD）、Timer Trigger によるクリーンアップとバックグラウンド死活判定（いずれも運用上の改善）。複数台運用とフロアマップ表示はプロトタイプの対象外。
- デプロイは `scripts/deploy.ps1` でまとめて実行できる。CI/CD（GitHub Actions からの自動デプロイ）は未実装。
- ブランチは `main`、リモートは `nicerice21/door-iot`。運用ルールは未定義なので勝手に決めない。

## 設計の正本

`docs/requirements.md` が唯一の設計の正本（学校提出物）。仕様を変える・決める場合はコードより先にこのファイルを更新する。ドキュメントは日本語で書く。

## 実装上の注意

- **コードの品質**: 保守性と可読性を重視する。
- **テスト戦略**: 簡単なテストを書く。
- **ドキュメント**: 実装内容は逐一ドキュメントに反映する。また，README.md にも簡単な説明を追記する。
- **適切な情報の使用**: 一次情報（公式ドキュメント・RFC・仕様書）を適宜参照する。実装状態・ドキュメント・一次情報を比較する．
- **バージョン管理**: Git を使用してバージョン管理を行う。ブランチを適切に切る。コミットメッセージは簡潔に書く。PR を作る場合は、レビュー者が理解できるように説明を追記する。

## 確定済みの技術選定（勝手に変えない）

これらは検討の末に決定済み。代替案を出すなら先にユーザーへ確認する。

| 領域           | 決定                                                           | 却下された案                              |
| :------------- | :------------------------------------------------------------- | :---------------------------------------- |
| PCゲートウェイ | C# / .NET のタスクトレイ常駐アプリ（単一 exe 配布）            | Python/PyInstaller、Go/Rust               |
| クラウド通信   | HTTPS → Azure Functions                                        | IoT Hub / MQTT                            |
| データ保存     | Azure Table Storage                                            | Cosmos DB（クレジット消費リスク）         |
| Web            | Azure Static Web Apps Free + ポーリング（5〜10秒）             | SignalR/Web PubSub によるリアルタイム配信 |
| 子機FW         | まず標準アプリ（`App_Twelite` / `App_IO`）。必要時のみカスタム | —                                         |

## 見落としやすい設計上の制約

- **予算**: Azure for Students（$100/年）内。無料枠で説明できない有料 SKU を提案しない。
- **Web は認証なしの全体公開**。認証・ログイン機能を前提にした設計をしない。
- **経過時間はサーバーで計算しない**。サーバーは `openedAt` (ISO8601 UTC) を返し、ブラウザ側が毎秒差分を描画する。ポーリング間隔に依存させない。
- **プロトタイプの範囲**: 子機1台を対象とする。複数台運用、フロアマップ表示、表示メタデータ（部屋名・地図座標・警告閾値）の管理 API/UI は対象外。
- **死活監視は親機(PC)と子機の2階層**。PC停止と電池切れ/圏外を切り分けて表示する。
- **リードスイッチは NO**（ドア閉＝導通）。ドア閉の長時間導通によるプルアップ待機電流が電池寿命の最大のリスク。省電力の議論をするときは必ずこれを前提にする。
- 子機はプロトタイプ1台。デバイスIDや件数をコードに埋め込まない（実機のIDは受信データから決まる）。
