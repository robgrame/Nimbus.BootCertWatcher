# Serilog Configuration - Implementation Summary

## ? Implementazione Completata

Ho configurato con successo **Serilog** per il logging su file in entrambi i progetti Web e API.

---

## ?? Pacchetti Installati

### SecureBootDashboard.Web
- ? `Serilog.AspNetCore` v9.0.0
- ? `Serilog.Sinks.File` v7.0.0

### SecureBootDashboard.Api
- ? `Serilog.AspNetCore` v9.0.0
- ? `Serilog.Sinks.File` v7.0.0

---

## ?? File Modificati/Creati

| File | Modifiche |
|------|-----------|
| `SecureBootDashboard.Web/Program.cs` | Configurato Serilog con logging su file |
| `SecureBootDashboard.Api/Program.cs` | Configurato Serilog con logging su file |
| `SecureBootDashboard.Web/appsettings.json` | Aggiunta sezione Serilog |
| `SecureBootDashboard.Web/appsettings.Production.json` | Configurazione produzione (solo Warning) |
| `SecureBootDashboard.Api/appsettings.json` | Aggiunta sezione Serilog |
| `SecureBootDashboard.Api/appsettings.Production.json` | Configurazione produzione (creato nuovo) |
| `.gitignore` | Aggiunta cartella `logs/` |

---

## ?? Struttura File Log

### Development (dotnet run)

```
SecureBootDashboard.Api/
??? logs/
    ??? api-20250115.log
    ??? api-20250116.log
    ??? ...

SecureBootDashboard.Web/
??? logs/
    ??? web-20250115.log
    ??? web-20250116.log
    ??? ...
```

### Production (IIS Deployment)

```
C:\inetpub\SecureBootDashboard.Api\
??? logs/
    ??? api-20250115.log
    ??? api-20250116.log
    ??? ...

C:\inetpub\SecureBootDashboard.Web\
??? logs/
    ??? web-20250115.log
    ??? web-20250116.log
    ??? ...
```

---

## ?? Configurazione Logging

### Livelli di Log

**Development (appsettings.json):**
- Default: `Information`
- Microsoft.AspNetCore: `Warning`
- Microsoft.EntityFrameworkCore: `Warning`
- QueueProcessorService: `Information`

**Production (appsettings.Production.json):**
- Default: `Warning`
- Microsoft.AspNetCore: `Warning`
- QueueProcessorService: `Information` (solo API)

### Retention Policy

- **Rolling Interval**: Giornaliero (nuovo file ogni giorno)
- **Retention**: 30 giorni (file più vecchi vengono eliminati automaticamente)
- **Formato Nome File**: `api-yyyyMMdd.log` / `web-yyyyMMdd.log`

### Formato Output

```
2025-01-15 14:32:45.123 +00:00 [INF] Application starting
2025-01-15 14:32:45.456 +00:00 [WRN] Connection to database failed, retrying...
2025-01-15 14:32:46.789 +00:00 [ERR] Fatal error occurred
System.Exception: Sample error
   at Program.Main() in Program.cs:line 42
```

Template utilizzato:
```
{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}
```

---

## ?? Test Rapido

### 1. Test API (Development)

```powershell
cd SecureBootDashboard.Api
dotnet run
```

**Verifica log creato:**
```powershell
Get-Content logs\api-$(Get-Date -Format 'yyyyMMdd').log
```

**Output atteso:**
```
2025-01-15 15:30:12.456 +01:00 [INF] Starting SecureBootDashboard.Api application
2025-01-15 15:30:13.123 +01:00 [INF] SecureBootDashboard.Api started successfully
```

### 2. Test Web (Development)

```powershell
cd SecureBootDashboard.Web
dotnet run
```

**Verifica log creato:**
```powershell
Get-Content logs\web-$(Get-Date -Format 'yyyyMMdd').log
```

**Output atteso:**
```
2025-01-15 15:31:45.789 +01:00 [INF] Starting SecureBootDashboard.Web application
2025-01-15 15:31:46.234 +01:00 [INF] SecureBootDashboard.Web started successfully
```

---

## ?? Monitoring dei Log

### Tail in Tempo Reale (PowerShell)

```powershell
# API logs
Get-Content "SecureBootDashboard.Api\logs\api-$(Get-Date -Format 'yyyyMMdd').log" -Tail 50 -Wait

# Web logs
Get-Content "SecureBootDashboard.Web\logs\web-$(Get-Date -Format 'yyyyMMdd').log" -Tail 50 -Wait
```

### Cerca Errori

```powershell
# Ultimi errori API
Select-String -Path "SecureBootDashboard.Api\logs\*.log" -Pattern "\[ERR\]|\[FTL\]" | Select-Object -Last 10

# Ultimi errori Web
Select-String -Path "SecureBootDashboard.Web\logs\*.log" -Pattern "\[ERR\]|\[FTL\]" | Select-Object -Last 10
```

### Filtra per Data/Ora

```powershell
# Log di oggi dopo le 14:00
Get-Content "SecureBootDashboard.Api\logs\api-$(Get-Date -Format 'yyyyMMdd').log" | Select-String "14:"
```

---

## ?? Permessi IIS (Deployment)

Dopo il deployment su IIS, assicurati che l'Application Pool identity abbia permessi di scrittura sulla cartella logs:

```powershell
# Per API
New-Item -Path "C:\inetpub\SecureBootDashboard.Api\logs" -ItemType Directory -Force
icacls "C:\inetpub\SecureBootDashboard.Api\logs" /grant "IIS AppPool\SecureBootDashboard.Api:(OI)(CI)M"

# Per Web
New-Item -Path "C:\inetpub\SecureBootDashboard.Web\logs" -ItemType Directory -Force
icacls "C:\inetpub\SecureBootDashboard.Web\logs" /grant "IIS AppPool\SecureBootDashboard.Web:(OI)(CI)M"
```

---

## ?? Vantaggi Implementati

? **File Logging Strutturato**: Log scritti su file con formato leggibile
? **Rolling Giornaliero**: Nuovo file ogni giorno, facile da gestire
? **Retention Automatica**: Cancellazione automatica file oltre 30 giorni
? **Console + File**: Log sia su console che su file contemporaneamente
? **Configurabile**: Livelli di log configurabili via appsettings
? **Production Ready**: Configurazione separata per dev e production
? **Timestamp Preciso**: Millisecondi e timezone inclusi
? **Stack Trace**: Eccezioni con stack trace completo

---

## ?? Prossimi Passi

### 1. Test Locale
```powershell
# Avvia API
cd SecureBootDashboard.Api
dotnet run

# Verifica log creato
dir logs
```

### 2. Deployment IIS
- Segui la guida in `docs/DEPLOYMENT_GUIDE.md`
- Aggiungi step per creare cartella logs e permessi
- Verifica che i log vengano scritti correttamente

### 3. Monitoring Produzione
- Configura log rotation esterni (se necessario)
- Setup alert per errori critici
- Integra con strumenti di monitoring (es. Azure Application Insights)

---

## ??? Configurazione Avanzata (Opzionale)

### Aggiungere Application Insights

```powershell
dotnet add package Serilog.Sinks.ApplicationInsights
```

In `Program.cs`:
```csharp
.WriteTo.ApplicationInsights(
    instrumentationKey: builder.Configuration["ApplicationInsights:InstrumentationKey"],
    TelemetryConverter.Traces)
```

### Aggiungere Seq (Centralizzato)

```powershell
dotnet add package Serilog.Sinks.Seq
```

In `appsettings.json`:
```json
"WriteTo": [
  {
    "Name": "Seq",
    "Args": {
      "serverUrl": "http://localhost:5341"
    }
  }
]
```

---

## ? Build Status

**Build:** ? SUCCESS

Tutti i progetti compilano senza errori con Serilog configurato.

---

## ?? Riferimenti

- [Serilog Documentation](https://serilog.net/)
- [Serilog ASP.NET Core](https://github.com/serilog/serilog-aspnetcore)
- [Serilog File Sink](https://github.com/serilog/serilog-sinks-file)
- `docs/LOGGING_GUIDE.md` - Guida dettagliata al logging
