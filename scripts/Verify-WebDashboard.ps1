# ===============================================================================
# Verify-WebDashboard.ps1
#
# Quick verification that SecureBootDashboard.Web is working
# ===============================================================================

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$Url = "https://secbootsrv.mslabs.local"
)

$ErrorActionPreference = "Continue"

Write-Host ""
Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host "SecureBootDashboard.Web - Verification" -ForegroundColor Cyan
Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host ""

# 1. Check IIS Services
Write-Host "1. Checking IIS Services..." -ForegroundColor Yellow
try {
    Import-Module WebAdministration -ErrorAction Stop
    
    $appPoolState = (Get-WebAppPoolState "SecureBootDashboard.Web").Value
    $siteState = (Get-WebsiteState "SecureBootDashboard.Web").Value
    
    Write-Host "  App Pool: " -NoNewline
    Write-Host $appPoolState -ForegroundColor $(if ($appPoolState -eq "Started") { "Green" } else { "Red" })
    
    Write-Host "  Website: " -NoNewline
    Write-Host $siteState -ForegroundColor $(if ($siteState -eq "Started") { "Green" } else { "Red" })
    
    if ($appPoolState -ne "Started") {
        Write-Host "`n  Starting App Pool..." -ForegroundColor Yellow
        Start-WebAppPool "SecureBootDashboard.Web"
        Start-Sleep -Seconds 3
    }
    
    if ($siteState -ne "Started") {
        Write-Host "  Starting Website..." -ForegroundColor Yellow
        Start-Website "SecureBootDashboard.Web"
        Start-Sleep -Seconds 3
    }
} catch {
    Write-Host "  ? Could not check IIS: $_" -ForegroundColor Red
}

# 2. Check Authentication Settings
Write-Host "`n2. Checking Authentication..." -ForegroundColor Yellow
try {
    $sitePath = "IIS:\Sites\SecureBootDashboard.Web"
    
    $anonymousAuth = Get-WebConfigurationProperty `
        -Filter /system.webServer/security/authentication/anonymousAuthentication `
        -Name enabled `
        -PSPath $sitePath
    
    $windowsAuth = Get-WebConfigurationProperty `
        -Filter /system.webServer/security/authentication/windowsAuthentication `
        -Name enabled `
        -PSPath $sitePath
    
    Write-Host "  Anonymous Auth: " -NoNewline
    Write-Host $anonymousAuth.Value -ForegroundColor $(if ($anonymousAuth.Value) { "Green" } else { "Red" })
    
    Write-Host "  Windows Auth: " -NoNewline
    Write-Host $windowsAuth.Value -ForegroundColor $(if (-not $windowsAuth.Value) { "Green" } else { "Yellow" })
} catch {
    Write-Host "  ? Could not check authentication: $_" -ForegroundColor Gray
}

# 3. Check Logs
Write-Host "`n3. Checking Logs..." -ForegroundColor Yellow
$logsPath = "C:\Logs\SecureBootDashboard"

if (Test-Path $logsPath) {
    $recentLogs = Get-ChildItem $logsPath -File |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 3
    
    if ($recentLogs) {
        Write-Host "  ? Recent log files found:" -ForegroundColor Green
        foreach ($log in $recentLogs) {
            $age = (Get-Date) - $log.LastWriteTime
            $ageStr = if ($age.TotalMinutes -lt 60) { 
                "$([Math]::Round($age.TotalMinutes)) min ago" 
            } else { 
                "$([Math]::Round($age.TotalHours, 1)) hours ago" 
            }
            Write-Host "    $($log.Name) ($ageStr)" -ForegroundColor Gray
        }
        
        # Check latest log for errors
        $latestLog = $recentLogs[0]
        $content = Get-Content $latestLog.FullName -Tail 50 -ErrorAction SilentlyContinue
        
        $errors = $content | Where-Object { $_ -match "\[ERR\]|\[FTL\]|Exception|Error" }
        $warnings = $content | Where-Object { $_ -match "\[WRN\]" }
        $success = $content | Where-Object { $_ -match "started successfully|Starting" }
        
        if ($errors) {
            Write-Host "`n  ? Recent errors found in logs:" -ForegroundColor Yellow
            $errors | Select-Object -Last 3 | ForEach-Object {
                Write-Host "    $_" -ForegroundColor Red
            }
        } elseif ($success) {
            Write-Host "  ? Application started successfully" -ForegroundColor Green
        }
        
        if ($warnings) {
            Write-Host "  ? $($warnings.Count) warning(s) in logs" -ForegroundColor Gray
        }
    } else {
        Write-Host "  ? No log files found" -ForegroundColor Yellow
    }
} else {
    Write-Host "  ? Logs directory not found: $logsPath" -ForegroundColor Red
}

# 4. Test HTTP Endpoint
Write-Host "`n4. Testing HTTP Endpoint..." -ForegroundColor Yellow
Write-Host "  URL: $Url" -ForegroundColor Gray

try {
    $response = Invoke-WebRequest -Uri $Url `
        -UseBasicParsing `
        -SkipCertificateCheck `
        -TimeoutSec 10 `
        -ErrorAction Stop
    
    Write-Host "  ? HTTP Status: " -NoNewline -ForegroundColor Green
    Write-Host $response.StatusCode -ForegroundColor Green
    Write-Host "  Content Length: $($response.Content.Length) bytes" -ForegroundColor Gray
    
    # Check if it's HTML
    if ($response.Content -match "<html|<HTML") {
        Write-Host "  ? HTML content detected" -ForegroundColor Green
    }
    
} catch {
    $statusCode = $null
    if ($_.Exception.Response) {
        $statusCode = [int]$_.Exception.Response.StatusCode
    }
    
    if ($statusCode) {
        Write-Host "  HTTP Status: " -NoNewline
        
        switch ($statusCode) {
            200 { 
                Write-Host "$statusCode OK" -ForegroundColor Green 
            }
            302 { 
                Write-Host "$statusCode Redirect (Normal for auth)" -ForegroundColor Yellow 
            }
            401 { 
                Write-Host "$statusCode Unauthorized" -ForegroundColor Yellow
                Write-Host "  ? Application is running but needs authentication configuration" -ForegroundColor Gray
            }
            500 { 
                Write-Host "$statusCode Server Error" -ForegroundColor Red
                Write-Host "  Check logs for details" -ForegroundColor Gray
            }
            default { 
                Write-Host "$statusCode" -ForegroundColor Yellow 
            }
        }
    } else {
        Write-Host "  ? Connection failed: $_" -ForegroundColor Red
    }
}

# 5. Summary
Write-Host "`n" + "="*79 -ForegroundColor Cyan
Write-Host "Summary" -ForegroundColor Cyan
Write-Host "="*79 -ForegroundColor Cyan

$allGood = $true

# Check critical items
if ($appPoolState -ne "Started") {
    Write-Host "? App Pool not running" -ForegroundColor Red
    $allGood = $false
}
if ($siteState -ne "Started") {
    Write-Host "? Website not running" -ForegroundColor Red
    $allGood = $false
}
if (-not $anonymousAuth.Value) {
    Write-Host "? Anonymous Authentication disabled (may cause 401)" -ForegroundColor Yellow
}
if ($errors) {
    Write-Host "? Errors found in recent logs" -ForegroundColor Yellow
}

if ($allGood -and $response -and $response.StatusCode -eq 200) {
    Write-Host ""
    Write-Host "?? SUCCESS - Application is fully operational!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Access your dashboard at: $Url" -ForegroundColor Cyan
    Write-Host ""
} elseif ($allGood) {
    Write-Host ""
    Write-Host "? Application is running" -ForegroundColor Green
    Write-Host "? May need additional configuration (authentication, DNS, etc.)" -ForegroundColor Gray
    Write-Host ""
} else {
    Write-Host ""
    Write-Host "? Application has issues - check details above" -ForegroundColor Yellow
    Write-Host ""
}

Write-Host "For detailed logs, run:" -ForegroundColor Cyan
Write-Host "  Get-Content '$logsPath\*.log' -Tail 50" -ForegroundColor Gray
Write-Host ""

