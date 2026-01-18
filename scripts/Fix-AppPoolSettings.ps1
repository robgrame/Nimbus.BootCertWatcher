# ===============================================================================
# Fix-AppPoolSettings.ps1
#
# Fix App Pool configuration to match working server
# ===============================================================================

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$AppPoolName = "SecureBootDashboard.Web"
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host "Fix App Pool Configuration" -ForegroundColor Cyan
Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host ""

Import-Module WebAdministration -ErrorAction Stop

$appPoolPath = "IIS:\AppPools\$AppPoolName"

if (-not (Test-Path $appPoolPath)) {
    Write-Host "? App Pool '$AppPoolName' not found!" -ForegroundColor Red
    exit 1
}

Write-Host "Checking current configuration..." -ForegroundColor Yellow

# Get current settings
$appPool = Get-Item $appPoolPath
$currentRuntime = $appPool.managedRuntimeVersion
$currentPipeline = $appPool.managedPipelineMode
$currentIdentity = $appPool.processModel.identityType
$current32Bit = $appPool.enable32BitAppOnWin64

Write-Host "`nCurrent Settings:" -ForegroundColor Cyan
Write-Host "  .NET CLR Version: $currentRuntime" -ForegroundColor Gray
Write-Host "  Pipeline Mode: $currentPipeline" -ForegroundColor Gray
Write-Host "  Identity: $currentIdentity" -ForegroundColor Gray
Write-Host "  Enable 32-bit: $current32Bit" -ForegroundColor Gray

# Recommended settings for ASP.NET Core
Write-Host "`nApplying recommended settings for ASP.NET Core..." -ForegroundColor Yellow

# Stop App Pool
Stop-WebAppPool $AppPoolName -ErrorAction SilentlyContinue
Start-Sleep -Seconds 3

# Set .NET CLR Version to v4.0 (same as working server)
Write-Host "  Setting .NET CLR Version to v4.0..." -ForegroundColor Gray
Set-ItemProperty $appPoolPath -Name "managedRuntimeVersion" -Value "v4.0"
Write-Host "  ? .NET CLR Version: v4.0" -ForegroundColor Green

# Set Pipeline Mode to Integrated
Write-Host "  Setting Pipeline Mode to Integrated..." -ForegroundColor Gray
Set-ItemProperty $appPoolPath -Name "managedPipelineMode" -Value "Integrated"
Write-Host "  ? Pipeline Mode: Integrated" -ForegroundColor Green

# Set Identity to ApplicationPoolIdentity (recommended)
Write-Host "  Setting Identity to ApplicationPoolIdentity..." -ForegroundColor Gray
Set-ItemProperty $appPoolPath -Name "processModel.identityType" -Value "ApplicationPoolIdentity"
Write-Host "  ? Identity: ApplicationPoolIdentity" -ForegroundColor Green

# Disable 32-bit apps (assuming x64 server)
Write-Host "  Disabling 32-bit applications..." -ForegroundColor Gray
Set-ItemProperty $appPoolPath -Name "enable32BitAppOnWin64" -Value $false
Write-Host "  ? 32-bit apps: Disabled" -ForegroundColor Green

# Additional recommended settings
Write-Host "  Configuring additional settings..." -ForegroundColor Gray

# Start Mode: AlwaysRunning (for better performance)
Set-ItemProperty $appPoolPath -Name "startMode" -Value "AlwaysRunning"

# Idle Timeout: 0 (never timeout)
Set-ItemProperty $appPoolPath -Name "processModel.idleTimeout" -Value ([TimeSpan]::FromMinutes(0))

# Periodic Restart: 1740 minutes (29 hours - recommended)
Set-ItemProperty $appPoolPath -Name "recycling.periodicRestart.time" -Value ([TimeSpan]::FromMinutes(1740))

Write-Host "  ? Additional settings configured" -ForegroundColor Green

# Verify new settings
Write-Host "`nVerifying new settings..." -ForegroundColor Yellow
$appPool = Get-Item $appPoolPath

Write-Host "`nNew Settings:" -ForegroundColor Cyan
Write-Host "  .NET CLR Version: " -NoNewline
Write-Host $appPool.managedRuntimeVersion -ForegroundColor Green

Write-Host "  Pipeline Mode: " -NoNewline
Write-Host $appPool.managedPipelineMode -ForegroundColor Green

Write-Host "  Identity: " -NoNewline
Write-Host $appPool.processModel.identityType -ForegroundColor Green

Write-Host "  Enable 32-bit: " -NoNewline
Write-Host $appPool.enable32BitAppOnWin64 -ForegroundColor Green

Write-Host "  Start Mode: " -NoNewline
Write-Host $appPool.startMode -ForegroundColor Green

Write-Host "  Idle Timeout: " -NoNewline
Write-Host $appPool.processModel.idleTimeout -ForegroundColor Green

# Start App Pool
Write-Host "`nStarting App Pool..." -ForegroundColor Yellow
Start-WebAppPool $AppPoolName
Start-Sleep -Seconds 5

$state = (Get-WebAppPoolState $AppPoolName).Value
Write-Host "? App Pool State: $state" -ForegroundColor Green

# Test site
Write-Host "`nTesting site..." -ForegroundColor Yellow
Start-Sleep -Seconds 5

try {
    $response = Invoke-WebRequest -Uri "https://secbootsrv.mslabs.local" `
        -UseBasicParsing `
        -SkipCertificateCheck `
        -TimeoutSec 15 `
        -ErrorAction Stop
    
    Write-Host "? Site responded: HTTP $($response.StatusCode)" -ForegroundColor Green
    
    if ($response.Content -match "An error occurred") {
        Write-Host "? Page shows error message" -ForegroundColor Yellow
    } else {
        Write-Host "? SUCCESS - Page loaded!" -ForegroundColor Green
    }
    
} catch {
    Write-Host "? Site test failed: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "Check logs for details:" -ForegroundColor Yellow
    Write-Host "  C:\Logs\SecureBootDashboard\*.log" -ForegroundColor Cyan
}

Write-Host ""
Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host "Configuration Complete" -ForegroundColor Cyan
Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "Summary:" -ForegroundColor Yellow
Write-Host "  • .NET CLR Version: v4.0 (same as working server)" -ForegroundColor White
Write-Host "  • Pipeline Mode: Integrated" -ForegroundColor White
Write-Host "  • Identity: ApplicationPoolIdentity" -ForegroundColor White
Write-Host "  • Start Mode: AlwaysRunning" -ForegroundColor White
Write-Host "  • Idle Timeout: Never" -ForegroundColor White
Write-Host ""

