<#
.SYNOPSIS
    Script automatizzato per il deployment locale di SecureBootWatcher

.DESCRIPTION
    Questo script automatizza il processo di deployment di SecureBootWatcher su un server Windows locale.
    Gestisce l'installazione di IIS, la configurazione del database, il deployment dell'applicazione e la configurazione del client.

.PARAMETER InstallPrerequisites
    Installa i prerequisiti (IIS, .NET Runtime, SQL Server)

.PARAMETER ConfigureDatabase
    Configura il database SQL Server

.PARAMETER DeployApplication
    Esegue il deployment dell'applicazione

.PARAMETER ConfigureClient
    Configura il client PowerShell

.PARAMETER All
    Esegue tutti i passaggi di deployment

.PARAMETER ServerName
    Nome del server SQL Server (default: localhost\SQLEXPRESS)

.PARAMETER DatabaseName
    Nome del database (default: SecureBootWatcher)

.PARAMETER AppPath
    Percorso installazione applicazione (default: C:\SecureBootWatcher)

.PARAMETER SiteName
    Nome del sito IIS (default: SecureBootWatcher)

.EXAMPLE
    .\Deploy-LocalServer.ps1 -All
    Esegue deployment completo

.EXAMPLE
    .\Deploy-LocalServer.ps1 -DeployApplication -AppPath "D:\Apps\SecureBootWatcher"
    Deploya solo l'applicazione in un percorso custom

.NOTES
    Richiede: PowerShell 5.1+, privilegi amministratore
    Versione: 1.14.0
    Author: Nimbus SecureBootWatcher Team
#>

[CmdletBinding()]
param(
    [Parameter()]
    [switch]$InstallPrerequisites,
    
    [Parameter()]
    [switch]$ConfigureDatabase,
    
    [Parameter()]
    [switch]$DeployApplication,
    
    [Parameter()]
    [switch]$ConfigureClient,
    
    [Parameter()]
    [switch]$All,
    
    [Parameter()]
    [string]$ServerName = "localhost\SQLEXPRESS",
    
    [Parameter()]
    [string]$DatabaseName = "SecureBootWatcher",
    
    [Parameter()]
    [string]$AppPath = "C:\SecureBootWatcher",
    
    [Parameter()]
    [string]$SiteName = "SecureBootWatcher",
    
    [Parameter()]
    [string]$DbPassword = "SecureBootP@ssw0rd123!",
    
    [Parameter()]
    [string]$DbUser = "SecureBootWatcherApp",
    
    [Parameter()]
    [int]$HttpPort = 80,
    
    [Parameter()]
    [int]$HttpsPort = 443
)

#Requires -Version 5.1
#Requires -RunAsAdministrator

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Script root
$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

# Color functions
function Write-Success { Write-Host "✓ $args" -ForegroundColor Green }
function Write-Info { Write-Host "ℹ $args" -ForegroundColor Cyan }
function Write-Warning { Write-Host "⚠ $args" -ForegroundColor Yellow }
function Write-Error { Write-Host "✗ $args" -ForegroundColor Red }
function Write-Step { Write-Host "`n═══ $args ═══" -ForegroundColor Magenta }

# Check if running as Administrator
function Test-Administrator {
    $currentUser = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentUser)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-Administrator)) {
    Write-Error "Questo script deve essere eseguito come Amministratore!"
    exit 1
}

# Function: Install Prerequisites
function Install-Prerequisites {
    Write-Step "Installazione Prerequisiti"
    
    # Install IIS
    Write-Info "Installazione IIS e moduli..."
    $iisFeatures = @(
        'Web-Server',
        'Web-WebServer',
        'Web-Common-Http',
        'Web-Default-Doc',
        'Web-Dir-Browsing',
        'Web-Http-Errors',
        'Web-Static-Content',
        'Web-Health',
        'Web-Http-Logging',
        'Web-Performance',
        'Web-Stat-Compression',
        'Web-Security',
        'Web-Filtering',
        'Web-App-Dev',
        'Web-Net-Ext45',
        'Web-Asp-Net45',
        'Web-ISAPI-Ext',
        'Web-ISAPI-Filter',
        'Web-WebSockets',
        'Web-Mgmt-Tools',
        'Web-Mgmt-Console'
    )
    
    foreach ($feature in $iisFeatures) {
        $installed = Get-WindowsFeature -Name $feature
        if (-not $installed.Installed) {
            Write-Info "Installing $feature..."
            Install-WindowsFeature -Name $feature -IncludeManagementTools | Out-Null
        }
    }
    Write-Success "IIS installato"
    
    # Check .NET 10 Runtime
    Write-Info "Verifica .NET 10 Runtime..."
    $dotnetRuntimes = dotnet --list-runtimes 2>$null
    if ($dotnetRuntimes -notmatch 'Microsoft.AspNetCore.App 10') {
        Write-Warning ".NET 10 Runtime non trovato"
        Write-Info "Download da: https://dotnet.microsoft.com/download/dotnet/10.0"
        Write-Info "Installare ASP.NET Core Runtime 10 - Hosting Bundle"
        
        $response = Read-Host "Continuare senza .NET 10? (s/n)"
        if ($response -ne 's') {
            exit 1
        }
    } else {
        Write-Success ".NET 10 Runtime installato"
    }
    
    # Check SQL Server
    Write-Info "Verifica SQL Server..."
    $sqlService = Get-Service -Name "MSSQL`$SQLEXPRESS" -ErrorAction SilentlyContinue
    if (-not $sqlService) {
        Write-Warning "SQL Server Express non trovato"
        Write-Info "Download da: https://www.microsoft.com/sql-server/sql-server-downloads"
        Write-Info "Installare SQL Server 2019/2022 Express"
        
        $response = Read-Host "Continuare senza SQL Server? (s/n)"
        if ($response -ne 's') {
            exit 1
        }
    } else {
        Write-Success "SQL Server installato"
        
        # Start SQL Server if not running
        if ($sqlService.Status -ne 'Running') {
            Write-Info "Avvio SQL Server..."
            Start-Service -Name "MSSQL`$SQLEXPRESS"
            Start-Sleep -Seconds 5
        }
    }
    
    Write-Success "Prerequisiti verificati"
}

# Function: Configure Database
function Configure-Database {
    Write-Step "Configurazione Database"
    
    # Test SQL connectivity
    Write-Info "Test connessione SQL Server: $ServerName"
    try {
        $result = sqlcmd -S $ServerName -Q "SELECT @@VERSION" 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "SQL Server non raggiungibile"
        }
        Write-Success "SQL Server raggiungibile"
    }
    catch {
        Write-Error "Impossibile connettersi a SQL Server: $_"
        Write-Info "Verificare che SQL Server sia in esecuzione e accetti connessioni TCP/IP"
        return $false
    }
    
    # Create database and user
    Write-Info "Creazione database e utente..."
    $sqlScript = @"
-- Create database if not exists
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'$DatabaseName')
BEGIN
    CREATE DATABASE [$DatabaseName]
    PRINT 'Database created'
END
ELSE
BEGIN
    PRINT 'Database already exists'
END
GO

USE [$DatabaseName]
GO

-- Create login if not exists
IF NOT EXISTS (SELECT name FROM sys.server_principals WHERE name = N'$DbUser')
BEGIN
    CREATE LOGIN [$DbUser] WITH PASSWORD = N'$DbPassword'
    PRINT 'Login created'
END
ELSE
BEGIN
    PRINT 'Login already exists'
END
GO

-- Create user if not exists
IF NOT EXISTS (SELECT name FROM sys.database_principals WHERE name = N'$DbUser')
BEGIN
    CREATE USER [$DbUser] FOR LOGIN [$DbUser]
    PRINT 'User created'
END
GO

-- Grant permissions
ALTER ROLE db_owner ADD MEMBER [$DbUser]
GO

PRINT 'Database configuration completed'
GO
"@
    
    $sqlScript | Out-File "$env:TEMP\setup-db.sql" -Encoding UTF8
    
    try {
        sqlcmd -S $ServerName -i "$env:TEMP\setup-db.sql" | Out-Host
        Remove-Item "$env:TEMP\setup-db.sql" -Force
        Write-Success "Database configurato"
    }
    catch {
        Write-Error "Errore configurazione database: $_"
        return $false
    }
    
    # Configure SQL Server for TCP/IP (if local)
    if ($ServerName -like "*localhost*" -or $ServerName -like "*.\*") {
        Write-Info "Configurazione TCP/IP per SQL Server..."
        try {
            # Enable TCP/IP via registry (requires restart)
            $regPath = "HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\MSSQL*\MSSQLServer\SuperSocketNetLib\Tcp"
            Get-Item -Path $regPath -ErrorAction SilentlyContinue | ForEach-Object {
                Set-ItemProperty -Path $_.PSPath -Name "Enabled" -Value 1
            }
            
            # Open firewall
            $firewallRule = Get-NetFirewallRule -DisplayName "SQL Server" -ErrorAction SilentlyContinue
            if (-not $firewallRule) {
                New-NetFirewallRule -DisplayName "SQL Server" `
                    -Direction Inbound `
                    -Protocol TCP `
                    -LocalPort 1433 `
                    -Action Allow | Out-Null
                Write-Success "Firewall configurato per SQL Server"
            }
        }
        catch {
            Write-Warning "Impossibile configurare TCP/IP automaticamente: $_"
        }
    }
    
    return $true
}

# Function: Deploy Application
function Deploy-Application {
    Write-Step "Deployment Applicazione"
    
    # Create directory structure
    Write-Info "Creazione struttura cartelle..."
    $directories = @(
        "$AppPath\App",
        "$AppPath\Logs",
        "$AppPath\Data",
        "$AppPath\Backups",
        "$AppPath\Client"
    )
    
    foreach ($dir in $directories) {
        if (-not (Test-Path $dir)) {
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
        }
    }
    Write-Success "Struttura cartelle creata"
    
    # Find published app
    Write-Info "Ricerca applicazione pubblicata..."
    $publishPath = Join-Path $ScriptRoot "..\publish"
    if (-not (Test-Path $publishPath)) {
        $publishPath = Join-Path $ScriptRoot "..\..\publish"
    }
    
    if (-not (Test-Path $publishPath)) {
        Write-Warning "Applicazione non trovata in $publishPath"
        Write-Info "Pubblicare l'applicazione con:"
        Write-Info "  dotnet publish SecureBootDashboard.Api/SecureBootDashboard.Api.csproj -c Release -o publish"
        
        $response = Read-Host "Continuare senza deployment app? (s/n)"
        if ($response -ne 's') {
            return $false
        }
    } else {
        # Copy application files
        Write-Info "Copia file applicazione..."
        Copy-Item -Path "$publishPath\*" -Destination "$AppPath\App" -Recurse -Force
        Write-Success "File applicazione copiati"
    }
    
    # Create appsettings.Production.json
    Write-Info "Creazione appsettings.Production.json..."
    $connectionString = "Server=$ServerName;Database=$DatabaseName;User Id=$DbUser;Password=$DbPassword;TrustServerCertificate=True;MultipleActiveResultSets=true"
    
    $appsettings = @{
        ConnectionStrings = @{
            DefaultConnection = $connectionString
        }
        Logging = @{
            LogLevel = @{
                Default = "Information"
                "Microsoft.AspNetCore" = "Warning"
                "Microsoft.EntityFrameworkCore" = "Warning"
            }
            File = @{
                Path = "$AppPath\Logs\app-.log"
                RollingInterval = "Day"
                RetainedFileCountLimit = 30
            }
        }
        ApplicationSettings = @{
            RequireMutualTls = $false
            EnableDeviceCleanup = $true
            DeviceInactivityThresholdDays = 90
            EnableSignalR = $true
        }
        AllowedHosts = "*"
    } | ConvertTo-Json -Depth 10
    
    $appsettings | Out-File "$AppPath\App\appsettings.Production.json" -Encoding UTF8
    Write-Success "Configurazione creata"
    
    # Import IIS module
    Import-Module WebAdministration -ErrorAction SilentlyContinue
    
    # Create Application Pool
    Write-Info "Creazione Application Pool..."
    if (Test-Path "IIS:\AppPools\$SiteName") {
        Write-Info "Application Pool già esistente, rimozione..."
        Remove-WebAppPool -Name $SiteName
    }
    
    New-WebAppPool -Name $SiteName | Out-Null
    Set-ItemProperty "IIS:\AppPools\$SiteName" -Name "managedRuntimeVersion" -Value ""
    Set-ItemProperty "IIS:\AppPools\$SiteName" -Name "enable32BitAppOnWin64" -Value $false
    Set-ItemProperty "IIS:\AppPools\$SiteName" -Name "startMode" -Value "AlwaysRunning"
    Set-ItemProperty "IIS:\AppPools\$SiteName" -Name "processModel.idleTimeout" -Value "00:00:00"
    Write-Success "Application Pool creato"
    
    # Create Website
    Write-Info "Creazione sito IIS..."
    if (Test-Path "IIS:\Sites\$SiteName") {
        Write-Info "Sito già esistente, rimozione..."
        Remove-WebSite -Name $SiteName
    }
    
    # Remove default website if on port 80
    if (Test-Path "IIS:\Sites\Default Web Site") {
        $defaultSite = Get-WebSite -Name "Default Web Site"
        if ($defaultSite.bindings.Collection.bindingInformation -like "*:80:*") {
            Write-Info "Rimozione Default Web Site..."
            Remove-WebSite -Name "Default Web Site"
        }
    }
    
    New-WebSite -Name $SiteName `
        -Port $HttpPort `
        -PhysicalPath "$AppPath\App" `
        -ApplicationPool $SiteName `
        -Force | Out-Null
    
    # Add HTTPS binding
    if ($HttpsPort -ne 0) {
        Write-Info "Creazione certificato self-signed..."
        $cert = New-SelfSignedCertificate `
            -DnsName "localhost", $env:COMPUTERNAME `
            -CertStoreLocation "cert:\LocalMachine\My" `
            -NotAfter (Get-Date).AddYears(5) `
            -KeyUsage DigitalSignature, KeyEncipherment `
            -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.1")
        
        New-WebBinding -Name $SiteName -Protocol https -Port $HttpsPort | Out-Null
        $binding = Get-WebBinding -Name $SiteName -Protocol https
        $binding.AddSslCertificate($cert.GetCertHashString(), "my")
        Write-Success "HTTPS configurato"
    }
    
    # Set permissions
    Write-Info "Configurazione permessi..."
    $acl = Get-Acl $AppPath
    
    # Add IIS_IUSRS
    $rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
        "IIS_IUSRS", "FullControl", "ContainerInherit,ObjectInherit", "None", "Allow"
    )
    $acl.SetAccessRule($rule)
    
    # Add ApplicationPoolIdentity
    $rule2 = New-Object System.Security.AccessControl.FileSystemAccessRule(
        "IIS APPPOOL\$SiteName", "FullControl", "ContainerInherit,ObjectInherit", "None", "Allow"
    )
    $acl.SetAccessRule($rule2)
    Set-Acl $AppPath $acl
    Write-Success "Permessi configurati"
    
    # Create web.config
    Write-Info "Creazione web.config..."
    $webConfig = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <location path="." inheritInChildApplications="false">
    <system.webServer>
      <handlers>
        <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
      </handlers>
      <aspNetCore processPath="dotnet" 
                  arguments=".\SecureBootDashboard.Api.dll" 
                  stdoutLogEnabled="true" 
                  stdoutLogFile=".\logs\stdout" 
                  hostingModel="inprocess">
        <environmentVariables>
          <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
        </environmentVariables>
      </aspNetCore>
    </system.webServer>
  </location>
</configuration>
"@
    $webConfig | Out-File "$AppPath\App\web.config" -Encoding UTF8
    Write-Success "web.config creato"
    
    # Start website
    Write-Info "Avvio sito..."
    Start-WebSite -Name $SiteName
    Start-Sleep -Seconds 3
    
    $site = Get-WebSite -Name $SiteName
    if ($site.State -eq 'Started') {
        Write-Success "Sito avviato"
    } else {
        Write-Error "Sito non avviato. Stato: $($site.State)"
        return $false
    }
    
    # Test endpoint
    Write-Info "Test endpoint..."
    Start-Sleep -Seconds 5
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:$HttpPort/health" -UseBasicParsing -TimeoutSec 10
        if ($response.StatusCode -eq 200) {
            Write-Success "Endpoint funzionante"
        }
    }
    catch {
        Write-Warning "Endpoint non raggiungibile: $_"
        Write-Info "Verificare i log in: $AppPath\App\logs\"
    }
    
    return $true
}

# Function: Configure Client
function Configure-Client {
    Write-Step "Configurazione Client PowerShell"
    
    # Copy client script
    Write-Info "Copia script client..."
    $clientSource = Join-Path $ScriptRoot "..\SecureBootWatcher-Client.ps1"
    $clientDest = "$AppPath\Client\SecureBootWatcher-Client.ps1"
    
    if (Test-Path $clientSource) {
        Copy-Item $clientSource -Destination $clientDest -Force
        Write-Success "Script client copiato"
    } else {
        Write-Warning "Script client non trovato: $clientSource"
    }
    
    # Create client configuration
    Write-Info "Creazione configurazione client..."
    $clientConfig = @{
        SecureBootWatcher = @{
            FleetId = "LocalFleet"
            RunMode = "Once"
            RegistryPollInterval = "00:30:00"
            EventQueryInterval = "00:30:00"
            EventLookbackPeriod = "7.00:00:00"
            EventChannels = @(
                "Microsoft-Windows-SecureBoot-Servicing/Operational",
                "Microsoft-Windows-SecureBoot-State/Operational",
                "System"
            )
            Sinks = @{
                ExecutionStrategy = "FirstSuccess"
                SinkPriority = "WebApi,FileShare"
                EnableFileShare = $false
                EnableWebApi = $true
                EnableAzureFunction = $false
                FileShare = @{
                    RootPath = "$AppPath\Data\Reports"
                    FileExtension = ".json"
                }
                WebApi = @{
                    BaseAddress = "http://localhost:$HttpPort"
                    IngestionRoute = "/api/SecureBootReports"
                    HttpTimeout = "00:02:00"
                }
                AzureFunction = @{
                    FunctionUrl = ""
                    ApiKey = ""
                    HttpTimeout = "00:02:00"
                    UseApiKeyAsQueryParameter = $true
                }
            }
            Commands = @{
                EnableCommandProcessing = $true
                ProcessBeforeInventory = $false
                MaxCommandsPerCycle = 10
                CommandExecutionDelay = "00:00:05"
                ContinueOnCommandFailure = $true
            }
        }
        Logging = @{
            LogLevel = @{
                Default = "Information"
            }
            Console = @{
                Enabled = $true
            }
            File = @{
                Enabled = $true
                Path = "logs/client.log"
                RollingInterval = "Day"
                RetainedFileCountLimit = 7
            }
        }
    } | ConvertTo-Json -Depth 10
    
    $clientConfig | Out-File "$AppPath\Client\appsettings.json" -Encoding UTF8
    Write-Success "Configurazione client creata"
    
    # Test client
    Write-Info "Test esecuzione client..."
    try {
        $output = & powershell.exe -ExecutionPolicy Bypass -File "$AppPath\Client\SecureBootWatcher-Client.ps1" -RunMode Once 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Success "Client eseguito con successo"
        } else {
            Write-Warning "Client completato con errori. Verificare log."
        }
    }
    catch {
        Write-Warning "Errore test client: $_"
    }
    
    # Create scheduled task
    Write-Info "Creazione Scheduled Task..."
    $taskName = "SecureBootWatcher Client"
    
    # Remove existing task
    $existingTask = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
    if ($existingTask) {
        Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
    }
    
    $action = New-ScheduledTaskAction `
        -Execute "PowerShell.exe" `
        -Argument "-NoProfile -ExecutionPolicy Bypass -File $AppPath\Client\SecureBootWatcher-Client.ps1 -RunMode Once" `
        -WorkingDirectory "$AppPath\Client"
    
    $trigger1 = New-ScheduledTaskTrigger -Daily -At "09:00AM"
    $trigger2 = New-ScheduledTaskTrigger -Once -At "12:00PM" -RepetitionInterval (New-TimeSpan -Hours 6) -RepetitionDuration ([TimeSpan]::MaxValue)
    
    $settings = New-ScheduledTaskSettingsSet `
        -ExecutionTimeLimit (New-TimeSpan -Hours 1) `
        -AllowStartIfOnBatteries `
        -DontStopIfGoingOnBatteries `
        -StartWhenAvailable `
        -MultipleInstances IgnoreNew
    
    Register-ScheduledTask `
        -TaskName $taskName `
        -Action $action `
        -Trigger $trigger1, $trigger2 `
        -Settings $settings `
        -User "SYSTEM" `
        -RunLevel Highest `
        -Description "SecureBootWatcher inventory collection - Runs daily and every 6 hours" | Out-Null
    
    Write-Success "Scheduled Task creato"
    
    return $true
}

# Function: Display Summary
function Show-Summary {
    Write-Step "Riepilogo Deployment"
    
    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host "  SecureBootWatcher - Deployment Completato" -ForegroundColor Green
    Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host ""
    
    Write-Host "📁 Percorso Applicazione:  " -NoNewline
    Write-Host "$AppPath\App" -ForegroundColor Yellow
    
    Write-Host "🗄️  Database:              " -NoNewline
    Write-Host "$ServerName\$DatabaseName" -ForegroundColor Yellow
    
    Write-Host "🌐 URL Dashboard:          " -NoNewline
    Write-Host "http://localhost:$HttpPort" -ForegroundColor Yellow
    
    if ($HttpsPort -ne 0) {
        Write-Host "🔒 URL HTTPS:              " -NoNewline
        Write-Host "https://localhost:$HttpsPort" -ForegroundColor Yellow
    }
    
    Write-Host "📊 Health Check:           " -NoNewline
    Write-Host "http://localhost:$HttpPort/health" -ForegroundColor Yellow
    
    Write-Host "💻 Client Path:            " -NoNewline
    Write-Host "$AppPath\Client\SecureBootWatcher-Client.ps1" -ForegroundColor Yellow
    
    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
    
    Write-Host ""
    Write-Info "Prossimi passi:"
    Write-Host "  1. Aprire browser: http://localhost:$HttpPort"
    Write-Host "  2. Verificare dashboard funzionante"
    Write-Host "  3. Eseguire client manualmente per test"
    Write-Host "  4. Verificare dati nel database"
    Write-Host ""
    
    Write-Info "Comandi utili:"
    Write-Host "  # Verificare sito IIS"
    Write-Host "  Get-WebSite -Name '$SiteName'"
    Write-Host ""
    Write-Host "  # Test client"
    Write-Host "  cd '$AppPath\Client'"
    Write-Host "  .\SecureBootWatcher-Client.ps1 -RunMode Once"
    Write-Host ""
    Write-Host "  # Visualizzare log"
    Write-Host "  Get-Content '$AppPath\Logs\app-*.log' -Tail 50"
    Write-Host ""
    
    Write-Info "Documentazione completa: docs\DEPLOYMENT_GUIDE_LOCAL.md"
    Write-Host ""
}

# Main execution
try {
    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host "  SecureBootWatcher - Deployment Automatizzato v1.14" -ForegroundColor Cyan
    Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host ""
    
    $startTime = Get-Date
    
    # Determine what to do
    $doPrerequisites = $All -or $InstallPrerequisites
    $doDatabase = $All -or $ConfigureDatabase
    $doApp = $All -or $DeployApplication
    $doClient = $All -or $ConfigureClient
    
    if (-not ($doPrerequisites -or $doDatabase -or $doApp -or $doClient)) {
        Write-Warning "Nessuna operazione specificata!"
        Write-Info "Usare: -All, -InstallPrerequisites, -ConfigureDatabase, -DeployApplication, o -ConfigureClient"
        Write-Info "Esempio: .\Deploy-LocalServer.ps1 -All"
        exit 1
    }
    
    # Execute steps
    if ($doPrerequisites) {
        Install-Prerequisites
    }
    
    if ($doDatabase) {
        if (-not (Configure-Database)) {
            Write-Error "Configurazione database fallita!"
            exit 1
        }
    }
    
    if ($doApp) {
        if (-not (Deploy-Application)) {
            Write-Error "Deployment applicazione fallito!"
            exit 1
        }
    }
    
    if ($doClient) {
        if (-not (Configure-Client)) {
            Write-Error "Configurazione client fallita!"
            exit 1
        }
    }
    
    $elapsed = (Get-Date) - $startTime
    Write-Host ""
    Write-Success "Deployment completato in $($elapsed.TotalSeconds.ToString('F1')) secondi"
    
    Show-Summary
    
    exit 0
}
catch {
    Write-Host ""
    Write-Error "Errore durante il deployment: $_"
    Write-Host $_.ScriptStackTrace -ForegroundColor Red
    exit 1
}
