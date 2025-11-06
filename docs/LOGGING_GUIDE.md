# Guida: Abilitare File Logging con Serilog

## Componenti Interessati

1. **SecureBootDashboard.Api** - Backend API
2. **SecureBootDashboard.Web** - Frontend Web
3. **SecureBootWatcher.Client** - Client Windows

---

## Configurazione API e Web (ASP.NET Core 8)

### 1. Installa Pacchetti NuGet

```powershell
# Per API
cd SecureBootDashboard.Api
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Sinks.File
dotnet add package Serilog.Sinks.Console

# Per Web
cd SecureBootDashboard.Web
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Sinks.File
dotnet add package Serilog.Sinks.Console
```

### 2. Modifica Program.cs (API)

```csharp
using Serilog;
using Serilog.Events;

// Configura Serilog PRIMA di build
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        path: "logs/api-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Usa Serilog come logger
    builder.Host.UseSerilog();

    // ... resto della configurazione ...
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
```

### 3. Modifica Program.cs (Web)

Stesso codice dell'API, ma cambia il path:

```csharp
.WriteTo.File(
    path: "logs/web-.log",
    rollingInterval: RollingInterval.Day,
    retainedFileCountLimit: 30,
    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
```

### 4. Configura appsettings.json (Opzionale)

Puoi anche configurare Serilog tramite appsettings:

```json
{
  "Serilog": {
    "Using": [ "Serilog.Sinks.Console", "Serilog.Sinks.File" ],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      { "Name": "Console" },
      {
        "Name": "File",
        "Args": {
          "path": "logs/api-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30,
          "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
        }
      }
    ],
    "Enrich": [ "FromLogContext" ]
  }
}
```

E nel Program.cs:

```csharp
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services));
```

---

## Configurazione Client (.NET Framework 4.8)

### 1. Installa Pacchetti NuGet

```powershell
cd SecureBootWatcher.Client
dotnet add package Serilog
dotnet add package Serilog.Sinks.File
dotnet add package Serilog.Sinks.Console
dotnet add package Serilog.Extensions.Logging
```

### 2. Modifica Program.cs (Client)

```csharp
using Serilog;
using Serilog.Events;

private static async Task<int> Main(string[] args)
{
    // Configura Serilog PRIMA di tutto
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Information()
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(
            path: Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "SecureBootWatcher",
                "logs",
                "client-.log"),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
        .CreateLogger();

    try
    {
        // ... resto del codice ...
    }
    finally
    {
        Log.CloseAndFlush();
    }
}

private static ServiceProvider BuildServices(IConfiguration configuration)
{
    var services = new ServiceCollection();

    // RIMUOVI builder.AddConsole() e usa Serilog
    services.AddLogging(builder =>
    {
        builder.AddSerilog(dispose: true);
        builder.SetMinimumLevel(LogLevel.Information);
    });

    // ... resto del codice ...
}
```

---

## Posizioni File Log

### Dopo la Configurazione

| Componente | Path Log | Formato File |
|------------|----------|--------------|
| **API** | `C:\inetpub\SecureBootDashboard.Api\logs\api-YYYYMMDD.log` | Rolling giornaliero |
| **Web** | `C:\inetpub\SecureBootDashboard.Web\logs\web-YYYYMMDD.log` | Rolling giornaliero |
| **Client** | `C:\ProgramData\SecureBootWatcher\logs\client-YYYYMMDD.log` | Rolling giornaliero |

### Development (dotnet run)

| Componente | Path Log |
|------------|----------|
| **API** | `SecureBootDashboard.Api\logs\api-YYYYMMDD.log` |
| **Web** | `SecureBootDashboard.Web\logs\web-YYYYMMDD.log` |
| **Client** | `C:\ProgramData\SecureBootWatcher\logs\client-YYYYMMDD.log` |

---

## Formato Log

Esempio di output:

```
2025-01-15 14:32:45.123 +00:00 [INF] Application starting
2025-01-15 14:32:45.456 +00:00 [WRN] Connection to database failed, retrying...
2025-01-15 14:32:46.789 +00:00 [ERR] Fatal error occurred
System.Exception: Sample error
   at Program.Main() in Program.cs:line 42
```

---

## Retention Policy

- **Rolling Interval**: Giornaliero (nuovo file ogni giorno)
- **Retention**: 30 giorni (cancellazione automatica file vecchi)
- **Formato Nome**: `api-20250115.log`, `api-20250116.log`, etc.

---

## Permessi IIS

Assicurati che l'Application Pool identity abbia permessi di scrittura sulla cartella logs:

```powershell
# Per API
icacls "C:\inetpub\SecureBootDashboard.Api\logs" /grant "IIS AppPool\SecureBootDashboard.Api:(OI)(CI)M"

# Per Web
icacls "C:\inetpub\SecureBootDashboard.Web\logs" /grant "IIS AppPool\SecureBootDashboard.Web:(OI)(CI)M"
```

---

## Test

### API
```powershell
dotnet run --project SecureBootDashboard.Api
# Verifica: SecureBootDashboard.Api\logs\api-YYYYMMDD.log
```

### Web
```powershell
dotnet run --project SecureBootDashboard.Web
# Verifica: SecureBootDashboard.Web\logs\web-YYYYMMDD.log
```

### Client
```powershell
.\SecureBootWatcher.Client.exe
# Verifica: C:\ProgramData\SecureBootWatcher\logs\client-YYYYMMDD.log
```

---

## Monitoring

### PowerShell
```powershell
# Tail dei log API
Get-Content "C:\inetpub\SecureBootDashboard.Api\logs\api-$(Get-Date -Format 'yyyyMMdd').log" -Tail 50 -Wait

# Tail dei log Web
Get-Content "C:\inetpub\SecureBootDashboard.Web\logs\web-$(Get-Date -Format 'yyyyMMdd').log" -Tail 50 -Wait

# Tail dei log Client
Get-Content "C:\ProgramData\SecureBootWatcher\logs\client-$(Get-Date -Format 'yyyyMMdd').log" -Tail 50 -Wait
```

### Cerca errori
```powershell
# Ultimi errori nell'API
Select-String -Path "C:\inetpub\SecureBootDashboard.Api\logs\*.log" -Pattern "\[ERR\]" | Select-Object -Last 10
```

---

## Alternative a Serilog

### NLog
- Pacchetto: `NLog.Web.AspNetCore`
- Configurazione: `nlog.config`

### log4net
- Pacchetto: `log4net`
- Configurazione: `log4net.config`

**Raccomandazione**: Serilog è la scelta migliore per ASP.NET Core 8 per la sua semplicità e potenza.
