# ===============================================================================
# Emergency-Diagnose-WebDashboard.ps1
#
# Emergency diagnostic for "An error occurred while starting the application"
# with no logs
# ===============================================================================

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$WebPath = "C:\inetpub\SecureBootDashboard.Web",
    
    [Parameter(Mandatory = $false)]
    [string]$AppPoolName = "SecureBootDashboard.Web",
    
    [Parameter(Mandatory = $false)]
    [string]$SiteName = "SecureBootDashboard.Web"
)

$ErrorActionPreference = "Continue"

Write-Host ""
Write-Host "===============================================================================" -ForegroundColor Red
Write-Host "EMERGENCY DIAGNOSTIC - No Logs Found" -ForegroundColor Red
Write-Host "===============================================================================" -ForegroundColor Red
Write-Host ""

# Step 1: Check Event Viewer FIRST (most important when no logs)
Write-Host "Step 1: Checking Windows Event Viewer..." -ForegroundColor Yellow
Write-Host "This is the ONLY place where errors are logged if app logs fail" -ForegroundColor Gray
Write-Host ""

try {
    $aspNetCoreErrors = Get-EventLog -LogName Application -Source "*AspNetCore*" -Newest 10 -ErrorAction SilentlyContinue |
        Where-Object { $_.EntryType -eq "Error" -or $_.EntryType -eq "Warning" }
    
    if ($aspNetCoreErrors) {
        Write-Host "Found $($aspNetCoreErrors.Count) ASP.NET Core event(s):" -ForegroundColor Yellow
        Write-Host ""
        
        foreach ($error in $aspNetCoreErrors) {
            Write-Host "[$($error.TimeGenerated)] $($error.EntryType)" -ForegroundColor $(if ($error.EntryType -eq "Error") { "Red" } else { "Yellow" })
            Write-Host $error.Message -ForegroundColor Gray
            Write-Host ("-" * 79) -ForegroundColor DarkGray
        }
    } else {
        Write-Host "No ASP.NET Core events found in Event Viewer" -ForegroundColor Green
    }
} catch {
    Write-Host "Could not read Event Viewer: $_" -ForegroundColor Red
}

# Step 2: Check all possible log locations
Write-Host "`nStep 2: Checking ALL possible log locations..." -ForegroundColor Yellow

$logPaths = @(
    "C:\Logs\SecureBootDashboard",
    "R:\Nimbus.SecureBootCert\logs",
    "$WebPath\logs",
    "$WebPath\App_Data\logs"
)

$foundLogs = $false

foreach ($logPath in $logPaths) {
    Write-Host "  Checking: $logPath" -ForegroundColor Gray
    
    if (Test-Path $logPath) {
        $logs = Get-ChildItem $logPath -File -ErrorAction SilentlyContinue
        if ($logs) {
            Write-Host "    ? Found $($logs.Count) file(s)" -ForegroundColor Green
            $foundLogs = $true
            
            $latest = $logs | Sort-Object LastWriteTime -Descending | Select-Object -First 1
            Write-Host "    Latest: $($latest.Name) ($(Get-Date $latest.LastWriteTime -Format 'yyyy-MM-dd HH:mm:ss'))" -ForegroundColor Cyan
        } else {
            Write-Host "    Directory exists but empty" -ForegroundColor Yellow
        }
    } else {
        Write-Host "    Directory does not exist" -ForegroundColor Red
    }
}

if (-not $foundLogs) {
    Write-Host "`n? NO LOGS FOUND IN ANY LOCATION!" -ForegroundColor Red
    Write-Host "This indicates a critical permissions or configuration issue" -ForegroundColor Red
}

# Step 3: Check permissions
Write-Host "`nStep 3: Checking Permissions..." -ForegroundColor Yellow

$identity = "IIS AppPool\$AppPoolName"

foreach ($logPath in $logPaths) {
    if (Test-Path $logPath) {
        Write-Host "  Checking: $logPath" -ForegroundColor Gray
        
        try {
            $acl = Get-Acl $logPath
            $hasPermission = $acl.Access | Where-Object { 
                $_.IdentityReference -eq $identity -and 
                ($_.FileSystemRights -match "FullControl|Write|Modify")
            }
            
            if ($hasPermission) {
                Write-Host "    ? App Pool has write access" -ForegroundColor Green
            } else {
                Write-Host "    ? App Pool does NOT have write access!" -ForegroundColor Red
                Write-Host "      Granting permissions..." -ForegroundColor Yellow
                
                $rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
                    $identity, "FullControl", "ContainerInherit,ObjectInherit", "None", "Allow"
                )
                $acl.AddAccessRule($rule)
                Set-Acl $logPath $acl
                
                Write-Host "      ? Permissions granted" -ForegroundColor Green
            }
        } catch {
            Write-Host "    ? Could not check/set permissions: $_" -ForegroundColor Red
        }
    }
}

# Step 4: Check web.config
Write-Host "`nStep 4: Checking web.config..." -ForegroundColor Yellow

$webConfigPath = "$WebPath\web.config"
if (Test-Path $webConfigPath) {
    try {
        [xml]$webConfig = Get-Content $webConfigPath
        
        $aspNetCore = $webConfig.SelectSingleNode("//aspNetCore")
        if ($aspNetCore) {
            $stdoutEnabled = $aspNetCore.GetAttribute("stdoutLogEnabled")
            $stdoutFile = $aspNetCore.GetAttribute("stdoutLogFile")
            $hostingModel = $aspNetCore.GetAttribute("hostingModel")
            
            Write-Host "  stdoutLogEnabled: " -NoNewline
            Write-Host $stdoutEnabled -ForegroundColor $(if ($stdoutEnabled -eq "true") { "Green" } else { "Red" })
            
            Write-Host "  stdoutLogFile: " -NoNewline
            Write-Host $stdoutFile -ForegroundColor $(if ($stdoutFile) { "Cyan" } else { "Red" })
            
            Write-Host "  hostingModel: " -NoNewline
            Write-Host $hostingModel -ForegroundColor $(if ($hostingModel -eq "outofprocess") { "Green" } else { "Yellow" })
            
            # Check if stdout directory exists
            if ($stdoutFile) {
                $stdoutDir = Split-Path $stdoutFile -Parent
                if (Test-Path $stdoutDir) {
                    Write-Host "  ? Stdout directory exists: $stdoutDir" -ForegroundColor Green
                } else {
                    Write-Host "  ? Stdout directory DOES NOT exist: $stdoutDir" -ForegroundColor Red
                    Write-Host "    Creating..." -ForegroundColor Yellow
                    New-Item -Path $stdoutDir -ItemType Directory -Force | Out-Null
                    Write-Host "    ? Created" -ForegroundColor Green
                }
            }
        } else {
            Write-Host "  ? No aspNetCore element found!" -ForegroundColor Red
        }
    } catch {
        Write-Host "  ? Could not parse web.config: $_" -ForegroundColor Red
    }
} else {
    Write-Host "  ? web.config not found!" -ForegroundColor Red
}

# Step 5: Check appsettings.Production.json
Write-Host "`nStep 5: Checking appsettings.Production.json..." -ForegroundColor Yellow

$settingsPath = "$WebPath\appsettings.Production.json"
if (Test-Path $settingsPath) {
    try {
        $settings = Get-Content $settingsPath -Raw | ConvertFrom-Json
        Write-Host "  ? File exists and is valid JSON" -ForegroundColor Green
        
        if ($settings.Serilog) {
            $logPath = $settings.Serilog.WriteTo[0].Args.path
            Write-Host "  Serilog log path: $logPath" -ForegroundColor Cyan
            
            $logDir = Split-Path $logPath -Parent
            if (Test-Path $logDir) {
                Write-Host "  ? Serilog directory exists" -ForegroundColor Green
            } else {
                Write-Host "  ? Serilog directory DOES NOT exist: $logDir" -ForegroundColor Red
            }
        }
        
        if ($settings.ApiSettings) {
            Write-Host "  API Base URL: $($settings.ApiSettings.BaseUrl)" -ForegroundColor Cyan
        }
    } catch {
        Write-Host "  ? Error reading file: $_" -ForegroundColor Red
    }
} else {
    Write-Host "  ? appsettings.Production.json not found!" -ForegroundColor Red
}

# Step 6: Check App Pool status
Write-Host "`nStep 6: Checking IIS Status..." -ForegroundColor Yellow

try {
    Import-Module WebAdministration -ErrorAction Stop
    
    $appPoolState = (Get-WebAppPoolState $AppPoolName).Value
    $siteState = (Get-WebsiteState $SiteName).Value
    
    Write-Host "  App Pool: " -NoNewline
    Write-Host $appPoolState -ForegroundColor $(if ($appPoolState -eq "Started") { "Green" } else { "Red" })
    
    Write-Host "  Website: " -NoNewline
    Write-Host $siteState -ForegroundColor $(if ($siteState -eq "Started") { "Green" } else { "Red" })
    
    if ($appPoolState -ne "Started") {
        Write-Host "  Starting App Pool..." -ForegroundColor Yellow
        Start-WebAppPool $AppPoolName
    }
    
    if ($siteState -ne "Started") {
        Write-Host "  Starting Website..." -ForegroundColor Yellow
        Start-Website $SiteName
    }
} catch {
    Write-Host "  ? Could not check IIS status: $_" -ForegroundColor Red
}

# Step 7: Test with detailed error
Write-Host "`nStep 7: Testing site with detailed error..." -ForegroundColor Yellow
Start-Sleep -Seconds 5

try {
    $response = Invoke-WebRequest -Uri "https://secbootsrv.mslabs.local" `
        -UseBasicParsing `
        -SkipCertificateCheck `
        -TimeoutSec 10 `
        -ErrorAction Stop
    
    Write-Host "  ? Site responded: HTTP $($response.StatusCode)" -ForegroundColor Green
    
    if ($response.Content -match "An error occurred") {
        Write-Host "  ? Page contains error message" -ForegroundColor Yellow
    }
} catch {
    Write-Host "  ? Site error: $_" -ForegroundColor Red
    
    if ($_.Exception.Response) {
        $statusCode = [int]$_.Exception.Response.StatusCode
        Write-Host "  Status Code: $statusCode" -ForegroundColor Red
    }
}

# Step 8: Recommendations
Write-Host ""
Write-Host "="*79 -ForegroundColor Cyan
Write-Host "RECOMMENDATIONS" -ForegroundColor Cyan
Write-Host "="*79 -ForegroundColor Cyan

$issues = @()

if (-not $foundLogs) {
    $issues += "No logs found - likely permissions issue"
}

if ($aspNetCoreErrors) {
    $issues += "Errors in Event Viewer (see above for details)"
}

if (-not (Test-Path $settingsPath)) {
    $issues += "appsettings.Production.json missing"
}

if ($issues.Count -gt 0) {
    Write-Host ""
    Write-Host "Critical Issues Found:" -ForegroundColor Red
    foreach ($issue in $issues) {
        Write-Host "  • $issue" -ForegroundColor Yellow
    }
    Write-Host ""
    Write-Host "NEXT STEPS:" -ForegroundColor Cyan
    Write-Host "  1. Check Event Viewer errors above" -ForegroundColor White
    Write-Host "  2. Fix permissions (already attempted)" -ForegroundColor White
    Write-Host "  3. Ensure appsettings.Production.json exists and is valid" -ForegroundColor White
    Write-Host "  4. Run: Restart-WebAppPool '$AppPoolName'" -ForegroundColor White
    Write-Host "  5. Check Event Viewer again after restart" -ForegroundColor White
} else {
    Write-Host ""
    Write-Host "? No obvious issues found" -ForegroundColor Green
    Write-Host "Check Event Viewer output above for any errors" -ForegroundColor Gray
}

Write-Host ""

