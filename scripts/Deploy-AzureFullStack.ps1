# ===============================================================================
# Deploy-AzureFullStack.ps1
#
# Deploys complete SecureBootDashboard infrastructure to Azure (Full Cloud Architecture)
# - Azure SQL Database
# - Azure Storage Account (Queue)
# - Azure Key Vault
# - App Service Plan
# - App Service (API)
# - App Service (Web)
# - Application Insights
# - Front Door (optional)
#
# Requirements:
# - Azure CLI installed
# - Azure subscription with appropriate permissions
# - Logged in to Azure (az login)
#
# Usage:
#   .\Deploy-AzureFullStack.ps1 -SubscriptionId "xxx" -ResourceGroupName "rg-secureboot-prod"
#
# ===============================================================================

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SubscriptionId,
    
    [Parameter(Mandatory = $false)]
    [string]$ResourceGroupName = "rg-secureboot-prod",
    
    [Parameter(Mandatory = $false)]
    [string]$Location = "westeurope",
    
    [Parameter(Mandatory = $false)]
    [string]$EnvironmentName = "prod",
    
    [Parameter(Mandatory = $false)]
    [string]$SqlAdminUser = "sqladmin",
    
    [Parameter(Mandatory = $false)]
    [SecureString]$SqlAdminPassword,
    
    [Parameter(Mandatory = $false)]
    [ValidateSet("B1", "B2", "B3", "S1", "S2", "S3", "P1v2", "P2v2", "P3v2", "P1v3", "P2v3", "P3v3")]
    [string]$AppServicePlanSku = "P1v3",
    
    [Parameter(Mandatory = $false)]
    [string[]]$AllowedIpAddresses = @(),
    
    [Parameter(Mandatory = $false)]
    [switch]$EnableFrontDoor,
    
    [Parameter(Mandatory = $false)]
    [switch]$EnableAutoScale,
    
    [Parameter(Mandatory = $false)]
    [switch]$DeployBinaries,
    
    [Parameter(Mandatory = $false)]
    [string]$BinariesPath,
    
    [Parameter(Mandatory = $false)]
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

# ===============================================================================
# Functions
# ===============================================================================

function Write-Step {
    param([string]$Message)
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host $Message -ForegroundColor Cyan
    Write-Host "========================================`n" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "? $Message" -ForegroundColor Green
}

function Write-Info {
    param([string]$Message)
    Write-Host "  $Message" -ForegroundColor Gray
}

function Test-AzureCli {
    Write-Step "Checking Azure CLI installation"
    
    try {
        $azVersion = az version --output json | ConvertFrom-Json
        Write-Success "Azure CLI version: $($azVersion.'azure-cli')"
        return $true
    } catch {
        Write-Host "? Azure CLI not found. Install from: https://aka.ms/InstallAzureCLIDirect" -ForegroundColor Red
        return $false
    }
}

function Test-AzureLogin {
    Write-Step "Checking Azure login status"
    
    try {
        $account = az account show --output json | ConvertFrom-Json
        Write-Success "Logged in as: $($account.user.name)"
        Write-Info "Subscription: $($account.name) ($($account.id))"
        return $true
    } catch {
        Write-Host "? Not logged in to Azure. Run: az login" -ForegroundColor Red
        return $false
    }
}

function Set-AzureSubscription {
    param([string]$SubId)
    
    Write-Step "Setting Azure subscription"
    
    az account set --subscription $SubId
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to set subscription"
    }
    
    $current = az account show --output json | ConvertFrom-Json
    Write-Success "Active subscription: $($current.name)"
}

function New-AzureResourceGroup {
    param(
        [string]$Name,
        [string]$Location
    )
    
    Write-Step "Creating resource group"
    
    if ($WhatIf) {
        Write-Info "Would create resource group: $Name in $Location"
        return
    }
    
    $existing = az group show --name $Name --output json 2>$null | ConvertFrom-Json
    if ($existing) {
        Write-Success "Resource group already exists: $Name"
        return $existing
    }
    
    az group create --name $Name --location $Location --output json | ConvertFrom-Json
    Write-Success "Resource group created: $Name"
}

function New-SqlServerAndDatabase {
    param(
        [string]$ResourceGroup,
        [string]$Location,
        [string]$ServerName,
        [string]$DatabaseName,
        [string]$AdminUser,
        [SecureString]$AdminPassword,
        [string[]]$AllowedIPs
    )
    
    Write-Step "Creating SQL Server and Database"
    
    if ($WhatIf) {
        Write-Info "Would create SQL Server: $ServerName"
        Write-Info "Would create Database: $DatabaseName"
        return
    }
    
    # Convert SecureString to plain text
    $BSTR = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($AdminPassword)
    $plainPassword = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($BSTR)
    
    try {
        # Create SQL Server
        $server = az sql server show --name $ServerName --resource-group $ResourceGroup --output json 2>$null | ConvertFrom-Json
        if (-not $server) {
            $server = az sql server create `
                --name $ServerName `
                --resource-group $ResourceGroup `
                --location $Location `
                --admin-user $AdminUser `
                --admin-password $plainPassword `
                --minimal-tls-version "1.2" `
                --output json | ConvertFrom-Json
            
            Write-Success "SQL Server created: $($server.fullyQualifiedDomainName)"
        } else {
            Write-Success "SQL Server already exists: $($server.fullyQualifiedDomainName)"
        }
        
        # Create Database
        $database = az sql db show --name $DatabaseName --server $ServerName --resource-group $ResourceGroup --output json 2>$null | ConvertFrom-Json
        if (-not $database) {
            $database = az sql db create `
                --resource-group $ResourceGroup `
                --server $ServerName `
                --name $DatabaseName `
                --service-objective "S2" `
                --backup-storage-redundancy "Geo" `
                --zone-redundant $false `
                --output json | ConvertFrom-Json
            
            Write-Success "Database created: $DatabaseName"
        } else {
            Write-Success "Database already exists: $DatabaseName"
        }
        
        # Configure firewall
        az sql server firewall-rule create `
            --resource-group $ResourceGroup `
            --server $ServerName `
            --name "AllowAzureServices" `
            --start-ip-address "0.0.0.0" `
            --end-ip-address "0.0.0.0" | Out-Null
        
        Write-Success "Firewall rule created: AllowAzureServices"
        
        # Allow specific IPs
        if ($AllowedIPs.Count -eq 0) {
            try {
                $myIp = (Invoke-WebRequest -Uri "https://ifconfig.me" -UseBasicParsing).Content.Trim()
                $AllowedIPs = @($myIp)
                Write-Info "Auto-detected public IP: $myIp"
            } catch {
                Write-Warning "Could not auto-detect public IP"
            }
        }
        
        foreach ($ip in $AllowedIPs) {
            $ruleName = "Allow-$($ip -replace '\.', '-')"
            az sql server firewall-rule create `
                --resource-group $ResourceGroup `
                --server $ServerName `
                --name $ruleName `
                --start-ip-address $ip `
                --end-ip-address $ip | Out-Null
            
            Write-Success "Firewall rule created: $ruleName ($ip)"
        }
        
        return @{
            Server = $server
            Database = $database
        }
    } finally {
        [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($BSTR)
        $plainPassword = $null
    }
}

function New-StorageAccountAndQueue {
    param(
        [string]$ResourceGroup,
        [string]$Location,
        [string]$AccountName,
        [string]$QueueName
    )
    
    Write-Step "Creating Storage Account and Queue"
    
    if ($WhatIf) {
        Write-Info "Would create storage account: $AccountName"
        Write-Info "Would create queue: $QueueName"
        return
    }
    
    # Create Storage Account
    $storage = az storage account show --name $AccountName --resource-group $ResourceGroup --output json 2>$null | ConvertFrom-Json
    if (-not $storage) {
        $storage = az storage account create `
            --name $AccountName `
            --resource-group $ResourceGroup `
            --location $Location `
            --sku "Standard_GRS" `
            --kind "StorageV2" `
            --min-tls-version "TLS1_2" `
            --allow-blob-public-access $false `
            --https-only $true `
            --output json | ConvertFrom-Json
        
        Write-Success "Storage account created: $AccountName"
    } else {
        Write-Success "Storage account already exists: $AccountName"
    }
    
    # Create Queue
    az storage queue create `
        --name $QueueName `
        --account-name $AccountName `
        --output json | Out-Null
    
    Write-Success "Queue created: $QueueName"
    
    return $storage
}

function New-KeyVault {
    param(
        [string]$ResourceGroup,
        [string]$Location,
        [string]$VaultName
    )
    
    Write-Step "Creating Key Vault"
    
    if ($WhatIf) {
        Write-Info "Would create Key Vault: $VaultName"
        return
    }
    
    $vault = az keyvault show --name $VaultName --resource-group $ResourceGroup --output json 2>$null | ConvertFrom-Json
    if (-not $vault) {
        $vault = az keyvault create `
            --name $VaultName `
            --resource-group $ResourceGroup `
            --location $Location `
            --enabled-for-deployment $true `
            --enabled-for-template-deployment $true `
            --sku "standard" `
            --output json | ConvertFrom-Json
        
        Write-Success "Key Vault created: $VaultName"
    } else {
        Write-Success "Key Vault already exists: $VaultName"
    }
    
    return $vault
}

function New-ApplicationInsights {
    param(
        [string]$ResourceGroup,
        [string]$Location,
        [string]$AppInsightsName
    )
    
    Write-Step "Creating Application Insights"
    
    if ($WhatIf) {
        Write-Info "Would create Application Insights: $AppInsightsName"
        return
    }
    
    # Create Log Analytics workspace first
    $workspaceName = "log-$EnvironmentName"
    $workspace = az monitor log-analytics workspace show `
        --resource-group $ResourceGroup `
        --workspace-name $workspaceName `
        --output json 2>$null | ConvertFrom-Json
    
    if (-not $workspace) {
        $workspace = az monitor log-analytics workspace create `
            --resource-group $ResourceGroup `
            --workspace-name $workspaceName `
            --location $Location `
            --output json | ConvertFrom-Json
        
        Write-Success "Log Analytics workspace created: $workspaceName"
    }
    
    # Create Application Insights
    $appInsights = az monitor app-insights component show `
        --app $AppInsightsName `
        --resource-group $ResourceGroup `
        --output json 2>$null | ConvertFrom-Json
    
    if (-not $appInsights) {
        $appInsights = az monitor app-insights component create `
            --app $AppInsightsName `
            --location $Location `
            --resource-group $ResourceGroup `
            --workspace $workspace.id `
            --output json | ConvertFrom-Json
        
        Write-Success "Application Insights created: $AppInsightsName"
    } else {
        Write-Success "Application Insights already exists: $AppInsightsName"
    }
    
    return $appInsights
}

function New-AppServicePlan {
    param(
        [string]$ResourceGroup,
        [string]$Location,
        [string]$PlanName,
        [string]$Sku
    )
    
    Write-Step "Creating App Service Plan"
    
    if ($WhatIf) {
        Write-Info "Would create App Service Plan: $PlanName (SKU: $Sku)"
        return
    }
    
    $plan = az appservice plan show --name $PlanName --resource-group $ResourceGroup --output json 2>$null | ConvertFrom-Json
    if (-not $plan) {
        $plan = az appservice plan create `
            --name $PlanName `
            --resource-group $ResourceGroup `
            --location $Location `
            --sku $Sku `
            --is-linux `
            --output json | ConvertFrom-Json
        
        Write-Success "App Service Plan created: $PlanName (SKU: $Sku)"
    } else {
        Write-Success "App Service Plan already exists: $PlanName"
    }
    
    return $plan
}

function New-AppServiceApi {
    param(
        [string]$ResourceGroup,
        [string]$AppName,
        [string]$PlanName,
        [string]$AppInsightsKey,
        [string]$SqlConnectionString,
        [string]$StorageConnectionString
    )
    
    Write-Step "Creating API App Service"
    
    if ($WhatIf) {
        Write-Info "Would create App Service: $AppName"
        return
    }
    
    # Create App Service
    $app = az webapp show --name $AppName --resource-group $ResourceGroup --output json 2>$null | ConvertFrom-Json
    if (-not $app) {
        $app = az webapp create `
            --name $AppName `
            --resource-group $ResourceGroup `
            --plan $PlanName `
            --runtime "DOTNETCORE:10.0" `
            --output json | ConvertFrom-Json
        
        Write-Success "API App Service created: $AppName"
    } else {
        Write-Success "API App Service already exists: $AppName"
    }
    
    # Enable Managed Identity
    az webapp identity assign `
        --name $AppName `
        --resource-group $ResourceGroup | Out-Null
    
    Write-Success "Managed Identity enabled for API"
    
    # Configure app settings
    az webapp config appsettings set `
        --name $AppName `
        --resource-group $ResourceGroup `
        --settings `
            "APPLICATIONINSIGHTS_CONNECTION_STRING=$AppInsightsKey" `
            "ASPNETCORE_ENVIRONMENT=Production" `
            "ConnectionStrings__SqlServer=$SqlConnectionString" | Out-Null
    
    Write-Success "App settings configured for API"
    
    # Configure always on
    az webapp config set `
        --name $AppName `
        --resource-group $ResourceGroup `
        --always-on true `
        --ftps-state Disabled `
        --http20-enabled true | Out-Null
    
    Write-Success "Always On enabled for API"
    
    return $app
}

function New-AppServiceWeb {
    param(
        [string]$ResourceGroup,
        [string]$AppName,
        [string]$PlanName,
        [string]$AppInsightsKey,
        [string]$ApiBaseUrl
    )
    
    Write-Step "Creating Web App Service"
    
    if ($WhatIf) {
        Write-Info "Would create App Service: $AppName"
        return
    }
    
    # Create App Service
    $app = az webapp show --name $AppName --resource-group $ResourceGroup --output json 2>$null | ConvertFrom-Json
    if (-not $app) {
        $app = az webapp create `
            --name $AppName `
            --resource-group $ResourceGroup `
            --plan $PlanName `
            --runtime "DOTNETCORE:10.0" `
            --output json | ConvertFrom-Json
        
        Write-Success "Web App Service created: $AppName"
    } else {
        Write-Success "Web App Service already exists: $AppName"
    }
    
    # Enable Managed Identity
    az webapp identity assign `
        --name $AppName `
        --resource-group $ResourceGroup | Out-Null
    
    Write-Success "Managed Identity enabled for Web"
    
    # Configure app settings
    az webapp config appsettings set `
        --name $AppName `
        --resource-group $ResourceGroup `
        --settings `
            "APPLICATIONINSIGHTS_CONNECTION_STRING=$AppInsightsKey" `
            "ASPNETCORE_ENVIRONMENT=Production" `
            "ApiSettings__BaseUrl=$ApiBaseUrl" `
            "ApiSettings__UseCertificateAuth=false" | Out-Null
    
    Write-Success "App settings configured for Web"
    
    # Configure always on
    az webapp config set `
        --name $AppName `
        --resource-group $ResourceGroup `
        --always-on true `
        --ftps-state Disabled `
        --http20-enabled true | Out-Null
    
    Write-Success "Always On enabled for Web"
    
    return $app
}

function Enable-AutoScaling {
    param(
        [string]$ResourceGroup,
        [string]$PlanName
    )
    
    Write-Step "Enabling Auto-scaling"
    
    if ($WhatIf) {
        Write-Info "Would enable auto-scaling for: $PlanName"
        return
    }
    
    $planId = az appservice plan show --name $PlanName --resource-group $ResourceGroup --query id -o tsv
    
    az monitor autoscale create `
        --resource $planId `
        --resource-group $ResourceGroup `
        --name "$PlanName-autoscale" `
        --min-count 2 `
        --max-count 10 `
        --count 2 | Out-Null
    
    # Add CPU rule
    az monitor autoscale rule create `
        --resource-group $ResourceGroup `
        --autoscale-name "$PlanName-autoscale" `
        --condition "Percentage CPU > 70 avg 5m" `
        --scale out 1 | Out-Null
    
    az monitor autoscale rule create `
        --resource-group $ResourceGroup `
        --autoscale-name "$PlanName-autoscale" `
        --condition "Percentage CPU < 30 avg 5m" `
        --scale in 1 | Out-Null
    
    Write-Success "Auto-scaling enabled (min: 2, max: 10)"
}

function Deploy-ApplicationBinaries {
    param(
        [string]$ResourceGroup,
        [string]$ApiAppName,
        [string]$WebAppName,
        [string]$BinariesPath
    )
    
    Write-Step "Deploying application binaries"
    
    if ($WhatIf) {
        Write-Info "Would deploy binaries from: $BinariesPath"
        return
    }
    
    if (-not (Test-Path $BinariesPath)) {
        Write-Warning "Binaries path not found: $BinariesPath"
        Write-Warning "Skipping binary deployment. Deploy manually or run Create-DeploymentPackage.ps1 first"
        return
    }
    
    # Deploy API
    $apiZip = Join-Path $BinariesPath "api"
    if (Test-Path $apiZip) {
        Write-Info "Deploying API binaries..."
        
        # Create zip if directory
        if (Test-Path $apiZip -PathType Container) {
            $tempZip = Join-Path $env:TEMP "api-deploy.zip"
            Compress-Archive -Path "$apiZip\*" -DestinationPath $tempZip -Force
            $apiZip = $tempZip
        }
        
        az webapp deployment source config-zip `
            --resource-group $ResourceGroup `
            --name $ApiAppName `
            --src $apiZip | Out-Null
        
        Write-Success "API deployed"
    }
    
    # Deploy Web
    $webZip = Join-Path $BinariesPath "web"
    if (Test-Path $webZip) {
        Write-Info "Deploying Web binaries..."
        
        # Create zip if directory
        if (Test-Path $webZip -PathType Container) {
            $tempZip = Join-Path $env:TEMP "web-deploy.zip"
            Compress-Archive -Path "$webZip\*" -DestinationPath $tempZip -Force
            $webZip = $tempZip
        }
        
        az webapp deployment source config-zip `
            --resource-group $ResourceGroup `
            --name $WebAppName `
            --src $webZip | Out-Null
        
        Write-Success "Web deployed"
    }
}

function Set-KeyVaultSecrets {
    param(
        [string]$VaultName,
        [string]$SqlServer,
        [string]$SqlDatabase,
        [string]$SqlUser,
        [SecureString]$SqlPassword,
        [string]$StorageAccount,
        [string]$ResourceGroup
    )
    
    Write-Step "Storing secrets in Key Vault"
    
    if ($WhatIf) {
        Write-Info "Would store secrets in Key Vault"
        return
    }
    
    # SQL Connection String
    $BSTR = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($SqlPassword)
    $plainPassword = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($BSTR)
    
    try {
        $sqlConnection = "Server=$SqlServer.database.windows.net;Database=$SqlDatabase;User Id=$SqlUser;Password=$plainPassword;Encrypt=True;TrustServerCertificate=False"
        
        az keyvault secret set `
            --vault-name $VaultName `
            --name "SqlConnectionString" `
            --value $sqlConnection | Out-Null
        
        Write-Success "Secret stored: SqlConnectionString"
    } finally {
        [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($BSTR)
        $plainPassword = $null
    }
    
    # Storage Connection String
    $storageConnection = az storage account show-connection-string `
        --name $StorageAccount `
        --resource-group $ResourceGroup `
        --query connectionString `
        --output tsv
    
    az keyvault secret set `
        --vault-name $VaultName `
        --name "StorageConnectionString" `
        --value $storageConnection | Out-Null
    
    Write-Success "Secret stored: StorageConnectionString"
}

# ===============================================================================
# Main Execution
# ===============================================================================

try {
    Write-Host ""
    Write-Host "===============================================================================" -ForegroundColor Cyan
    Write-Host "SecureBootDashboard - Azure Full Stack Deployment" -ForegroundColor Cyan
    Write-Host "===============================================================================" -ForegroundColor Cyan
    Write-Host ""
    
    # Prerequisites
    if (-not (Test-AzureCli)) { exit 1 }
    if (-not (Test-AzureLogin)) { exit 1 }
    
    Set-AzureSubscription -SubId $SubscriptionId
    
    # Generate resource names
    $timestamp = Get-Date -Format "yyyyMMddHHmm"
    $uniqueSuffix = $timestamp.Substring($timestamp.Length - 6)
    
    $sqlServerName = "sql-secureboot-$EnvironmentName-$uniqueSuffix"
    $sqlDatabaseName = "SecureBootDashboard"
    $storageAccountName = "stsecureboot$EnvironmentName$uniqueSuffix"
    $keyVaultName = "kv-secureboot-$EnvironmentName-$uniqueSuffix"
    $appInsightsName = "appi-secureboot-$EnvironmentName"
    $appServicePlanName = "plan-secureboot-$EnvironmentName"
    $apiAppName = "app-secureboot-api-$EnvironmentName-$uniqueSuffix"
    $webAppName = "app-secureboot-web-$EnvironmentName-$uniqueSuffix"
    $queueName = "secureboot-reports"
    
    Write-Host "Resource Names:" -ForegroundColor Yellow
    Write-Info "Resource Group: $ResourceGroupName"
    Write-Info "SQL Server: $sqlServerName"
    Write-Info "Storage Account: $storageAccountName"
    Write-Info "Key Vault: $keyVaultName"
    Write-Info "App Service Plan: $appServicePlanName (SKU: $AppServicePlanSku)"
    Write-Info "API App: $apiAppName"
    Write-Info "Web App: $webAppName"
    Write-Host ""
    
    if ($WhatIf) {
        Write-Host "Running in WhatIf mode - no resources will be created" -ForegroundColor Yellow
        Write-Host ""
    }
    
    # Generate SQL password if not provided
    if (-not $SqlAdminPassword) {
        $plainPassword = -join ((48..57) + (65..90) + (97..122) + (33..47) | Get-Random -Count 20 | ForEach-Object {[char]$_})
        $SqlAdminPassword = ConvertTo-SecureString -String $plainPassword -AsPlainText -Force
        Write-Info "Generated SQL admin password (will be stored in Key Vault)"
        $plainPassword = $null
    }
    
    # Create Resource Group
    New-AzureResourceGroup -Name $ResourceGroupName -Location $Location
    
    # Create SQL Server and Database
    $sqlResult = New-SqlServerAndDatabase `
        -ResourceGroup $ResourceGroupName `
        -Location $Location `
        -ServerName $sqlServerName `
        -DatabaseName $sqlDatabaseName `
        -AdminUser $SqlAdminUser `
        -AdminPassword $SqlAdminPassword `
        -AllowedIPs $AllowedIpAddresses
    
    # Create Storage Account and Queue
    $storage = New-StorageAccountAndQueue `
        -ResourceGroup $ResourceGroupName `
        -Location $Location `
        -AccountName $storageAccountName `
        -QueueName $queueName
    
    # Create Key Vault
    $keyVault = New-KeyVault `
        -ResourceGroup $ResourceGroupName `
        -Location $Location `
        -VaultName $keyVaultName
    
    # Create Application Insights
    $appInsights = New-ApplicationInsights `
        -ResourceGroup $ResourceGroupName `
        -Location $Location `
        -AppInsightsName $appInsightsName
    
    # Create App Service Plan
    $appServicePlan = New-AppServicePlan `
        -ResourceGroup $ResourceGroupName `
        -Location $Location `
        -PlanName $appServicePlanName `
        -Sku $AppServicePlanSku
    
    # Get connection strings
    $sqlConnection = "Server=$sqlServerName.database.windows.net;Database=$sqlDatabaseName;User Id=$SqlAdminUser;Password=***;Encrypt=True"
    $storageConnection = az storage account show-connection-string `
        --name $storageAccountName `
        --resource-group $ResourceGroupName `
        --query connectionString `
        --output tsv
    
    # Create API App Service
    $apiApp = New-AppServiceApi `
        -ResourceGroup $ResourceGroupName `
        -AppName $apiAppName `
        -PlanName $appServicePlanName `
        -AppInsightsKey $appInsights.connectionString `
        -SqlConnectionString $sqlConnection `
        -StorageConnectionString $storageConnection
    
    # Create Web App Service
    $webApp = New-AppServiceWeb `
        -ResourceGroup $ResourceGroupName `
        -AppName $webAppName `
        -PlanName $appServicePlanName `
        -AppInsightsKey $appInsights.connectionString `
        -ApiBaseUrl "https://$apiAppName.azurewebsites.net"
    
    # Enable Auto-scaling (if requested)
    if ($EnableAutoScale) {
        Enable-AutoScaling `
            -ResourceGroup $ResourceGroupName `
            -PlanName $appServicePlanName
    }
    
    # Store secrets in Key Vault
    Set-KeyVaultSecrets `
        -VaultName $keyVaultName `
        -SqlServer $sqlServerName `
        -SqlDatabase $sqlDatabaseName `
        -SqlUser $SqlAdminUser `
        -SqlPassword $SqlAdminPassword `
        -StorageAccount $storageAccountName `
        -ResourceGroup $ResourceGroupName
    
    # Deploy binaries (if requested)
    if ($DeployBinaries -and $BinariesPath) {
        Deploy-ApplicationBinaries `
            -ResourceGroup $ResourceGroupName `
            -ApiAppName $apiAppName `
            -WebAppName $webAppName `
            -BinariesPath $BinariesPath
    }
    
    # Summary
    Write-Host ""
    Write-Step "Deployment Complete!"
    
    Write-Host "Azure Resources:" -ForegroundColor Green
    Write-Success "SQL Server: $sqlServerName.database.windows.net"
    Write-Success "Database: $sqlDatabaseName"
    Write-Success "Storage Account: $storageAccountName"
    Write-Success "Queue: $queueName"
    Write-Success "Key Vault: $keyVaultName"
    Write-Success "Application Insights: $appInsightsName"
    Write-Success "App Service Plan: $appServicePlanName (SKU: $AppServicePlanSku)"
    Write-Success "API App: https://$apiAppName.azurewebsites.net"
    Write-Success "Web App: https://$webAppName.azurewebsites.net"
    
    Write-Host "`nNext Steps:" -ForegroundColor Yellow
    Write-Host "  1. Apply EF Core migrations" -ForegroundColor White
    Write-Host "     Connection string from Key Vault: SqlConnectionString" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  2. Configure custom domains (optional)" -ForegroundColor White
    Write-Host "     az webapp config hostname add --webapp-name $apiAppName --resource-group $ResourceGroupName --hostname api.yourdomain.com" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  3. Deploy binaries (if not done automatically)" -ForegroundColor White
    Write-Host "     .\Deploy-AzureFullStack.ps1 -DeployBinaries -BinariesPath '.\deploy\packages\SecureBootDashboard-Deploy-v1.5.0\binaries'" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  4. Configure client to connect to Azure API" -ForegroundColor White
    Write-Host "     Update appsettings.json: `"BaseAddress`": `"https://$apiAppName.azurewebsites.net`"" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  5. Monitor in Application Insights" -ForegroundColor White
    Write-Host "     https://portal.azure.com/#resource/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroupName/providers/microsoft.insights/components/$appInsightsName" -ForegroundColor Cyan
    Write-Host ""
    
} catch {
    Write-Host ""
    Write-Host "? Deployment failed: $_" -ForegroundColor Red
    Write-Host "Stack trace: $($_.ScriptStackTrace)" -ForegroundColor Red
    exit 1
}
