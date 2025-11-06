# Assign Azure Storage Queue RBAC Role to Service Principal
# Run this script with Azure CLI or Az PowerShell module

param(
    [string]$TenantId = "d6dbad84-5922-4700-a049-c7068c37c884",
    [string]$ClientId = "c8034569-4990-4823-9f1d-b46223789c35",
    [string]$StorageAccountName = "secbootcert",
    [string]$ResourceGroupName = "" # Will auto-detect if not provided
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Azure Storage Queue RBAC Assignment" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if Azure CLI is available
$azCliAvailable = $false
try {
    $null = az version 2>$null
    $azCliAvailable = $true
    Write-Host "? Azure CLI detected" -ForegroundColor Green
} catch {
    Write-Host "??  Azure CLI not found" -ForegroundColor Yellow
}

# Check if Az PowerShell module is available
$azModuleAvailable = $false
try {
    $null = Get-Module -ListAvailable -Name Az.Accounts
    $azModuleAvailable = $true
    Write-Host "? Az PowerShell module detected" -ForegroundColor Green
} catch {
    Write-Host "??  Az PowerShell module not found" -ForegroundColor Yellow
}

if (-not $azCliAvailable -and -not $azModuleAvailable) {
    Write-Host ""
    Write-Host "? ERROR: Neither Azure CLI nor Az PowerShell module found!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Install one of the following:" -ForegroundColor Yellow
    Write-Host "  1. Azure CLI: https://aka.ms/installazurecliwindows" -ForegroundColor White
    Write-Host "  2. Az PowerShell: Install-Module -Name Az -AllowClobber -Scope CurrentUser" -ForegroundColor White
    exit 1
}

Write-Host ""
Write-Host "Using Azure CLI for role assignment..." -ForegroundColor Cyan
Write-Host ""

# Login check
Write-Host "[1/5] Checking Azure authentication..." -ForegroundColor Yellow
try {
    $account = az account show 2>$null | ConvertFrom-Json
    if (-not $account) {
        Write-Host "  Not logged in. Logging in..." -ForegroundColor Yellow
        az login --tenant $TenantId
        $account = az account show | ConvertFrom-Json
    }
    
    Write-Host "  ? Logged in" -ForegroundColor Green
    Write-Host "     Subscription: $($account.name)" -ForegroundColor Gray
    Write-Host "     Tenant: $($account.tenantId)" -ForegroundColor Gray
    
    if ($account.tenantId -ne $TenantId) {
        Write-Host "  ??  WARNING: Logged into different tenant!" -ForegroundColor Yellow
        Write-Host "     Expected: $TenantId" -ForegroundColor Yellow
        Write-Host "     Current: $($account.tenantId)" -ForegroundColor Yellow
        Write-Host ""
        $continue = Read-Host "Continue anyway? (y/n)"
        if ($continue -ne 'y') {
            exit 0
        }
    }
} catch {
    Write-Host "  ? Failed to authenticate: $_" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Get Service Principal
Write-Host "[2/5] Getting Service Principal..." -ForegroundColor Yellow
try {
    $sp = az ad sp list --filter "appId eq '$ClientId'" 2>$null | ConvertFrom-Json
    if (-not $sp -or $sp.Count -eq 0) {
        Write-Host "  ? Service Principal not found with Client ID: $ClientId" -ForegroundColor Red
        exit 1
    }
    
    $spObjectId = $sp[0].id
    $spDisplayName = $sp[0].displayName
    
    Write-Host "  ? Service Principal found" -ForegroundColor Green
    Write-Host "     Display Name: $spDisplayName" -ForegroundColor Gray
    Write-Host "     Object ID: $spObjectId" -ForegroundColor Gray
} catch {
    Write-Host "  ? Failed to get Service Principal: $_" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Get Storage Account
Write-Host "[3/5] Getting Storage Account..." -ForegroundColor Yellow
try {
    # If resource group not provided, find it
    if ([string]::IsNullOrEmpty($ResourceGroupName)) {
        Write-Host "     Searching for storage account..." -ForegroundColor Gray
        $storageList = az storage account list --query "[?name=='$StorageAccountName']" | ConvertFrom-Json
        
        if ($storageList.Count -eq 0) {
            Write-Host "  ? Storage account '$StorageAccountName' not found" -ForegroundColor Red
            exit 1
        }
        
        $ResourceGroupName = $storageList[0].resourceGroup
    }
    
    $storageAccount = az storage account show `
        --name $StorageAccountName `
        --resource-group $ResourceGroupName `
        2>$null | ConvertFrom-Json
    
    if (-not $storageAccount) {
        Write-Host "  ? Storage account not found" -ForegroundColor Red
        Write-Host "     Name: $StorageAccountName" -ForegroundColor Red
        Write-Host "     Resource Group: $ResourceGroupName" -ForegroundColor Red
        exit 1
    }
    
    $storageId = $storageAccount.id
    
    Write-Host "  ? Storage Account found" -ForegroundColor Green
    Write-Host "     Name: $StorageAccountName" -ForegroundColor Gray
    Write-Host "     Resource Group: $ResourceGroupName" -ForegroundColor Gray
    Write-Host "     Resource ID: $storageId" -ForegroundColor Gray
} catch {
    Write-Host "  ? Failed to get Storage Account: $_" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Check existing role assignments
Write-Host "[4/5] Checking existing role assignments..." -ForegroundColor Yellow
try {
    $existingRoles = az role assignment list `
        --assignee $spObjectId `
        --scope $storageId `
        --query "[?roleDefinitionName=='Storage Queue Data Contributor']" `
        2>$null | ConvertFrom-Json
    
    if ($existingRoles -and $existingRoles.Count -gt 0) {
        Write-Host "  ??  Role already assigned!" -ForegroundColor Cyan
        Write-Host "     Role: Storage Queue Data Contributor" -ForegroundColor Gray
        Write-Host "     Scope: $StorageAccountName" -ForegroundColor Gray
        Write-Host ""
        Write-Host "No action needed. Role is already configured." -ForegroundColor Green
        exit 0
    } else {
        Write-Host "  ??  Role not assigned yet" -ForegroundColor Cyan
    }
} catch {
    Write-Host "  ??  Could not check existing roles: $_" -ForegroundColor Yellow
}
Write-Host ""

# Assign role
Write-Host "[5/5] Assigning 'Storage Queue Data Contributor' role..." -ForegroundColor Yellow
try {
    $result = az role assignment create `
        --role "Storage Queue Data Contributor" `
        --assignee $spObjectId `
        --scope $storageId `
        2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  ? Role assigned successfully!" -ForegroundColor Green
    } else {
        Write-Host "  ? Failed to assign role" -ForegroundColor Red
        Write-Host "     Error: $result" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "  ? Failed to assign role: $_" -ForegroundColor Red
    exit 1
}
Write-Host ""

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  ? SUCCESS!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "The Service Principal '$spDisplayName' now has" -ForegroundColor White
Write-Host "'Storage Queue Data Contributor' permissions on" -ForegroundColor White
Write-Host "storage account '$StorageAccountName'." -ForegroundColor White
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Restart the API application" -ForegroundColor White
Write-Host "  2. Check logs for successful queue processing" -ForegroundColor White
Write-Host "  3. Monitor: Get-Content 'C:\path\to\api\logs\api-*.log' -Tail 50 -Wait" -ForegroundColor White
Write-Host ""
Write-Host "Expected log output:" -ForegroundColor Yellow
Write-Host "  [INF] Queue processor started successfully." -ForegroundColor Gray
Write-Host "  [INF] Received X message(s) from queue secureboot-reports" -ForegroundColor Gray
Write-Host ""
