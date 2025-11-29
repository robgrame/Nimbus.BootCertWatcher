# Queue Processor Troubleshooting Guide

## Issue: API Non Connesso alla Coda Azure

**Data:** 2025-01-24  
**Versione:** v1.11.3  
**Tipo:** Configurazione

---

## Problema

Il servizio API non si connette alla coda di Azure Storage Account.

### Sintomi

- Nessun log di "Queue processor started successfully"
- Messaggi nella coda non vengono processati
- Client inviano report alla coda ma non appaiono nel dashboard

---

## Causa Root

Il `QueueProcessor` è **disabilitato** nella configurazione dell'API.

### Verifica

Controlla `SecureBootDashboard.Api\appsettings.json`:

```json
"QueueProcessor": {
    "Enabled": false,  // ? PROBLEMA: È false!
    ...
}
```

---

## Soluzione

### Opzione 1: Abilitare il QueueProcessor

Se vuoi che l'API processi i messaggi dalla coda Azure:

1. **Modifica** `SecureBootDashboard.Api\appsettings.json`
2. **Imposta** `"Enabled": true` nella sezione QueueProcessor
3. **Riavvia** il servizio API

```json
"QueueProcessor": {
    "Enabled": true,  // ? Abilitato
    "QueueServiceUri": "https://secbootcert.queue.core.windows.net",
    "QueueName": "secureboot-reports",
    "AuthenticationMethod": "Certificate",
    "TenantId": "d6dbad84-5922-4700-a049-c7068c37c884",
    "ClientId": "c8034569-4990-4823-9f1d-b46223789c35",
    "CertificateThumbprint": "61FC110D5BABD61419B106862B304C2FFF57A262",
    "CertificateStoreLocation": "LocalMachine",
    "CertificateStoreName": "My",
    "MaxMessages": 10,
    "ProcessingInterval": "00:00:02",
    "EmptyQueuePollInterval": "00:00:10",
    "VisibilityTimeout": "00:05:00",
    "MaxDequeueCount": 5
}
```

### Opzione 2: Usare WebAPI Diretto (Senza Coda)

Se preferisci che i client inviino direttamente all'API (senza passare per la coda):

1. **Lascia** `QueueProcessor.Enabled: false`
2. **Configura i client** per usare il sink WebAPI:

```json
// Nel client appsettings.json
"SecureBootWatcher": {
    "Sinks": {
        "EnableWebApi": true,
        "WebApi": {
            "BaseAddress": "https://your-api.contoso.com:5001",
            "IngestionRoute": "/api/SecureBootReports"
        },
        "EnableAzureQueue": false  // ? Disabilita la coda
    }
}
```

---

## Test della Configurazione

### Script di Test

Esegui lo script di verifica:

```powershell
.\scripts\Test-QueueConnection.ps1
```

**Output Atteso (QueueProcessor abilitato):**
```
=============================================
Azure Queue Connection Test
=============================================

Configuration:
  Enabled: True
  Queue URI: https://secbootcert.queue.core.windows.net
  Queue Name: secureboot-reports
  Auth Method: Certificate

Checking certificate...
  Thumbprint: 61FC110D5BABD61419B106862B304C2FFF57A262
  Store: LocalMachine\My
  ? Certificate found!
    Subject: CN=SecureBootWatcher
    Issuer: CN=SecureBootWatcher
    Valid From: 01/15/2025 10:00:00
    Valid To: 01/15/2026 10:00:00
  ? Private key is available

=============================================
Summary
=============================================

? QueueProcessor is ENABLED

Next steps:
  1. Ensure the certificate has 'Storage Queue Data Contributor' role
  2. Start the API service and check logs for queue processor
  3. Look for 'Queue processor started successfully' message
```

### Test Manuale API

Avvia l'API e controlla i log:

```powershell
cd SecureBootDashboard.Api
dotnet run
```

**Log Attesi (QueueProcessor abilitato):**
```
info: SecureBootDashboard.Api.Services.QueueProcessorService[0]
      Queue processor starting. Queue: secureboot-reports, AuthMethod: Certificate
info: SecureBootDashboard.Api.Services.QueueProcessorService[0]
      Using Certificate-based authentication with Client ID: c8034569-4990-4823-9f1d-b46223789c35
info: SecureBootDashboard.Api.Services.QueueProcessorService[0]
      Loaded certificate from store. Thumbprint: 61FC110D5BABD61419B106862B304C2FFF57A262, Subject: CN=SecureBootWatcher
info: SecureBootDashboard.Api.Services.QueueProcessorService[0]
      Queue processor started successfully.
```

**Log Attesi (QueueProcessor disabilitato):**
```
info: SecureBootDashboard.Api.Services.QueueProcessorService[0]
      Queue processor is disabled. Skipping startup.
```

---

## Verifica Permessi Azure

Se il QueueProcessor è abilitato ma non riesce a connettersi, verifica i permessi:

### 1. Service Principal deve avere il ruolo corretto

```powershell
# Ottieni l'App Registration Object ID
$appId = "c8034569-4990-4823-9f1d-b46223789c35"
$sp = Get-AzADServicePrincipal -ApplicationId $appId

# Ottieni lo Storage Account
$storageAccount = Get-AzStorageAccount -ResourceGroupName "rg-secureboot" -Name "secbootcert"

# Assegna il ruolo
New-AzRoleAssignment `
    -ObjectId $sp.Id `
    -RoleDefinitionName "Storage Queue Data Contributor" `
    -Scope $storageAccount.Id

Write-Host "? Ruolo assegnato con successo"
```

### 2. Verifica Assegnazioni di Ruolo

```powershell
# Lista tutte le assegnazioni per il Service Principal
Get-AzRoleAssignment -ObjectId $sp.Id | Format-Table RoleDefinitionName, Scope
```

**Output Atteso:**
```
RoleDefinitionName                Scope
------------------                -----
Storage Queue Data Contributor    /subscriptions/.../resourceGroups/rg-secureboot/providers/Microsoft.Storage/storageAccounts/secbootcert
```

---

## Architettura del Flusso Dati

### Con Queue (QueueProcessor Enabled)

```
Client ? Azure Queue ? API QueueProcessor ? Database
  ?          ?              ?                   ?
  ?          ?              ?                   ?
Report   Message      Processing           Persistence
```

**Vantaggi:**
- ? Resilienza: messaggi persistono nella coda
- ? Buffering: API può processare a ritmo controllato
- ? Retry: messaggi falliti vengono ritentati automaticamente
- ? Scalabilità: può gestire picchi di traffico

**Svantaggi:**
- ? Complessità: richiede configurazione Azure
- ? Costi: Azure Storage costa (minimo)
- ? Latenza: leggero ritardo (secondi)

### Senza Queue (QueueProcessor Disabled)

```
Client ? API Endpoint ? Database
  ?          ?              ?
  ?          ?              ?
Report   Processing   Persistence
```

**Vantaggi:**
- ? Semplicità: nessuna configurazione Azure richiesta
- ? Zero Costi: no Azure Storage
- ? Immediato: nessun ritardo

**Svantaggi:**
- ? Nessun buffer: API deve essere sempre disponibile
- ? No retry: se API è down, report si perde
- ? Scalabilità limitata: picchi di traffico sovraccaricano l'API

---

## Configurazione Consigliata per Ambiente

### Sviluppo / Test

```json
"QueueProcessor": {
    "Enabled": false  // ? WebAPI diretto, più semplice
}
```

**Configurazione Client:**
```json
"Sinks": {
    "EnableWebApi": true,
    "EnableAzureQueue": false
}
```

### Produzione (Piccola Fleet <100 dispositivi)

```json
"QueueProcessor": {
    "Enabled": false  // ? WebAPI diretto va bene
}
```

### Produzione (Grande Fleet >100 dispositivi)

```json
"QueueProcessor": {
    "Enabled": true,  // ? Usa la coda per resilienza
    "QueueServiceUri": "https://prodaccount.queue.core.windows.net",
    "QueueName": "secureboot-reports",
    "AuthenticationMethod": "ManagedIdentity"  // ? Recommended per Azure App Service
}
```

**Configurazione Client:**
```json
"Sinks": {
    "ExecutionStrategy": "StopOnFirstSuccess",
    "SinkPriority": "WebApi,AzureQueue",  // ? WebAPI first, Queue as backup
    "EnableWebApi": true,
    "EnableAzureQueue": true
}
```

---

## Troubleshooting Avanzato

### Issue: "Authorization failed for queue"

**Errore:**
```
Authorization failed for queue secureboot-reports.
Check that the service principal has 'Storage Queue Data Contributor' role.
```

**Soluzione:**
1. Verifica che il Service Principal abbia il ruolo corretto
2. Controlla che l'assegnazione sia sullo Storage Account (non sul Resource Group)
3. Attendi 5-10 minuti dopo l'assegnazione (propagazione RBAC)

### Issue: "Authentication failed for queue"

**Errore:**
```
Authentication failed for queue secureboot-reports.
Check authentication configuration (TenantId, ClientId, Certificate).
```

**Soluzioni:**
1. Verifica TenantId: `az account show --query tenantId`
2. Verifica ClientId dell'App Registration
3. Verifica che il certificato sia valido e non scaduto
4. Verifica che il certificato abbia private key
5. Verifica che l'App Registration usi lo stesso certificato

### Issue: "Queue does not exist"

**Errore:**
```
Queue secureboot-reports does not exist. Will retry periodically.
```

**Soluzione:**
```powershell
# Crea la coda
$ctx = New-AzStorageContext -StorageAccountName "secbootcert"
New-AzStorageQueue -Name "secureboot-reports" -Context $ctx
```

---

## File Modificati

| File | Status | Descrizione |
|------|--------|-------------|
| `SecureBootDashboard.Api/appsettings.json` | ? Modified | Enabled QueueProcessor |
| `scripts/Test-QueueConnection.ps1` | ? New | Script di test connessione |
| `docs/QUEUE_PROCESSOR_TROUBLESHOOTING.md` | ? New | Questa documentazione |

---

## Riferimenti

- **Queue Processor Service**: `SecureBootDashboard.Api/Services/QueueProcessorService.cs`
- **Queue Configuration**: `SecureBootDashboard.Api/Configuration/QueueProcessorOptions.cs`
- **Client Queue Sink**: `SecureBootWatcher.Client/Sinks/AzureQueueReportSink.cs`
- **Azure Storage Queue Docs**: https://learn.microsoft.com/azure/storage/queues/

---

## Changelog

| Data | Versione | Cambiamento |
|------|----------|-------------|
| 2025-01-24 | v1.11.3 | QueueProcessor abilitato in appsettings.json |
| 2025-01-24 | v1.11.3 | Creato script Test-QueueConnection.ps1 |
| 2025-01-24 | v1.11.3 | Creata documentazione troubleshooting |

---

**Status:** ? Risolto  
**Priorità:** Alta  
**Impatto:** Processing dei report dalla coda Azure

---

**Made with ?? for IT Operations Teams**
