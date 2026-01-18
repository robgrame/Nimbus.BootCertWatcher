# ===============================================================================
# Fix-CLRError.ps1
#
# Fix "CLR worker thread exited prematurely" error
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
Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host "Fix CLR Worker Thread Error" -ForegroundColor Cyan
Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Check .NET Runtime
Write-Host "Step 1: Checking .NET Runtime..." -ForegroundColor Yellow
$runtimes = & dotnet --list-runtimes 2>&1
$net10 = $runtimes | Where-Object { $_ -like "*Microsoft.NETCore.App 10.*" }
$aspNet10 = $runtimes | Where-Object { $_ -like "*Microsoft.AspNetCore.App 10.*" }

if ($net10 -and $aspNet10) {
    Write-Host "? .NET 10 Runtime installed" -ForegroundColor Green
    Write-Host "  $net10" -ForegroundColor Gray
    Write-Host "  $aspNet10" -ForegroundColor Gray
} else {
    Write-Host "? .NET 10 Runtime NOT installed or incomplete" -ForegroundColor Red
    Write-Host ""
    Write-Host "Download and install .NET 10 Hosting Bundle:" -ForegroundColor Yellow
    Write-Host "  https://aka.ms/dotnet/10.0/dotnet-hosting-win.exe" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "After installation, run: iisreset" -ForegroundColor Yellow
    Write-Host ""
    exit 1
}

# Step 2: Check AspNetCoreModuleV2
Write-Host "`nStep 2: Checking AspNetCoreModuleV2..." -ForegroundColor Yellow
$modulePath = "$env:SystemRoot\System32\inetsrv\aspnetcorev2.dll"
if (Test-Path $modulePath) {
    $moduleInfo = Get-Item $modulePath
    Write-Host "? Module found: $modulePath" -ForegroundColor Green
    Write-Host "  Version: $($moduleInfo.VersionInfo.FileVersion)" -ForegroundColor Gray
    
    # Check version (should be 20.0.x or higher)
    if ($moduleInfo.VersionInfo.FileMajorPart -lt 20) {
        Write-Host "? Module version is old - consider updating Hosting Bundle" -ForegroundColor Yellow
    }
} else {
    Write-Host "? AspNetCoreModuleV2 not found - install Hosting Bundle" -ForegroundColor Red
    exit 1
}

# Step 3: Stop services
Write-Host "`nStep 3: Stopping services..." -ForegroundColor Yellow
Import-Module WebAdministration

try {
    Stop-WebAppPool $AppPoolName -ErrorAction SilentlyContinue
    Stop-Website $SiteName -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    Write-Host "? Services stopped" -ForegroundColor Green
} catch {
    Write-Host "? Could not stop services: $_" -ForegroundColor Yellow
}

# Step 4: Fix web.config
Write-Host "`nStep 4: Updating web.config..." -ForegroundColor Yellow
$webConfigPath = Join-Path $WebPath "web.config"

if (Test-Path $webConfigPath) {
    # Backup original
    $backupPath = "$webConfigPath.backup_$(Get-Date -Format 'yyyyMMddHHmmss')"
    Copy-Item $webConfigPath $backupPath
    Write-Host "  Backup created: $backupPath" -ForegroundColor Gray
}

# Create new web.config with outofprocess hosting
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
Write-Host "? web.config updated (hostingModel=outofprocess)" -ForegroundColor Green

# Step 5: Verify files
Write-Host "`nStep 5: Verifying application files..." -ForegroundColor Yellow
$mainDll = Join-Path $WebPath "SecureBootDashboard.Web.dll"
$dllExists = Test-Path $mainDll

if ($dllExists) {
    $dllInfo = Get-Item $mainDll
    Write-Host "? Main DLL exists" -ForegroundColor Green
    Write-Host "  Size: $([Math]::Round($dllInfo.Length / 1KB, 2)) KB" -ForegroundColor Gray
    Write-Host "  Modified: $($dllInfo.LastWriteTime)" -ForegroundColor Gray
} else {
    Write-Host "? Main DLL not found: $mainDll" -ForegroundColor Red
    Write-Host "  Please redeploy the application" -ForegroundColor Yellow
    exit 1
}

# Check for runtimeconfig.json
$runtimeConfig = Join-Path $WebPath "SecureBootDashboard.Web.runtimeconfig.json"
if (Test-Path $runtimeConfig) {
    Write-Host "? Runtime config exists" -ForegroundColor Green
} else {
    Write-Host "? Runtime config missing (might be embedded)" -ForegroundColor Yellow
}

# Step 6: Set permissions
Write-Host "`nStep 6: Setting permissions..." -ForegroundColor Yellow
$logsPath = "C:\Logs\SecureBootDashboard"

if (-not (Test-Path $logsPath)) {
    New-Item -Path $logsPath -ItemType Directory -Force | Out-Null
    Write-Host "  Created logs directory" -ForegroundColor Gray
}

try {
    $acl = Get-Acl $logsPath
    $identity = "IIS AppPool\$AppPoolName"
    $accessRule = New-Object System.Security.AccessControl.FileSystemAccessRule(
        $identity,
        "FullControl",
        "ContainerInherit,ObjectInherit",
        "None",
        "Allow"
    )
    $acl.AddAccessRule($accessRule)
    Set-Acl $logsPath $acl
    Write-Host "? Permissions set for $identity" -ForegroundColor Green
} catch {
    Write-Host "? Could not set permissions: $_" -ForegroundColor Yellow
}

# Step 7: Restart services
Write-Host "`nStep 7: Starting services..." -ForegroundColor Yellow
try {
    Start-WebAppPool $AppPoolName
    Start-Sleep -Seconds 2
    Start-Website $SiteName
    Start-Sleep -Seconds 3
    
    $appPoolState = (Get-WebAppPoolState $AppPoolName).Value
    $siteState = (Get-WebsiteState $SiteName).Value
    
    Write-Host "? App Pool: $appPoolState" -ForegroundColor Green
    Write-Host "? Website: $siteState" -ForegroundColor Green
} catch {
    Write-Host "? Could not start services: $_" -ForegroundColor Red
}

# Step 8: Test site
Write-Host "`nStep 8: Testing site..." -ForegroundColor Yellow
Start-Sleep -Seconds 5  # Give app time to start

try {
    $response = Invoke-WebRequest -Uri "https://secbootsrv.mslabs.local" `
        -UseBasicParsing `
        -SkipCertificateCheck `
        -TimeoutSec 10 `
        -ErrorAction Stop
    
    Write-Host "? Site is responding: HTTP $($response.StatusCode)" -ForegroundColor Green
} catch {
    Write-Host "? Site is not responding: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "Check logs:" -ForegroundColor Yellow
    Write-Host "  1. Stdout: C:\Logs\SecureBootDashboard\stdout-*.log" -ForegroundColor Cyan
    Write-Host "  2. Event Viewer: Application > ASP.NET Core" -ForegroundColor Cyan
    Write-Host "  3. Run: .\scripts\Diagnose-WebDashboard.ps1" -ForegroundColor Cyan
}

# Step 9: Show recent logs
Write-Host "`nStep 9: Recent logs..." -ForegroundColor Yellow
$recentLog = Get-ChildItem "$logsPath\*" -File -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if ($recentLog) {
    Write-Host "Latest log: $($recentLog.Name)" -ForegroundColor Cyan
    Write-Host "Last 20 lines:" -ForegroundColor Gray
    Write-Host "----------------------------------------" -ForegroundColor DarkGray
    Get-Content $recentLog.FullName -Tail 20 -ErrorAction SilentlyContinue
    Write-Host "----------------------------------------" -ForegroundColor DarkGray
} else {
    Write-Host "? No logs found yet" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host "Fix Complete" -ForegroundColor Cyan
Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "If the site still doesn't work:" -ForegroundColor Yellow
Write-Host "  1. Check Event Viewer for specific errors" -ForegroundColor White
Write-Host "  2. Review stdout logs in C:\Logs\SecureBootDashboard" -ForegroundColor White
Write-Host "  3. Verify appsettings.Production.json configuration" -ForegroundColor White
Write-Host "  4. Try reinstalling .NET 10 Hosting Bundle" -ForegroundColor White
Write-Host ""

