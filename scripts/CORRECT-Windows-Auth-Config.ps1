# ===============================================================================
# CORRECT-Windows-Auth-Config.ps1
#
# CORRECT configuration based on working server
# ENABLE Windows Auth + DISABLE Anonymous Auth
# ===============================================================================

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "===============================================================================" -ForegroundColor Green
Write-Host "CORRECT Windows Authentication Configuration" -ForegroundColor Green
Write-Host "Based on WORKING server configuration" -ForegroundColor Green
Write-Host "===============================================================================" -ForegroundColor Green
Write-Host ""

Write-Host "Working server has:" -ForegroundColor Cyan
Write-Host "  Windows Authentication: ENABLED" -ForegroundColor White
Write-Host "  Anonymous Authentication: DISABLED" -ForegroundColor White
Write-Host ""
Write-Host "Applying same configuration..." -ForegroundColor Yellow
Write-Host ""

$appcmd = "$env:SystemRoot\System32\inetsrv\appcmd.exe"
$siteName = "SecureBootDashboard.Web"

# Step 1: ENABLE Windows Authentication
Write-Host "Step 1: Enabling Windows Authentication..." -ForegroundColor Yellow

& $appcmd set config "$siteName" `
    -section:system.webServer/security/authentication/windowsAuthentication `
    /enabled:true `
    /commit:apphost | Out-Null

Write-Host "? Windows Authentication ENABLED" -ForegroundColor Green

# Step 2: DISABLE Anonymous Authentication
Write-Host "`nStep 2: Disabling Anonymous Authentication..." -ForegroundColor Yellow

& $appcmd set config "$siteName" `
    -section:system.webServer/security/authentication/anonymousAuthentication `
    /enabled:false `
    /commit:apphost | Out-Null

Write-Host "? Anonymous Authentication DISABLED" -ForegroundColor Green

# Step 3: Verify
Write-Host "`nStep 3: Verifying configuration..." -ForegroundColor Yellow

Write-Host "`nWindows Authentication:" -ForegroundColor Cyan
& $appcmd list config "$siteName" -section:windowsAuthentication | Select-String "enabled"

Write-Host "`nAnonymous Authentication:" -ForegroundColor Cyan
& $appcmd list config "$siteName" -section:anonymousAuthentication | Select-String "enabled"

# Step 4: PowerShell verification
Write-Host "`nStep 4: PowerShell verification..." -ForegroundColor Yellow

Import-Module WebAdministration
$sitePath = "IIS:\Sites\$siteName"

$winAuth = Get-WebConfigurationProperty `
    -Filter /system.webServer/security/authentication/windowsAuthentication `
    -Name enabled `
    -PSPath $sitePath

$anonAuth = Get-WebConfigurationProperty `
    -Filter /system.webServer/security/authentication/anonymousAuthentication `
    -Name enabled `
    -PSPath $sitePath

Write-Host "  Windows Auth: " -NoNewline
Write-Host $winAuth.Value -ForegroundColor $(if ($winAuth.Value) { "Green" } else { "Red" })

Write-Host "  Anonymous Auth: " -NoNewline
Write-Host $anonAuth.Value -ForegroundColor $(if (-not $anonAuth.Value) { "Green" } else { "Red" })

# Step 5: Restart IIS
Write-Host "`nStep 5: Restarting IIS..." -ForegroundColor Yellow
iisreset /noforce
Start-Sleep -Seconds 10
Write-Host "? IIS restarted" -ForegroundColor Green

# Step 6: Restart App Pool
Write-Host "`nStep 6: Restarting App Pool..." -ForegroundColor Yellow
Restart-WebAppPool $siteName
Start-Sleep -Seconds 5
Write-Host "? App Pool restarted" -ForegroundColor Green

# Step 7: Test site
Write-Host "`nStep 7: Testing site..." -ForegroundColor Yellow
Start-Sleep -Seconds 5

try {
    $response = Invoke-WebRequest -Uri "https://secbootsrv.mslabs.local" `
        -UseBasicParsing `
        -UseDefaultCredentials `
        -SkipCertificateCheck `
        -TimeoutSec 15 `
        -ErrorAction Stop
    
    Write-Host "? Site responded: HTTP $($response.StatusCode)" -ForegroundColor Green
    
    if ($response.Content -match "An error occurred") {
        Write-Host "? Page shows error" -ForegroundColor Yellow
    } else {
        Write-Host "? SUCCESS - Page loaded!" -ForegroundColor Green
    }
} catch {
    Write-Host "Response: $_" -ForegroundColor Yellow
    
    if ($_.Exception.Response) {
        $statusCode = [int]$_.Exception.Response.StatusCode
        if ($statusCode -eq 401) {
            Write-Host "  Got 401 - This is EXPECTED, browser will handle auth" -ForegroundColor Cyan
        }
    }
}

# Step 8: Check logs
Write-Host "`nStep 8: Checking recent log..." -ForegroundColor Yellow

$latestLog = Get-ChildItem "C:\Logs\SecureBootDashboard" -File -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if ($latestLog) {
    Write-Host "Latest log: $($latestLog.Name)" -ForegroundColor Cyan
    
    $content = Get-Content $latestLog.FullName -Tail 30
    $negotiateError = $content | Where-Object { $_ -match "Negotiate Authentication handler cannot be used" }
    
    if ($negotiateError) {
        Write-Host ""
        Write-Host "? Negotiate error still present" -ForegroundColor Red
        $negotiateError | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    } else {
        Write-Host "? No Negotiate errors in log!" -ForegroundColor Green
    }
    
    Write-Host "`nLast 10 log lines:" -ForegroundColor Cyan
    Write-Host "---" -ForegroundColor DarkGray
    Get-Content $latestLog.FullName -Tail 10 | ForEach-Object { Write-Host $_ -ForegroundColor Gray }
    Write-Host "---" -ForegroundColor DarkGray
}

Write-Host ""
Write-Host "===============================================================================" -ForegroundColor Green
Write-Host "CONFIGURATION COMPLETE" -ForegroundColor Green
Write-Host "===============================================================================" -ForegroundColor Green
Write-Host ""

Write-Host "Final Configuration (matching working server):" -ForegroundColor Yellow
Write-Host "  Windows Authentication: ENABLED" -ForegroundColor White
Write-Host "  Anonymous Authentication: DISABLED" -ForegroundColor White
Write-Host ""
Write-Host "Test in browser:" -ForegroundColor Yellow
Write-Host "  Navigate to: https://secbootsrv.mslabs.local" -ForegroundColor Cyan
Write-Host "  Expected: Windows login prompt, then dashboard loads" -ForegroundColor Cyan
Write-Host ""

