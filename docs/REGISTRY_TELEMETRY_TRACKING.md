# Registry Telemetry Tracking Implementation

**Date:** 2025-01-13  
**Status:** ? Completed  
**Branch:** main  

## Overview

Aggiunto il tracciamento completo delle chiavi di registro per gli aggiornamenti UEFI CA 2023 e le politiche di telemetria necessarie per determinare l'idoneità al Controlled Feature Rollout (CFR) di Microsoft.

## Changes Made

### 1. Nuove Chiavi di Registro Tracciate

#### Chiavi Secure Boot (`HKLM\SYSTEM\CurrentControlSet\Control\SecureBoot`)

- **`UpdateType`** (REG_DWORD)
  - 0 = Nessuna azione
  - 1 = Aggiornamento DB
  - 2 = Aggiornamento Boot Manager
  - Tipo di aggiornamento richiesto

#### Chiavi Telemetria (`HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection`)

- **`AllowTelemetry`** (REG_DWORD)
  - 0 = Security (solo per Enterprise/Education/Server)
  - 1 = Basic
  - 2 = Enhanced
  - 3 = Full
  - **Requisito CFR:** Basic (1) o superiore

### 2. Nuovi Modelli

#### `TelemetryPolicySnapshot`

```csharp
public sealed class TelemetryPolicySnapshot
{
    public const string RegistryRootPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\DataCollection";
    
    public uint? AllowTelemetry { get; set; }
    public DateTimeOffset CollectedAtUtc { get; set; }
    
    // Proprietà calcolate
    public string TelemetryLevelDescription { get; }
    public bool MeetsCfrTelemetryRequirement { get; }
}
```

### 3. Modifiche al Modello `SecureBootRegistrySnapshot`

Aggiunta proprietà:
- `UpdateType` - Tipo di aggiornamento richiesto

### 4. Modifiche al Modello `SecureBootStatusReport`

Aggiunta proprietà:
- `TelemetryPolicy` - Snapshot delle politiche di telemetria

### 5. Servizi Aggiornati

#### `IRegistrySnapshotProvider`

Aggiunto metodo:
```csharp
Task<TelemetryPolicySnapshot> CaptureTelemetryPolicyAsync(CancellationToken cancellationToken);
```

#### `RegistrySnapshotProvider`

- Implementato `CaptureTelemetryPolicyAsync()`
- Aggiunta lettura di `UpdateType`

#### `ReportBuilder`

- Integrata cattura telemetria nel flusso di raccolta dati
- Aggiunti alert per:
  - ? Telemetry level insufficiente per CFR
  - ? Telemetry level conforme a CFR (quando MicrosoftUpdateManagedOptIn è abilitato)

## Alert Generati

### Nuovi Alert Telemetria

1. **Telemetria Insufficiente per CFR**
   ```
   ? Telemetry level (Security) does not meet CFR requirements. 
   Basic (1) or higher required for Microsoft managed rollout.
   ```

2. **Telemetria Conforme a CFR**
   ```
   ? Telemetry level (Basic) meets CFR requirements.
   ```

## Controlled Feature Rollout (CFR) Eligibility

### Requisiti per CFR

Per essere idonei al rollout controllato gestito da Microsoft:

1. **`MicrosoftUpdateManagedOptIn = 1`**
   - Opt-in esplicito al CFR

2. **`AllowTelemetry >= 1`** (Basic o superiore)
   - Dati diagnostici obbligatori o opzionali abilitati
   - Permette a Microsoft di valutare l'idoneità del dispositivo

### Interpretazione Livelli Telemetria

| Valore | Livello | CFR Eligible | Note |
|--------|---------|--------------|------|
| 0 | Security | ? No | Solo Enterprise/Education/Server |
| 1 | Basic | ? Sì | Dati diagnostici obbligatori |
| 2 | Enhanced | ? Sì | Dati diagnostici opzionali |
| 3 | Full | ? Sì | Dati diagnostici completi |

## Persistenza Dati

I dati di telemetria sono automaticamente inclusi in:

1. **Report JSON** (`SecureBootStatusReport`)
   - Proprietà `TelemetryPolicy` serializzata

2. **Database** (`SecureBootReportEntity`)
   - Campo `RegistryStateJson` include `TelemetryPolicy`

3. **Azure Queue** (se configurato)
   - Messaggio completo include telemetria

## Testing

### Verificare Livello Telemetria

```powershell
# Leggere il valore corrente
Get-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection" -Name AllowTelemetry

# Output esempio:
# AllowTelemetry : 1  (Basic - CFR eligible)
```

### Verificare Opt-in CFR

```powershell
Get-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\SecureBoot" -Name MicrosoftUpdateManagedOptIn

# Output esempio:
# MicrosoftUpdateManagedOptIn : 1  (Opted in)
```

## Chiavi di Registro - Riepilogo Completo

### HKLM\SYSTEM\CurrentControlSet\Control\SecureBoot

| Chiave | Tipo | Valori | Descrizione |
|--------|------|--------|-------------|
| AvailableUpdates | DWORD | 0x0, 0x40, 0x100, 0x5944 | Bitmask aggiornamenti pendenti |
| UpdateType | DWORD | 0, 1, 2 | Tipo di aggiornamento richiesto |
| HighConfidenceOptOut | DWORD | 0, 1 | Opt-out aggiornamenti automatici |
| MicrosoftUpdateManagedOptIn | DWORD | 0, 1 | Opt-in CFR gestito Microsoft |

### HKLM\SYSTEM\CurrentControlSet\Control\SecureBoot\Servicing

| Chiave | Tipo | Valori | Descrizione |
|--------|------|--------|-------------|
| UEFICA2023Status | DWORD/SZ | 0-2, NotStarted/InProgress/Updated | Stato aggiornamento CA 2023 |
| UEFICA2023Error | DWORD | Codici errore | Errori CA 2023 |
| WindowsUEFICA2023Capable | DWORD | 0, 1, 2 | Compatibilità CA 2023 |

### HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection

| Chiave | Tipo | Valori | Descrizione |
|--------|------|--------|-------------|
| AllowTelemetry | DWORD | 0-3 | Livello telemetria (CFR: ?1) |

## Related Documentation

- [Microsoft Secure Boot Updates](https://learn.microsoft.com/windows-hardware/drivers/bringup/secure-boot-key-generation-and-signing-using-hsm-)
- [Windows Diagnostic Data](https://learn.microsoft.com/windows/privacy/configure-windows-diagnostic-data-in-your-organization)
- [Controlled Feature Rollout](https://learn.microsoft.com/windows/deployment/update/deployment-service-feature-updates)

## Build Verification

? Build successful - All tests passing

```bash
dotnet build
# Build succeeded.
```

## Next Steps

1. ? **Verificare sui dispositivi reali**
   - Testare su device con CFR abilitato
   - Verificare correlazione tra telemetria e rollout

2. **Dashboard Web Enhancement** (Future)
   - Visualizzare stato telemetria nella pagina dispositivo
   - Report CFR eligibility aggregato per flotta

3. **Documentazione Utente** (Future)
   - Guida configurazione CFR
   - Best practices per opt-in gestito

---

**Autore:** SecureBootWatcher Development Team  
**Versione:** 1.6.0+  
