# Guida al Deployment Locale - SecureBootWatcher v1.14

Questa guida fornisce istruzioni dettagliate per il deployment di SecureBootWatcher su un server Windows locale, utilizzando IIS e SQL Server.

## 📋 Indice

1. [Prerequisiti](#prerequisiti)
2. [Preparazione del Server](#preparazione-del-server)
3. [Installazione e Configurazione SQL Server](#installazione-sql-server)
4. [Configurazione dell'Applicazione](#configurazione-applicazione)
5. [Deployment su IIS](#deployment-iis)
6. [Configurazione Client](#configurazione-client)
7. [Verifica e Test](#verifica-test)
8. [Troubleshooting](#troubleshooting)
9. [Manutenzione](#manutenzione)

---

## 1. Prerequisiti

### Server Requirements

**Sistema Operativo:**
- Windows Server 2019 o superiore
- Windows 10/11 Pro (per test)

**Software Necessario:**
- .NET 10 Runtime (ASP.NET Core Runtime)
- .NET Framework 4.8 (per client legacy)
- IIS 10 o superiore
- SQL Server 2019 o superiore (Express, Standard, o Enterprise)
- PowerShell 5.1 o superiore

**Risorse Hardware Minime:**
- CPU: 4 core
- RAM: 8 GB (16 GB raccomandato)
- Spazio Disco: 20 GB liberi
- Rete: 1 Gbps

### Software di Build (per compilazione)

Sul PC di sviluppo:
- Visual Studio 2022 (17.8+) con workload ASP.NET
- .NET 10 SDK
- Git
- SQL Server Management Studio (SSMS)

---

## 2. Preparazione del Server

### 2.1 Installazione Componenti Windows

#### Abilitare IIS e Funzionalità

```powershell
# Eseguire PowerShell come Amministratore

# Installare IIS con tutti i componenti necessari
Install-WindowsFeature -Name Web-Server -IncludeManagementTools

# Installare ASP.NET Core Module
Install-WindowsFeature -Name Web-Asp-Net45
Install-WindowsFeature -Name Web-Net-Ext45
Install-WindowsFeature -Name Web-ISAPI-Ext
Install-WindowsFeature -Name Web-ISAPI-Filter

# Installare WebSocket Protocol (per SignalR)
Install-WindowsFeature -Name Web-WebSockets

# Verificare installazione IIS
Get-WindowsFeature -Name Web-* | Where-Object {$_.Installed -eq $true}
```

#### Installare .NET 10 Runtime

```powershell
# Download .NET 10 Hosting Bundle per IIS
# URL: https://dotnet.microsoft.com/download/dotnet/10.0

# Esempio: usando winget
winget install Microsoft.DotNet.HostingBundle.10

# Oppure download manuale da:
# https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-aspnetcore-10.0.0-windows-hosting-bundle-installer

# Dopo l'installazione, riavviare IIS
net stop was /y
net start w3svc
```

#### Verificare Installazione .NET

```powershell
# Verificare versioni .NET installate
dotnet --list-runtimes

# Output atteso:
# Microsoft.AspNetCore.App 10.x.x
# Microsoft.NETCore.App 10.x.x
```

### 2.2 Creare Cartelle Applicazione

```powershell
# Creare struttura cartelle
$appRoot = "C:\SecureBootWatcher"

New-Item -ItemType Directory -Path "$appRoot\App" -Force
New-Item -ItemType Directory -Path "$appRoot\Logs" -Force
New-Item -ItemType Directory -Path "$appRoot\Data" -Force
New-Item -ItemType Directory -Path "$appRoot\Backups" -Force
New-Item -ItemType Directory -Path "$appRoot\Client" -Force

# Impostare permessi per IIS
$acl = Get-Acl "$appRoot"
$rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
    "IIS_IUSRS", "FullControl", "ContainerInherit,ObjectInherit", "None", "Allow"
)
$acl.SetAccessRule($rule)
Set-Acl "$appRoot" $acl

# Permessi per ApplicationPoolIdentity
$rule2 = New-Object System.Security.AccessControl.FileSystemAccessRule(
    "IIS APPPOOL\SecureBootWatcher", "FullControl", "ContainerInherit,ObjectInherit", "None", "Allow"
)
$acl.SetAccessRule($rule2)
Set-Acl "$appRoot" $acl
```

---

## 3. Installazione SQL Server

### 3.1 Installazione SQL Server Express

#### Download e Installazione

```powershell
# Download SQL Server 2022 Express
# URL: https://www.microsoft.com/it-it/sql-server/sql-server-downloads

# Installazione tramite GUI:
# 1. Eseguire SQL2022-SSEI-Expr.exe
# 2. Scegliere "Download Media" o "Basic" installation
# 3. Accettare i termini di licenza
# 4. Selezionare cartella di installazione
# 5. Attendere completamento

# Oppure installazione silenziosa:
.\SQLEXPR_x64_ENU.exe /Q /IACCEPTSQLSERVERLICENSETERMS `
    /ACTION=Install `
    /FEATURES=SQLEngine `
    /INSTANCENAME=SQLEXPRESS `
    /SECURITYMODE=SQL `
    /SAPWD="YourStrongPassword123!" `
    /SQLSVCACCOUNT="NT AUTHORITY\SYSTEM" `
    /SQLSYSADMINACCOUNTS="BUILTIN\Administrators" `
    /TCPENABLED=1
```

### 3.2 Configurazione SQL Server

#### Abilitare TCP/IP

```powershell
# Avviare SQL Server Configuration Manager
# o usare PowerShell:

# Importare modulo SQL Server (se installato SSMS)
Import-Module "sqlps" -DisableNameChecking

# Abilitare TCP/IP
$smo = 'Microsoft.SqlServer.Management.Smo.'
$wmi = New-Object ($smo + 'Wmi.ManagedComputer')
$uri = "ManagedComputer[@Name='$env:COMPUTERNAME']/ServerInstance[@Name='SQLEXPRESS']/ServerProtocol[@Name='Tcp']"
$Tcp = $wmi.GetSmoObject($uri)
$Tcp.IsEnabled = $true
$Tcp.Alter()

# Riavviare servizio SQL Server
Restart-Service -Name 'MSSQL$SQLEXPRESS'
```

#### Creare Database e User

```sql
-- Connettersi a SQL Server con SSMS o sqlcmd
-- sqlcmd -S localhost\SQLEXPRESS -U sa -P YourPassword

-- Creare database
CREATE DATABASE SecureBootWatcher
GO

USE SecureBootWatcher
GO

-- Creare login per l'applicazione
CREATE LOGIN SecureBootWatcherApp 
WITH PASSWORD = 'StrongPassword123!@#'
GO

-- Creare user nel database
CREATE USER SecureBootWatcherApp FOR LOGIN SecureBootWatcherApp
GO

-- Assegnare permessi
ALTER ROLE db_owner ADD MEMBER SecureBootWatcherApp
GO

-- Verificare
SELECT name, type_desc FROM sys.database_principals 
WHERE name = 'SecureBootWatcherApp'
GO
```

### 3.3 Configurare Firewall SQL Server

```powershell
# Aprire porta SQL Server (1433)
New-NetFirewallRule -DisplayName "SQL Server" `
    -Direction Inbound `
    -Protocol TCP `
    -LocalPort 1433 `
    -Action Allow

# Aprire porta SQL Browser (1434 UDP)
New-NetFirewallRule -DisplayName "SQL Browser" `
    -Direction Inbound `
    -Protocol UDP `
    -LocalPort 1434 `
    -Action Allow
```

---

## 4. Configurazione dell'Applicazione

### 4.1 Build dell'Applicazione

Sul PC di sviluppo:

```powershell
# Navigare nella cartella della soluzione
cd C:\Users\nefario\source\repos\robgrame\Nimbus.BootCertWatcher

# Assicurarsi di essere sul branch corretto
git checkout copilot/merge-web-app-and-api
git pull origin copilot/merge-web-app-and-api

# Build in modalità Release
dotnet build -c Release

# Pubblicare l'applicazione unificata
dotnet publish SecureBootDashboard.Api/SecureBootDashboard.Api.csproj `
    -c Release `
    -o C:\Publish\SecureBootWatcher `
    --self-contained false

# Verificare output
Get-ChildItem C:\Publish\SecureBootWatcher
```

### 4.2 Configurare appsettings.Production.json

Creare/modificare il file `appsettings.Production.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost\\SQLEXPRESS;Database=SecureBootWatcher;User Id=SecureBootWatcherApp;Password=StrongPassword123!@#;TrustServerCertificate=True;MultipleActiveResultSets=true"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    },
    "File": {
      "Path": "C:\\SecureBootWatcher\\Logs\\app-.log",
      "RollingInterval": "Day",
      "RetainedFileCountLimit": 30
    }
  },
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://localhost:5000"
      },
      "Https": {
        "Url": "https://localhost:5001",
        "Certificate": {
          "Path": "C:\\SecureBootWatcher\\Data\\certificate.pfx",
          "Password": ""
        }
      }
    }
  },
  "ApplicationSettings": {
    "RequireMutualTls": false,
    "EnableDeviceCleanup": true,
    "DeviceInactivityThresholdDays": 90,
    "EnableSignalR": true
  },
  "AllowedHosts": "*"
}
```

### 4.3 Copiare File sul Server

```powershell
# Sul PC di sviluppo
$source = "C:\Publish\SecureBootWatcher"
$destination = "\\ServerName\C$\SecureBootWatcher\App"

# Oppure se sul server locale:
$destination = "C:\SecureBootWatcher\App"

# Copiare file
Copy-Item -Path "$source\*" -Destination $destination -Recurse -Force

# Copiare file di configurazione Production
Copy-Item -Path "$source\appsettings.Production.json" `
    -Destination "$destination\appsettings.Production.json" -Force
```

---

## 5. Deployment su IIS

### 5.1 Creare Application Pool

```powershell
# Importare modulo IIS
Import-Module WebAdministration

# Creare Application Pool
New-WebAppPool -Name "SecureBootWatcher" -Force

# Configurare Application Pool
Set-ItemProperty IIS:\AppPools\SecureBootWatcher -Name "managedRuntimeVersion" -Value ""
Set-ItemProperty IIS:\AppPools\SecureBootWatcher -Name "enable32BitAppOnWin64" -Value $false

# Impostare identità
Set-ItemProperty IIS:\AppPools\SecureBootWatcher -Name "processModel.identityType" -Value "ApplicationPoolIdentity"

# Impostare opzioni avanzate
Set-ItemProperty IIS:\AppPools\SecureBootWatcher -Name "startMode" -Value "AlwaysRunning"
Set-ItemProperty IIS:\AppPools\SecureBootWatcher -Name "processModel.idleTimeout" -Value "00:00:00"

# Abilitare preload
Set-ItemProperty IIS:\AppPools\SecureBootWatcher -Name "processModel.loadUserProfile" -Value $true
```

### 5.2 Creare Sito Web IIS

```powershell
# Rimuovere Default Web Site (opzionale)
Remove-WebSite -Name "Default Web Site"

# Creare nuovo sito
New-WebSite -Name "SecureBootWatcher" `
    -Port 80 `
    -PhysicalPath "C:\SecureBootWatcher\App" `
    -ApplicationPool "SecureBootWatcher" `
    -Force

# Aggiungere binding HTTPS (porta 443)
New-WebBinding -Name "SecureBootWatcher" `
    -Protocol https `
    -Port 443 `
    -HostHeader "" `
    -SslFlags 0

# Configurare HTTPS (con certificato self-signed per test)
$cert = New-SelfSignedCertificate `
    -DnsName "localhost", "$env:COMPUTERNAME" `
    -CertStoreLocation "cert:\LocalMachine\My" `
    -NotAfter (Get-Date).AddYears(5)

# Bind certificato al sito
$binding = Get-WebBinding -Name "SecureBootWatcher" -Protocol "https"
$binding.AddSslCertificate($cert.GetCertHashString(), "my")
```

### 5.3 Configurare URL Rewrite per API Routes

Creare `web.config` nella root dell'applicazione (se non presente):

```xml
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
      <modules runAllManagedModulesForAllRequests="false">
        <remove name="WebDAVModule" />
      </modules>
    </system.webServer>
  </location>
</configuration>
```

### 5.4 Configurare Permessi Cartelle

```powershell
# Dare permessi al pool identity sulle cartelle
$appPoolSid = (Get-ItemProperty "IIS:\AppPools\SecureBootWatcher").processModel.userName
$identity = "IIS APPPOOL\SecureBootWatcher"

# Permessi su cartella Logs
$acl = Get-Acl "C:\SecureBootWatcher\Logs"
$rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
    $identity, "FullControl", "ContainerInherit,ObjectInherit", "None", "Allow"
)
$acl.SetAccessRule($rule)
Set-Acl "C:\SecureBootWatcher\Logs" $acl

# Permessi su cartella Data
$acl = Get-Acl "C:\SecureBootWatcher\Data"
$acl.SetAccessRule($rule)
Set-Acl "C:\SecureBootWatcher\Data" $acl
```

### 5.5 Applicare Migrazioni Database

```powershell
# Sul server, navigare nella cartella app
cd C:\SecureBootWatcher\App

# Verificare connection string
$env:ASPNETCORE_ENVIRONMENT = "Production"

# Applicare migrazioni usando l'assembly pubblicato
# Nota: potrebbe essere necessario installare dotnet-ef tool
dotnet tool install --global dotnet-ef

# Applicare migrazioni
dotnet ef database update --project "SecureBootDashboard.Api.dll"

# Oppure eseguire direttamente dall'app al primo avvio
# (l'app applicherà automaticamente le migrazioni pending)
```

### 5.6 Avviare il Sito

```powershell
# Avviare il sito IIS
Start-WebSite -Name "SecureBootWatcher"

# Verificare stato
Get-WebSite -Name "SecureBootWatcher"

# Test del sito
Start-Process "http://localhost"
Start-Process "https://localhost"
```

---

## 6. Configurazione Client

### 6.1 Preparare Client PowerShell

```powershell
# Copiare client PowerShell nella cartella Client
$clientSource = "C:\Users\nefario\source\repos\robgrame\Nimbus.BootCertWatcher"
$clientDest = "C:\SecureBootWatcher\Client"

# Copiare script PowerShell
Copy-Item "$clientSource\SecureBootWatcher-Client.ps1" -Destination $clientDest

# Copiare configurazione
Copy-Item "$clientSource\appsettings.powershell-client.json" -Destination "$clientDest\appsettings.json"
```

### 6.2 Configurare appsettings.json del Client

Modificare `C:\SecureBootWatcher\Client\appsettings.json`:

```json
{
  "SecureBootWatcher": {
    "FleetId": "LocalFleet",
    "RunMode": "Once",
    "RegistryPollInterval": "00:30:00",
    "EventQueryInterval": "00:30:00",
    "EventLookbackPeriod": "7.00:00:00",
    "EventChannels": [
      "Microsoft-Windows-SecureBoot-Servicing/Operational",
      "Microsoft-Windows-SecureBoot-State/Operational",
      "System"
    ],
    "Sinks": {
      "ExecutionStrategy": "FirstSuccess",
      "SinkPriority": "WebApi,FileShare",
      "EnableFileShare": false,
      "EnableWebApi": true,
      "EnableAzureFunction": false,
      "FileShare": {
        "RootPath": "C:\\SecureBootWatcher\\Data\\Reports",
        "FileExtension": ".json"
      },
      "WebApi": {
        "BaseAddress": "http://localhost",
        "IngestionRoute": "/api/SecureBootReports",
        "HttpTimeout": "00:02:00"
      },
      "AzureFunction": {
        "FunctionUrl": "",
        "ApiKey": "",
        "HttpTimeout": "00:02:00",
        "UseApiKeyAsQueryParameter": true
      }
    },
    "Commands": {
      "EnableCommandProcessing": true,
      "ProcessBeforeInventory": false,
      "MaxCommandsPerCycle": 10,
      "CommandExecutionDelay": "00:00:05",
      "ContinueOnCommandFailure": true
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    },
    "Console": {
      "Enabled": true
    },
    "File": {
      "Enabled": true,
      "Path": "logs/client.log",
      "RollingInterval": "Day",
      "RetainedFileCountLimit": 7
    }
  }
}
```

### 6.3 Test Client PowerShell

```powershell
# Eseguire test del client (come Administrator)
cd C:\SecureBootWatcher\Client

# Test esecuzione singola
.\SecureBootWatcher-Client.ps1 -RunMode Once -Verbose

# Verificare log
Get-Content "logs\client.log" -Tail 50
```

### 6.4 Creare Scheduled Task per Client

```powershell
# Creare Scheduled Task per esecuzione periodica
$action = New-ScheduledTaskAction `
    -Execute "PowerShell.exe" `
    -Argument "-NoProfile -ExecutionPolicy Bypass -File C:\SecureBootWatcher\Client\SecureBootWatcher-Client.ps1" `
    -WorkingDirectory "C:\SecureBootWatcher\Client"

$trigger = New-ScheduledTaskTrigger -Daily -At "09:00AM"

# Trigger aggiuntivo ogni 6 ore
$trigger2 = New-ScheduledTaskTrigger -Once -At "12:00PM" -RepetitionInterval (New-TimeSpan -Hours 6) -RepetitionDuration ([TimeSpan]::MaxValue)

$settings = New-ScheduledTaskSettingsSet `
    -ExecutionTimeLimit (New-TimeSpan -Hours 1) `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries `
    -StartWhenAvailable

# Creare task
Register-ScheduledTask `
    -TaskName "SecureBootWatcher Client" `
    -Action $action `
    -Trigger $trigger, $trigger2 `
    -Settings $settings `
    -User "SYSTEM" `
    -RunLevel Highest `
    -Description "SecureBootWatcher inventory collection"

# Verificare task
Get-ScheduledTask -TaskName "SecureBootWatcher Client"

# Eseguire manualmente per test
Start-ScheduledTask -TaskName "SecureBootWatcher Client"
```

---

## 7. Verifica e Test

### 7.1 Verificare Servizi

```powershell
# Verificare IIS
Get-WebSite -Name "SecureBootWatcher"
Get-WebAppPoolState -Name "SecureBootWatcher"

# Verificare SQL Server
Get-Service -Name "MSSQL`$SQLEXPRESS"

# Verificare porte aperte
netstat -ano | findstr ":80"
netstat -ano | findstr ":443"
netstat -ano | findstr ":1433"
```

### 7.2 Test API Endpoints

```powershell
# Test health endpoint
Invoke-RestMethod -Uri "http://localhost/health" -Method Get

# Test API status
Invoke-RestMethod -Uri "http://localhost/api/status" -Method Get

# Test invio report (dal client)
cd C:\SecureBootWatcher\Client
.\SecureBootWatcher-Client.ps1 -RunMode Once

# Verificare log applicazione
Get-Content "C:\SecureBootWatcher\Logs\app-*.log" -Tail 50
```

### 7.3 Test Dashboard Web

```powershell
# Aprire browser
Start-Process "http://localhost"

# URL da testare:
# - http://localhost/                 (Home page)
# - http://localhost/Devices/List     (Lista dispositivi)
# - http://localhost/Devices/Details  (Dettagli dispositivo)
# - http://localhost/Reports          (Report)
```

### 7.4 Verificare Database

```sql
-- Connettersi al database
USE SecureBootWatcher
GO

-- Verificare tabelle create
SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_TYPE = 'BASE TABLE'
GO

-- Verificare dispositivi registrati
SELECT COUNT(*) as DeviceCount FROM Devices
GO

-- Verificare ultimi report
SELECT TOP 10 
    d.MachineName,
    r.CreatedAtUtc,
    r.ClientVersion
FROM SecureBootReports r
INNER JOIN Devices d ON r.DeviceId = d.Id
ORDER BY r.CreatedAtUtc DESC
GO
```

---

## 8. Troubleshooting

### 8.1 Problemi Comuni

#### Errore: "HTTP Error 500.30 - ASP.NET Core app failed to start"

**Soluzione:**
```powershell
# Verificare log stdout
Get-Content "C:\SecureBootWatcher\App\logs\stdout*.log" -Tail 100

# Verificare permessi cartelle
icacls "C:\SecureBootWatcher\App"

# Verificare .NET Runtime installato
dotnet --list-runtimes

# Riavviare Application Pool
Restart-WebAppPool -Name "SecureBootWatcher"
```

#### Errore: "Connection to SQL Server failed"

**Soluzione:**
```powershell
# Verificare servizio SQL Server
Get-Service -Name "MSSQL`$SQLEXPRESS"
Start-Service -Name "MSSQL`$SQLEXPRESS"

# Test connessione
sqlcmd -S localhost\SQLEXPRESS -U SecureBootWatcherApp -P "StrongPassword123!@#" -Q "SELECT @@VERSION"

# Verificare firewall
Test-NetConnection -ComputerName localhost -Port 1433

# Verificare TCP/IP abilitato in SQL Configuration Manager
```

#### Errore: "Client non riesce a inviare report"

**Soluzione:**
```powershell
# Verificare connettività
Test-NetConnection -ComputerName localhost -Port 80

# Verificare URL nel appsettings.json del client
Get-Content "C:\SecureBootWatcher\Client\appsettings.json" | Select-String "BaseAddress"

# Test manuale invio HTTP
$report = @{
    Device = @{
        MachineName = "TEST"
        DomainName = "WORKGROUP"
    }
    CreatedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    ClientVersion = "1.14.0"
} | ConvertTo-Json -Depth 10

Invoke-RestMethod -Uri "http://localhost/api/SecureBootReports" `
    -Method Post `
    -Body $report `
    -ContentType "application/json"
```

#### SignalR non funziona

**Soluzione:**
```powershell
# Verificare WebSocket abilitato in IIS
Get-WindowsFeature -Name Web-WebSockets

# Installare se mancante
Install-WindowsFeature -Name Web-WebSockets

# Verificare nel browser (F12 Console):
# Dovrebbe connettersi a /dashboardHub

# Riavviare IIS
iisreset /restart
```

### 8.2 Log da Controllare

```powershell
# Log applicazione
Get-Content "C:\SecureBootWatcher\Logs\app-*.log" -Tail 100

# Log IIS stdout
Get-Content "C:\SecureBootWatcher\App\logs\stdout*.log" -Tail 100

# Event Viewer - Application Log
Get-EventLog -LogName Application -Source "IIS*" -Newest 50

# Event Viewer - System Log
Get-EventLog -LogName System -Newest 50 | Where-Object {$_.Source -like "*SQL*"}
```

### 8.3 Performance Tuning

```powershell
# Aumentare limiti Application Pool
Set-ItemProperty IIS:\AppPools\SecureBootWatcher `
    -Name "processModel.maxProcesses" -Value 4

Set-ItemProperty IIS:\AppPools\SecureBootWatcher `
    -Name "queueLength" -Value 5000

# Configurare recycling
Set-ItemProperty IIS:\AppPools\SecureBootWatcher `
    -Name "recycling.periodicRestart.time" -Value "1.05:00:00"

# Abilitare compression
Set-WebConfigurationProperty `
    -Filter "/system.webServer/httpCompression/scheme[@name='gzip']" `
    -Name "dll" `
    -Value "%Windir%\system32\inetsrv\gzip.dll" `
    -PSPath "IIS:\Sites\SecureBootWatcher"
```

---

## 9. Manutenzione

### 9.1 Backup Database

```powershell
# Script backup database
$backupPath = "C:\SecureBootWatcher\Backups"
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$backupFile = "$backupPath\SecureBootWatcher_$timestamp.bak"

# Backup via T-SQL
$query = @"
BACKUP DATABASE [SecureBootWatcher] 
TO DISK = N'$backupFile' 
WITH FORMAT, INIT, NAME = N'SecureBootWatcher Full Backup', SKIP, NOREWIND, NOUNLOAD, STATS = 10
"@

sqlcmd -S localhost\SQLEXPRESS -Q $query

# Creare scheduled task per backup giornaliero
$action = New-ScheduledTaskAction `
    -Execute "sqlcmd.exe" `
    -Argument "-S localhost\SQLEXPRESS -Q `"$query`""

$trigger = New-ScheduledTaskTrigger -Daily -At "02:00AM"

Register-ScheduledTask `
    -TaskName "SecureBootWatcher DB Backup" `
    -Action $action `
    -Trigger $trigger `
    -User "SYSTEM" `
    -Description "Daily database backup"
```

### 9.2 Pulizia Log Vecchi

```powershell
# Script pulizia log
$logPath = "C:\SecureBootWatcher\Logs"
$daysToKeep = 30

Get-ChildItem -Path $logPath -Recurse -File | 
    Where-Object {$_.LastWriteTime -lt (Get-Date).AddDays(-$daysToKeep)} |
    Remove-Item -Force

# Scheduled task per pulizia settimanale
$action = New-ScheduledTaskAction `
    -Execute "PowerShell.exe" `
    -Argument "-Command `"Get-ChildItem -Path '$logPath' -Recurse -File | Where-Object {`$_.LastWriteTime -lt (Get-Date).AddDays(-$daysToKeep)} | Remove-Item -Force`""

$trigger = New-ScheduledTaskTrigger -Weekly -DaysOfWeek Sunday -At "03:00AM"

Register-ScheduledTask `
    -TaskName "SecureBootWatcher Log Cleanup" `
    -Action $action `
    -Trigger $trigger `
    -User "SYSTEM"
```

### 9.3 Monitoraggio

```powershell
# Script monitoraggio salute
$healthCheck = @{
    Timestamp = Get-Date
    IISStatus = (Get-WebAppPoolState -Name "SecureBootWatcher").Value
    SQLStatus = (Get-Service -Name "MSSQL`$SQLEXPRESS").Status
    APIHealth = $null
    DiskSpace = (Get-PSDrive C).Free / 1GB
}

try {
    $response = Invoke-RestMethod -Uri "http://localhost/health" -TimeoutSec 10
    $healthCheck.APIHealth = "OK"
} catch {
    $healthCheck.APIHealth = "FAILED"
}

# Inviare notifica se problemi
if ($healthCheck.APIHealth -ne "OK" -or 
    $healthCheck.IISStatus -ne "Started" -or 
    $healthCheck.SQLStatus -ne "Running") {
    
    # Log errore
    $healthCheck | ConvertTo-Json | Out-File "C:\SecureBootWatcher\Logs\health-error.log" -Append
    
    # Invia email o altra notifica
    # Send-MailMessage -To "admin@company.com" -Subject "SecureBootWatcher Alert" -Body ($healthCheck | ConvertTo-Json)
}
```

### 9.4 Update Applicazione

```powershell
# Script update
function Update-SecureBootWatcher {
    param(
        [string]$SourcePath = "\\BuildServer\Releases\SecureBootWatcher\Latest"
    )
    
    # Stop IIS site
    Stop-WebSite -Name "SecureBootWatcher"
    Stop-WebAppPool -Name "SecureBootWatcher"
    
    # Backup corrente
    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $backupPath = "C:\SecureBootWatcher\Backups\App_$timestamp"
    Copy-Item "C:\SecureBootWatcher\App" -Destination $backupPath -Recurse
    
    # Backup database
    $dbBackup = "C:\SecureBootWatcher\Backups\DB_$timestamp.bak"
    $query = "BACKUP DATABASE [SecureBootWatcher] TO DISK = N'$dbBackup'"
    sqlcmd -S localhost\SQLEXPRESS -Q $query
    
    # Update files
    Copy-Item "$SourcePath\*" -Destination "C:\SecureBootWatcher\App" -Recurse -Force
    
    # Applicare migrazioni
    cd C:\SecureBootWatcher\App
    dotnet ef database update
    
    # Restart
    Start-WebAppPool -Name "SecureBootWatcher"
    Start-WebSite -Name "SecureBootWatcher"
    
    # Verify
    Start-Sleep -Seconds 10
    Invoke-RestMethod -Uri "http://localhost/health"
}

# Eseguire update
# Update-SecureBootWatcher
```

---

## 10. Checklist Post-Deployment

- [ ] IIS installato e configurato
- [ ] .NET 10 Runtime installato
- [ ] SQL Server installato e configurato
- [ ] Database creato e migrazioni applicate
- [ ] Applicazione pubblicata in C:\SecureBootWatcher\App
- [ ] Application Pool creato e configurato
- [ ] Sito IIS creato e avviato
- [ ] Certificato HTTPS configurato (anche self-signed)
- [ ] Permessi cartelle configurati correttamente
- [ ] Firewall configurato (porte 80, 443, 1433)
- [ ] Client PowerShell configurato
- [ ] Scheduled Task creato per client
- [ ] Test endpoint API funzionanti
- [ ] Dashboard web accessibile
- [ ] SignalR funzionante (real-time updates)
- [ ] Log applicazione configurati
- [ ] Backup database schedulato
- [ ] Pulizia log schedulata
- [ ] Monitoraggio configurato
- [ ] Documentazione deployment completata

---

## 11. Sicurezza - Best Practices

### 11.1 Hardening IIS

```powershell
# Rimuovere headers non necessari
Add-WebConfigurationProperty -PSPath "IIS:\Sites\SecureBootWatcher" `
    -Filter "system.webServer/httpProtocol/customHeaders" `
    -Name "." `
    -Value @{name='X-Content-Type-Options';value='nosniff'}

# Disabilitare directory browsing
Set-WebConfigurationProperty -PSPath "IIS:\Sites\SecureBootWatcher" `
    -Filter "system.webServer/directoryBrowse" `
    -Name "enabled" `
    -Value $false

# Request filtering
Set-WebConfigurationProperty -PSPath "IIS:\Sites\SecureBootWatcher" `
    -Filter "system.webServer/security/requestFiltering" `
    -Name "allowDoubleEscaping" `
    -Value $false
```

### 11.2 SQL Server Security

```sql
-- Disabilitare sa account (se non usato)
ALTER LOGIN sa DISABLE
GO

-- Configurare password policy
ALTER LOGIN SecureBootWatcherApp 
WITH CHECK_POLICY = ON, 
     CHECK_EXPIRATION = ON
GO

-- Audit login falliti
USE master
GO
EXEC sp_configure 'show advanced options', 1
RECONFIGURE
GO
EXEC sp_configure 'login auditing', 3
RECONFIGURE
GO
```

### 11.3 Firewall Regole

```powershell
# Limitare accesso SQL Server solo da localhost
New-NetFirewallRule -DisplayName "SQL Server - Allow Localhost" `
    -Direction Inbound `
    -Protocol TCP `
    -LocalPort 1433 `
    -RemoteAddress 127.0.0.1 `
    -Action Allow

# Bloccare tutto il resto
New-NetFirewallRule -DisplayName "SQL Server - Block All" `
    -Direction Inbound `
    -Protocol TCP `
    -LocalPort 1433 `
    -Action Block
```

---

## Supporto e Riferimenti

**Documentazione:**
- [ASP.NET Core Deployment](https://docs.microsoft.com/aspnet/core/host-and-deploy/iis/)
- [SQL Server Installation](https://docs.microsoft.com/sql/database-engine/install-windows/)
- [IIS Configuration](https://docs.microsoft.com/iis/)

**Versione:** 1.14 (Unified Web+API)
**Data:** Dicembre 2024
**Branch:** copilot/merge-web-app-and-api

---

Per assistenza aggiuntiva, consultare:
- `docs/TROUBLESHOOTING_PORTS.md`
- `docs/RELEASE_NOTES_*.md`
- Repository GitHub: https://github.com/robgrame/Nimbus.BootCertWatcher
