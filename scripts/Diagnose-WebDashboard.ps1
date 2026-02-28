# ===============================================================================
# Diagnose-WebDashboard.ps1
#
# Quick diagnostic script for HTTP 500.30 errors
# Run this to identify the root cause of startup failures
# ===============================================================================

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$PhysicalPath = "C:\inetpub\SecureBootDashboard.Web",
    
    [Parameter(Mandatory = $false)]
    [string]$LogsPath = "C:\Logs\SecureBootDashboard",
    
    [Parameter(Mandatory = $false)]
    [string]$AppPoolName = "SecureBootDashboard.Web",
    
    [Parameter(Mandatory = $false)]
    [string]$SiteName = "SecureBootDashboard.Web"
)

$ErrorActionPreference = "Continue"

function Write-Header {
    param([string]$Message)
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host $Message -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
}

function Write-CheckResult {
    param(
        [string]$Check,
        [bool]$Success,
        [string]$Details = ""
    )
    
    $icon = if ($Success) { "?" } else { "?" }
    $color = if ($Success) { "Green" } else { "Red" }
    
    Write-Host "$icon $Check" -ForegroundColor $color
    if ($Details) {
        Write-Host "  $Details" -ForegroundColor Gray
    }
}

# ===============================================================================
# Main Diagnostics
# ===============================================================================

Write-Host ""
Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host "SecureBootDashboard.Web - HTTP 500.30 Diagnostics" -ForegroundColor Cyan
Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Analyzing application startup failure..." -ForegroundColor Yellow
Write-Host ""

# 1. Check Application Files
Write-Header "1. Application Files"

$dllPath = Join-Path $PhysicalPath "SecureBootDashboard.Web.dll"
$dllExists = Test-Path $dllPath
Write-CheckResult "Main DLL exists" $dllExists $dllPath

$settingsPath = Join-Path $PhysicalPath "appsettings.json"
$settingsExists = Test-Path $settingsPath
Write-CheckResult "appsettings.json exists" $settingsExists $settingsPath

$prodSettingsPath = Join-Path $PhysicalPath "appsettings.Production.json"
$prodSettingsExists = Test-Path $prodSettingsPath
Write-CheckResult "appsettings.Production.json exists" $prodSettingsExists $prodSettingsPath

$webConfigPath = Join-Path $PhysicalPath "web.config"
$webConfigExists = Test-Path $webConfigPath
Write-CheckResult "web.config exists" $webConfigExists $webConfigPath

# 2. Check .NET Runtime
Write-Header "2. .NET Runtime"

try {
    $runtimes = & dotnet --list-runtimes 2>&1
    $aspNetCore10 = $runtimes | Where-Object { $_ -like "*Microsoft.AspNetCore.App 10.*" }
    $netCore10 = $runtimes | Where-Object { $_ -like "*Microsoft.NETCore.App 10.*" }
    
    Write-CheckResult ".NET 10 Runtime installed" ($netCore10 -ne $null) $netCore10
    Write-CheckResult "ASP.NET Core 10 Runtime installed" ($aspNetCore10 -ne $null) $aspNetCore10
} catch {
    Write-CheckResult ".NET CLI available" $false "dotnet command not found"
}

# 3. Check IIS Configuration
Write-Header "3. IIS Configuration"

try {
    Import-Module WebAdministration -ErrorAction Stop
    
    $appPoolExists = Test-Path "IIS:\AppPools\$AppPoolName"
    Write-CheckResult "Application Pool exists" $appPoolExists $AppPoolName
    
    if ($appPoolExists) {
        $appPool = Get-Item "IIS:\AppPools\$AppPoolName"
        $runtime = $appPool.managedRuntimeVersion
        Write-CheckResult "Managed Runtime Version" ($runtime -eq "") "No Managed Code (.NET Core): $runtime"
        
        try {
            $state = (Get-WebAppPoolState -Name $AppPoolName).Value
            Write-CheckResult "Application Pool State" ($state -eq "Started") "State: $state"
        } catch {
            Write-Host "  ? Could not determine App Pool state: $_" -ForegroundColor Yellow
        }
    }
    
    $siteExists = Test-Path "IIS:\Sites\$SiteName"
    Write-CheckResult "Website exists" $siteExists $SiteName
    
    if ($siteExists) {
        try {
            $siteState = (Get-WebsiteState -Name $SiteName).Value
            Write-CheckResult "Website State" ($siteState -eq "Started") "State: $siteState"
        } catch {
            Write-Host "  ? Could not determine site state: $_" -ForegroundColor Yellow
        }
    }
} catch {
    Write-Host "? Could not load WebAdministration module: $_" -ForegroundColor Red
}

# 4. Check Logs
Write-Header "4. Application Logs"

if (Test-Path $LogsPath) {
    Write-CheckResult "Logs directory exists" $true $LogsPath
    
    # Check for stdout logs
    $stdoutLogs = Get-ChildItem "$LogsPath\stdout-*.log" -ErrorAction SilentlyContinue | 
        Sort-Object LastWriteTime -Descending
    
    if ($stdoutLogs) {
        $latestStdout = $stdoutLogs | Select-Object -First 1
        Write-CheckResult "Stdout logs found" $true "$($stdoutLogs.Count) file(s)"
        Write-Host "  Latest: $($latestStdout.Name) ($(Get-Date $latestStdout.LastWriteTime -Format 'yyyy-MM-dd HH:mm:ss'))" -ForegroundColor Gray
    } else {
        Write-CheckResult "Stdout logs found" $false "No stdout-*.log files (check web.config)"
    }
    
    # Check for Serilog logs
    $serilogLogs = Get-ChildItem "$LogsPath\web-*.log" -ErrorAction SilentlyContinue | 
        Sort-Object LastWriteTime -Descending
    
    if ($serilogLogs) {
        $latestSerilog = $serilogLogs | Select-Object -First 1
        Write-CheckResult "Serilog logs found" $true "$($serilogLogs.Count) file(s)"
        Write-Host "  Latest: $($latestSerilog.Name) ($(Get-Date $latestSerilog.LastWriteTime -Format 'yyyy-MM-dd HH:mm:ss'))" -ForegroundColor Gray
    } else {
        Write-Host "  ? No Serilog logs found (will be created after successful startup)" -ForegroundColor Gray
    }
} else {
    Write-CheckResult "Logs directory exists" $false "$LogsPath (will create)"
    New-Item -Path $LogsPath -ItemType Directory -Force | Out-Null
    Write-Host "  ? Created logs directory" -ForegroundColor Green
}

# 5. Check Permissions
Write-Header "5. Permissions"

try {
    $identity = "IIS AppPool\$AppPoolName"
    $acl = Get-Acl $LogsPath
    $hasPermission = $acl.Access | Where-Object { 
        $_.IdentityReference -eq $identity -and $_.FileSystemRights -match "FullControl|Write" 
    }
    
    if ($hasPermission) {
        Write-CheckResult "App Pool has write access to logs" $true
    } else {
        Write-CheckResult "App Pool has write access to logs" $false "Adding permissions..."
        
        $accessRule = New-Object System.Security.AccessControl.FileSystemAccessRule(
            $identity, 
            "FullControl", 
            "ContainerInherit,ObjectInherit", 
            "None", 
            "Allow"
        )
        $acl.AddAccessRule($accessRule)
        Set-Acl $LogsPath $acl
        
        Write-Host "  ? Granted FullControl to $identity" -ForegroundColor Green
    }
} catch {
    Write-Host "  ? Could not check permissions: $_" -ForegroundColor Yellow
}

# 6. Check Configuration
Write-Header "6. Configuration Files"

if ($prodSettingsExists) {
    try {
        $settings = Get-Content $prodSettingsPath -Raw | ConvertFrom-Json
        Write-CheckResult "appsettings.Production.json is valid JSON" $true
        
        if ($settings.ApiSettings) {
            $baseUrl = $settings.ApiSettings.BaseUrl
            Write-Host "  API Base URL: $baseUrl" -ForegroundColor Gray
            
            if ($settings.ApiSettings.UseCertificateAuth) {
                $thumbprint = $settings.ApiSettings.CertificateThumbprint
                if ($thumbprint -and $thumbprint -ne "YOUR_WEB_CLIENT_CERT_THUMBPRINT") {
                    Write-Host "  Certificate: $thumbprint" -ForegroundColor Gray
                } else {
                    Write-Host "  ? Certificate thumbprint not configured!" -ForegroundColor Yellow
                }
            }
        } else {
            Write-Host "  ? ApiSettings section not found" -ForegroundColor Yellow
        }
    } catch {
        Write-CheckResult "appsettings.Production.json is valid JSON" $false $_.Exception.Message
    }
}

# 7. Check Event Viewer
Write-Header "7. Recent Errors (Event Viewer)"

try {
    $recentErrors = Get-EventLog -LogName Application -Source "*AspNetCore*" -Newest 5 -ErrorAction SilentlyContinue | 
        Where-Object { $_.EntryType -eq "Error" }
    
    if ($recentErrors) {
        Write-Host "Found $($recentErrors.Count) recent ASP.NET Core error(s):" -ForegroundColor Yellow
        foreach ($error in $recentErrors) {
            Write-Host "`n  Time: $(Get-Date $error.TimeGenerated -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Gray
            Write-Host "  $($error.Message.Substring(0, [Math]::Min(200, $error.Message.Length)))" -ForegroundColor Gray
            if ($error.Message.Length > 200) {
                Write-Host "  ..." -ForegroundColor Gray
            }
        }
    } else {
        Write-Host "  No recent ASP.NET Core errors found in Event Viewer" -ForegroundColor Green
    }
} catch {
    Write-Host "  ? Could not read Event Viewer: $_" -ForegroundColor Yellow
}

# 8. Show Latest Log Content
Write-Header "8. Latest Log Content"

$latestLog = Get-ChildItem "$LogsPath\*" -File -ErrorAction SilentlyContinue | 
    Sort-Object LastWriteTime -Descending | 
    Select-Object -First 1

if ($latestLog) {
    Write-Host "File: $($latestLog.Name)" -ForegroundColor Cyan
    Write-Host "Time: $(Get-Date $latestLog.LastWriteTime -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Cyan
    Write-Host "Size: $([Math]::Round($latestLog.Length / 1KB, 2)) KB" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Last 30 lines:" -ForegroundColor Yellow
    Write-Host "----------------------------------------" -ForegroundColor Gray
    Get-Content $latestLog.FullName -Tail 30 | ForEach-Object {
        if ($_ -match "error|exception|fail") {
            Write-Host $_ -ForegroundColor Red
        } elseif ($_ -match "warn") {
            Write-Host $_ -ForegroundColor Yellow
        } else {
            Write-Host $_ -ForegroundColor Gray
        }
    }
    Write-Host "----------------------------------------" -ForegroundColor Gray
} else {
    Write-Host "No log files found yet" -ForegroundColor Yellow
    Write-Host "This might mean:" -ForegroundColor Yellow
    Write-Host "  1. The app hasn't started yet" -ForegroundColor Gray
    Write-Host "  2. Logging is not configured in web.config" -ForegroundColor Gray
    Write-Host "  3. App Pool doesn't have write permissions" -ForegroundColor Gray
}

# 9. Recommendations
Write-Header "9. Recommendations"

$issues = @()

if (-not $dllExists) { $issues += "Main DLL is missing - redeploy application" }
if (-not $prodSettingsExists) { $issues += "appsettings.Production.json is missing" }
if (-not ($netCore10 -or $aspNetCore10)) { $issues += "Install .NET 10 Hosting Bundle" }
if (-not $stdoutLogs) { $issues += "Enable stdout logging in web.config" }

if ($issues.Count -eq 0) {
    Write-Host "? All basic checks passed!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "  1. Review the log content above for specific errors" -ForegroundColor White
    Write-Host "  2. Check Event Viewer for detailed error messages" -ForegroundColor White
    Write-Host "  3. Verify appsettings.Production.json configuration" -ForegroundColor White
} else {
    Write-Host "Found $($issues.Count) issue(s):" -ForegroundColor Red
    foreach ($issue in $issues) {
        Write-Host "  • $issue" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host "Diagnostics Complete" -ForegroundColor Cyan
Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "For more help, see: TROUBLESHOOT_HTTP_500_30.md" -ForegroundColor Cyan
Write-Host ""

