# Prepare Intune Package Script
# This script prepares the SecureBootWatcher client package for Intune deployment
# It creates a staging directory with all required files for conversion to .intunewin format

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$OutputPath = "C:\Temp\SecureBootWatcher-Intune",
    
    [Parameter(Mandatory = $false)]
    [string]$CertificatePath = "",
    
    [Parameter(Mandatory = $false)]
    [switch]$Force
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Intune Package Preparation" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Get script directory and repository root
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir

# Define paths
$clientPackageZip = Join-Path $repoRoot "client-package\SecureBootWatcher-Client.zip"
$installScript = Join-Path $scriptDir "Install-Client-Intune.ps1"
$uninstallScript = Join-Path $scriptDir "Uninstall-Client-Intune.ps1"
$detectScript = Join-Path $scriptDir "Detect-Client-Intune.ps1"

# Step 1: Verify client package exists
Write-Host "[1/4] Verifying client package..." -ForegroundColor Yellow

if (-not (Test-Path $clientPackageZip)) {
    Write-Host "ERROR: Client package not found at: $clientPackageZip" -ForegroundColor Red
    Write-Host ""
    Write-Host "You must build the client package first by running:" -ForegroundColor Yellow
    Write-Host "  .\scripts\Deploy-Client.ps1" -ForegroundColor Cyan
    Write-Host ""
    exit 1
}

$zipSize = (Get-Item $clientPackageZip).Length / 1MB
Write-Host "  Found: SecureBootWatcher-Client.zip ($([math]::Round($zipSize, 2)) MB)" -ForegroundColor Green
Write-Host ""

# Step 2: Create staging directory
Write-Host "[2/4] Creating staging directory..." -ForegroundColor Yellow

if (Test-Path $OutputPath) {
    if ($Force) {
        Write-Host "  Removing existing directory (Force specified)" -ForegroundColor Yellow
        Remove-Item -Path $OutputPath -Recurse -Force
    } else {
        Write-Host "ERROR: Output directory already exists: $OutputPath" -ForegroundColor Red
        Write-Host ""
        Write-Host "Options:" -ForegroundColor Yellow
        Write-Host "  1. Remove it manually and re-run this script" -ForegroundColor Cyan
        Write-Host "  2. Use -Force parameter to overwrite" -ForegroundColor Cyan
        Write-Host "  3. Specify a different -OutputPath" -ForegroundColor Cyan
        Write-Host ""
        exit 1
    }
}

New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
Write-Host "  Created: $OutputPath" -ForegroundColor Green
Write-Host ""

# Step 3: Copy required files
Write-Host "[3/4] Copying package files..." -ForegroundColor Yellow

try {
    # Copy client package ZIP
    Copy-Item -Path $clientPackageZip -Destination $OutputPath -Force
    Write-Host "  Copied: SecureBootWatcher-Client.zip" -ForegroundColor Green
    
    # Copy Intune scripts
    Copy-Item -Path $installScript -Destination $OutputPath -Force
    Write-Host "  Copied: Install-Client-Intune.ps1" -ForegroundColor Green
    
    Copy-Item -Path $uninstallScript -Destination $OutputPath -Force
    Write-Host "  Copied: Uninstall-Client-Intune.ps1" -ForegroundColor Green
    
    Copy-Item -Path $detectScript -Destination $OutputPath -Force
    Write-Host "  Copied: Detect-Client-Intune.ps1" -ForegroundColor Green
    
    # Copy certificate if specified
    if (-not [string]::IsNullOrEmpty($CertificatePath)) {
        if (Test-Path $CertificatePath) {
            $certFileName = Split-Path -Leaf $CertificatePath
            $destCertPath = Join-Path $OutputPath "SecureBootWatcher.pfx"
            
            Copy-Item -Path $CertificatePath -Destination $destCertPath -Force
            Write-Host "  Copied: $certFileName -> SecureBootWatcher.pfx" -ForegroundColor Green
        } else {
            Write-Host "  WARNING: Certificate not found at: $CertificatePath" -ForegroundColor Yellow
            Write-Host "  Continuing without certificate (optional)" -ForegroundColor Yellow
        }
    }
}
catch {
    Write-Host "  ERROR: Failed to copy files: $_" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Step 4: Verify package structure
Write-Host "[4/4] Verifying package structure..." -ForegroundColor Yellow

$requiredFiles = @(
    "SecureBootWatcher-Client.zip",
    "Install-Client-Intune.ps1",
    "Uninstall-Client-Intune.ps1",
    "Detect-Client-Intune.ps1"
)

$allPresent = $true
foreach ($file in $requiredFiles) {
    $filePath = Join-Path $OutputPath $file
    if (Test-Path $filePath) {
        $fileSize = (Get-Item $filePath).Length / 1KB
        Write-Host "  ? $file ($([math]::Round($fileSize, 2)) KB)" -ForegroundColor Green
    } else {
        Write-Host "  ? $file (MISSING)" -ForegroundColor Red
        $allPresent = $false
    }
}

# Check for optional certificate
$certPath = Join-Path $OutputPath "SecureBootWatcher.pfx"
if (Test-Path $certPath) {
    $certSize = (Get-Item $certPath).Length / 1KB
    Write-Host "  ? SecureBootWatcher.pfx ($([math]::Round($certSize, 2)) KB) - OPTIONAL" -ForegroundColor Green
} else {
    Write-Host "  - SecureBootWatcher.pfx (not included - optional)" -ForegroundColor Gray
}

Write-Host ""

if (-not $allPresent) {
    Write-Host "ERROR: Package verification failed - missing required files" -ForegroundColor Red
    exit 1
}

# Summary
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Package Prepared Successfully!" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Package Location:" -ForegroundColor White
Write-Host "  $OutputPath" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor White
Write-Host ""
Write-Host "1. Convert to .intunewin format using Microsoft Win32 Content Prep Tool:" -ForegroundColor Yellow
Write-Host "   IntuneWinAppUtil.exe ``" -ForegroundColor Cyan
Write-Host "       -c `"$OutputPath`" ``" -ForegroundColor Cyan
Write-Host "       -s `"Install-Client-Intune.ps1`" ``" -ForegroundColor Cyan
Write-Host "       -o `"C:\Temp\IntunePackages`" ``" -ForegroundColor Cyan
Write-Host "       -q" -ForegroundColor Cyan
Write-Host ""
Write-Host "2. Upload the .intunewin file to Microsoft Endpoint Manager" -ForegroundColor Yellow
Write-Host ""
Write-Host "3. Configure install command in Intune (example):" -ForegroundColor Yellow
Write-Host "   powershell.exe -ExecutionPolicy Bypass -NoProfile -File `"Install-Client-Intune.ps1`" ``" -ForegroundColor Cyan
Write-Host "       -ApiBaseUrl `"https://your-api.contoso.com`" ``" -ForegroundColor Cyan
Write-Host "       -FleetId `"production-fleet`" ``" -ForegroundColor Cyan
Write-Host "       -CertificatePassword `"YourPassword`" ``" -ForegroundColor Cyan
Write-Host "       -ScheduleType Custom ``" -ForegroundColor Cyan
Write-Host "       -RepeatEveryHours 4" -ForegroundColor Cyan
Write-Host ""
Write-Host "Documentation:" -ForegroundColor White
Write-Host "  See docs\INTUNE_WIN32_DEPLOYMENT.md for complete deployment guide" -ForegroundColor Gray
Write-Host ""
