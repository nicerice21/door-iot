# Azure へのデプロイ

`infra/main.bicep` で Azure リソースを作成し、Functions と Web のコードを配置する手順です。設計の正本は [requirements.md](requirements.md)、ローカル開発は [development.md](development.md) を参照してください。

> この手順は Azure サブスクリプションとネットワーク接続が必要なため、CI では実行していません。CI が確認するのは `az bicep build` によるテンプレートの構文だけです。

## 1. 作成されるリソース

| リソース | SKU / プラン | 用途 |
| :--- | :--- | :--- |
| Storage Account | Standard LRS（StorageV2） | `Devices` / `Gateways` / `DoorEvents` テーブルと Functions の内部状態 |
| Functions | Consumption（Y1 / Linux / .NET 8 Isolated） | 6 本の HTTP API |
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

### 2.1 ログインでつまずいたとき

`AADSTS50076`（MFA が必要）で `az login` が失敗する場合は、CLI のトークンキャッシュを消してデバイスコード方式で入り直します。

```powershell
az account clear
Remove-Item -Force "$env:USERPROFILE\.azure\msal_token_cache.*" -ErrorAction SilentlyContinue
az login --use-device-code
```

学生サブスクリプションが見つからない（`No subscriptions found`）ときは、契約したアカウントとテナントを確認します。

```powershell
az account tenant list -o table
az login --tenant "<テナントIDまたはドメイン>" --use-device-code
```

## 3. リソースを作成する

### 3.1 許可されたリージョンを確認する

サブスクリプションに「許可された場所（Allowed locations）」のポリシーがあると、既定のリソースグループの場所（この例では `japaneast`）では `RequestDisallowedByAzure` で失敗します。先に許可されたリージョンを確認してください。

```powershell
$sub = az account show --query id -o tsv

# 「Allowed resource locations」などの割り当てを探す
az policy assignment list --scope "/subscriptions/$sub" `
  --query "[].{name:name, displayName:displayName}" -o table

# 割り当てのパラメータに許可リージョンの配列（listOfAllowedLocations など）が入る
az policy assignment show --name "<割り当て名>" --scope "/subscriptions/$sub" `
  --query parameters -o json
```

- `location` は Functions / Storage / Log Analytics / Application Insights を作る場所です。許可されたリージョンから選びます。
- `staticWebAppLocation` は Static Web Apps の場所です。Free プランは `centralus` / `eastus2` / `eastasia` / `westeurope` / `westus2` のみ対応するため、**許可リージョンと Free 対応リージョンの両方に含まれる場所**を選びます。
- リソースグループの場所とリソースの場所は一致している必要はありません。リソースグループはそのままで、`location` だけ変えて再デプロイできます。
- このリポジトリでは、学生サブスクリプションの許可リージョンだった `eastasia` でリソース作成から実機での通し確認まで完了しています（2026-11-23）。

### 3.2 リソースグループと API キーを用意する

```powershell
az login

# リソースグループの場所はメタデータ。リソースの場所は location で指定する
az group create --name rg-door-iot --location japaneast

# API キーを生成する（16 文字以上。Bicep 側でも検証している）
$bytes = New-Object byte[] 32
[Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
$apiKey = -join ($bytes | ForEach-Object { $_.ToString('x2') })

$location = "eastasia"       # 許可されたリージョンに置き換える
$swaLocation = "eastasia"    # 許可リージョンかつ Static Web Apps Free 対応リージョン
```

### 3.3 デプロイする

```powershell
az deployment group create `
  --name door-iot `
  --resource-group rg-door-iot `
  --template-file infra/main.bicep `
  --parameters environmentName=dev `
  --parameters apiKey=$apiKey `
  --parameters location=$location `
  --parameters staticWebAppLocation=$swaLocation `
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

### 3.4 スクリプトでまとめてデプロイする

`scripts/deploy.ps1` は、リソースグループの確認、Bicep の検証とデプロイ、Web と Functions の配置、API の確認をまとめて実行します。個別の手順を確認したい場合は 3.2〜3.3 と 4 章以降をそのまま使えます。

初回は API キーを指定します。

```powershell
$bytes = New-Object byte[] 32
[Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
$apiKey = -join ($bytes | ForEach-Object { $_.ToString('x2') })

pwsh .\scripts\deploy.ps1 -ApiKey $apiKey
```

2 回目以降は、Functions のアプリ設定から `ApiKey` を読み取って再利用します。

```powershell
pwsh .\scripts\deploy.ps1
```

コードだけを更新する場合:

```powershell
pwsh .\scripts\deploy.ps1 -Target Code
```

| パラメータ | 既定値 | 内容 |
| :--- | :--- | :--- |
| `-ResourceGroup` | `rg-door-iot` | リソースグループ名 |
| `-ResourceGroupLocation` | `japaneast` | リソースグループを作成する場所（既存なら変更されない） |
| `-Target` | `All` | `All` / `Infrastructure` / `Code` / `Web` / `Functions` / `Verify` |
| `-EnvironmentName` | `dev` | リソース名の識別子（8 文字以内） |
| `-Location` | `eastasia` | Functions・Storage などの場所（許可リージョン） |
| `-StaticWebAppLocation` | `eastasia` | Static Web Apps の場所 |
| `-ApiKey` | なし | 初回のみ指定。2 回目以降は既存値を再利用 |
| `-SubscriptionId` | なし | 使用するサブスクリプション。省略時は az の現在値 |
| `-FunctionAppName` / `-StaticWebAppName` | 自動 | 名前を自動で特定できない場合に指定 |

- `All` と `Code` は **Web → Functions** の順で配置します。現在のバージョンは API から `meta` を削除しているため、古い Web が新しい Functions を参照しないようにするためです。
- API キーは画面に表示せず、リポジトリにも保存しません。
- このスクリプトはローカル実行用です。GitHub Actions からの自動デプロイ（CD）はまだ実装していません。
- 実行ポリシーで拒否される場合は `pwsh -ExecutionPolicy Bypass -File .\scripts\deploy.ps1 ...` を使ってください。

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

> このバージョンで `GET /api/doors` のレスポンスから `meta` を削除しました。Web は `meta` を参照しないため、Web を先に（または Functions と同時に）配置してください。古い Web を残したまま Functions だけを更新すると、カードの表示に失敗します。

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

### 5.1 カスタムドメイン（任意）

サブドメインを Cloudflare などの外部 DNS で運用する場合は、CNAME で Static Web Apps を指します。無料の TLS 証明書は Azure が自動で発行します。

1. CNAME のターゲットを確認します。

   ```powershell
   az staticwebapp show -n $staticWebApp -g rg-door-iot --query defaultHostname -o tsv
   ```

   例: `mango-pond-07925d800.8.azurestaticapps.net`（リソースを作り直すと変わります）

2. DNS に CNAME を追加します（Cloudflare の例）。

   | 種類 | 名前 | ターゲット | プロキシ |
   | :--- | :--- | :--- | :--- |
   | CNAME | `door` | 上で確認したホスト名 | **DNS only（灰色）** |

   Cloudflare のプロキシ（オレンジ）を有効にすると、Azure のドメイン検証と証明書発行が失敗します。検証中は必ず DNS only にしてください。

3. DNS が反映されたら、Azure にドメインを登録します。

   ```powershell
   az staticwebapp hostname set `
     --name $staticWebApp `
     --resource-group rg-door-iot `
     --hostname door.example.com
   ```

4. 反映を確認します。

   ```powershell
   az staticwebapp hostname list -n $staticWebApp -g rg-door-iot
   curl.exe -I https://door.example.com
   ```

- CNAME 検証に失敗する場合は `--validation-method dns-txt-token` を使い、`_dnsauth.door` の TXT レコードに次の値を追加します。

  ```powershell
  az staticwebapp hostname show -n $staticWebApp -g rg-door-iot `
    --hostname door.example.com --query validationToken -o tsv
  ```

- 証明書の発行には数分〜数時間かかることがあります。発行後も Cloudflare のプロキシは有効にしないでください（Azure の管理証明書と競合します）。

## 6. ゲートウェイを配置する

### 6.1 Windows（タスクトレイ）

タスクトレイの「設定...」で次を設定します。

| 項目 | 値 |
| :--- | :--- |
| API ベース URL | `https://func-doormon-dev-xxxxxxxxxxxxx.azurewebsites.net`（末尾スラッシュなし） |
| API キー | デプロイ時に指定した `apiKey` |

設定は `%LOCALAPPDATA%\DoorMonitor\gateway.settings.json` に保存されます。`ApiKey` が空のまま本番環境へ送ると 401 になるため、必ず設定してください。

### 6.2 Ubuntu（ヘッドレス / systemd）

画面のない Ubuntu 22.04 以降 (x64) では、GitHub Release の `linux-x64` 版を使います。

1. 実行ファイルを配置します。

   ```bash
   sudo install -d -m 755 /opt/doormonitor
   sudo install -m 755 doormonitor-gateway /opt/doormonitor/
   ```

2. シリアルデバイスへアクセスできるようにします（再ログインが必要です）。

   ```bash
   sudo usermod -aG dialout "$USER"
   ```

3. MONOSTICK のポートを確認します。

   ```bash
   /opt/doormonitor/doormonitor-gateway --list-ports
   ```

4. 初回は設定ファイルを作成するため、終了コード 2 で止まります。表示されたパスの値を編集します。

   ```bash
   /opt/doormonitor/doormonitor-gateway
   # → ~/.local/share/DoorMonitor/gateway.settings.json を作成して終了

   nano ~/.local/share/DoorMonitor/gateway.settings.json
   # comPort: /dev/ttyUSB0
   # apiBaseUrl: https://func-doormon-dev-xxxxxxxxxxxxx.azurewebsites.net
   # apiKey: デプロイ時に指定した値
   ```

   ```bash
   /opt/doormonitor/doormonitor-gateway --print-config   # 内容を確認（API キーは伏せ字）
   ```

5. systemd に登録します。`scripts/linux/doormonitor-gateway@.service` はユーザー名を引数に取るテンプレートユニットです。

   ```bash
   sudo cp scripts/linux/doormonitor-gateway@.service /etc/systemd/system/
   sudo systemctl daemon-reload
   sudo systemctl enable --now "doormonitor-gateway@$USER"
   ```

6. 状態とログを確認します。

   ```bash
   systemctl status "doormonitor-gateway@$USER"
   journalctl -u "doormonitor-gateway@$USER" -f
   ```

   ```text
   ... [/dev/ttyUSB0 接続中（SerialConnected）]
   ... [0x8201015A open を送信しました]
   ```

設定を変更したら `sudo systemctl restart "doormonitor-gateway@$USER"` で反映します。

- 設定を `/etc` など別の場所に置く場合は、ユニットの `ExecStart` に `--settings /etc/doormonitor/gateway.settings.json` を追加してください。
- シリアルポートが `/dev/ttyUSB0` 以外になる場合は `comPort` を変更します（`/dev/ttyACM0` など）。
- ログは journal に残ります。`journalctl` の保存期間は `/etc/systemd/journald.conf` で調整できます。

## 7. コストの確認と削除

- 無料枠の消費状況は Azure Portal の「コスト管理」で確認できます。必要なら予算アラート（例: 月 $5）を設定してください。
- Application Insights は日次上限に達すると取り込みが止まります。調査が必要なときは上限を一時的に上げ、確認後に戻してください。
- 検証が終わったらリソースグループごと削除すると課金が止まります。

```powershell
az group delete --name rg-door-iot --yes --no-wait
```

## 8. 自動化していないこと

現時点では次を手動で行います。必要になった段階で追加します。

- コード配置の自動化（CD）。ローカルでは `scripts/deploy.ps1` で実行できるが、GitHub Actions からの自動デプロイは未実装
- Static Web Apps のデプロイトークンの GitHub シークレット登録
- カスタムドメイン、Key Vault とマネージド ID による API キー管理
- `DoorEvents` の古い行の削除（Timer Trigger）
- Linux Consumption プランから Flex Consumption への移行（Linux Consumption は 2028-09-30 に提供終了予定）

## 9. テンプレートの検証

Bicep CLI が入っていれば、デプロイ前に構文を確認できます。

```powershell
bicep build infra/main.bicep --stdout > $null
```

CI でも `az bicep build --file infra/main.bicep --stdout` を実行しています。
