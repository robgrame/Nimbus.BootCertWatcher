# 🚀 Quick Start - SecureBootWatcher Local Deployment

Guida rapida per deployment locale in 15 minuti.

## ⚡ Deployment Automatico (Raccomandato)

### Step 1: Prerequisiti Rapidi

```powershell
# Eseguire PowerShell come Amministratore

# Verificare versioni
dotnet --version              # Dovrebbe essere 10.x
$PSVersionTable.PSVersion     # Dovrebbe essere 5.1+

# Verificare servizi
Get-Service -Name "W3SVC"              # IIS
Get-Service -Name "MSSQL$SQLEXPRESS"   # SQL Server
```

### Step 2: Eseguire Deployment

```powershell
# Navigare nella cartella scripts
cd C:\Users\nefario\source\repos\robgrame\Nimbus.BootCertWatcher\scripts

# Eseguire deployment completo
.\Deploy-LocalServer.ps1 -All

# Oppure step singoli:
.\Deploy-LocalServer.ps1 -InstallPrerequisites
.\Deploy-LocalServer.ps1 -ConfigureDatabase
.\Deploy-LocalServer.ps1 -DeployApplication
.\Deploy-LocalServer.ps1 -ConfigureClient
```

### Step 3: Verifica

```powershell
# Aprire browser
Start-Process "http://localhost"

# Test API
Invoke-RestMethod -Uri "http://localhost/health"

# Test client
cd C:\SecureBootWatcher\Client
.\SecureBootWatcher-Client.ps1 -RunMode Once
```

---

## 🔧 Deployment Manuale (Step by Step)

### 1. Installa Prerequisiti (5 min)

```powershell
# IIS
Install-WindowsFeature -Name Web-Server -IncludeManagementTools

# .NET 10 Hosting Bundle
# Download: https://dotnet.microsoft.com/download/dotnet/10.0
# Installer: dotnet-hosting-10.x.x-win.exe

# Riavvia IIS
net stop was /y
net start w3svc
```

### 2. Configura SQL Server (3 min)

```powershell
# Crea database
$query = @"
CREATE DATABASE SecureBootWatcher
GO
USE SecureBootWatcher
GO
CREATE LOGIN SecureBootWatcherApp WITH PASSWORD = 'YourPassword123!'
CREATE USER SecureBootWatcherApp FOR LOGIN SecureBootWatcherApp
ALTER ROLE db_owner ADD MEMBER SecureBootWatcherApp
GO
"@

sqlcmd -S localhost\SQLEXPRESS -Q $query
```

### 3. Build & Publish (2 min)

```powershell
cd C:\Users\nefario\source\repos\robgrame\Nimbus.BootCertWatcher

# Build Release
dotnet publish SecureBootDashboard.Web/SecureBootDashboard.Web.csproj `
    -c Release `
    -o C:\SecureBootWatcher\App
```

**Nota:** Il progetto `SecureBootDashboard.Web` è l'applicazione unificata che contiene sia l'interfaccia web che gli endpoint API.

### 4. Configura IIS (3 min)

```powershell
Import-Module WebAdministration

# Crea App Pool
New-WebAppPool -Name "SecureBootWatcher"
Set-ItemProperty IIS:\AppPools\SecureBootWatcher -Name managedRuntimeVersion -Value ""

# Crea Sito
New-WebSite -Name "SecureBootWatcher" `
    -Port 80 `
    -PhysicalPath "C:\SecureBootWatcher\App" `
    -ApplicationPool "SecureBootWatcher"

# Start
Start-WebSite -Name "SecureBootWatcher"
```

### 5. Test (2 min)

```powershell
# Test web
Start-Process "http://localhost"

# Test API
Invoke-RestMethod -Uri "http://localhost/health"
```

---

## 📝 Configurazione Minima appsettings.Production.json

Crea questo file in `C:\SecureBootWatcher\App\appsettings.Production.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost\\SQLEXPRESS;Database=SecureBootWatcher;User Id=SecureBootWatcherApp;Password=YourPassword123!;TrustServerCertificate=True"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "ApplicationSettings": {
    "RequireMutualTls": false,
    "EnableSignalR": true
  }
}
```

---

## 🎯 URL Importanti

| Servizio | URL |
|----------|-----|
| Dashboard | http://localhost |
| API Health | http://localhost/health |
| Swagger API | http://localhost/swagger |
| Devices List | http://localhost/Devices/List |

---

## 🔍 Troubleshooting Rapido

### Errore: "HTTP 500.30"
```powershell
# Verifica runtime
dotnet --list-runtimes

# Verifica log
Get-Content "C:\SecureBootWatcher\App\logs\stdout*.log" -Tail 50
```

### Errore: "Cannot connect to SQL Server"
```powershell
# Verifica servizio
Get-Service -Name "MSSQL$SQLEXPRESS" | Start-Service

# Test connessione
sqlcmd -S localhost\SQLEXPRESS -U SecureBootWatcherApp -P "YourPassword123!"
```

### Errore: "IIS not responding"
```powershell
# Restart IIS
iisreset /restart

# Verifica sito
Get-WebSite -Name "SecureBootWatcher"
Get-WebAppPoolState -Name "SecureBootWatcher"
```

---

## 📊 Verifica Deployment

Checklist rapida:

```powershell
# 1. Verifica IIS
Get-WebSite -Name "SecureBootWatcher" | Select-Object Name, State, PhysicalPath

# 2. Verifica SQL
sqlcmd -S localhost\SQLEXPRESS -Q "SELECT name FROM sys.databases WHERE name='SecureBootWatcher'"

# 3. Test API
Invoke-RestMethod -Uri "http://localhost/health"

# 4. Test Dashboard
Start-Process "http://localhost"

# 5. Verifica Client
Test-Path "C:\SecureBootWatcher\Client\SecureBootWatcher-Client.ps1"
```

Se tutti i test passano: ✅ **Deployment Completato!**

---

## 🚀 Prossimi Passi

1. **Configura Client su dispositivi**: 
   - Copia `C:\SecureBootWatcher\Client\*` sui dispositivi
   - Esegui client manualmente o via GPO/Intune

2. **Monitora Dashboard**:
   - Accedi a http://localhost/Devices/List
   - Verifica report in arrivo

3. **Configura Backup**:
   - Setup backup SQL giornaliero
   - Retention policy log files

4. **Sicurezza**:
   - Configura HTTPS con certificato valido
   - Restrizioni firewall se necessario
   - Strong password SQL Server

---

## 📚 Documentazione Completa

Per configurazioni avanzate, vedere:
- `docs/DEPLOYMENT_GUIDE_LOCAL.md` - Guida completa
- `scripts/Deploy-LocalServer.ps1` - Script automatizzato

---

**Versione**: 1.14 (Unified Web+API)  
**Branch**: copilot/merge-web-app-and-api  
**Tempo Stimato**: 15 minuti
