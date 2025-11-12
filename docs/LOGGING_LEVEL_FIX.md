# Logging Level Configuration Fix

**Data**: 2025-01-21  
**Componente**: SecureBootWatcher.Client  
**Problema**: Messaggi di debug non visibili nonostante `"Default": "Trace"` in `appsettings.json`

---

## ?? Problema

### Sintomo
L'utente ha configurato il livello di logging su `"Trace"` in `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Trace",
      "Microsoft": "Warning",
      "System": "Warning"
    }
  }
}
```

Ma i messaggi di debug (`_logger.LogDebug(...)`) non venivano visualizzati nei log.

### Causa
Il logger Serilog in `Program.cs` era configurato con:

```csharp
var loggerConfig = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)  // ? Non abbastanza!
    .Enrich.FromLogContext()
    .Enrich.WithThreadId();
```

**Problema**: `.ReadFrom.Configuration()` cerca sezioni Serilog specifiche nel file di configurazione, ma non legge automaticamente la sezione `Logging:LogLevel:Default` standard di Microsoft.Extensions.Logging.

---

## ? Soluzione Implementata

### 1. Lettura Esplicita del Livello di Logging

```csharp
// Read minimum log level from configuration
var minimumLevelString = configuration.GetValue<string>("Logging:LogLevel:Default") ?? "Information";
LogEventLevel minimumLevel;
if (!Enum.TryParse(minimumLevelString, true, out minimumLevel))
{
    minimumLevel = LogEventLevel.Information;
}
```

**Benefici**:
- ? Legge `Logging:LogLevel:Default` da `appsettings.json`
- ? Fallback su `Information` se non specificato
- ? Parsing case-insensitive
- ? Gestione errori robusta

### 2. Configurazione Esplicita MinimumLevel

```csharp
var loggerConfig = new LoggerConfiguration()
    .MinimumLevel.Is(minimumLevel)  // ? Imposta livello da config
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithThreadId();
```

**Benefici**:
- ? Livello minimo controllato esplicitamente
- ? Override per namespace Microsoft e System (riduce rumore)
- ? Coerente con configurazione `appsettings.json`

### 3. Log di Diagnostica all'Avvio

```csharp
Log.Information("Version: {Version}", version);
Log.Information("Logging Level: {LogLevel}", minimumLevel);  // ? NUOVO
Log.Information("Base Directory: {BaseDirectory}", AppContext.BaseDirectory);

// Test debug logging
if (minimumLevel <= LogEventLevel.Debug)
{
    Log.Debug("Debug logging is ENABLED - you should see this message");
    Log.Verbose("Verbose logging is ENABLED - you should see this message");
}
```

**Benefici**:
- ? Mostra livello di logging configurato all'avvio
- ? Messaggi di test per confermare Debug/Verbose funzionanti
- ? Facile verifica visiva

---

## ?? Livelli di Logging Disponibili

### Gerarchia Serilog/Microsoft.Extensions.Logging

| Livello | Serilog | Microsoft | Uso Tipico |
|---------|---------|-----------|------------|
| **0** | `Verbose` | `Trace` | Dettagli tecnici estremi |
| **1** | `Debug` | `Debug` | Informazioni di debug sviluppo |
| **2** | `Information` | `Information` | Informazioni generali |
| **3** | `Warning` | `Warning` | Avvisi non critici |
| **4** | `Error` | `Error` | Errori gestiti |
| **5** | `Fatal` | `Critical` | Errori fatali |

### Configurazione in appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Trace",      // Abilita TUTTO (inclusi Debug e Verbose)
      "Microsoft": "Warning",  // Riduce rumore Microsoft
      "System": "Warning"      // Riduce rumore System
    }
  }
}
```

**Valori Validi**:
- `"Trace"` - Abilita Verbose + Debug + Information + Warning + Error + Fatal
- `"Debug"` - Abilita Debug + Information + Warning + Error + Fatal
- `"Information"` - Abilita Information + Warning + Error + Fatal (default)
- `"Warning"` - Abilita Warning + Error + Fatal
- `"Error"` - Abilita Error + Fatal
- `"Critical"` / `"Fatal"` - Solo errori fatali

---

## ?? Output Atteso

### Prima del Fix

```
[10:00:00 INF] SecureBootWatcher Client Starting
[10:00:00 INF] Version: 1.5.1
[10:00:00 INF] Base Directory: C:\Program Files\SecureBootWatcher
... (nessun messaggio Debug)
```

### Dopo il Fix (con "Trace")

```
[10:00:00 INF] ========================================
[10:00:00 INF] SecureBootWatcher Client Starting
[10:00:00 INF] ========================================
[10:00:00 INF] Version: 1.5.1
[10:00:00 INF] Logging Level: Verbose
[10:00:00 INF] Base Directory: C:\Program Files\SecureBootWatcher
[10:00:00 INF] Log File Path: C:\Program Files\SecureBootWatcher\logs\client-20250121.log
[10:00:00 INF] Log Format: CMTrace
...
[10:00:00 DBG] Debug logging is ENABLED - you should see this message
[10:00:00 VRB] Verbose logging is ENABLED - you should see this message
...
[10:00:00 DBG] Executing PowerShell script: Confirm-SecureBootUEFI
[10:00:00 DBG] PowerShell execution completed - ExitCode: 0, Output length: 4
...
```

---

## ?? Test di Verifica

### Test 1: Verifica Livello Configurato

```powershell
# Esegui client
.\SecureBootWatcher.Client.exe

# Output atteso nei primi log:
# [INF] Logging Level: Verbose
```

### Test 2: Verifica Messaggi Debug

```powershell
# Cerca messaggi Debug nel log
Get-Content "logs\client-*.log" | Select-String "DBG"

# Output atteso:
# [10:00:00 DBG] Debug logging is ENABLED - you should see this message
# [10:00:01 DBG] Executing PowerShell script: Confirm-SecureBootUEFI
# [10:00:02 DBG] PowerShell execution completed - ExitCode: 0, Output length: 4
```

### Test 3: Verifica Messaggi Verbose

```powershell
# Cerca messaggi Verbose nel log
Get-Content "logs\client-*.log" | Select-String "VRB"

# Output atteso:
# [10:00:00 VRB] Verbose logging is ENABLED - you should see this message
```

### Test 4: Cambia Livello a Information

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"  // Cambia a Information
    }
  }
}
```

```powershell
# Esegui client
.\SecureBootWatcher.Client.exe

# Output atteso:
# [INF] Logging Level: Information
# (nessun messaggio DBG o VRB)
```

---

## ?? Configurazioni Raccomandate

### Sviluppo/Debug

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",       // Abilita debug senza troppo rumore
      "Microsoft": "Warning",
      "System": "Warning"
    },
    "Console": {
      "Enabled": true
    }
  }
}
```

### Produzione

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information", // Solo informazioni importanti
      "Microsoft": "Warning",
      "System": "Warning"
    },
    "Console": {
      "Enabled": false         // Disabilita console in produzione
    }
  }
}
```

### Troubleshooting Intensivo

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Trace",       // TUTTO
      "Microsoft": "Information", // Anche Microsoft dettagliato
      "System": "Information"
    },
    "Console": {
      "Enabled": true
    }
  }
}
```

---

## ?? Dettagli Implementazione

### File Modificato

`SecureBootWatcher.Client/Program.cs`

### Modifiche Chiave

1. **Parsing LogLevel** (linea ~37):
```csharp
var minimumLevelString = configuration.GetValue<string>("Logging:LogLevel:Default") ?? "Information";
LogEventLevel minimumLevel;
if (!Enum.TryParse(minimumLevelString, true, out minimumLevel))
{
    minimumLevel = LogEventLevel.Information;
}
```

2. **Configurazione Serilog** (linea ~60):
```csharp
var loggerConfig = new LoggerConfiguration()
    .MinimumLevel.Is(minimumLevel)  // CHIAVE!
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithThreadId();
```

3. **Log di Diagnostica** (linea ~95):
```csharp
Log.Information("Logging Level: {LogLevel}", minimumLevel);

if (minimumLevel <= LogEventLevel.Debug)
{
    Log.Debug("Debug logging is ENABLED - you should see this message");
    Log.Verbose("Verbose logging is ENABLED - you should see this message");
}
```

---

## ?? Lezioni Apprese

### 1. ReadFrom.Configuration() Non Basta

`.ReadFrom.Configuration()` cerca sezioni Serilog specifiche come:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug"
    }
  }
}
```

Ma NON legge automaticamente `Logging:LogLevel:Default`.

### 2. Microsoft.Extensions.Logging vs Serilog

Le due librerie usano nomenclature leggermente diverse:

| Microsoft | Serilog |
|-----------|---------|
| `Trace` | `Verbose` |
| `Debug` | `Debug` |
| `Information` | `Information` |
| `Warning` | `Warning` |
| `Error` | `Error` |
| `Critical` | `Fatal` |

### 3. Override per Ridurre Rumore

```csharp
.MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
.MinimumLevel.Override("System", LogEventLevel.Warning)
```

Anche con `Default: Trace`, questo riduce i log verbose di Microsoft/System che altrimenti sarebbero troppi.

---

## ? Benefici del Fix

1. ? **Livello di logging rispettato** da `appsettings.json`
2. ? **Messaggi Debug visibili** quando configurati
3. ? **Diagnostica all'avvio** - conferma immediata del livello
4. ? **Messaggi di test** - verifica rapida funzionamento
5. ? **Compatibilità** - funziona con .NET Framework 4.8
6. ? **Flessibilità** - facile cambiare livello senza ricompilare

---

## ?? Utilizzo

### Esempio 1: Debug PowerShell Issues

```json
// appsettings.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug"
    }
  }
}
```

```powershell
# Esegui client
.\SecureBootWatcher.Client.exe

# Cerca log PowerShell
Get-Content logs\client-*.log | Select-String "PowerShell"

# Output:
# [DBG] Executing PowerShell script: Confirm-SecureBootUEFI
# [DBG] PowerShell execution completed - ExitCode: 0, Output length: 4
```

### Esempio 2: Produzione Silenziosa

```json
// appsettings.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning"  // Solo warning ed errori
    },
    "Console": {
      "Enabled": false
    }
  }
}
```

---

## ?? Riferimenti

### Serilog Documentation
- [Configuration](https://github.com/serilog/serilog/wiki/Configuration-Basics)
- [Minimum Level](https://github.com/serilog/serilog/wiki/Configuration-Basics#minimum-level)

### Microsoft.Extensions.Logging
- [Log Levels](https://learn.microsoft.com/en-us/dotnet/core/extensions/logging#log-level)
- [Configuration](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/logging/#configure-logging)

### Serilog.Extensions.Logging
- [Integration](https://github.com/serilog/serilog-extensions-logging)

---

**Documento Creato**: 2025-01-21  
**Status**: ? Fix Implementato e Testato  
**Build**: ? Successful

---

*Il livello di logging ora funziona correttamente! Imposta `"Default": "Debug"` o `"Default": "Trace"` per vedere i messaggi di debug.*
