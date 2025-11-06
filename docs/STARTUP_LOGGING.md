# Enhanced Startup Logging - Documentation

## ?? Nuove Informazioni Loggate all'Avvio

Ho aggiornato entrambi i progetti (API e Web) per loggare informazioni dettagliate all'avvio dell'applicazione.

---

## ?? Esempio Output Log API

Quando avvii **SecureBootDashboard.Api**, vedrai questi log:

```
========================================
Starting SecureBootDashboard.Api application
========================================
Environment: Production
Base Directory: C:\inetpub\SecureBootDashboard.Api
Log Path: C:\inetpub\SecureBootDashboard.Api\logs\api-20250115.log
Machine Name: SRVSQL
User: SYSTEM
.NET Version: 8.0.0
Configuration Sources:
  - Microsoft.Extensions.Configuration.Json.JsonConfigurationSource
  - Microsoft.Extensions.Configuration.Json.JsonConfigurationSource
  - Microsoft.Extensions.Configuration.EnvironmentVariables.EnvironmentVariablesConfigurationSource
  - Microsoft.Extensions.Configuration.CommandLine.CommandLineConfigurationSource
SQL Server Connection: Server=SRVSQL;Database=SecureBootDashboard;Trusted_Connection=True;Password=***MASKED***
Storage Provider: EfCore
Configured URLs: https://localhost:7120;http://localhost:5027
========================================
SecureBootDashboard.Api started successfully
========================================
Queue processor starting. Queue: secureboot-reports, AuthMethod: Certificate
Loaded certificate from store. Thumbprint: 61FC110D5BABD61419B106862B304C2FFF57A262
Using Certificate-based authentication with Client ID: c8034569-4990-4823-9f1d-b46223789c35
Queue processor started successfully.
```

### Informazioni Loggate (API)

| Campo | Descrizione | Esempio |
|-------|-------------|---------|
| **Environment** | Ambiente di esecuzione | `Production`, `Development` |
| **Base Directory** | Directory di installazione | `C:\inetpub\SecureBootDashboard.Api` |
| **Log Path** | Percorso file log | `C:\inetpub\...\logs\api-20250115.log` |
| **Machine Name** | Nome del server | `SRVSQL` |
| **User** | Utente che esegue l'app | `SYSTEM`, `IIS APPPOOL\...` |
| **.NET Version** | Versione runtime .NET | `8.0.0` |
| **Configuration Sources** | Fonti di configurazione | JSON files, Environment vars, Command line |
| **SQL Server Connection** | Connection string (password mascherata) | `Server=SRVSQL;...;Password=***MASKED***` |
| **Storage Provider** | Provider di storage usato | `EfCore` o `File` |
| **File Storage Base Path** | Path per file storage (se usato) | `R:\Nimbus.SecureBootCert\...` |
| **Configured URLs** | Porte configurate | `https://localhost:7120;...` |
| **Swagger** | Se Swagger è abilitato | `Swagger enabled at: /swagger` |

---

## ?? Esempio Output Log Web

Quando avvii **SecureBootDashboard.Web**, vedrai questi log:

```
========================================
Starting SecureBootDashboard.Web application
========================================
Environment: Production
Base Directory: C:\inetpub\SecureBootDashboard.Web
Log Path: C:\inetpub\SecureBootDashboard.Web\logs\web-20250115.log
Machine Name: SRVSQL
User: SYSTEM
.NET Version: 8.0.0
Configuration Sources:
  - Microsoft.Extensions.Configuration.Json.JsonConfigurationSource
  - Microsoft.Extensions.Configuration.Json.JsonConfigurationSource
  - Microsoft.Extensions.Configuration.EnvironmentVariables.EnvironmentVariablesConfigurationSource
API Base URL: https://localhost:7120
Configured URLs: https://localhost:7001;http://localhost:5174
HSTS enabled
========================================
SecureBootDashboard.Web started successfully
========================================
```

### Informazioni Loggate (Web)

| Campo | Descrizione | Esempio |
|-------|-------------|---------|
| **Environment** | Ambiente di esecuzione | `Production`, `Development` |
| **Base Directory** | Directory di installazione | `C:\inetpub\SecureBootDashboard.Web` |
| **Log Path** | Percorso file log | `C:\inetpub\...\logs\web-20250115.log` |
| **Machine Name** | Nome del server | `SRVSQL` |
| **User** | Utente che esegue l'app | `SYSTEM`, `IIS APPPOOL\...` |
| **.NET Version** | Versione runtime .NET | `8.0.0` |
| **Configuration Sources** | Fonti di configurazione | JSON files, Environment vars |
| **API Base URL** | URL dell'API backend | `https://localhost:7120` |
| **Configured URLs** | Porte configurate per il Web | `https://localhost:7001;...` |
| **HSTS** | Se HSTS è abilitato | `HSTS enabled` (solo Production) |

---

## ??? Utilizzo per Troubleshooting

### 1. Verifica Percorso Log

Se non riesci a trovare i log, controlla la riga:
```
Log Path: C:\inetpub\SecureBootDashboard.Api\logs\api-20250115.log
```

Questo ti dice esattamente dove cercare.

### 2. Verifica Configurazione

Se l'API non si connette al database, controlla:
```
SQL Server Connection: Server=SRVSQL;Database=SecureBootDashboard;...
```

### 3. Verifica Ambiente

Se ti aspetti Development ma è Production:
```
Environment: Production
```

Imposta la variabile d'ambiente: `ASPNETCORE_ENVIRONMENT=Development`

### 4. Verifica Porte

Se l'app non è raggiungibile, controlla:
```
Configured URLs: https://localhost:7120;http://localhost:5027
```

### 5. Verifica API Base URL (Web)

Se il Web non raggiunge l'API, controlla:
```
API Base URL: https://localhost:7120
```

Se vedi:
```
API Base URL not configured!
```

Significa che manca la configurazione in `appsettings.json`.

---

## ?? Comandi per Visualizzare i Log

### PowerShell

```powershell
# Visualizza ultimi 50 log API
Get-Content "C:\inetpub\SecureBootDashboard.Api\logs\api-$(Get-Date -Format 'yyyyMMdd').log" -Tail 50

# Visualizza ultimi 50 log Web
Get-Content "C:\inetpub\SecureBootDashboard.Web\logs\web-$(Get-Date -Format 'yyyyMMdd').log" -Tail 50

# Tail in tempo reale (API)
Get-Content "C:\inetpub\SecureBootDashboard.Api\logs\api-$(Get-Date -Format 'yyyyMMdd').log" -Tail 50 -Wait

# Filtra per errori
Get-Content "C:\inetpub\SecureBootDashboard.Api\logs\api-*.log" | Select-String "\[ERR\]|\[FTL\]"

# Filtra per startup info
Get-Content "C:\inetpub\SecureBootDashboard.Api\logs\api-*.log" | Select-String "Starting\|Environment\|Log Path\|SQL Server\|Storage Provider\|Configured URLs"
```

### CMD

```cmd
REM Visualizza ultimi log API
type "C:\inetpub\SecureBootDashboard.Api\logs\api-20250115.log"

REM Filtra per errori
findstr /C:"[ERR]" /C:"[FTL]" "C:\inetpub\SecureBootDashboard.Api\logs\api-*.log"
```

---

## ?? Sicurezza

### Password Masking

La password nella connection string viene **automaticamente mascherata**:

```
SQL Server Connection: Server=SRVSQL;...;Password=***MASKED***
```

Anche se qualcuno legge i log, non vedrà la password reale.

### Informazioni Non Sensibili

Tutte le altre informazioni loggate sono **non sensibili**:
- Percorsi file
- Nomi server
- Porte
- Versioni .NET

---

## ?? Esempi di Troubleshooting

### Scenario 1: API Non Si Avvia

**Log:**
```
SQL Server Connection: Server=WRONGSERVER;...
[ERR] Failed to connect to database
```

**Soluzione:** Correggi il server name in `appsettings.json`

---

### Scenario 2: Web Non Raggiunge API

**Log Web:**
```
API Base URL: https://localhost:7120
```

**Log API:**
```
Configured URLs: https://localhost:5001
```

**Problema:** Le porte non corrispondono!

**Soluzione:** Allinea le porte in `appsettings.json`

---

### Scenario 3: Log Non Vengono Scritti

**Log Console:**
```
Log Path: C:\inetpub\SecureBootDashboard.Api\logs\api-20250115.log
```

**Problema:** Il log path è corretto ma il file non esiste

**Soluzione:** 
1. Verifica che la cartella `logs` esista
2. Verifica permessi scrittura per Application Pool

```powershell
New-Item -Path "C:\inetpub\SecureBootDashboard.Api\logs" -ItemType Directory -Force
icacls "C:\inetpub\SecureBootDashboard.Api\logs" /grant "IIS APPPOOL\SecureBootDashboard.Api:(OI)(CI)M"
```

---

### Scenario 4: Storage Provider Non Chiaro

**Log:**
```
Storage Provider: File
File Storage Base Path: R:\Nimbus.SecureBootCert\SecureBootDashboard.Api\reports
```

Ora sai esattamente dove vengono salvati i report!

---

## ?? Log Strutturati

I log seguono un formato consistente:

```
{Timestamp} [{Level}] {Message}
```

**Esempi:**

```
2025-01-15 14:30:12.456 +01:00 [INF] Starting SecureBootDashboard.Api application
2025-01-15 14:30:13.123 +01:00 [INF] Environment: Production
2025-01-15 14:30:13.456 +01:00 [INF] Log Path: C:\inetpub\...\logs\api-20250115.log
2025-01-15 14:30:14.789 +01:00 [ERR] Failed to connect to database
System.Data.SqlClient.SqlException: Cannot open database...
```

---

## ?? Benefici

### Prima (Senza Logging Dettagliato)

```
[INF] Application started
```

? Dove sono i log?  
? Quale database sta usando?  
? Quale porta sta ascoltando?  

### Dopo (Con Logging Dettagliato)

```
[INF] Starting SecureBootDashboard.Api application
[INF] Log Path: C:\inetpub\SecureBootDashboard.Api\logs\api-20250115.log
[INF] SQL Server Connection: Server=SRVSQL;Database=SecureBootDashboard;...
[INF] Storage Provider: EfCore
[INF] Configured URLs: https://localhost:7120;http://localhost:5027
```

? Tutte le informazioni chiave in un colpo d'occhio!

---

## ?? Checklist Post-Deployment

Dopo il deployment, verifica questi log:

- [ ] **Log Path** esiste e ha permessi scrittura
- [ ] **Environment** è corretto (Production/Development)
- [ ] **SQL Server Connection** punta al server giusto
- [ ] **Storage Provider** è configurato correttamente
- [ ] **Configured URLs** corrispondono al binding IIS
- [ ] **API Base URL** (Web) corrisponde all'URL dell'API
- [ ] Nessun errore **[ERR]** o **[FTL]** nei log

---

## ?? Best Practices

### 1. Controlla Sempre i Log All'Avvio

Dopo ogni deployment o restart:

```powershell
# Verifica ultimi log
Get-Content "C:\path\to\logs\api-$(Get-Date -Format 'yyyyMMdd').log" -Tail 100
```

### 2. Confronta Development vs Production

```powershell
# Development
Get-Content ".\logs\api-*.log" | Select-String "Environment:"

# Production
Get-Content "C:\inetpub\SecureBootDashboard.Api\logs\api-*.log" | Select-String "Environment:"
```

### 3. Automatizza Verifica Log

```powershell
# Script di health check
$logFile = "C:\inetpub\SecureBootDashboard.Api\logs\api-$(Get-Date -Format 'yyyyMMdd').log"
$errors = Get-Content $logFile | Select-String "\[ERR\]|\[FTL\]"

if ($errors) {
    Write-Host "? ERRORS FOUND:" -ForegroundColor Red
    $errors
} else {
    Write-Host "? No errors in logs" -ForegroundColor Green
}
```

---

## ?? Riferimenti

- **Serilog Documentation**: https://serilog.net/
- **ASP.NET Core Logging**: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/logging/
- **SERILOG_IMPLEMENTATION.md**: Guida completa Serilog
- **LOGGING_GUIDE.md**: Guida configurazione logging
