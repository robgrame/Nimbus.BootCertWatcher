# ===============================================================================
# Deploy-AzureHybrid.ps1
#
# Deploys SecureBootDashboard infrastructure to Azure (Hybrid Architecture)
# - Azure SQL Database
# - Azure Storage Account (Queue)
# - Azure Key Vault
# - On-premises servers connect to Azure resources
#
# Requirements:
# - Azure CLI installed
# - Azure subscription with appropriate permissions
# - Logged in to Azure (az login)
#
# Usage:
#   .\Deploy-AzureHybrid.ps1 -SubscriptionId "xxx" -ResourceGroupName "rg-secureboot-prod"
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
    [string[]]$AllowedIpAddresses = @(),
    
    [Parameter(Mandatory = $false)]
    [switch]$EnableVpnGateway,
    
    [Parameter(Mandatory = $false)]
    [switch]$EnableDiagnostics,
    
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
    
    # Check if exists
    $existing = az group show --name $Name --output json 2>$null | ConvertFrom-Json
    if ($existing) {
        Write-Success "Resource group already exists: $Name"
        return $existing
    }
    
    az group create --name $Name --location $Location --output json | ConvertFrom-Json
    Write-Success "Resource group created: $Name"
}

function New-AzureSqlServer {
    param(
        [string]$ResourceGroup,
        [string]$Location,
        [string]$ServerName,
        [string]$AdminUser,
        [SecureString]$AdminPassword
    )
    
    Write-Step "Creating SQL Server"
    
    if ($WhatIf) {
        Write-Info "Would create SQL Server: $ServerName"
        return
    }
    
    # Convert SecureString to plain text (needed for Azure CLI)
    $BSTR = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($AdminPassword)
    $plainPassword = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($BSTR)
    
    try {
        # Check if exists
        $existing = az sql server show --name $ServerName --resource-group $ResourceGroup --output json 2>$null | ConvertFrom-Json
        if ($existing) {
            Write-Success "SQL Server already exists: $ServerName"
            return $existing
        }
        
        $server = az sql server create `
            --name $ServerName `
            --resource-group $ResourceGroup `
            --location $Location `
            --admin-user $AdminUser `
            --admin-password $plainPassword `
            --minimal-tls-version "1.2" `
            --output json | ConvertFrom-Json
        
        Write-Success "SQL Server created: $($server.fullyQualifiedDomainName)"
        return $server
    } finally {
        # Clear password from memory
        [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($BSTR)
        $plainPassword = $null
    }
}

function New-AzureSqlDatabase {
    param(
        [string]$ResourceGroup,
        [string]$ServerName,
        [string]$DatabaseName,
        [string]$ServiceObjective = "S2"
    )
    
    Write-Step "Creating SQL Database"
    
    if ($WhatIf) {
        Write-Info "Would create database: $DatabaseName (tier: $ServiceObjective)"
        return
    }
    
    # Check if exists
    $existing = az sql db show --name $DatabaseName --server $ServerName --resource-group $ResourceGroup --output json 2>$null | ConvertFrom-Json
    if ($existing) {
        Write-Success "Database already exists: $DatabaseName"
        return $existing
    }
    
    $database = az sql db create `
        --resource-group $ResourceGroup `
        --server $ServerName `
        --name $DatabaseName `
        --service-objective $ServiceObjective `
        --backup-storage-redundancy "Geo" `
        --zone-redundant $false `
        --output json | ConvertFrom-Json
    
    Write-Success "Database created: $DatabaseName (tier: $ServiceObjective)"
    return $database
}

function Set-AzureSqlFirewallRules {
    param(
        [string]$ResourceGroup,
        [string]$ServerName,
        [string[]]$AllowedIPs
    )
    
    Write-Step "Configuring SQL Server firewall"
    
    if ($WhatIf) {
        Write-Info "Would configure firewall rules"
        return
    }
    
    # Allow Azure services
    az sql server firewall-rule create `
        --resource-group $ResourceGroup `
        --server $ServerName `
        --name "AllowAzureServices" `
        --start-ip-address "0.0.0.0" `
        --end-ip-address "0.0.0.0" | Out-Null
    
    Write-Success "Firewall rule created: AllowAzureServices"
    
    # Allow specific IPs
    if ($AllowedIPs.Count -eq 0) {
        # Get current public IP
        try {
            $myIp = (Invoke-WebRequest -Uri "https://ifconfig.me" -UseBasicParsing).Content.Trim()
            $AllowedIPs = @($myIp)
            Write-Info "Auto-detected public IP: $myIp"
        } catch {
            Write-Warning "Could not auto-detect public IP. Skipping IP whitelist."
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
}

function New-AzureStorageAccount {
    param(
        [string]$ResourceGroup,
        [string]$Location,
        [string]$AccountName
    )
    
    Write-Step "Creating Storage Account"
    
    if ($WhatIf) {
        Write-Info "Would create storage account: $AccountName"
        return
    }
    
    # Check if exists
    $existing = az storage account show --name $AccountName --resource-group $ResourceGroup --output json 2>$null | ConvertFrom-Json
    if ($existing) {
        Write-Success "Storage account already exists: $AccountName"
        return $existing
    }
    
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
    return $storage
}

function New-AzureStorageQueue {
    param(
        [string]$AccountName,
        [string]$QueueName
    )
    
    Write-Step "Creating Storage Queue"
    
    if ($WhatIf) {
        Write-Info "Would create queue: $QueueName"
        return
    }
    
    az storage queue create `
        --name $QueueName `
        --account-name $AccountName `
        --output json | Out-Null
    
    Write-Success "Queue created: $QueueName"
}

function New-AzureKeyVault {
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
    
    # Check if exists
    $existing = az keyvault show --name $VaultName --resource-group $ResourceGroup --output json 2>$null | ConvertFrom-Json
    if ($existing) {
        Write-Success "Key Vault already exists: $VaultName"
        return $existing
    }
    
    $vault = az keyvault create `
        --name $VaultName `
        --resource-group $ResourceGroup `
        --location $Location `
        --enabled-for-deployment $true `
        --enabled-for-template-deployment $true `
        --sku "standard" `
        --output json | ConvertFrom-Json
    
    Write-Success "Key Vault created: $VaultName"
    return $vault
}

function Set-KeyVaultSecrets {
    param(
        [string]$VaultName,
        [string]$SqlServer,
        [string]$SqlDatabase,
        [string]$SqlUser,
        [SecureString]$SqlPassword,
        [string]$StorageAccount
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
        
        # SQL Admin Password
        az keyvault secret set `
            --vault-name $VaultName `
            --name "SqlAdminPassword" `
            --value $plainPassword | Out-Null
        
        Write-Success "Secret stored: SqlAdminPassword"
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

function Enable-Diagnostics {
    param(
        [string]$ResourceGroup,
        [string]$Location,
        [string]$SqlServer,
        [string]$SqlDatabase
    )
    
    Write-Step "Enabling diagnostics and monitoring"
    
    if ($WhatIf) {
        Write-Info "Would enable diagnostics"
        return
    }
    
    # Create Log Analytics workspace
    $workspaceName = "log-secureboot-$EnvironmentName"
    
    $workspace = az monitor log-analytics workspace create `
        --resource-group $ResourceGroup `
        --workspace-name $workspaceName `
        --location $Location `
        --output json | ConvertFrom-Json
    
    Write-Success "Log Analytics workspace created: $workspaceName"
    
    # Get database resource ID
    $dbResourceId = az sql db show `
        --name $SqlDatabase `
        --server $SqlServer `
        --resource-group $ResourceGroup `
        --query id `
        --output tsv
    
    # Enable SQL diagnostics
    az monitor diagnostic-settings create `
        --name "sql-diagnostics" `
        --resource $dbResourceId `
        --workspace $workspace.id `
        --logs '[{"category": "SQLInsights", "enabled": true}, {"category": "QueryStoreRuntimeStatistics", "enabled": true}]' `
        --metrics '[{"category": "Basic", "enabled": true}]' | Out-Null
    
    Write-Success "Diagnostics enabled for SQL Database"
}

# ===============================================================================
# Main Execution
# ===============================================================================

try {
    Write-Host ""
    Write-Host "===============================================================================" -ForegroundColor Cyan
    Write-Host "SecureBootDashboard - Azure Hybrid Deployment" -ForegroundColor Cyan
    Write-Host "===============================================================================" -ForegroundColor Cyan
    Write-Host ""
    
    # Step 1: Verify prerequisites
    if (-not (Test-AzureCli)) {
        exit 1
    }
    
    if (-not (Test-AzureLogin)) {
        exit 1
    }
    
    # Step 2: Set subscription
    Set-AzureSubscription -SubId $SubscriptionId
    
    # Step 3: Generate resource names
    $timestamp = Get-Date -Format "yyyyMMddHHmm"
    $uniqueSuffix = $timestamp.Substring($timestamp.Length - 6)
    
    $sqlServerName = "sql-secureboot-$EnvironmentName-$uniqueSuffix"
    $sqlDatabaseName = "SecureBootDashboard"
    $storageAccountName = "stsecureboot$EnvironmentName$uniqueSuffix"
    $keyVaultName = "kv-secureboot-$EnvironmentName-$uniqueSuffix"
    $queueName = "secureboot-reports"
    
    Write-Host "Resource Names:" -ForegroundColor Yellow
    Write-Info "Resource Group: $ResourceGroupName"
    Write-Info "SQL Server: $sqlServerName"
    Write-Info "Database: $sqlDatabaseName"
    Write-Info "Storage Account: $storageAccountName"
    Write-Info "Key Vault: $keyVaultName"
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
    
    # Step 4: Create resource group
    $resourceGroup = New-AzureResourceGroup -Name $ResourceGroupName -Location $Location
    
    # Step 5: Create SQL Server
    $sqlServer = New-AzureSqlServer `
        -ResourceGroup $ResourceGroupName `
        -Location $Location `
        -ServerName $sqlServerName `
        -AdminUser $SqlAdminUser `
        -AdminPassword $SqlAdminPassword
    
    # Step 6: Create SQL Database
    $sqlDatabase = New-AzureSqlDatabase `
        -ResourceGroup $ResourceGroupName `
        -ServerName $sqlServerName `
        -DatabaseName $sqlDatabaseName `
        -ServiceObjective "S2"
    
    # Step 7: Configure firewall
    Set-AzureSqlFirewallRules `
        -ResourceGroup $ResourceGroupName `
        -ServerName $sqlServerName `
        -AllowedIPs $AllowedIpAddresses
    
    # Step 8: Create Storage Account
    $storageAccount = New-AzureStorageAccount `
        -ResourceGroup $ResourceGroupName `
        -Location $Location `
        -AccountName $storageAccountName
    
    # Step 9: Create Queue
    New-AzureStorageQueue `
        -AccountName $storageAccountName `
        -QueueName $queueName
    
    # Step 10: Create Key Vault
    $keyVault = New-AzureKeyVault `
        -ResourceGroup $ResourceGroupName `
        -Location $Location `
        -VaultName $keyVaultName
    
    # Step 11: Store secrets
    Set-KeyVaultSecrets `
        -VaultName $keyVaultName `
        -SqlServer $sqlServerName `
        -SqlDatabase $sqlDatabaseName `
        -SqlUser $SqlAdminUser `
        -SqlPassword $SqlAdminPassword `
        -StorageAccount $storageAccountName
    
    # Step 12: Enable diagnostics (optional)
    if ($EnableDiagnostics) {
        Enable-Diagnostics `
            -ResourceGroup $ResourceGroupName `
            -Location $Location `
            -SqlServer $sqlServerName `
            -SqlDatabase $sqlDatabaseName
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
    
    Write-Host "`nConnection Information:" -ForegroundColor Yellow
    Write-Info "Retrieve connection strings from Key Vault:"
    Write-Host "  az keyvault secret show --vault-name $keyVaultName --name SqlConnectionString --query value -o tsv" -ForegroundColor Cyan
    Write-Host "  az keyvault secret show --vault-name $keyVaultName --name StorageConnectionString --query value -o tsv" -ForegroundColor Cyan
    
    Write-Host "`nNext Steps:" -ForegroundColor Yellow
    Write-Host "  1. Apply EF Core migrations to database" -ForegroundColor White
    Write-Host "     cd SecureBootDashboard.Api" -ForegroundColor Cyan
    Write-Host "     dotnet ef database update --connection `"`$(az keyvault secret show --vault-name $keyVaultName --name SqlConnectionString --query value -o tsv)`"" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  2. Create App Registration for Queue access" -ForegroundColor White
    Write-Host "     az ad app create --display-name SecureBootDashboard-API" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  3. Update API appsettings.Production.json" -ForegroundColor White
    Write-Host "  4. Deploy API and Web to on-premises servers" -ForegroundColor White
    Write-Host ""
    
} catch {
    Write-Host ""
    Write-Host "? Deployment failed: $_" -ForegroundColor Red
    Write-Host "Stack trace: $($_.ScriptStackTrace)" -ForegroundColor Red
    exit 1
}
