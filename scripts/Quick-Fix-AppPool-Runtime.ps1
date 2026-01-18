# ===============================================================================
# Quick-Fix-AppPool-Runtime.ps1
#
# Quick fix to change App Pool .NET CLR Version from unmanaged to v4.0
# This fixes Windows Authentication conflict issue
# ===============================================================================

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$AppPoolName = "SecureBootDashboard.Web"
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host "Quick Fix - App Pool .NET CLR Version" -ForegroundColor Cyan
Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host ""

Import-Module WebAdministration -ErrorAction Stop

$appPoolPath = "IIS:\AppPools\$AppPoolName"

if (-not (Test-Path $appPoolPath)) {
    Write-Host "? App Pool '$AppPoolName' not found!" -ForegroundColor Red
    exit 1
}

# Get current settings
$appPool = Get-Item $appPoolPath
$currentRuntime = $appPool.managedRuntimeVersion

Write-Host "Current Configuration:" -ForegroundColor Yellow
Write-Host "  App Pool: $AppPoolName" -ForegroundColor Gray
Write-Host "  .NET CLR Version: '$currentRuntime'" -ForegroundColor Gray

if ($currentRuntime -eq "v4.0") {
    Write-Host "`n? Already set to v4.0 - no change needed!" -ForegroundColor Green
    exit 0
}

Write-Host "`nChanging to .NET CLR v4.0..." -ForegroundColor Yellow

# Stop App Pool
Stop-WebAppPool $AppPoolName -ErrorAction SilentlyContinue
Start-Sleep -Seconds 3

# Change runtime version
Set-ItemProperty $appPoolPath -Name "managedRuntimeVersion" -Value "v4.0"

Write-Host "? Changed to v4.0" -ForegroundColor Green

# Start App Pool
Start-WebAppPool $AppPoolName
Start-Sleep -Seconds 3

# Verify
$appPool = Get-Item $appPoolPath
$newRuntime = $appPool.managedRuntimeVersion

Write-Host "`nNew Configuration:" -ForegroundColor Cyan
Write-Host "  .NET CLR Version: $newRuntime" -ForegroundColor Green

if ($newRuntime -eq "v4.0") {
    Write-Host "`n? SUCCESS - App Pool now uses .NET CLR v4.0" -ForegroundColor Green
    Write-Host "  This resolves Windows Authentication conflict" -ForegroundColor Gray
} else {
    Write-Host "`n? WARNING - Runtime version is '$newRuntime'" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Restart website: Restart-Website '$AppPoolName'" -ForegroundColor Cyan
Write-Host "  2. Test: Invoke-WebRequest -Uri https://secbootsrv.mslabs.local -UseDefaultCredentials" -ForegroundColor Cyan
Write-Host ""

