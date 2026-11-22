# Azure へのデプロイ

`infra/main.bicep` で Azure リソースを作成し、Functions と Web のコードを配置する手順です。設計の正本は [requirements.md](requirements.md)、ローカル開発は [development.md](development.md) を参照してください。

> この手順は Azure サブスクリプションとネットワーク接続が必要なため、CI では実行していません。CI が確認するのは `az bicep build` によるテンプレートの構文だけです。

## 1. 作成されるリソース

| リソース | SKU / プラン | 用途 |
| :--- | :--- | :--- |
| Storage Account | Standard LRS（StorageV2） | `Devices` / `Gateways` / `DoorEvents` テーブルと Functions の内部状態 |
| Functions | Consumption（Y1 / Linux / .NET 8 Isolated） | 4 本の HTTP API |
| Static Web Apps | Free | React の画面 |
| Log Analytics + Application Insights | 従量課金（日次上限 0.1 GB） | Functions のログと失敗の調査 |

- すべて要件定義 6 章の無料枠で説明できる SKU です。
- Application Insights の日次上限は 0.1 GB（約 3 GB/月）に設定し、無料枠の 5 GB/月を超えないようにしています。
- Storage Account は Functions の内部状態とアプリのデータで共用します。データ量が数 MB のため、分けてもコストはほぼ変わりません。
- API キーは Bicep の `@secure()` パラメータで受け取り、リポジトリには保存しません。デプロイ後は Functions のアプリ設定に保存されます。

## 2. 前提

- [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local)
- Node.js 22（Web のビルドと Static Web Apps CLI 用）
- Azure for Students などのサブスクリプション

Bicep は Azure CLI に含まれるものを使います（`az bicep install` で最新化できます）。

## 3. リソースを作成する

```powershell
az login

# リソースグループ（場所は任意。以降のリソースは既定でこの場所に作られる）
az group create --name rg-door-iot --location japaneast

# API キーを生成する（16 文字以上。Bicep 側でも検証している）
$bytes = New-Object byte[] 32
[Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
$apiKey = -join ($bytes | ForEach-Object { $_.ToString('x2') })

az deployment group create `
  --name door-iot `
  --resource-group rg-door-iot `
  --template-file infra/main.bicep `
  --parameters environmentName=dev `
  --parameters apiKey=$apiKey `
  --query properties.outputs -o json
```

出力例:

```json
{
  "functionAppName": { "type": "String", "value": "func-doormon-dev-xxxxxxxxxxxxx" },
  "functionAppUrl": { "type": "String", "value": "https://func-doormon-dev-xxxxxxxxxxxxx.azurewebsites.net" },
  "staticWebAppName": { "type": "String", "value": "swa-doormon-dev-xxxxxxxxxxxxx" },
  "staticWebAppUrl": { "type": "String", "value": "https://happy-tree-0123456.azurestaticapps.net" },
  "storageAccountName": { "type": "String", "value": "stdoormonxxxxxxxxxxxxx" }
}
```

`apiKey` は必須パラメータです。設定値を変えて再デプロイするときも、同じ `apiKey` を毎回渡してください（未指定だと検証で失敗します）。別の値にすると、ゲートウェイ側の設定も更新が必要です。

以降のコマンドで使う値を変数に入れておきます。

```powershell
$out = az deployment group show `
  --resource-group rg-door-iot `
  --name door-iot `
  --query properties.outputs -o json | ConvertFrom-Json

$functionApp = $out.functionAppName.value
$apiBaseUrl = $out.functionAppUrl.value
$staticWebApp = $out.staticWebAppName.value
```

`apiKey` はゲートウェイの設定で使うため、安全な場所に控えてください。紛失した場合は同じ値で再デプロイするか、新しい値を生成して再デプロイします（ゲートウェイ側も更新が必要です）。

## 4. Functions のコードを配置する

`func azure functionapp publish` はビルド済みの成果物を zip で送ります。

```powershell
Push-Location src\DoorMonitor.Functions
func azure functionapp publish $functionApp --dotnet-isolated
Pop-Location
```

デプロイ後に API を確認します。

```powershell
# 公開 API（認証なし）
Invoke-RestMethod "$apiBaseUrl/api/doors"

# 書き込み API（X-Api-Key が必要）
$body = @{
  gatewayId  = "gw-pc-01"
  deviceId   = "0x8201015A"
  logicalId  = 1
  state      = "open"
  batteryMv  = 2950
  lqi        = 135
  isHeartbeat = $false
  observedAt = (Get-Date).ToUniversalTime().ToString("o")
} | ConvertTo-Json

Invoke-RestMethod -Method Post `
  -Uri "$apiBaseUrl/api/telemetry" `
  -Headers @{ "X-Api-Key" = $apiKey } `
  -ContentType "application/json" `
  -Body $body
```

`Storage__Mode=Table` でデプロイしているため、初回の書き込み時にストレージアカウントへテーブルが自動作成されます。

## 5. Web を配置する

Web はビルド時に API の URL を埋め込みます。

```powershell
$env:VITE_API_BASE_URL = $apiBaseUrl
Push-Location web
npm ci
npm run build
Pop-Location
```

Static Web Apps のデプロイトークンを取得して配置します。

```powershell
$token = az staticwebapp secrets list `
  --name $staticWebApp `
  --resource-group rg-door-iot `
  --query properties.apiKey -o tsv

npx @azure/static-web-apps-cli deploy web\dist `
  --deployment-token $token `
  --env production
```

GitHub Actions から配置する場合は、取得したトークンをリポジトリのシークレット（例: `AZURE_STATIC_WEB_APPS_API_TOKEN`）に登録し、`Azure/static-web-apps-deploy@v1` を使うワークフローを追加します。`app_location: web`、`output_location: dist`、ビルド時の環境変数 `VITE_API_BASE_URL` を設定してください。このワークフローはまだリポジトリに含めていません。

## 6. ゲートウェイの接続先を変更する

タスクトレイの「設定...」で次を設定します。

| 項目 | 値 |
| :--- | :--- |
| API ベース URL | `https://func-doormon-dev-xxxxxxxxxxxxx.azurewebsites.net`（末尾スラッシュなし） |
| API キー | デプロイ時に指定した `apiKey` |

設定は `%LOCALAPPDATA%\DoorMonitor\gateway.settings.json` に保存されます。`ApiKey` が空のまま本番環境へ送ると 401 になるため、必ず設定してください。

## 7. コストの確認と削除

- 無料枠の消費状況は Azure Portal の「コスト管理」で確認できます。必要なら予算アラート（例: 月 $5）を設定してください。
- Application Insights は日次上限に達すると取り込みが止まります。調査が必要なときは上限を一時的に上げ、確認後に戻してください。
- 検証が終わったらリソースグループごと削除すると課金が止まります。

```powershell
az group delete --name rg-door-iot --yes --no-wait
```

## 8. 自動化していないこと

現時点では次を手動で行います。必要になった段階で追加します。

- Functions / Static Web Apps のコード配置（CD）
- Static Web Apps のデプロイトークンの GitHub シークレット登録
- カスタムドメイン、Key Vault とマネージド ID による API キー管理
- `DoorEvents` の古い行の削除（Timer Trigger）
- Linux Consumption プランから Flex Consumption への移行（Linux Consumption は 2028-09-30 に提供終了予定）
- 実機（TWELITE + MONOSTICK）での通し試験

## 9. テンプレートの検証

Bicep CLI が入っていれば、デプロイ前に構文を確認できます。

```powershell
bicep build infra/main.bicep --stdout > $null
```

CI でも `az bicep build --file infra/main.bicep --stdout` を実行しています。
