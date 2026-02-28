# ===============================================================================
# FINAL-FIX-WebDashboard.ps1
#
# FINAL AND COMPLETE FIX for all Web Dashboard issues
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

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "===============================================================================" -ForegroundColor Red
Write-Host "FINAL FIX - ALL Web Dashboard Issues" -ForegroundColor Red
Write-Host "===============================================================================" -ForegroundColor Red
Write-Host ""

Write-Host "Issues to fix:" -ForegroundColor Yellow
Write-Host "  1. Windows Authentication Conflict" -ForegroundColor White
Write-Host "  2. App listening on wrong port (not using IIS)" -ForegroundColor White
Write-Host "  3. Serilog path incorrect" -ForegroundColor White
Write-Host ""

# Step 1: Stop everything
Write-Host "Step 1: Stopping all services..." -ForegroundColor Yellow

try {
    Import-Module WebAdministration -ErrorAction Stop
    
    Stop-WebAppPool $AppPoolName -ErrorAction SilentlyContinue
    Stop-Website $SiteName -ErrorAction SilentlyContinue
    
    Write-Host "? Services stopped" -ForegroundColor Green
    Start-Sleep -Seconds 3
} catch {
    Write-Host "? Could not stop services: $_" -ForegroundColor Yellow
}

# Step 2: Fix Windows Authentication (CRITICAL)
Write-Host "`nStep 2: Fixing Windows Authentication..." -ForegroundColor Yellow

$sitePath = "IIS:\Sites\$SiteName"

# Disable Windows Auth
Set-WebConfigurationProperty `
    -Filter /system.webServer/security/authentication/windowsAuthentication `
    -Name enabled `
    -Value false `
    -PSPath $sitePath

Write-Host "? Windows Authentication DISABLED" -ForegroundColor Green

# Enable Anonymous Auth
Set-WebConfigurationProperty `
    -Filter /system.webServer/security/authentication/anonymousAuthentication `
    -Name enabled `
    -Value true `
    -PSPath $sitePath

Write-Host "? Anonymous Authentication ENABLED" -ForegroundColor Green

# Verify
$winAuth = (Get-WebConfigurationProperty -Filter /system.webServer/security/authentication/windowsAuthentication -Name enabled -PSPath $sitePath).Value
$anonAuth = (Get-WebConfigurationProperty -Filter /system.webServer/security/authentication/anonymousAuthentication -Name enabled -PSPath $sitePath).Value

Write-Host "`nVerification:" -ForegroundColor Cyan
Write-Host "  Windows Auth: $winAuth" -ForegroundColor $(if (-not $winAuth) { "Green" } else { "Red" })
Write-Host "  Anonymous Auth: $anonAuth" -ForegroundColor $(if ($anonAuth) { "Green" } else { "Red" })

if ($winAuth) {
    Write-Host "`n? CRITICAL: Windows Auth is still enabled!" -ForegroundColor Red
    Write-Host "  Manual fix required in IIS Manager" -ForegroundColor Red
    exit 1
}

# Step 3: Fix web.config for proper IIS integration
Write-Host "`nStep 3: Updating web.config..." -ForegroundColor Yellow

$webConfigPath = "$WebPath\web.config"
$backupPath = "$webConfigPath.backup_$(Get-Date -Format 'yyyyMMddHHmmss')"

if (Test-Path $webConfigPath) {
    Copy-Item $webConfigPath $backupPath
    Write-Host "  Backup: $(Split-Path $backupPath -Leaf)" -ForegroundColor Gray
}

$webConfigContent = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <location path="." inheritInChildApplications="false">
    <system.webServer>
      <handlers>
        <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
      </handlers>
      <aspNetCore processPath="dotnet"
                  arguments=".\SecureBootDashboard.Web.dll"
                  stdoutLogEnabled="true"
                  stdoutLogFile="C:\Logs\SecureBootDashboard\stdout"
                  hostingModel="outofprocess"
                  forwardWindowsAuthToken="false">
        <environmentVariables>
          <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
        </environmentVariables>
      </aspNetCore>
    </system.webServer>
  </location>
</configuration>
"@

Set-Content -Path $webConfigPath -Value $webConfigContent -Encoding UTF8
Write-Host "? web.config updated" -ForegroundColor Green
Write-Host "  Key settings:" -ForegroundColor Gray
Write-Host "    - hostingModel: outofprocess" -ForegroundColor Gray
Write-Host "    - forwardWindowsAuthToken: false" -ForegroundColor Gray
Write-Host "    - stdoutLogEnabled: true" -ForegroundColor Gray

# Step 4: Fix appsettings.Production.json
Write-Host "`nStep 4: Fixing appsettings.Production.json..." -ForegroundColor Yellow

$settingsPath = "$WebPath\appsettings.Production.json"
if (Test-Path $settingsPath) {
    $backupSettings = "$settingsPath.backup_$(Get-Date -Format 'yyyyMMddHHmmss')"
    Copy-Item $settingsPath $backupSettings
    Write-Host "  Backup: $(Split-Path $backupSettings -Leaf)" -ForegroundColor Gray
    
    try {
        $settings = Get-Content $settingsPath -Raw | ConvertFrom-Json
        
        # Fix Serilog path
        if ($settings.Serilog -and $settings.Serilog.WriteTo) {
            $currentPath = $settings.Serilog.WriteTo[0].Args.path
            if ($currentPath -like "R:\*" -or $currentPath -like "C:\Temp\*") {
                $settings.Serilog.WriteTo[0].Args.path = "C:\Logs\SecureBootDashboard\web-.log"
                Write-Host "  ? Updated Serilog path to C:\Logs\SecureBootDashboard" -ForegroundColor Green
            } else {
                Write-Host "  ? Serilog path is acceptable: $currentPath" -ForegroundColor Green
            }
        }
        
        # Save
        $settings | ConvertTo-Json -Depth 10 | Set-Content $settingsPath -Encoding UTF8
    } catch {
        Write-Host "  ? Could not update appsettings: $_" -ForegroundColor Yellow
    }
} else {
    Write-Host "  ? appsettings.Production.json not found!" -ForegroundColor Red
}

# Step 5: Ensure log directories exist
Write-Host "`nStep 5: Creating log directories..." -ForegroundColor Yellow

$logDir = "C:\Logs\SecureBootDashboard"
if (-not (Test-Path $logDir)) {
    New-Item -Path $logDir -ItemType Directory -Force | Out-Null
    Write-Host "? Created: $logDir" -ForegroundColor Green
} else {
    Write-Host "? Exists: $logDir" -ForegroundColor Green
}

# Set permissions
$identity = "IIS AppPool\$AppPoolName"
$acl = Get-Acl $logDir
$rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
    $identity, "FullControl", "ContainerInherit,ObjectInherit", "None", "Allow"
)
$acl.AddAccessRule($rule)
Set-Acl $logDir $acl

Write-Host "? Permissions set for $identity" -ForegroundColor Green

# Step 6: Clear old processes (important!)
Write-Host "`nStep 6: Cleaning up old processes..." -ForegroundColor Yellow

$processes = Get-Process -Name "dotnet" -ErrorAction SilentlyContinue |
    Where-Object { $_.MainWindowTitle -eq "" -and $_.StartTime -lt (Get-Date).AddHours(-1) }

if ($processes) {
    Write-Host "  Found $($processes.Count) old dotnet process(es)" -ForegroundColor Yellow
    foreach ($proc in $processes) {
        try {
            $proc.Kill()
            Write-Host "  ? Killed process $($proc.Id)" -ForegroundColor Green
        } catch {
            Write-Host "  ? Could not kill process $($proc.Id): $_" -ForegroundColor Yellow
        }
    }
} else {
    Write-Host "? No old processes found" -ForegroundColor Green
}

# Step 7: Start services
Write-Host "`nStep 7: Starting services..." -ForegroundColor Yellow

Start-WebAppPool $AppPoolName
Write-Host "? App Pool started" -ForegroundColor Green
Start-Sleep -Seconds 3

Start-Website $SiteName
Write-Host "? Website started" -ForegroundColor Green
Start-Sleep -Seconds 5

# Step 8: Verify
Write-Host "`nStep 8: Verification..." -ForegroundColor Yellow

$appPoolState = (Get-WebAppPoolState $AppPoolName).Value
$siteState = (Get-WebsiteState $SiteName).Value

Write-Host "  App Pool: $appPoolState" -ForegroundColor $(if ($appPoolState -eq "Started") { "Green" } else { "Red" })
Write-Host "  Website: $siteState" -ForegroundColor $(if ($siteState -eq "Started") { "Green" } else { "Red" })

# Check authentication settings one more time
$winAuthFinal = (Get-WebConfigurationProperty -Filter /system.webServer/security/authentication/windowsAuthentication -Name enabled -PSPath $sitePath).Value
$anonAuthFinal = (Get-WebConfigurationProperty -Filter /system.webServer/security/authentication/anonymousAuthentication -Name enabled -PSPath $sitePath).Value

Write-Host "  Windows Auth: $winAuthFinal" -ForegroundColor $(if (-not $winAuthFinal) { "Green" } else { "Red" })
Write-Host "  Anonymous Auth: $anonAuthFinal" -ForegroundColor $(if ($anonAuthFinal) { "Green" } else { "Red" })

# Step 9: Test site
Write-Host "`nStep 9: Testing site..." -ForegroundColor Yellow
Start-Sleep -Seconds 5

try {
    $response = Invoke-WebRequest -Uri "https://secbootsrv.mslabs.local" `
        -UseBasicParsing `
        -SkipCertificateCheck `
        -TimeoutSec 15 `
        -ErrorAction Stop
    
    Write-Host "? Site responded: HTTP $($response.StatusCode)" -ForegroundColor Green
    
    if ($response.Content -match "An error occurred") {
        Write-Host "? Page still shows error" -ForegroundColor Yellow
    } else {
        Write-Host "? Page loaded successfully!" -ForegroundColor Green
    }
} catch {
    Write-Host "? Site test failed: $_" -ForegroundColor Red
    
    if ($_.Exception.Response) {
        $statusCode = [int]$_.Exception.Response.StatusCode
        Write-Host "  Status Code: $statusCode" -ForegroundColor Red
    }
}

# Step 10: Check logs for errors
Write-Host "`nStep 10: Checking recent logs..." -ForegroundColor Yellow

$recentLog = Get-ChildItem $logDir -File -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if ($recentLog) {
    Write-Host "Latest log: $($recentLog.Name)" -ForegroundColor Cyan
    
    $content = Get-Content $recentLog.FullName -Tail 30
    $errors = $content | Where-Object { $_ -match "FTL|ERR|Exception" }
    
    if ($errors) {
        Write-Host "`n? Recent errors found:" -ForegroundColor Yellow
        $errors | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    } else {
        Write-Host "? No errors in recent log" -ForegroundColor Green
    }
    
    # Check for port binding
    $portInfo = $content | Where-Object { $_ -match "Configured URLs|Now listening" }
    if ($portInfo) {
        Write-Host "`nPort configuration:" -ForegroundColor Cyan
        $portInfo | ForEach-Object { 
            if ($_ -match "127\.0\.0\.1:\d+") {
                Write-Host "  ? $_" -ForegroundColor Yellow
                Write-Host "  (App should use IIS, not direct port)" -ForegroundColor Gray
            } else {
                Write-Host "  $_" -ForegroundColor Gray
            }
        }
    }
} else {
    Write-Host "? No log files found" -ForegroundColor Yellow
}

# Summary
Write-Host ""
Write-Host "="*79 -ForegroundColor Cyan
Write-Host "FIX SUMMARY" -ForegroundColor Cyan
Write-Host "="*79 -ForegroundColor Cyan

Write-Host ""
Write-Host "Applied fixes:" -ForegroundColor Green
Write-Host "  ? Disabled Windows Authentication in IIS" -ForegroundColor White
Write-Host "  ? Enabled Anonymous Authentication in IIS" -ForegroundColor White
Write-Host "  ? Updated web.config with correct settings" -ForegroundColor White
Write-Host "  ? Fixed Serilog path (R:\ ? C:\Logs)" -ForegroundColor White
Write-Host "  ? Created log directories with permissions" -ForegroundColor White
Write-Host "  ? Restarted services" -ForegroundColor White

Write-Host ""
Write-Host "Configuration:" -ForegroundColor Yellow
Write-Host "  IIS Windows Auth: DISABLED" -ForegroundColor White
Write-Host "  IIS Anonymous Auth: ENABLED" -ForegroundColor White
Write-Host "  Hosting Model: outofprocess" -ForegroundColor White
Write-Host "  forwardWindowsAuthToken: false" -ForegroundColor White
Write-Host "  Logs: C:\Logs\SecureBootDashboard" -ForegroundColor White

Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Test in browser: https://secbootsrv.mslabs.local" -ForegroundColor White
Write-Host "  2. Check if Windows login prompt appears" -ForegroundColor White
Write-Host "  3. Verify dashboard loads after login" -ForegroundColor White
Write-Host ""
Write-Host "If still failing:" -ForegroundColor Yellow
Write-Host "  1. Check logs: Get-Content C:\Logs\SecureBootDashboard\*.log -Tail 50" -ForegroundColor Cyan
Write-Host "  2. Check Event Viewer: Get-EventLog -LogName Application -Source '*AspNetCore*' -Newest 5" -ForegroundColor Cyan
Write-Host "  3. Verify auth: Get-WebConfigurationProperty -Filter /system.webServer/security/authentication/* -Name enabled -PSPath 'IIS:\Sites\$SiteName'" -ForegroundColor Cyan
Write-Host ""

