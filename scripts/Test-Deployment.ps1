<#
.SYNOPSIS
    Script di verifica post-deployment per SecureBootWatcher

.DESCRIPTION
    Esegue una serie di controlli per verificare che il deployment di SecureBootWatcher
    sia stato completato correttamente e che tutti i componenti siano funzionanti.

.PARAMETER AppPath
    Percorso installazione applicazione (default: C:\SecureBootWatcher)

.PARAMETER ServerName
    Nome del server SQL Server (default: localhost\SQLEXPRESS)

.PARAMETER DatabaseName
    Nome del database (default: SecureBootWatcher)

.PARAMETER SiteName
    Nome del sito IIS (default: SecureBootWatcher)

.PARAMETER HttpPort
    Porta HTTP (default: 80)

.PARAMETER Detailed
    Mostra output dettagliato di tutti i controlli

.EXAMPLE
    .\Test-Deployment.ps1
    Esegue verifica base con parametri default

.EXAMPLE
    .\Test-Deployment.ps1 -Detailed
    Esegue verifica completa con output dettagliato

.NOTES
    Versione: 1.14.0
    Richiede: PowerShell 5.1+, privilegi amministratore per alcuni controlli
#>

[CmdletBinding()]
param(
    [Parameter()]
    [string]$AppPath = "C:\SecureBootWatcher",
    
    [Parameter()]
    [string]$ServerName = "localhost\SQLEXPRESS",
    
    [Parameter()]
    [string]$DatabaseName = "SecureBootWatcher",
    
    [Parameter()]
    [string]$SiteName = "SecureBootWatcher",
    
    [Parameter()]
    [int]$HttpPort = 80,
    
    [Parameter()]
    [switch]$Detailed
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Continue'  # Continue on errors to show all issues

# Color functions
function Write-Pass { Write-Host "✓ PASS: $args" -ForegroundColor Green }
function Write-Fail { Write-Host "✗ FAIL: $args" -ForegroundColor Red }
function Write-Warn { Write-Host "⚠ WARN: $args" -ForegroundColor Yellow }
function Write-Info { Write-Host "ℹ INFO: $args" -ForegroundColor Cyan }
function Write-Section { 
    Write-Host "`n" -NoNewline
    Write-Host "═══ $args ═══" -ForegroundColor Magenta 
}

# Global counters
$script:PassCount = 0
$script:FailCount = 0
$script:WarnCount = 0

function Test-Check {
    param(
        [string]$Name,
        [scriptblock]$Test,
        [string]$FailMessage,
        [string]$PassMessage,
        [bool]$Critical = $true
    )
    
    if ($Detailed) {
        Write-Info "Testing: $Name..."
    }
    
    try {
        $result = & $Test
        if ($result) {
            Write-Pass "$Name - $PassMessage"
            $script:PassCount++
            return $true
        } else {
            if ($Critical) {
                Write-Fail "$Name - $FailMessage"
                $script:FailCount++
            } else {
                Write-Warn "$Name - $FailMessage"
                $script:WarnCount++
            }
            return $false
        }
    }
    catch {
        if ($Critical) {
            Write-Fail "$Name - Error: $_"
            $script:FailCount++
        } else {
            Write-Warn "$Name - Error: $_"
            $script:WarnCount++
        }
        return $false
    }
}

# Start verification
Write-Host ""
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  SecureBootWatcher - Deployment Verification v1.14" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Info "Starting verification at: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
Write-Host ""

# ═══ PREREQUISITI ═══
Write-Section "Prerequisiti"

Test-Check -Name "PowerShell Version" -Test {
    $PSVersionTable.PSVersion.Major -ge 5
} -PassMessage "PowerShell $($PSVersionTable.PSVersion) installed" `
  -FailMessage "PowerShell 5.0+ required"

Test-Check -Name ".NET 10 Runtime" -Test {
    $runtimes = dotnet --list-runtimes 2>$null
    $runtimes -match 'Microsoft.AspNetCore.App 10'
} -PassMessage ".NET 10 Runtime found" `
  -FailMessage ".NET 10 Runtime not found"

Test-Check -Name "IIS Installed" -Test {
    Get-Service -Name "W3SVC" -ErrorAction SilentlyContinue
} -PassMessage "IIS service found" `
  -FailMessage "IIS not installed"

Test-Check -Name "SQL Server" -Test {
    Get-Service -Name "MSSQL`$SQLEXPRESS" -ErrorAction SilentlyContinue
} -PassMessage "SQL Server service found" `
  -FailMessage "SQL Server not found" `
  -Critical $false

# ═══ FILE SYSTEM ═══
Write-Section "File System Structure"

Test-Check -Name "Application Root" -Test {
    Test-Path $AppPath
} -PassMessage "Found at $AppPath" `
  -FailMessage "Not found at $AppPath"

Test-Check -Name "Application Files" -Test {
    Test-Path "$AppPath\App\SecureBootDashboard.Api.dll"
} -PassMessage "Application DLL found" `
  -FailMessage "Application DLL not found"

Test-Check -Name "Configuration File" -Test {
    $configExists = Test-Path "$AppPath\App\appsettings.json"
    $prodConfigExists = Test-Path "$AppPath\App\appsettings.Production.json"
    $configExists -or $prodConfigExists
} -PassMessage "Configuration files found" `
  -FailMessage "Configuration files not found"

Test-Check -Name "Logs Directory" -Test {
    Test-Path "$AppPath\Logs"
} -PassMessage "Logs directory exists" `
  -FailMessage "Logs directory not found"

Test-Check -Name "Client Files" -Test {
    Test-Path "$AppPath\Client\SecureBootWatcher-Client.ps1"
} -PassMessage "Client script found" `
  -FailMessage "Client script not found" `
  -Critical $false

# ═══ IIS CONFIGURATION ═══
Write-Section "IIS Configuration"

Import-Module WebAdministration -ErrorAction SilentlyContinue

Test-Check -Name "IIS Module" -Test {
    Get-Module -Name WebAdministration
} -PassMessage "WebAdministration module loaded" `
  -FailMessage "Cannot load WebAdministration module"

Test-Check -Name "Application Pool" -Test {
    Test-Path "IIS:\AppPools\$SiteName"
} -PassMessage "Application pool '$SiteName' exists" `
  -FailMessage "Application pool '$SiteName' not found"

if (Test-Path "IIS:\AppPools\$SiteName") {
    Test-Check -Name "App Pool Running" -Test {
        $state = Get-WebAppPoolState -Name $SiteName
        $state.Value -eq "Started"
    } -PassMessage "Application pool is running" `
      -FailMessage "Application pool is not running"
    
    Test-Check -Name "App Pool .NET Version" -Test {
        $version = Get-ItemProperty "IIS:\AppPools\$SiteName" -Name managedRuntimeVersion
        $version.Value -eq ""
    } -PassMessage "No Managed Code (correct for .NET Core)" `
      -FailMessage "Incorrect runtime version"
}

Test-Check -Name "IIS Website" -Test {
    Test-Path "IIS:\Sites\$SiteName"
} -PassMessage "Website '$SiteName' exists" `
  -FailMessage "Website '$SiteName' not found"

if (Test-Path "IIS:\Sites\$SiteName") {
    Test-Check -Name "Website Running" -Test {
        $site = Get-WebSite -Name $SiteName
        $site.State -eq "Started"
    } -PassMessage "Website is running" `
      -FailMessage "Website is not running"
    
    Test-Check -Name "HTTP Binding" -Test {
        $bindings = Get-WebBinding -Name $SiteName
        $bindings | Where-Object {$_.protocol -eq "http"}
    } -PassMessage "HTTP binding configured" `
      -FailMessage "HTTP binding not found"
    
    Test-Check -Name "HTTPS Binding" -Test {
        $bindings = Get-WebBinding -Name $SiteName
        $bindings | Where-Object {$_.protocol -eq "https"}
    } -PassMessage "HTTPS binding configured" `
      -FailMessage "HTTPS binding not found" `
      -Critical $false
}

# ═══ SQL SERVER ═══
Write-Section "SQL Server Configuration"

Test-Check -Name "SQL Server Service" -Test {
    $service = Get-Service -Name "MSSQL`$SQLEXPRESS" -ErrorAction SilentlyContinue
    $service -and $service.Status -eq "Running"
} -PassMessage "SQL Server is running" `
  -FailMessage "SQL Server is not running"

Test-Check -Name "SQL Connectivity" -Test {
    $result = sqlcmd -S $ServerName -Q "SELECT @@VERSION" -b 2>&1
    $LASTEXITCODE -eq 0
} -PassMessage "Can connect to SQL Server" `
  -FailMessage "Cannot connect to SQL Server"

Test-Check -Name "Database Exists" -Test {
    $query = "SELECT name FROM sys.databases WHERE name='$DatabaseName'"
    $result = sqlcmd -S $ServerName -Q $query -h-1 2>&1
    $result -match $DatabaseName
} -PassMessage "Database '$DatabaseName' exists" `
  -FailMessage "Database '$DatabaseName' not found"

Test-Check -Name "Database Tables" -Test {
    $query = "USE [$DatabaseName]; SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE'"
    $result = sqlcmd -S $ServerName -Q $query -h-1 2>&1
    [int]$result -gt 0
} -PassMessage "Database tables created" `
  -FailMessage "No tables found in database"

# ═══ API ENDPOINTS ═══
Write-Section "API Endpoints"

$baseUrl = "http://localhost:$HttpPort"

Test-Check -Name "HTTP Connectivity" -Test {
    Test-NetConnection -ComputerName localhost -Port $HttpPort -WarningAction SilentlyContinue | 
        Select-Object -ExpandProperty TcpTestSucceeded
} -PassMessage "Port $HttpPort is open" `
  -FailMessage "Port $HttpPort is not accessible"

Test-Check -Name "Health Endpoint" -Test {
    $response = Invoke-WebRequest -Uri "$baseUrl/health" -UseBasicParsing -TimeoutSec 10 -ErrorAction Stop
    $response.StatusCode -eq 200
} -PassMessage "/health endpoint returns 200 OK" `
  -FailMessage "/health endpoint not responding"

Test-Check -Name "Root Endpoint" -Test {
    $response = Invoke-WebRequest -Uri $baseUrl -UseBasicParsing -TimeoutSec 10 -ErrorAction Stop
    $response.StatusCode -eq 200
} -PassMessage "Root endpoint accessible" `
  -FailMessage "Root endpoint not accessible"

Test-Check -Name "API Route" -Test {
    $response = Invoke-WebRequest -Uri "$baseUrl/api/status" -UseBasicParsing -TimeoutSec 10 -ErrorAction Stop
    $response.StatusCode -eq 200
} -PassMessage "/api/status endpoint responds" `
  -FailMessage "/api/status endpoint not responding" `
  -Critical $false

Test-Check -Name "Swagger UI" -Test {
    $response = Invoke-WebRequest -Uri "$baseUrl/swagger" -UseBasicParsing -TimeoutSec 10 -ErrorAction Stop
    $response.StatusCode -eq 200
} -PassMessage "Swagger UI accessible" `
  -FailMessage "Swagger UI not accessible" `
  -Critical $false

# ═══ CLIENT CONFIGURATION ═══
Write-Section "Client Configuration"

if (Test-Path "$AppPath\Client\appsettings.json") {
    Test-Check -Name "Client Config Valid" -Test {
        $config = Get-Content "$AppPath\Client\appsettings.json" -Raw | ConvertFrom-Json
        $config.SecureBootWatcher.Sinks.WebApi.BaseAddress -ne $null
    } -PassMessage "Client configuration is valid" `
      -FailMessage "Client configuration is invalid"
    
    Test-Check -Name "Client Points to API" -Test {
        $config = Get-Content "$AppPath\Client\appsettings.json" -Raw | ConvertFrom-Json
        $apiUrl = $config.SecureBootWatcher.Sinks.WebApi.BaseAddress
        $apiUrl -like "*localhost*" -or $apiUrl -like "*127.0.0.1*"
    } -PassMessage "Client configured to use local API" `
      -FailMessage "Client not pointing to local API" `
      -Critical $false
}

Test-Check -Name "Scheduled Task" -Test {
    Get-ScheduledTask -TaskName "SecureBootWatcher Client" -ErrorAction SilentlyContinue
} -PassMessage "Scheduled task exists" `
  -FailMessage "Scheduled task not found" `
  -Critical $false

# ═══ SECURITY ═══
Write-Section "Security Configuration"

Test-Check -Name "HTTPS Certificate" -Test {
    if (Test-Path "IIS:\Sites\$SiteName") {
        $bindings = Get-WebBinding -Name $SiteName | Where-Object {$_.protocol -eq "https"}
        $bindings -ne $null
    } else {
        $false
    }
} -PassMessage "HTTPS certificate configured" `
  -FailMessage "HTTPS certificate not configured" `
  -Critical $false

Test-Check -Name "SQL Authentication" -Test {
    $query = "USE [$DatabaseName]; SELECT COUNT(*) FROM sys.database_principals WHERE name='SecureBootWatcherApp'"
    $result = sqlcmd -S $ServerName -Q $query -h-1 2>&1
    [int]$result -gt 0
} -PassMessage "SQL user configured" `
  -FailMessage "SQL user not found" `
  -Critical $false

Test-Check -Name "File Permissions" -Test {
    $acl = Get-Acl $AppPath
    $hasIISPerms = $acl.Access | Where-Object {
        $_.IdentityReference -like "*IIS*" -or 
        $_.IdentityReference -like "*APPPOOL*"
    }
    $hasIISPerms -ne $null
} -PassMessage "IIS has file permissions" `
  -FailMessage "IIS permissions may be missing" `
  -Critical $false

# ═══ LOGGING ═══
Write-Section "Logging & Monitoring"

Test-Check -Name "Application Logs" -Test {
    Test-Path "$AppPath\Logs\*.log"
} -PassMessage "Application log files exist" `
  -FailMessage "No application log files found" `
  -Critical $false

Test-Check -Name "IIS Stdout Logs" -Test {
    Test-Path "$AppPath\App\logs\stdout*.log"
} -PassMessage "IIS stdout logs configured" `
  -FailMessage "IIS stdout logs not found" `
  -Critical $false

Test-Check -Name "Recent Log Activity" -Test {
    $logs = Get-ChildItem "$AppPath\Logs\*.log" -ErrorAction SilentlyContinue
    if ($logs) {
        $latestLog = $logs | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        ((Get-Date) - $latestLog.LastWriteTime).TotalHours -lt 24
    } else {
        $false
    }
} -PassMessage "Recent log activity detected" `
  -FailMessage "No recent log activity" `
  -Critical $false

# ═══ FUNCTIONAL TESTS ═══
Write-Section "Functional Tests"

Test-Check -Name "API Accepts POST" -Test {
    $testReport = @{
        Device = @{
            MachineName = "TEST_VERIFICATION"
            DomainName = "TEST"
        }
        CreatedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
        ClientVersion = "1.14.0-test"
        CorrelationId = [Guid]::NewGuid().ToString('N')
    } | ConvertTo-Json -Depth 10
    
    $response = Invoke-RestMethod -Uri "$baseUrl/api/SecureBootReports" `
        -Method Post `
        -Body $testReport `
        -ContentType "application/json" `
        -TimeoutSec 10 `
        -ErrorAction Stop
    
    $response -ne $null
} -PassMessage "API accepts POST requests" `
  -FailMessage "API does not accept POST requests" `
  -Critical $false

Test-Check -Name "Database Write" -Test {
    $query = "USE [$DatabaseName]; SELECT COUNT(*) FROM Devices WHERE MachineName='TEST_VERIFICATION'"
    $result = sqlcmd -S $ServerName -Q $query -h-1 2>&1
    [int]$result -gt 0
} -PassMessage "Data written to database" `
  -FailMessage "Data not written to database" `
  -Critical $false

if ($Detailed) {
    Test-Check -Name "SignalR Hub" -Test {
        # Simple check if SignalR endpoint responds
        $response = Invoke-WebRequest -Uri "$baseUrl/dashboardHub" -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
        $response.StatusCode -in @(200, 307, 308)  # 200 OK or redirects
    } -PassMessage "SignalR hub endpoint accessible" `
      -FailMessage "SignalR hub not responding" `
      -Critical $false
}

# ═══ SUMMARY ═══
Write-Host ""
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Verification Summary" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

$total = $script:PassCount + $script:FailCount + $script:WarnCount
$passPercent = if ($total -gt 0) { [math]::Round(($script:PassCount / $total) * 100, 1) } else { 0 }

Write-Host "Total Checks:    " -NoNewline
Write-Host $total -ForegroundColor White

Write-Host "Passed:          " -NoNewline
Write-Host $script:PassCount -ForegroundColor Green -NoNewline
Write-Host " ($passPercent%)" -ForegroundColor Green

Write-Host "Failed:          " -NoNewline
Write-Host $script:FailCount -ForegroundColor Red

Write-Host "Warnings:        " -NoNewline
Write-Host $script:WarnCount -ForegroundColor Yellow

Write-Host ""

# Overall status
if ($script:FailCount -eq 0) {
    Write-Host "✓ DEPLOYMENT STATUS: " -NoNewline -ForegroundColor Green
    Write-Host "HEALTHY" -ForegroundColor Green
    Write-Host ""
    Write-Info "All critical checks passed successfully!"
    Write-Host ""
    
    if ($script:WarnCount -gt 0) {
        Write-Warn "There are $($script:WarnCount) warning(s) that should be reviewed"
        Write-Info "These are non-critical issues that may affect optional features"
    }
    
    Write-Host ""
    Write-Info "Next steps:"
    Write-Host "  1. Access dashboard: http://localhost:$HttpPort"
    Write-Host "  2. Run client manually to test: cd $AppPath\Client"
    Write-Host "  3. Monitor logs: Get-Content '$AppPath\Logs\app-*.log' -Tail 50"
    Write-Host "  4. Verify data collection in dashboard"
    Write-Host ""
    
    exit 0
} else {
    Write-Host "✗ DEPLOYMENT STATUS: " -NoNewline -ForegroundColor Red
    Write-Host "ISSUES DETECTED" -ForegroundColor Red
    Write-Host ""
    Write-Fail "$($script:FailCount) critical issue(s) detected"
    Write-Host ""
    Write-Info "Review the failures above and take corrective action"
    Write-Info "Consult: docs\DEPLOYMENT_GUIDE_LOCAL.md for troubleshooting"
    Write-Host ""
    
    # Suggest common fixes
    if ($script:FailCount -gt 0) {
        Write-Host "Common solutions:" -ForegroundColor Yellow
        Write-Host "  • Check IIS Application Pool is started"
        Write-Host "  • Verify .NET 10 Runtime is installed"
        Write-Host "  • Ensure SQL Server service is running"
        Write-Host "  • Check firewall allows port $HttpPort"
        Write-Host "  • Review logs in $AppPath\Logs\"
        Write-Host ""
    }
    
    exit 1
}
