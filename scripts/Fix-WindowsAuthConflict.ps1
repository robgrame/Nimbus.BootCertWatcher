# ===============================================================================
# Fix-WindowsAuthConflict.ps1
#
# Fix "Negotiate Authentication handler cannot be used" error
# Disables Windows Authentication in IIS so the app can handle it
# ===============================================================================

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$SiteName = "SecureBootDashboard.Web",
    
    [Parameter(Mandatory = $false)]
    [string]$AppPoolName = "SecureBootDashboard.Web"
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host "Fix Windows Authentication Conflict" -ForegroundColor Cyan
Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "Issue: Negotiate Authentication handler conflict with IIS Windows Auth" -ForegroundColor Yellow
Write-Host ""

# Import WebAdministration
try {
    Import-Module WebAdministration -ErrorAction Stop
    Write-Host "? WebAdministration module loaded" -ForegroundColor Green
} catch {
    Write-Host "? Failed to load WebAdministration module: $_" -ForegroundColor Red
    exit 1
}

# Check current authentication settings
Write-Host "`nCurrent Authentication Settings:" -ForegroundColor Cyan

$sitePath = "IIS:\Sites\$SiteName"
if (-not (Test-Path $sitePath)) {
    Write-Host "? Site '$SiteName' not found!" -ForegroundColor Red
    exit 1
}

try {
    $windowsAuth = Get-WebConfigurationProperty `
        -Filter /system.webServer/security/authentication/windowsAuthentication `
        -Name enabled `
        -PSPath $sitePath
    
    $anonymousAuth = Get-WebConfigurationProperty `
        -Filter /system.webServer/security/authentication/anonymousAuthentication `
        -Name enabled `
        -PSPath $sitePath
    
    Write-Host "  Windows Authentication: $($windowsAuth.Value)" -ForegroundColor Gray
    Write-Host "  Anonymous Authentication: $($anonymousAuth.Value)" -ForegroundColor Gray
} catch {
    Write-Host "  ? Could not read current settings: $_" -ForegroundColor Yellow
}

# Apply fix
Write-Host "`nApplying fix..." -ForegroundColor Yellow
Write-Host "  1. Disabling Windows Authentication in IIS" -ForegroundColor Gray
Write-Host "  2. Enabling Anonymous Authentication" -ForegroundColor Gray
Write-Host "  3. Letting ASP.NET Core handle authentication" -ForegroundColor Gray
Write-Host ""

try {
    # Disable Windows Authentication
    Set-WebConfigurationProperty `
        -Filter /system.webServer/security/authentication/windowsAuthentication `
        -Name enabled `
        -Value false `
        -PSPath $sitePath
    
    Write-Host "? Windows Authentication disabled in IIS" -ForegroundColor Green
    
    # Enable Anonymous Authentication
    Set-WebConfigurationProperty `
        -Filter /system.webServer/security/authentication/anonymousAuthentication `
        -Name enabled `
        -Value true `
        -PSPath $sitePath
    
    Write-Host "? Anonymous Authentication enabled in IIS" -ForegroundColor Green
    
    # Also ensure at application level
    try {
        Set-WebConfigurationProperty `
            -Filter /system.webServer/security/authentication/anonymousAuthentication `
            -Name enabled `
            -Value true `
            -Location "$SiteName"
        Write-Host "? Anonymous Authentication enabled at application level" -ForegroundColor Green
    } catch {
        Write-Host "  ? Application level setting not needed or already set" -ForegroundColor Gray
    }
    
} catch {
    Write-Host "? Failed to update authentication settings: $_" -ForegroundColor Red
    exit 1
}

# Verify changes
Write-Host "`nVerifying changes..." -ForegroundColor Yellow

try {
    $windowsAuth = Get-WebConfigurationProperty `
        -Filter /system.webServer/security/authentication/windowsAuthentication `
        -Name enabled `
        -PSPath $sitePath
    
    $anonymousAuth = Get-WebConfigurationProperty `
        -Filter /system.webServer/security/authentication/anonymousAuthentication `
        -Name enabled `
        -PSPath $sitePath
    
    if ($windowsAuth.Value -eq $false -and $anonymousAuth.Value -eq $true) {
        Write-Host "? Settings updated successfully" -ForegroundColor Green
        Write-Host "  Windows Authentication: $($windowsAuth.Value)" -ForegroundColor Gray
        Write-Host "  Anonymous Authentication: $($anonymousAuth.Value)" -ForegroundColor Gray
    } else {
        Write-Host "? Settings may not have updated correctly" -ForegroundColor Yellow
        Write-Host "  Windows Authentication: $($windowsAuth.Value)" -ForegroundColor Gray
        Write-Host "  Anonymous Authentication: $($anonymousAuth.Value)" -ForegroundColor Gray
    }
} catch {
    Write-Host "? Could not verify settings: $_" -ForegroundColor Yellow
}

# Restart App Pool
Write-Host "`nRestarting Application Pool..." -ForegroundColor Yellow

try {
    Stop-WebAppPool $AppPoolName -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    Start-WebAppPool $AppPoolName
    Start-Sleep -Seconds 3
    
    $state = (Get-WebAppPoolState $AppPoolName).Value
    Write-Host "? App Pool restarted: $state" -ForegroundColor Green
} catch {
    Write-Host "? Could not restart App Pool: $_" -ForegroundColor Yellow
}

# Test site
Write-Host "`nTesting site..." -ForegroundColor Yellow
Start-Sleep -Seconds 5

try {
    $response = Invoke-WebRequest -Uri "https://secbootsrv.mslabs.local" `
        -UseBasicParsing `
        -SkipCertificateCheck `
        -TimeoutSec 10 `
        -ErrorAction Stop
    
    Write-Host "? Site is responding: HTTP $($response.StatusCode)" -ForegroundColor Green
    Write-Host ""
    Write-Host "===============================================================================" -ForegroundColor Cyan
    Write-Host "SUCCESS - Authentication conflict resolved!" -ForegroundColor Green
    Write-Host "===============================================================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Your application is now handling Windows Authentication via Negotiate." -ForegroundColor White
    Write-Host ""
    
} catch {
    Write-Host "? Site test failed: $_" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Check logs for any remaining issues:" -ForegroundColor Yellow
    Write-Host "  C:\Logs\SecureBootDashboard\stdout-*.log" -ForegroundColor Cyan
    Write-Host ""
    
    # Show recent log
    $latestLog = Get-ChildItem "C:\Logs\SecureBootDashboard\*" -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    
    if ($latestLog) {
        Write-Host "Latest log (last 15 lines):" -ForegroundColor Cyan
        Write-Host "----------------------------------------" -ForegroundColor DarkGray
        Get-Content $latestLog.FullName -Tail 15 -ErrorAction SilentlyContinue
        Write-Host "----------------------------------------" -ForegroundColor DarkGray
    }
}

Write-Host ""
Write-Host "Authentication Settings Summary:" -ForegroundColor Cyan
Write-Host "  • IIS Windows Auth: Disabled" -ForegroundColor White
Write-Host "  • IIS Anonymous Auth: Enabled" -ForegroundColor White
Write-Host "  • ASP.NET Core Negotiate Auth: Handling Windows Authentication" -ForegroundColor White
Write-Host ""

