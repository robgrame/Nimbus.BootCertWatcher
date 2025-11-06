# Secure Boot Dashboard - Guida al Deployment

## Architettura Deployata

```
???????????????????????????????????????????
?  Utenti (Browser)                       ?
?  https://securebootdashboard.local      ?
???????????????????????????????????????????
                  ?
                  ?
???????????????????????????????????????????
?  IIS - SecureBootDashboard.Web          ?
?  Frontend Razor Pages                   ?
?  Port 443 (HTTPS)                       ?
?  C:\inetpub\SecureBootDashboard.Web     ?
???????????????????????????????????????????
                  ? HTTP/HTTPS
                  ?
???????????????????????????????????????????
?  IIS - SecureBootDashboard.Api          ?
?  Backend REST API                       ?
?  Port 5001 (HTTPS)                      ?
?  C:\inetpub\SecureBootDashboard.Api     ?
???????????????????????????????????????????
                  ?
                  ?
???????????????????????????????????????????
?  SQL Server (SRVSQL)                    ?
?  Database: SecureBootDashboard          ?
???????????????????????????????????????????
```

## Prerequisiti Server

- Windows Server 2016+ o Windows 10/11
- IIS con ASP.NET Core Hosting Bundle 8.0
- .NET 8 Runtime (ASP.NET Core Runtime)
- SQL Server (locale o remoto)
- Certificati SSL per HTTPS

## 1. Installazione Prerequisiti

### A. Installa IIS

```powershell
# Installa IIS e funzionalità necessarie
Install-WindowsFeature -Name Web-Server, Web-Asp-Net45, Web-Windows-Auth
```

### B. Installa ASP.NET Core Hosting Bundle

1. Scarica da: https://dotnet.microsoft.com/download/dotnet/8.0
2. Cerca "Hosting Bundle" e installa
3. Riavvia IIS: `iisreset`

### C. Verifica Installazione

```powershell
dotnet --list-runtimes
# Deve mostrare Microsoft.AspNetCore.App 8.0.x
```

## 2. Pubblicazione dei Progetti

### A. Pubblica l'API

```powershell
cd C:\Users\nefario\source\repos\robgrame\Nimbus.BootCertWatcher

dotnet publish SecureBootDashboard.Api\SecureBootDashboard.Api.csproj `
  -c Release `
  -o C:\Deploy\SecureBootDashboard.Api
```

### B. Pubblica il Web

```powershell
dotnet publish SecureBootDashboard.Web\SecureBootDashboard.Web.csproj `
  -c Release `
  -o C:\Deploy\SecureBootDashboard.Web
```

## 3. Configura SQL Server

### A. Crea Database

```sql
CREATE DATABASE SecureBootDashboard;
GO
```

### B. Applica Migrazioni

```powershell
# Dal PC di sviluppo, con connection string temporanea al server
cd SecureBootDashboard.Api

# Modifica appsettings.json temporaneamente con la connection string del server
dotnet ef database update
```

### C. Crea Login per Application Pool

```sql
USE [master]
GO
CREATE LOGIN [IIS APPPOOL\SecureBootDashboard.Api] FROM WINDOWS
GO

USE [SecureBootDashboard]
GO
CREATE USER [IIS APPPOOL\SecureBootDashboard.Api] FOR LOGIN [IIS APPPOOL\SecureBootDashboard.Api]
GO
ALTER ROLE [db_datareader] ADD MEMBER [IIS APPPOOL\SecureBootDashboard.Api]
ALTER ROLE [db_datawriter] ADD MEMBER [IIS APPPOOL\SecureBootDashboard.Api]
GO
```

## 4. Configura IIS - API

### A. Crea Application Pool

```powershell
Import-Module WebAdministration

New-WebAppPool -Name "SecureBootDashboard.Api"
Set-ItemProperty IIS:\AppPools\SecureBootDashboard.Api -Name managedRuntimeVersion -Value ""
Set-ItemProperty IIS:\AppPools\SecureBootDashboard.Api -Name processModel.identityType -Value 2
```

### B. Copia File

```powershell
# Crea directory
New-Item -Path "C:\inetpub\SecureBootDashboard.Api" -ItemType Directory -Force

# Copia file pubblicati
Copy-Item -Path "C:\Deploy\SecureBootDashboard.Api\*" `
  -Destination "C:\inetpub\SecureBootDashboard.Api" `
  -Recurse -Force
```

### C. Configura appsettings.json

Modifica `C:\inetpub\SecureBootDashboard.Api\appsettings.json`:

```json
{
  "ConnectionStrings": {
    "SqlServer": "Server=SRVSQL;Database=SecureBootDashboard;Trusted_Connection=True;TrustServerCertificate=True"
  },
  "Storage": {
    "Provider": "EfCore"
  },
  "QueueProcessor": {
    "Enabled": false
  }
}
```

### D. Crea Sito IIS

```powershell
New-Website -Name "SecureBootDashboard.Api" `
  -PhysicalPath "C:\inetpub\SecureBootDashboard.Api" `
  -ApplicationPool "SecureBootDashboard.Api" `
  -Port 5001 `
  -Ssl

# Binding con certificato (sostituisci con il tuo thumbprint)
$cert = Get-ChildItem Cert:\LocalMachine\My | Where-Object {$_.Subject -like "*secureboot*"}
New-WebBinding -Name "SecureBootDashboard.Api" -Protocol https -Port 5001 -SslFlags 0
$binding = Get-WebBinding -Name "SecureBootDashboard.Api" -Protocol https
$binding.AddSslCertificate($cert.Thumbprint, "My")
```

### E. Configura Permessi

```powershell
icacls "C:\inetpub\SecureBootDashboard.Api" `
  /grant "IIS AppPool\SecureBootDashboard.Api:(OI)(CI)F" /T
```

## 5. Configura IIS - Web

### A. Crea Application Pool

```powershell
New-WebAppPool -Name "SecureBootDashboard.Web"
Set-ItemProperty IIS:\AppPools\SecureBootDashboard.Web -Name managedRuntimeVersion -Value ""
```

### B. Copia File

```powershell
New-Item -Path "C:\inetpub\SecureBootDashboard.Web" -ItemType Directory -Force

Copy-Item -Path "C:\Deploy\SecureBootDashboard.Web\*" `
  -Destination "C:\inetpub\SecureBootDashboard.Web" `
  -Recurse -Force
```

### C. Configura appsettings.json

Modifica `C:\inetpub\SecureBootDashboard.Web\appsettings.json`:

```json
{
  "ApiSettings": {
    "BaseUrl": "https://localhost:5001"
  }
}
```

O crea `appsettings.Production.json`:

```json
{
  "ApiSettings": {
    "BaseUrl": "https://api.securebootdashboard.local"
  }
}
```

### D. Crea Sito IIS

```powershell
New-Website -Name "SecureBootDashboard.Web" `
  -PhysicalPath "C:\inetpub\SecureBootDashboard.Web" `
  -ApplicationPool "SecureBootDashboard.Web" `
  -Port 443 `
  -Ssl

# Binding con hostname
New-WebBinding -Name "SecureBootDashboard.Web" `
  -Protocol https `
  -Port 443 `
  -HostHeader "securebootdashboard.local"

# Associa certificato
$cert = Get-ChildItem Cert:\LocalMachine\My | Where-Object {$_.Subject -like "*secureboot*"}
$binding = Get-WebBinding -Name "SecureBootDashboard.Web" -Protocol https
$binding.AddSslCertificate($cert.Thumbprint, "My")
```

### E. Configura Permessi

```powershell
icacls "C:\inetpub\SecureBootDashboard.Web" `
  /grant "IIS AppPool\SecureBootDashboard.Web:(OI)(CI)F" /T
```

## 6. Configura DNS/Hosts

### Sul Server

Aggiungi a `C:\Windows\System32\drivers\etc\hosts`:

```
127.0.0.1 securebootdashboard.local
127.0.0.1 api.securebootdashboard.local
```

### Su Client

Aggiungi l'IP del server:

```
192.168.1.100 securebootdashboard.local
192.168.1.100 api.securebootdashboard.local
```

## 7. Test

### A. Testa API

```powershell
Invoke-WebRequest -Uri "https://localhost:5001/health" -UseBasicParsing
# Deve rispondere con 200 OK
```

### B. Testa Web

Apri browser e vai su: `https://securebootdashboard.local`

## 8. Troubleshooting

### Errore 500.19 - Configuration Error

**Problema:** ASP.NET Core Hosting Bundle non installato

**Soluzione:**
```powershell
# Scarica e installa Hosting Bundle
# Poi riavvia IIS
iisreset
```

### Errore 500.30 - ANCM In-Process Start Failure

**Problema:** Errore nell'applicazione o manca il runtime

**Soluzione:**
1. Verifica i log in `C:\inetpub\SecureBootDashboard.Api\logs`
2. Controlla `appsettings.json`
3. Verifica connection string

### Errore Database Connection

**Problema:** Application Pool identity non ha permessi su SQL

**Soluzione:**
```sql
-- Esegui su SQL Server
CREATE LOGIN [IIS APPPOOL\SecureBootDashboard.Api] FROM WINDOWS
USE [SecureBootDashboard]
CREATE USER [IIS APPPOOL\SecureBootDashboard.Api] FOR LOGIN [IIS APPPOOL\SecureBootDashboard.Api]
ALTER ROLE [db_datareader] ADD MEMBER [IIS APPPOOL\SecureBootDashboard.Api]
ALTER ROLE [db_datawriter] ADD MEMBER [IIS APPPOOL\SecureBootDashboard.Api]
```

### Web non Raggiunge API

**Problema:** BaseUrl non corretto o firewall

**Soluzione:**
1. Verifica `ApiSettings:BaseUrl` in `appsettings.json`
2. Testa connettività: `Test-NetConnection localhost -Port 5001`
3. Controlla firewall Windows

## 9. Manutenzione

### Aggiornamento Applicazione

```powershell
# Stop siti
Stop-Website "SecureBootDashboard.Web"
Stop-Website "SecureBootDashboard.Api"

# Pubblica nuove versioni
dotnet publish [...] -o C:\Deploy\...

# Copia file
Copy-Item -Path "C:\Deploy\..." -Destination "C:\inetpub\..." -Recurse -Force

# Start siti
Start-Website "SecureBootDashboard.Api"
Start-Website "SecureBootDashboard.Web"
```

### Backup Database

```sql
BACKUP DATABASE [SecureBootDashboard]
TO DISK = 'C:\Backup\SecureBootDashboard.bak'
WITH FORMAT, INIT, COMPRESSION;
```

### Log Monitoring

```powershell
# API logs
Get-Content "C:\inetpub\SecureBootDashboard.Api\logs\*.log" -Tail 50

# IIS logs
Get-Content "C:\inetpub\logs\LogFiles\W3SVC*\*.log" -Tail 50
```

## 10. Sicurezza

### HTTPS Obbligatorio

Entrambi i siti sono configurati solo per HTTPS.

### Application Pool Identity

Usa identità con privilegi minimi necessari.

### Firewall

```powershell
# Apri solo le porte necessarie
New-NetFirewallRule -DisplayName "SecureBootDashboard API" `
  -Direction Inbound -LocalPort 5001 -Protocol TCP -Action Allow

New-NetFirewallRule -DisplayName "SecureBootDashboard Web" `
  -Direction Inbound -LocalPort 443 -Protocol TCP -Action Allow
```

## Supporto

Per problemi o domande, consultare la documentazione principale nel README.md del repository.
