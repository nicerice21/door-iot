// ドア開閉モニターの Azure リソース定義。
// 学生クレジット内で収まるよう、無料枠のある SKU だけを使う（docs/requirements.md 6 章）。
//
// 作成するリソース:
//   - Storage Account (Standard LRS / Table Storage を Functions と共用)
//   - Functions (Consumption / Linux / .NET 8 Isolated)
//   - Static Web Apps (Free)
//   - Log Analytics + Application Insights (取り込み量に日次上限を設定)

targetScope = 'resourceGroup'

@description('環境名。リソース名の識別子に使う。')
@minLength(1)
@maxLength(8)
param environmentName string = 'dev'

@description('リソースの場所。サブスクリプションのポリシーで許可されたリージョンを指定する。既定はリソースグループの場所。')
param location string = resourceGroup().location

@description('Static Web Apps の場所。Free プランは centralus / eastus2 / eastasia / westeurope / westus2 のみ対応する。')
@allowed([
  'centralus'
  'eastus2'
  'eastasia'
  'westeurope'
  'westus2'
])
param staticWebAppLocation string = 'eastasia'

@description('書き込み API (POST /api/telemetry, /api/heartbeat) を保護する API キー。16 文字以上。')
@secure()
@minLength(16)
param apiKey string

@description('ゲートウェイの死活判定しきい値（秒）。')
@minValue(60)
param gatewayOfflineThresholdSec int = 300

@description('子機の死活判定しきい値（秒）。')
@minValue(60)
param deviceOfflineThresholdSec int = 300

// リソースグループから決まる 13 文字の接尾辞。再デプロイしても名前が変わらない。
var suffix = uniqueString(resourceGroup().id)

var storageAccountName = 'stdoormon${suffix}'
var planName = 'asp-doormon-${environmentName}'
var functionAppName = 'func-doormon-${environmentName}-${suffix}'
var staticWebAppName = 'swa-doormon-${environmentName}-${suffix}'
var logAnalyticsName = 'log-doormon-${environmentName}'
var appInsightsName = 'appi-doormon-${environmentName}'

// コスト管理でリソースをまとめて確認できるようにする。
var tags = {
  project: 'door-iot'
  environment: environmentName
}

var storageConnectionString = 'DefaultEndpointsProtocol=https;AccountName=${storage.name};AccountKey=${storage.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'

// ---------------------------------------------------------------------------
// ストレージ: Table Storage（Devices / Gateways / DoorEvents）と Functions の内部状態を兼用する。
// ---------------------------------------------------------------------------
resource storage 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  tags: tags
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: true
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
  }
}

// ---------------------------------------------------------------------------
// 監視: ログの取り込み量に日次上限を設け、無料枠（月 5GB）を超えないようにする。
// ---------------------------------------------------------------------------
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: logAnalyticsName
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
    workspaceCapping: {
      // 0.1 GB/日 ≒ 3 GB/月。要件 6 章の無料枠（5GB/月）内に収める。
      // Bicep は小数リテラルを持たないため json() で数値にする。
      dailyQuotaGb: json('0.1')
    }
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

// ---------------------------------------------------------------------------
// Functions: Consumption プラン（Y1）。月 100 万実行まで無料。
// ---------------------------------------------------------------------------
resource plan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: planName
  location: location
  tags: tags
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
    size: 'Y1'
    family: 'Y'
    capacity: 0
  }
  properties: {
    // Linux の従量課金プラン（sku.tier=Dynamic で従量課金になる）。
    reserved: true
  }
}

resource functionApp 'Microsoft.Web/sites@2024-04-01' = {
  name: functionAppName
  location: location
  tags: tags
  kind: 'functionapp,linux'
  properties: {
    reserved: true
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNET-ISOLATED|8.0'
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      http20Enabled: true
      // GET は公開 API。書き込みは X-Api-Key で保護する。
      cors: {
        allowedOrigins: [
          '*'
        ]
      }
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: storageConnectionString
        }
        {
          // Linux の従量課金プランは Azure Files をコンテンツ共有に使う。
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: storageConnectionString
        }
        {
          name: 'WEBSITE_CONTENTSHARE'
          value: toLower(functionAppName)
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          // 本番は Table Storage を使う（docs/requirements.md 8.5）。
          name: 'Storage__Mode'
          value: 'Table'
        }
        {
          name: 'TableStorageConnection'
          value: storageConnectionString
        }
        {
          name: 'ApiKey'
          value: apiKey
        }
        {
          name: 'Gateway__Id'
          value: 'gw-pc-01'
        }
        {
          name: 'Monitoring__GatewayOfflineThresholdSec'
          value: string(gatewayOfflineThresholdSec)
        }
        {
          name: 'Monitoring__DeviceOfflineThresholdSec'
          value: string(deviceOfflineThresholdSec)
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
      ]
    }
  }
}

// ---------------------------------------------------------------------------
// Static Web Apps: Free プラン。デプロイは GitHub Actions から行う。
// ---------------------------------------------------------------------------
resource staticWebApp 'Microsoft.Web/staticSites@2024-04-01' = {
  name: staticWebAppName
  location: staticWebAppLocation
  tags: tags
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {
    allowConfigFileUpdates: true
  }
}

// ---------------------------------------------------------------------------
// 出力: デプロイ後のコード配置や Web の接続先設定に使う。
// ---------------------------------------------------------------------------
@description('Functions のアプリ名。func azure functionapp publish に渡す。')
output functionAppName string = functionApp.name

@description('Functions のベース URL。Web の VITE_API_BASE_URL に設定する。')
output functionAppUrl string = 'https://${functionApp.properties.defaultHostName}'

@description('Static Web Apps の名前。デプロイトークンの取得に使う。')
output staticWebAppName string = staticWebApp.name

@description('Static Web Apps の URL。')
output staticWebAppUrl string = 'https://${staticWebApp.properties.defaultHostname}'

@description('ストレージアカウント名。テーブルの中身を確認するときに使う。')
output storageAccountName string = storage.name
