# ===============================================================================
# Fix-StdoutRedirection.ps1
#
# Fix "Could not start stdout file redirection" error
# Creates logs directory and fixes web.config path
# ===============================================================================

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$WebPath = "C:\inetpub\SecureBootDashboard.Web",
    
    [Parameter(Mandatory = $false)]
    [string]$AppPoolName = "SecureBootDashboard.Web",
    
    [Parameter(Mandatory = $false)]
    [string]$LogsPath = "C:\Logs\SecureBootDashboard"
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host "Fix Stdout Redirection Error" -ForegroundColor Cyan
Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "Issue: Could not start stdout file redirection to '.\logs\stdout'" -ForegroundColor Yellow
Write-Host "Solution: Create logs directory and fix web.config path" -ForegroundColor Yellow
Write-Host ""

# Step 1: Create logs directory in application folder (for relative path)
Write-Host "Step 1: Creating application logs directory..." -ForegroundColor Yellow
$appLogsDir = Join-Path $WebPath "logs"

if (-not (Test-Path $appLogsDir)) {
    try {
        New-Item -Path $appLogsDir -ItemType Directory -Force | Out-Null
        Write-Host "? Created: $appLogsDir" -ForegroundColor Green
    } catch {
        Write-Host "? Failed to create directory: $_" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "? Directory exists: $appLogsDir" -ForegroundColor Green
}

# Step 2: Set permissions on application logs directory
Write-Host "`nStep 2: Setting permissions on application logs..." -ForegroundColor Yellow
try {
    $acl = Get-Acl $appLogsDir
    $identity = "IIS AppPool\$AppPoolName"
    $rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
        $identity, 
        "FullControl", 
        "ContainerInherit,ObjectInherit", 
        "None", 
        "Allow"
    )
    $acl.AddAccessRule($rule)
    Set-Acl $appLogsDir $acl
    Write-Host "? Permissions set for: $identity" -ForegroundColor Green
} catch {
    Write-Host "? Could not set permissions: $_" -ForegroundColor Yellow
}

# Step 3: Create central logs directory (recommended location)
Write-Host "`nStep 3: Creating central logs directory..." -ForegroundColor Yellow
if (-not (Test-Path $LogsPath)) {
    try {
        New-Item -Path $LogsPath -ItemType Directory -Force | Out-Null
        Write-Host "? Created: $LogsPath" -ForegroundColor Green
    } catch {
        Write-Host "? Could not create central logs: $_" -ForegroundColor Yellow
    }
} else {
    Write-Host "? Directory exists: $LogsPath" -ForegroundColor Green
}

# Step 4: Set permissions on central logs directory
Write-Host "`nStep 4: Setting permissions on central logs..." -ForegroundColor Yellow
if (Test-Path $LogsPath) {
    try {
        $acl = Get-Acl $LogsPath
        $identity = "IIS AppPool\$AppPoolName"
        $rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
            $identity, 
            "FullControl", 
            "ContainerInherit,ObjectInherit", 
            "None", 
            "Allow"
        )
        $acl.AddAccessRule($rule)
        Set-Acl $LogsPath $acl
        Write-Host "? Permissions set for: $identity" -ForegroundColor Green
    } catch {
        Write-Host "? Could not set permissions: $_" -ForegroundColor Yellow
    }
}

# Step 5: Fix web.config to use absolute path
Write-Host "`nStep 5: Updating web.config..." -ForegroundColor Yellow
$webConfigPath = Join-Path $WebPath "web.config"

if (Test-Path $webConfigPath) {
    # Backup
    $backupPath = "$webConfigPath.backup_$(Get-Date -Format 'yyyyMMddHHmmss')"
    Copy-Item $webConfigPath $backupPath
    Write-Host "  Backup created: $(Split-Path $backupPath -Leaf)" -ForegroundColor Gray
    
    # Create new web.config with absolute path
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
                  stdoutLogFile="$($LogsPath -replace '\\', '\\')\stdout"
                  hostingModel="outofprocess">
        <environmentVariables>
          <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
        </environmentVariables>
      </aspNetCore>
    </system.webServer>
  </location>
</configuration>
"@
    
    Set-Content -Path $webConfigPath -Value $webConfigContent -Encoding UTF8
    Write-Host "? web.config updated with absolute path" -ForegroundColor Green
    Write-Host "  Stdout log path: $LogsPath\stdout-*.log" -ForegroundColor Gray
} else {
    Write-Host "? web.config not found at: $webConfigPath" -ForegroundColor Red
    exit 1
}

# Step 6: Verify Anonymous Authentication
Write-Host "`nStep 6: Verifying authentication settings..." -ForegroundColor Yellow
try {
    Import-Module WebAdministration -ErrorAction Stop
    
    $sitePath = "IIS:\Sites\SecureBootDashboard.Web"
    if (Test-Path $sitePath) {
        $anonymousAuth = Get-WebConfigurationProperty `
            -Filter /system.webServer/security/authentication/anonymousAuthentication `
            -Name enabled `
            -PSPath $sitePath
        
        $windowsAuth = Get-WebConfigurationProperty `
            -Filter /system.webServer/security/authentication/windowsAuthentication `
            -Name enabled `
            -PSPath $sitePath
        
        Write-Host "  Anonymous Authentication: $($anonymousAuth.Value)" -ForegroundColor $(if ($anonymousAuth.Value) { "Green" } else { "Red" })
        Write-Host "  Windows Authentication: $($windowsAuth.Value)" -ForegroundColor $(if (-not $windowsAuth.Value) { "Green" } else { "Yellow" })
        
        if (-not $anonymousAuth.Value) {
            Write-Host "`n? Anonymous Authentication is disabled!" -ForegroundColor Yellow
            Write-Host "  Enabling it now..." -ForegroundColor Gray
            
            Set-WebConfigurationProperty `
                -Filter /system.webServer/security/authentication/anonymousAuthentication `
                -Name enabled `
                -Value true `
                -PSPath $sitePath
            
            Write-Host "? Anonymous Authentication enabled" -ForegroundColor Green
        }
    }
} catch {
    Write-Host "  ? Could not verify authentication: $_" -ForegroundColor Gray
}

# Step 7: Restart App Pool
Write-Host "`nStep 7: Restarting Application Pool..." -ForegroundColor Yellow
try {
    Stop-WebAppPool $AppPoolName -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    Start-WebAppPool $AppPoolName
    Start-Sleep -Seconds 5
    
    $state = (Get-WebAppPoolState $AppPoolName).Value
    Write-Host "? App Pool restarted: $state" -ForegroundColor Green
} catch {
    Write-Host "? Could not restart App Pool: $_" -ForegroundColor Yellow
}

# Step 8: Verify logs are being created
Write-Host "`nStep 8: Checking for log files..." -ForegroundColor Yellow
Start-Sleep -Seconds 3

$foundLogs = $false

# Check application logs directory
$appLogs = Get-ChildItem $appLogsDir -File -ErrorAction SilentlyContinue
if ($appLogs) {
    Write-Host "? Logs found in application directory:" -ForegroundColor Green
    $appLogs | ForEach-Object { Write-Host "  $($_.Name)" -ForegroundColor Gray }
    $foundLogs = $true
}

# Check central logs directory
$centralLogs = Get-ChildItem $LogsPath -File -ErrorAction SilentlyContinue
if ($centralLogs) {
    Write-Host "? Logs found in central directory:" -ForegroundColor Green
    $centralLogs | Sort-Object LastWriteTime -Descending | Select-Object -First 5 | 
        ForEach-Object { Write-Host "  $($_.Name) ($(Get-Date $_.LastWriteTime -Format 'yyyy-MM-dd HH:mm:ss'))" -ForegroundColor Gray }
    $foundLogs = $true
}

if (-not $foundLogs) {
    Write-Host "? No log files found yet" -ForegroundColor Yellow
    Write-Host "  Logs will be created when the application starts" -ForegroundColor Gray
}

# Step 9: Test site
Write-Host "`nStep 9: Testing site..." -ForegroundColor Yellow
Start-Sleep -Seconds 5

try {
    $response = Invoke-WebRequest -Uri "https://secbootsrv.mslabs.local" `
        -UseBasicParsing `
        -SkipCertificateCheck `
        -TimeoutSec 15 `
        -ErrorAction Stop
    
    Write-Host "? Site is responding: HTTP $($response.StatusCode)" -ForegroundColor Green
    Write-Host ""
    Write-Host "===============================================================================" -ForegroundColor Cyan
    Write-Host "SUCCESS - Stdout Redirection Fixed!" -ForegroundColor Green
    Write-Host "===============================================================================" -ForegroundColor Cyan
    Write-Host ""
    
} catch {
    Write-Host "? Site test: $_" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Check logs for details:" -ForegroundColor Yellow
    
    # Show latest log if available
    $latestLog = Get-ChildItem $LogsPath -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    
    if ($latestLog) {
        Write-Host "`nLatest log (last 20 lines):" -ForegroundColor Cyan
        Write-Host "File: $($latestLog.Name)" -ForegroundColor Gray
        Write-Host "----------------------------------------" -ForegroundColor DarkGray
        Get-Content $latestLog.FullName -Tail 20 -ErrorAction SilentlyContinue
        Write-Host "----------------------------------------" -ForegroundColor DarkGray
    }
}

Write-Host ""
Write-Host "Configuration Summary:" -ForegroundColor Cyan
Write-Host "  App Logs: $appLogsDir" -ForegroundColor White
Write-Host "  Central Logs: $LogsPath" -ForegroundColor White
Write-Host "  Stdout Path: $LogsPath\stdout-*.log" -ForegroundColor White
Write-Host "  web.config: Updated with absolute path" -ForegroundColor White
Write-Host "  Permissions: Set for $identity" -ForegroundColor White
Write-Host ""

