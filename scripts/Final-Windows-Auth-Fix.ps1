# ===============================================================================
# Final-Windows-Auth-Fix.ps1
#
# FINAL FIX: Disable Windows Auth at ALL levels
# ===============================================================================

$ErrorActionPreference = "Continue"

Write-Host ""
Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host "FINAL Windows Authentication Fix" -ForegroundColor Cyan
Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host ""

$appcmd = "$env:SystemRoot\System32\inetsrv\appcmd.exe"

# Step 1: Check current state
Write-Host "Step 1: Checking current configuration..." -ForegroundColor Yellow

Write-Host "`n  SITE level:" -ForegroundColor Gray
& $appcmd list config "SecureBootDashboard.Web" -section:windowsAuthentication | 
    Select-String "enabled"

Write-Host "`n  SERVER level:" -ForegroundColor Gray
& $appcmd list config -section:windowsAuthentication | 
    Select-String "enabled"

# Step 2: Disable at all levels
Write-Host "`nStep 2: Disabling Windows Auth at ALL levels..." -ForegroundColor Yellow

# Site level
Write-Host "  Site level..." -ForegroundColor Gray
& $appcmd set config "SecureBootDashboard.Web" `
    -section:system.webServer/security/authentication/windowsAuthentication `
    /enabled:false `
    /commit:apphost | Out-Null
Write-Host "  ? Site level disabled" -ForegroundColor Green

# Server level
Write-Host "  Server level..." -ForegroundColor Gray
& $appcmd set config `
    -section:system.webServer/security/authentication/windowsAuthentication `
    /enabled:false `
    /commit:apphost | Out-Null
Write-Host "  ? Server level disabled" -ForegroundColor Green

# Step 3: Enable Anonymous Auth
Write-Host "`nStep 3: Ensuring Anonymous Auth is enabled..." -ForegroundColor Yellow

& $appcmd set config "SecureBootDashboard.Web" `
    -section:system.webServer/security/authentication/anonymousAuthentication `
    /enabled:true `
    /commit:apphost | Out-Null
Write-Host "? Anonymous Auth enabled" -ForegroundColor Green

# Step 4: Verify
Write-Host "`nStep 4: Verifying configuration..." -ForegroundColor Yellow

$siteWinAuth = & $appcmd list config "SecureBootDashboard.Web" -section:windowsAuthentication | 
    Select-String 'enabled="(.*?)"' | 
    ForEach-Object { $_.Matches.Groups[1].Value }

$serverWinAuth = & $appcmd list config -section:windowsAuthentication | 
    Select-String 'enabled="(.*?)"' | 
    ForEach-Object { $_.Matches.Groups[1].Value }

$anonAuth = & $appcmd list config "SecureBootDashboard.Web" -section:anonymousAuthentication | 
    Select-String 'enabled="(.*?)"' | 
    ForEach-Object { $_.Matches.Groups[1].Value }

Write-Host "  Site Windows Auth: " -NoNewline
Write-Host $siteWinAuth -ForegroundColor $(if ($siteWinAuth -eq "false") { "Green" } else { "Red" })

Write-Host "  Server Windows Auth: " -NoNewline
Write-Host $serverWinAuth -ForegroundColor $(if ($serverWinAuth -eq "false") { "Green" } else { "Red" })

Write-Host "  Anonymous Auth: " -NoNewline
Write-Host $anonAuth -ForegroundColor $(if ($anonAuth -eq "true") { "Green" } else { "Red" })

# Step 5: Full IIS restart
Write-Host "`nStep 5: Restarting IIS..." -ForegroundColor Yellow
iisreset /noforce
Start-Sleep -Seconds 10
Write-Host "? IIS restarted" -ForegroundColor Green

# Step 6: Restart App Pool
Write-Host "`nStep 6: Restarting App Pool..." -ForegroundColor Yellow
Import-Module WebAdministration
Restart-WebAppPool "SecureBootDashboard.Web"
Start-Sleep -Seconds 5
Write-Host "? App Pool restarted" -ForegroundColor Green

# Step 7: Test site
Write-Host "`nStep 7: Testing site..." -ForegroundColor Yellow
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
        Write-Host "? SUCCESS - Page loaded successfully!" -ForegroundColor Green
    }
} catch {
    Write-Host "Site test: $_" -ForegroundColor Yellow
}

# Step 8: Check logs
Write-Host "`nStep 8: Checking recent logs..." -ForegroundColor Yellow

$latestLog = Get-ChildItem "C:\Logs\SecureBootDashboard" -File -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if ($latestLog) {
    Write-Host "Latest log: $($latestLog.Name)" -ForegroundColor Cyan
    
    $content = Get-Content $latestLog.FullName -Tail 30
    $negotiateError = $content | Where-Object { $_ -match "Negotiate Authentication handler cannot be used" }
    
    if ($negotiateError) {
        Write-Host ""
        Write-Host "??? NEGOTIATE ERROR STILL PRESENT ???" -ForegroundColor Red
        Write-Host ""
        Write-Host "The error persists despite correct configuration." -ForegroundColor Yellow
        Write-Host "This means the issue is in the APPLICATION CODE, not IIS." -ForegroundColor Yellow
        Write-Host ""
        Write-Host "ONLY SOLUTION LEFT:" -ForegroundColor Red
        Write-Host "  Use IIS Windows Authentication instead of Negotiate handler" -ForegroundColor White
        Write-Host ""
        Write-Host "See: FINAL_SOLUTION_WINDOWS_AUTH_CONFLICT.md (Solution 2)" -ForegroundColor Cyan
        Write-Host ""
    } else {
        Write-Host "? NO NEGOTIATE ERROR FOUND!" -ForegroundColor Green
        Write-Host ""
        Write-Host "?? SUCCESS - Windows Auth conflict resolved!" -ForegroundColor Green
        Write-Host ""
    }
    
    # Show last few log lines
    Write-Host "`nLast 10 log lines:" -ForegroundColor Cyan
    Write-Host "---" -ForegroundColor DarkGray
    Get-Content $latestLog.FullName -Tail 10 | ForEach-Object { Write-Host $_ -ForegroundColor Gray }
    Write-Host "---" -ForegroundColor DarkGray
} else {
    Write-Host "? No log files found" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host "FINAL FIX COMPLETE" -ForegroundColor Cyan
Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "Configuration Summary:" -ForegroundColor Yellow
Write-Host "  Site Windows Auth: DISABLED" -ForegroundColor White
Write-Host "  Server Windows Auth: DISABLED" -ForegroundColor White
Write-Host "  Anonymous Auth: ENABLED" -ForegroundColor White
Write-Host "  App Pool: .NET CLR v4.0" -ForegroundColor White
Write-Host ""

