# ?? API Startup Troubleshooting Guide

## ? Problema: HostAbortedException

### Sintomi
```
[FTL] SecureBootDashboard.Api terminated unexpectedly
Microsoft.Extensions.Hosting.HostAbortedException: The host was aborted.
   at Microsoft.Extensions.Hosting.HostFactoryResolver.HostingListener.ThrowHostAborted()
```

### Causa
L'applicazione viene interrotta durante lo startup, **prima** che il web server venga avviato. Questo indica un problema di configurazione o dipendenze.

---

## ?? Diagnosi Step-by-Step

### 1. Verifica Log Startup

Cerca nei log (console o file) questi messaggi **in ordine**:

```
? [INF] Starting SecureBootDashboard.Api application
? [INF] Configuration Sources: ...
? [INF] SQL Server Connection: ...
? [INF] Configuring DbContext...
? [INF] Configuring Storage services...
? [INF] Configuring Queue Processor...
? [INF] Queue Processor Enabled: True
? [INF] Queue URI: https://secbootcert.queue.core.windows.net
? [INF] Queue Name: secureboot-reports
? [INF] Auth Method: Certificate
? [INF] Building WebApplication...    ? SE IL CRASH AVVIENE QUI, VEDI SOTTO
? [INF] SecureBootDashboard.Api started successfully
```

**Se il crash avviene prima di "Building WebApplication..."**:
? Problema di configurazione (vedi sezione Configurazione)

**Se il crash avviene durante "Building WebApplication..."**:
? Problema con dipendenze o hosted services (vedi sezione Dipendenze)

---

## ?? Causa 1: QueueServiceUri Errato

### Problema
```json
"QueueServiceUri": "https://secbootcert.queue.core.windows.net/secureboot-reports"
```

? **L'URI include il nome della queue!**

### Soluzione
```json
"QueueServiceUri": "https://secbootcert.queue.core.windows.net"
```

Il nome della queue viene aggiunto dal codice tramite il parametro `QueueName`.

---

## ?? Causa 2: SQL Server Non Raggiungibile

### Problema
```
Server=SRVSQL;Database=SecureBootDashboard;...
```

Il server SQL potrebbe essere:
- Spento
- Non raggiungibile dalla macchina
- Nome errato

### Diagnosi

```powershell
# Test connettività SQL Server
Test-NetConnection -ComputerName SRVSQL -Port 1433

# Se usa named instance
Test-NetConnection -ComputerName SRVSQL -Port 1434
```

**Output atteso:**
```
TcpTestSucceeded : True ?
```

**Se False:**
- Verifica che SQL Server sia avviato
- Verifica firewall rules
- Verifica che TCP/IP sia abilitato in SQL Server Configuration Manager

### Soluzione Temporanea: Disabilita EF Core

Se vuoi testare senza SQL Server:

```json
{
  "Storage": {
    "Provider": "File",
    "File": {
      "BasePath": "C:\\Temp\\SecureBootReports"
    }
  }
}
```

---

## ?? Causa 3: Database Non Esiste

### Problema
Il database `SecureBootDashboard` non esiste su SQL Server.

### Diagnosi

```sql
-- Connettiti a SQL Server e verifica
SELECT name FROM sys.databases WHERE name = 'SecureBootDashboard';
```

**Se vuoto:**
Il database non esiste.

### Soluzione

```powershell
# Applica migrazioni EF Core
cd SecureBootDashboard.Api
dotnet ef database update

# Output atteso:
# Build succeeded.
# Applying migration '20251105093532_InitialCreate'.
# Done.
```

**Verifica:**
```sql
USE SecureBootDashboard;
SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES;
-- Output atteso: Devices, SecureBootReports, SecureBootEvents
```

---

## ?? Causa 4: Certificate Non Trovato

### Problema
Il QueueProcessorService prova a caricare il certificato ma non lo trova.

### Diagnosi

```powershell
# Verifica certificato
$thumbprint = "61FC110D5BABD61419B106862B304C2FFF57A262"
Get-ChildItem Cert:\LocalMachine\My | Where-Object { $_.Thumbprint -eq $thumbprint }
```

**Output atteso:**
```
Thumbprint: 61FC110D5BABD61419B106862B304C2FFF57A262 ?
Subject: CN=Azure Queue Client
```

**Se vuoto:**
Certificato non installato.

### Soluzione

**Opzione 1: Installa Certificato**
```powershell
# Importa .pfx
$password = ConvertTo-SecureString -String "YourPassword" -Force -AsPlainText
Import-PfxCertificate -FilePath "C:\Certs\QueueClient.pfx" `
    -CertStoreLocation Cert:\LocalMachine\My `
    -Password $password
```

**Opzione 2: Disabilita Queue Processor Temporaneamente**
```json
{
  "QueueProcessor": {
    "Enabled": false
  }
}
```

---

## ?? Causa 5: Porta Già in Uso

### Problema
```
[FTL] Failed to bind to address https://localhost:5000
System.IO.IOException: The configured user limit (128) on the number of inotify instances has been exceeded
```

### Diagnosi

```powershell
# Windows: Verifica porte in uso
netstat -ano | findstr :5000
netstat -ano | findstr :5001

# Output atteso (nessuno in ascolto):
# (vuoto) ?

# Se vedi output:
# TCP    0.0.0.0:5000    0.0.0.0:0    LISTENING    12345
```

### Soluzione

**Opzione 1: Cambia Porta**
```json
{
  "Urls": "https://localhost:5050;http://localhost:5051"
}
```

**Opzione 2: Termina Processo**
```powershell
# Windows
$processId = 12345  # ID del processo dal comando netstat
Stop-Process -Id $processId -Force
```

---

## ?? Causa 6: Permessi Certificato

### Problema
L'API gira sotto un account (es. IIS APPPOOL) che non ha accesso alla chiave privata del certificato.

### Diagnosi

```powershell
# Trova chiave privata
$cert = Get-ChildItem Cert:\LocalMachine\My | Where-Object { $_.Thumbprint -eq "61FC110D5BABD61419B106862B304C2FFF57A262" }
$rsaCert = [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($cert)
$keyPath = "C:\ProgramData\Microsoft\Crypto\Keys\$($rsaCert.Key.UniqueName)"

# Verifica permessi
icacls $keyPath

# Output atteso:
# IIS APPPOOL\SecureBootDashboard.Api:(R) ?
```

### Soluzione

```powershell
# Garantisci accesso
$appPoolIdentity = "IIS APPPOOL\SecureBootDashboard.Api"
icacls $keyPath /grant "${appPoolIdentity}:R"
```

---

## ? Checklist Pre-Avvio

Prima di avviare l'API, verifica:

- [ ] SQL Server è raggiungibile (`Test-NetConnection SRVSQL -Port 1433`)
- [ ] Database esiste (`SELECT * FROM sys.databases WHERE name='SecureBootDashboard'`)
- [ ] Migrazioni applicate (`dotnet ef migrations list` mostra `(Applied)`)
- [ ] `QueueServiceUri` **NON** include il nome della queue
- [ ] Se `QueueProcessor.Enabled = true`:
  - [ ] Certificato installato in `LocalMachine\My`
  - [ ] Certificato ha chiave privata
  - [ ] Account di esecuzione ha accesso alla chiave
- [ ] Porte non in uso (`netstat -ano | findstr :5000`)

---

## ?? Test Rapido: Configurazione Minima

Se vuoi testare l'API **senza dipendenze esterne**:

```json
{
  "ConnectionStrings": {
    "SqlServer": "Server=(localdb)\\mssqllocaldb;Database=SecureBootTest;Integrated Security=True"
  },
  "Storage": {
    "Provider": "File",
    "File": {
      "BasePath": "C:\\Temp\\Reports"
    }
  },
  "QueueProcessor": {
    "Enabled": false
  }
}
```

Poi:
```powershell
# Crea database LocalDB
sqllocaldb create "MSSQLLocalDB"
sqllocaldb start "MSSQLLocalDB"

# Applica migrations
dotnet ef database update

# Avvia API
dotnet run
```

**Output atteso:**
```
[INF] SecureBootDashboard.Api started successfully ?
Now listening on: https://localhost:5000 ?
```

---

## ?? Log Avvio Completo (Esempio Successo)

```
[12:50:00 INF] ========================================
[12:50:00 INF] Starting SecureBootDashboard.Api application
[12:50:00 INF] ========================================
[12:50:00 INF] Environment: Development
[12:50:00 INF] Base Directory: C:\...\SecureBootDashboard.Api\bin\Debug\net8.0
[12:50:00 INF] Machine Name: SRVCM00
[12:50:00 INF] Configuration Sources:
[12:50:00 INF]   - Microsoft.Extensions.Configuration.Json.JsonConfigurationSource
[12:50:00 INF]   - Microsoft.Extensions.Configuration.Json.JsonConfigurationSource
[12:50:00 INF] SQL Server Connection: Server=SRVSQL;Database=SecureBootDashboard;...
[12:50:00 INF] Configuring DbContext...
[12:50:00 INF] Configuring Storage services...
[12:50:00 INF] Configuring Queue Processor...
[12:50:00 INF] Queue Processor Enabled: True
[12:50:00 INF]   Queue URI: https://secbootcert.queue.core.windows.net
[12:50:00 INF]   Queue Name: secureboot-reports
[12:50:00 INF]   Auth Method: Certificate
[12:50:00 INF] Building WebApplication...
[12:50:01 INF] Configured URLs: https://localhost:5000;http://localhost:5001
[12:50:01 INF] Swagger enabled at: /swagger
[12:50:01 INF] ========================================
[12:50:01 INF] SecureBootDashboard.Api started successfully
[12:50:01 INF] ========================================
[12:50:01 INF] Now listening on: https://localhost:5000
[12:50:01 INF] Now listening on: http://localhost:5001
```

---

## ?? Se Niente Funziona

1. **Cattura Stack Trace Completo**

```powershell
# Abilita debug logging
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --verbosity detailed 2>&1 | Tee-Object -FilePath startup-error.log
```

2. **Test Componenti Singolarmente**

```csharp
// Test solo DbContext
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<SecureBootDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SqlServer")));
var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SecureBootDbContext>();
    Console.WriteLine($"Database can connect: {db.Database.CanConnect()}");
}
```

3. **Contatta Support**

Includi:
- Log completo startup
- Output `dotnet --info`
- Output `netstat -ano`
- SQL Server version: `SELECT @@VERSION`

---

## ?? Quick Reference

| Errore | Causa Probabile | Fix |
|--------|----------------|-----|
| Host aborted before "Building WebApplication" | Configurazione | Verifica appsettings.json |
| Host aborted durante "Building WebApplication" | Hosted Service | Disabilita QueueProcessor |
| "No DbContext found" | EF Core | `dotnet tool install --global dotnet-ef` |
| "Cannot connect to SQL Server" | Network/SQL | `Test-NetConnection SRVSQL -Port 1433` |
| "Certificate not found" | Certificato | Installa cert in LocalMachine\My |
| "Port already in use" | Porta occupata | Cambia porta in Urls |

---

**Riprova l'avvio e controlla i nuovi log dettagliati!** ??
