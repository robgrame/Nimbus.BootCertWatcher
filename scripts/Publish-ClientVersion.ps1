# SecureBootWatcher Client - Release and Publish Script
# This script builds, packages, and publishes a new client version

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    
    [Parameter(Mandatory = $false)]
    [string]$OutputPath = ".\release",
    
    [Parameter(Mandatory = $false)]
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",
    
    [Parameter(Mandatory = $false)]
    [string]$AzureStorageAccount = "",
    
    [Parameter(Mandatory = $false)]
    [string]$AzureContainer = "client-packages",
    
    [Parameter(Mandatory = $false)]
    [switch]$UploadToAzure,
    
    [Parameter(Mandatory = $false)]
    [switch]$UpdateApiConfig
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  SecureBootWatcher Client Publisher" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Version: $Version" -ForegroundColor Green
Write-Host "Configuration: $Configuration" -ForegroundColor Gray
Write-Host ""

# Get script directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent $scriptDir

# Validate version format
if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    Write-Host "Error: Version must be in format X.Y.Z (e.g., 1.2.0)" -ForegroundColor Red
    exit 1
}

# Parse version components
$versionParts = $Version.Split('.')
$majorVersion = $versionParts[0]
$minorVersion = $versionParts[1]
$patchVersion = $versionParts[2]
$fullVersion = "$majorVersion.$minorVersion.$patchVersion.0"

Write-Host "[1/6] Updating project version..." -ForegroundColor Yellow

# Update .csproj file
$csprojPath = Join-Path $rootDir "SecureBootWatcher.Client\SecureBootWatcher.Client.csproj"
$csprojContent = Get-Content $csprojPath -Raw

# Update version properties
$csprojContent = $csprojContent -replace '<Version>.*?</Version>', "<Version>$Version</Version>"
$csprojContent = $csprojContent -replace '<AssemblyVersion>.*?</AssemblyVersion>', "<AssemblyVersion>$fullVersion</AssemblyVersion>"
$csprojContent = $csprojContent -replace '<FileVersion>.*?</FileVersion>', "<FileVersion>$fullVersion</FileVersion>"
$csprojContent = $csprojContent -replace '<InformationalVersion>.*?</InformationalVersion>', "<InformationalVersion>$Version</InformationalVersion>"

Set-Content -Path $csprojPath -Value $csprojContent -NoNewline
Write-Host "  Project version updated to: $Version" -ForegroundColor Green
Write-Host ""

Write-Host "[2/6] Building client..." -ForegroundColor Yellow

try {
    Push-Location $rootDir
    
    # Clean previous builds
    $cleanResult = dotnet clean SecureBootWatcher.Client -c $Configuration 2>&1
    
    # Build project
    $buildResult = dotnet build SecureBootWatcher.Client -c $Configuration --no-incremental 2>&1
    
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code $LASTEXITCODE"
    }
    
    Pop-Location
    Write-Host "  Build successful" -ForegroundColor Green
}
catch {
    Pop-Location
    Write-Host "  Build failed: $_" -ForegroundColor Red
    exit 1
}
Write-Host ""

Write-Host "[3/6] Publishing client..." -ForegroundColor Yellow

$publishPath = Join-Path $rootDir "SecureBootWatcher.Client\bin\$Configuration\net48\win-x86\publish"

try {
    Push-Location $rootDir
    
    dotnet publish SecureBootWatcher.Client `
        -c $Configuration `
        -r win-x86 `
        --self-contained false `
        -o $publishPath `
        /p:PublishSingleFile=false `
        /p:PublishTrimmed=false
    
    if ($LASTEXITCODE -ne 0) {
        throw "Publish failed with exit code $LASTEXITCODE"
    }
    
    Pop-Location
    Write-Host "  Publish successful" -ForegroundColor Green
    Write-Host "     Output: $publishPath" -ForegroundColor Gray
}
catch {
    Pop-Location
    Write-Host "  Publish failed: $_" -ForegroundColor Red
    exit 1
}
Write-Host ""

Write-Host "[4/6] Creating package..." -ForegroundColor Yellow

# Create output directory
$releasePath = Join-Path $OutputPath $Version
if (-not (Test-Path $releasePath)) {
    New-Item -ItemType Directory -Path $releasePath -Force | Out-Null
}

# Package filename
$packageName = "SecureBootWatcher-Client-$Version.zip"
$packagePath = Join-Path $releasePath $packageName

try {
    # Remove old package if exists
    if (Test-Path $packagePath) {
        Remove-Item $packagePath -Force
    }
    
    # Create ZIP package
    Compress-Archive -Path "$publishPath\*" -DestinationPath $packagePath -Force
    
    $packageSize = (Get-Item $packagePath).Length / 1MB
    Write-Host "  Package created" -ForegroundColor Green
    Write-Host "     Path: $packagePath" -ForegroundColor Gray
    Write-Host "     Size: $([math]::Round($packageSize, 2)) MB" -ForegroundColor Gray
}
catch {
    Write-Host "  Package creation failed: $_" -ForegroundColor Red
    exit 1
}
Write-Host ""

Write-Host "[5/6] Calculating checksum..." -ForegroundColor Yellow

try {
    $hash = Get-FileHash $packagePath -Algorithm SHA256
    $checksum = $hash.Hash
    
    # Save checksum to file
    $checksumPath = Join-Path $releasePath "SecureBootWatcher-Client-$Version.sha256"
    $checksum | Out-File -FilePath $checksumPath -Encoding UTF8 -NoNewline
    
    Write-Host "  SHA256: $checksum" -ForegroundColor Green
    Write-Host "     Saved to: $checksumPath" -ForegroundColor Gray
}
catch {
    Write-Host "  Checksum calculation failed: $_" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Upload to Azure Blob Storage
if ($UploadToAzure) {
    Write-Host "[6/6] Uploading to Azure Blob Storage..." -ForegroundColor Yellow
    
    if ([string]::IsNullOrEmpty($AzureStorageAccount)) {
        Write-Host "  Error: Azure Storage Account name required for upload" -ForegroundColor Red
        Write-Host "  Use: -AzureStorageAccount 'yourstorageaccount'" -ForegroundColor Yellow
        exit 1
    }
    
    try {
        # Check if Azure CLI is available
        $azVersion = az version 2>$null
        if (-not $azVersion) {
            throw "Azure CLI not found. Install from: https://aka.ms/installazurecliwindows"
        }
        
        # Upload package
        Write-Host "  Uploading package..." -ForegroundColor Gray
        az storage blob upload `
            --account-name $AzureStorageAccount `
            --container-name $AzureContainer `
            --name $packageName `
            --file $packagePath `
            --auth-mode login `
            --overwrite
        
        if ($LASTEXITCODE -ne 0) {
            throw "Upload failed with exit code $LASTEXITCODE"
        }
        
        # Upload checksum
        Write-Host "  Uploading checksum..." -ForegroundColor Gray
        az storage blob upload `
            --account-name $AzureStorageAccount `
            --container-name $AzureContainer `
            --name "SecureBootWatcher-Client-$Version.sha256" `
            --file $checksumPath `
            --auth-mode login `
            --overwrite
        
        # Get blob URL
        $blobUrl = "https://$AzureStorageAccount.blob.core.windows.net/$AzureContainer/$packageName"
        
        Write-Host "  Upload successful" -ForegroundColor Green
        Write-Host "     URL: $blobUrl" -ForegroundColor Gray
    }
    catch {
        Write-Host "  Upload failed: $_" -ForegroundColor Red
        Write-Host "  Package is still available locally at: $packagePath" -ForegroundColor Yellow
    }
    Write-Host ""
} else {
    Write-Host "[6/6] Skipping Azure upload (use -UploadToAzure to enable)" -ForegroundColor Yellow
    Write-Host ""
}

# Update API configuration
if ($UpdateApiConfig) {
    Write-Host "[BONUS] Updating API configuration..." -ForegroundColor Yellow
    
    $apiConfigPath = Join-Path $rootDir "SecureBootDashboard.Api\appsettings.json"
    
    if (Test-Path $apiConfigPath) {
        try {
            $config = Get-Content $apiConfigPath -Raw | ConvertFrom-Json
            
            # Update version info
            $config.ClientUpdate.LatestVersion = $fullVersion
            $config.ClientUpdate.ReleaseDate = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
            
            if ($UploadToAzure) {
                $config.ClientUpdate.DownloadUrl = "https://$AzureStorageAccount.blob.core.windows.net/$AzureContainer/$packageName"
            }
            
            $config.ClientUpdate.Checksum = $checksum
            $config.ClientUpdate.FileSize = (Get-Item $packagePath).Length
            
            # Save updated config
            $config | ConvertTo-Json -Depth 10 | Set-Content $apiConfigPath -Encoding UTF8
            
            Write-Host "  API configuration updated" -ForegroundColor Green
            Write-Host "     Latest Version: $fullVersion" -ForegroundColor Gray
            Write-Host "     Release Date: $($config.ClientUpdate.ReleaseDate)" -ForegroundColor Gray
        }
        catch {
            Write-Host "  Failed to update API config: $_" -ForegroundColor Red
        }
    } else {
        Write-Host "  API config not found at: $apiConfigPath" -ForegroundColor Yellow
    }
    Write-Host ""
}

# Summary
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Release Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Version: $Version ($fullVersion)" -ForegroundColor Green
Write-Host "Package: $packagePath" -ForegroundColor Cyan
Write-Host "Checksum: $checksum" -ForegroundColor Gray
Write-Host ""

if ($UploadToAzure -and $LASTEXITCODE -eq 0) {
    Write-Host "? Package published to Azure Storage" -ForegroundColor Green
    Write-Host "  URL: https://$AzureStorageAccount.blob.core.windows.net/$AzureContainer/$packageName" -ForegroundColor Cyan
    Write-Host ""
}

Write-Host "Next Steps:" -ForegroundColor White
Write-Host "1. Test the package locally" -ForegroundColor Gray
Write-Host "2. Update API configuration if not done automatically:" -ForegroundColor Gray
Write-Host "   - LatestVersion: $fullVersion" -ForegroundColor Gray
Write-Host "   - DownloadUrl: <your-blob-url>" -ForegroundColor Gray
Write-Host "   - Checksum: $checksum" -ForegroundColor Gray
Write-Host "3. Deploy to Intune/GPO/SCCM" -ForegroundColor Gray
Write-Host "4. Monitor dashboard for version adoption" -ForegroundColor Gray
Write-Host ""

Write-Host "Done! ?" -ForegroundColor Green
