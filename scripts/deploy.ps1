<#
.SYNOPSIS
    Azure リソース、Functions、Web をまとめてデプロイする。

.DESCRIPTION
    infra/main.bicep によるリソースの作成・更新、Functions のコード配置、
    Web のビルドと Static Web Apps への配置、GET /api/doors の確認を行う。

    初回は -ApiKey を指定する。2 回目以降は、Functions のアプリ設定に保存された
    ApiKey を自動で再利用する（-ApiKey で変更もできる）。

.PARAMETER Target
    All: リソース、Web、Functions、API 確認（既定）
    Infrastructure: Bicep のデプロイだけ
    Code: Web、Functions、API 確認
    Web: Web のビルドと配置だけ
    Functions: Functions の配置だけ
    Verify: API の確認だけ

.PARAMETER ApiKey
    書き込み API を保護するキー（16 文字以上）。
    初回デプロイでは必須。既存の Function App がある場合は省略できる。

.PARAMETER SubscriptionId
    使用するサブスクリプション。省略時は az の現在のサブスクリプション。

.PARAMETER FunctionAppName
    Functions のアプリ名。省略時はデプロイ出力またはリソースグループから特定する。

.PARAMETER StaticWebAppName
    Static Web Apps の名前。省略時はデプロイ出力またはリソースグループから特定する。

.EXAMPLE
    # 初回
    .\scripts\deploy.ps1 -ApiKey $apiKey

.EXAMPLE
    # コードだけ更新（API キーは既存の設定を再利用）
    .\scripts\deploy.ps1 -Target Code

.EXAMPLE
    # Web だけ配置
    .\scripts\deploy.ps1 -Target Web

.NOTES
    現在のバージョンは API レスポンスから meta を削除しているため、
    古い Web が新しい Functions を参照しないよう Web を先に配置する。
#>
[CmdletBinding()]
param(
    [string]$ResourceGroup = 'rg-door-iot',

    [string]$ResourceGroupLocation = 'japaneast',

    [ValidateSet('All', 'Infrastructure', 'Code', 'Web', 'Functions', 'Verify')]
    [string]$Target = 'All',

    [string]$EnvironmentName = 'dev',

    [string]$Location = 'eastasia',

    [string]$StaticWebAppLocation = 'eastasia',

    [string]$DeploymentName = 'door-iot',

    [string]$ApiKey,

    [string]$SubscriptionId,

    [string]$FunctionAppName,

    [string]$StaticWebAppName
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($PSVersionTable.PSVersion -lt [Version]'5.1') {
    throw 'PowerShell 5.1 以上が必要です。'
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$bicepFile = Join-Path $repoRoot 'infra\main.bicep'
$functionsDir = Join-Path $repoRoot 'src\DoorMonitor.Functions'
$webDir = Join-Path $repoRoot 'web'

function Write-Step {
    param([string]$Message)
    Write-Host "`n==> $Message" -ForegroundColor Cyan
}

function Write-Info {
    param([string]$Message)
    Write-Host "    $Message"
}

function Write-Note {
    param([string]$Message)
    Write-Host "    $Message" -ForegroundColor Yellow
}

function Add-AzureCliToPath {
    if (Get-Command az -ErrorAction SilentlyContinue) {
        return
    }

    $programFiles = [Environment]::GetFolderPath('ProgramFiles')
    if ([string]::IsNullOrWhiteSpace($programFiles)) {
        return
    }

    $candidate = Join-Path $programFiles 'Microsoft SDKs\Azure\CLI2\wbin'
    if (Test-Path (Join-Path $candidate 'az.cmd')) {
        $env:PATH = "$candidate;$env:PATH"
    }
}

function Assert-Command {
    param(
        [string]$Name,
        [string]$Hint
    )

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "$Name が見つかりません。$Hint"
    }
}

function Invoke-Checked {
    param(
        [string]$Description,
        [scriptblock]$Command
    )

    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$Description に失敗しました（終了コード $LASTEXITCODE）。"
    }
}

function Get-DeploymentOutputs {
    $json = az deployment group show `
        --resource-group $ResourceGroup `
        --name $DeploymentName `
        --query properties.outputs `
        --only-show-errors `
        -o json 2>$null

    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($json)) {
        return $null
    }

    try {
        return $json | ConvertFrom-Json
    }
    catch {
        return $null
    }
}

function Find-FunctionAppName {
    $name = az functionapp list `
        --resource-group $ResourceGroup `
        --query "[?starts_with(name, 'func-doormon-')].name | [0]" `
        --only-show-errors `
        -o tsv 2>$null

    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($name)) {
        return $null
    }

    return $name.Trim()
}

function Find-StaticWebAppName {
    $name = az staticwebapp list `
        --resource-group $ResourceGroup `
        --query "[?starts_with(name, 'swa-doormon-')].name | [0]" `
        --only-show-errors `
        -o tsv 2>$null

    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($name)) {
        return $null
    }

    return $name.Trim()
}

function Get-FunctionApiKey {
    param([string]$Name)

    if ([string]::IsNullOrWhiteSpace($Name)) {
        return $null
    }

    $value = az functionapp config appsettings list `
        --resource-group $ResourceGroup `
        --name $Name `
        --query "[?name=='ApiKey'].value | [0]" `
        --only-show-errors `
        -o tsv 2>$null

    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($value)) {
        return $null
    }

    return $value.Trim()
}

function Get-FunctionHostName {
    param([string]$Name)

    $hostName = az functionapp show `
        --resource-group $ResourceGroup `
        --name $Name `
        --query defaultHostName `
        --only-show-errors `
        -o tsv 2>$null

    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($hostName)) {
        return $null
    }

    return $hostName.Trim()
}

function Get-StaticWebAppUrl {
    param([string]$Name)

    $hostName = az staticwebapp show `
        --resource-group $ResourceGroup `
        --name $Name `
        --query defaultHostname `
        --only-show-errors `
        -o tsv 2>$null

    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($hostName)) {
        return $null
    }

    return "https://$($hostName.Trim())"
}

try {
    $doInfrastructure = $Target -in @('All', 'Infrastructure')
    $doWeb = $Target -in @('All', 'Code', 'Web')
    $doFunctions = $Target -in @('All', 'Code', 'Functions')
    $doVerify = $Target -in @('All', 'Code', 'Verify')

    if (-not (Test-Path $bicepFile)) {
        throw "Bicep ファイルが見つかりません: $bicepFile"
    }

    if ([string]::IsNullOrWhiteSpace($EnvironmentName) -or $EnvironmentName.Length -gt 8) {
        throw 'EnvironmentName は 1〜8 文字にしてください。'
    }

    Add-AzureCliToPath
    Assert-Command az 'Azure CLI をインストールし、PATH に追加してください。'

    Write-Step 'Azure のログイン状態を確認'
    az account show --only-show-errors -o none
    if ($LASTEXITCODE -ne 0) {
        throw "'az login' で Azure にサインインしてください。"
    }

    if (-not [string]::IsNullOrWhiteSpace($SubscriptionId)) {
        az account set --subscription $SubscriptionId --only-show-errors
        if ($LASTEXITCODE -ne 0) {
            throw "サブスクリプション '$SubscriptionId' を選択できませんでした。"
        }
    }

    $subscriptionName = az account show --query name --only-show-errors -o tsv
    if (-not [string]::IsNullOrWhiteSpace($subscriptionName)) {
        Write-Info "サブスクリプション: $($subscriptionName.Trim())"
    }

    if (-not $doInfrastructure) {
        $groupExists = az group exists --name $ResourceGroup --only-show-errors -o tsv
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($groupExists) -or $groupExists.Trim() -ne 'true') {
            throw "リソースグループ '$ResourceGroup' がありません。初回は -Target All でデプロイしてください。"
        }
    }

    $outputs = Get-DeploymentOutputs
    if ($null -ne $outputs) {
        if ([string]::IsNullOrWhiteSpace($FunctionAppName)) {
            $FunctionAppName = $outputs.functionAppName.value
        }

        if ([string]::IsNullOrWhiteSpace($StaticWebAppName)) {
            $StaticWebAppName = $outputs.staticWebAppName.value
        }
    }

    if ([string]::IsNullOrWhiteSpace($FunctionAppName)) {
        $FunctionAppName = Find-FunctionAppName
    }

    if ([string]::IsNullOrWhiteSpace($StaticWebAppName)) {
        $StaticWebAppName = Find-StaticWebAppName
    }

    $apiBaseUrl = $null
    if ($null -ne $outputs) {
        $apiBaseUrl = $outputs.functionAppUrl.value
    }

    if ([string]::IsNullOrWhiteSpace($apiBaseUrl) -and -not [string]::IsNullOrWhiteSpace($FunctionAppName)) {
        $hostName = Get-FunctionHostName -Name $FunctionAppName
        if (-not [string]::IsNullOrWhiteSpace($hostName)) {
            $apiBaseUrl = "https://$hostName"
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($apiBaseUrl)) {
        $apiBaseUrl = $apiBaseUrl.TrimEnd('/')
    }

    $staticWebAppUrl = $null
    if ($null -ne $outputs) {
        $staticWebAppUrl = $outputs.staticWebAppUrl.value
    }

    if ($doInfrastructure) {
        $existingApiKey = Get-FunctionApiKey -Name $FunctionAppName

        if ([string]::IsNullOrWhiteSpace($ApiKey)) {
            if ([string]::IsNullOrWhiteSpace($existingApiKey)) {
                throw 'API キーが指定されていません。初回は -ApiKey を指定してください（16 文字以上）。生成例は docs/deployment.md 3.2 を参照してください。'
            }

            $ApiKey = $existingApiKey
            Write-Info '既存の Function App の ApiKey を再利用します。'
        }
        elseif (-not [string]::IsNullOrWhiteSpace($existingApiKey) -and $ApiKey -ne $existingApiKey) {
            Write-Note 'API キーを変更します。ゲートウェイ側の設定も更新してください。'
        }

        if ($ApiKey.Length -lt 16) {
            throw 'API キーは 16 文字以上にしてください。'
        }

        if ($ApiKey -match '\s') {
            throw 'API キーに空白は使えません。'
        }

        Write-Step 'リソースグループを確認'
        Invoke-Checked 'リソースグループの作成/確認' {
            az group create `
                --name $ResourceGroup `
                --location $ResourceGroupLocation `
                --only-show-errors `
                -o none
        }

        Write-Step 'Bicep を検証'
        Invoke-Checked 'Bicep の検証' {
            az bicep build --file $bicepFile --stdout | Out-Null
        }

        Write-Step 'Azure リソースをデプロイ'
        $deploymentJson = az deployment group create `
            --name $DeploymentName `
            --resource-group $ResourceGroup `
            --template-file $bicepFile `
            --parameters environmentName=$EnvironmentName `
            --parameters apiKey=$ApiKey `
            --parameters location=$Location `
            --parameters staticWebAppLocation=$StaticWebAppLocation `
            --query properties.outputs `
            --only-show-errors `
            -o json

        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($deploymentJson)) {
            throw 'Azure リソースのデプロイに失敗しました。'
        }

        $outputs = $deploymentJson | ConvertFrom-Json
        $FunctionAppName = $outputs.functionAppName.value
        $StaticWebAppName = $outputs.staticWebAppName.value
        $apiBaseUrl = $outputs.functionAppUrl.value.TrimEnd('/')
        $staticWebAppUrl = $outputs.staticWebAppUrl.value

        Write-Info "Functions: $FunctionAppName"
        Write-Info "Static Web Apps: $StaticWebAppName"
    }

    if ($doWeb) {
        Write-Step 'Web をビルドして配置'

        Assert-Command npm 'Node.js をインストールしてください。'
        Assert-Command npx 'Node.js をインストールしてください。'

        if ([string]::IsNullOrWhiteSpace($apiBaseUrl)) {
            throw 'Functions の URL を特定できませんでした。-FunctionAppName を指定するか、先に -Target Infrastructure を実行してください。'
        }

        if ([string]::IsNullOrWhiteSpace($StaticWebAppName)) {
            throw 'Static Web Apps の名前を特定できませんでした。-StaticWebAppName を指定するか、先に -Target Infrastructure を実行してください。'
        }

        $previousApiBaseUrl = $env:VITE_API_BASE_URL
        $previousUseMock = $env:VITE_USE_MOCK

        try {
            $env:VITE_API_BASE_URL = $apiBaseUrl
            $env:VITE_USE_MOCK = 'false'

            Push-Location $webDir
            try {
                Invoke-Checked 'npm ci' {
                    npm ci --no-audit --no-fund
                }

                Invoke-Checked 'npm run build' {
                    npm run build
                }
            }
            finally {
                Pop-Location
            }
        }
        finally {
            if ($null -eq $previousApiBaseUrl) {
                Remove-Item Env:\VITE_API_BASE_URL -ErrorAction SilentlyContinue
            }
            else {
                $env:VITE_API_BASE_URL = $previousApiBaseUrl
            }

            if ($null -eq $previousUseMock) {
                Remove-Item Env:\VITE_USE_MOCK -ErrorAction SilentlyContinue
            }
            else {
                $env:VITE_USE_MOCK = $previousUseMock
            }
        }

        $token = az staticwebapp secrets list `
            --name $StaticWebAppName `
            --resource-group $ResourceGroup `
            --query properties.apiKey `
            --only-show-errors `
            -o tsv

        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($token)) {
            throw 'Static Web Apps のデプロイトークンを取得できませんでした。'
        }

        Invoke-Checked 'Static Web Apps への配置' {
            npx --yes @azure/static-web-apps-cli deploy (Join-Path $webDir 'dist') `
                --deployment-token $token `
                --env production
        }
    }

    if ($doFunctions) {
        Write-Step 'Functions を配置'

        Assert-Command func 'Azure Functions Core Tools v4 をインストールしてください。'

        if ([string]::IsNullOrWhiteSpace($FunctionAppName)) {
            throw 'Functions のアプリ名を特定できませんでした。-FunctionAppName を指定してください。'
        }

        Push-Location $functionsDir
        try {
            Invoke-Checked 'Functions の配置' {
                func azure functionapp publish $FunctionAppName --dotnet-isolated
            }
        }
        finally {
            Pop-Location
        }
    }

    if ($doVerify) {
        Write-Step 'API を確認'

        if ([string]::IsNullOrWhiteSpace($apiBaseUrl)) {
            throw 'Functions の URL を特定できませんでした。-FunctionAppName を指定してください。'
        }

        $doorsUrl = "$apiBaseUrl/api/doors"
        $response = $null
        $lastError = $null

        for ($attempt = 1; $attempt -le 6; $attempt++) {
            try {
                $response = Invoke-RestMethod -Uri $doorsUrl -TimeoutSec 30
                $lastError = $null
                break
            }
            catch {
                $lastError = $_
                if ($attempt -lt 6) {
                    Write-Info "応答待ち ($attempt/6)。5 秒後に再試行します。"
                    Start-Sleep -Seconds 5
                }
            }
        }

        if ($null -ne $lastError) {
            throw "API の確認に失敗しました: $($lastError.Exception.Message)"
        }

        Write-Info "GET $doorsUrl"
        Write-Info "ドア数: $(@($response.doors).Count)"
        if ($null -ne $response.gateway) {
            Write-Info "ゲートウェイ: $($response.gateway.status)"
        }
    }

    if ([string]::IsNullOrWhiteSpace($staticWebAppUrl) -and -not [string]::IsNullOrWhiteSpace($StaticWebAppName)) {
        $staticWebAppUrl = Get-StaticWebAppUrl -Name $StaticWebAppName
    }

    Write-Step '完了'
    if (-not [string]::IsNullOrWhiteSpace($apiBaseUrl)) {
        Write-Info "API: $apiBaseUrl"
    }

    if (-not [string]::IsNullOrWhiteSpace($staticWebAppUrl)) {
        Write-Info "Web: $staticWebAppUrl"
    }

    Write-Info 'API キーは表示していません。ゲートウェイの設定を確認してください。'
}
catch {
    Write-Host ''
    Write-Host "デプロイに失敗しました: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
