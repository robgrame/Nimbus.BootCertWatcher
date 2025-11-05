# Azure Queue Processor - API Implementation Guide

## ?? Panoramica

L'API ora supporta la **lettura automatica** dei report dalla **Azure Queue Storage** tramite un **Background Service** che processa i messaggi in background e li salva nel database.

## ?? Architettura Flusso Completo

```
???????????????????????????????????????????????????????????????
?  Client (.NET Framework 4.8) - Windows Devices       ?
?  ?? Enumera certificati Secure Boot    ?
?  ?? Crea SecureBootStatusReport            ?
?  ?? Invia a Azure Queue (con Entra ID auth ?)  ?
???????????????????????????????????????????????????????????????
        ?
   [Azure Storage Queue - Buffer]
           ?
???????????????????????????????????????????????????????????????
?  API Background Service (.NET 8)           ?
?  ?? QueueProcessorService (Hosted Service)   ?
?  ?? Polling ogni 5-30 secondi                  ?
?  ?? Autentica con Entra ID (Managed Identity ?)    ?
?  ?? Riceve messaggi (batch fino a 32)?
?  ?? Deserializza SecureBootQueueEnvelope       ?
?  ?? Salva in database tramite IReportStore        ?
?  ?? Elimina messaggio dalla queue     ?
???????????????????????????????????????????????????????????????
 ?
  [SQL Database - SecureBootReports, Devices, Events]
           ?
???????????????????????????????????????????????????????????????
?  Web Dashboard (Razor Pages)       ?
?  ?? Visualizza report dal database          ?
???????????????????????????????????????????????????????????????
```

## ? Caratteristiche Implementate

### 1. **Background Service (Hosted Service)**
- ? Eseguito automaticamente all'avvio dell'API
- ? Polling continuo della queue
- ? Processamento batch di messaggi (fino a 32)
- ? Gestione errori con retry automatici
- ? Poison message detection (MaxDequeueCount)

### 2. **Autenticazione Entra ID Completa**
- ? **Managed Identity** (raccomandato per Azure App Service)
- ? **Certificate** (Windows Store o file .pfx)
- ? **App Registration** (Client Secret)
- ? **DefaultAzureCredential** (sviluppo locale)
- ?? **Connection String** (solo sviluppo)

### 3. **Configurazione Flessibile**
- ? Abilita/disabilita tramite flag
- ? Tuning performance (batch size, intervals)
- ? Configurazione timeout e retry
- ? Support per appsettings.json e variabili d'ambiente

### 4. **Reliability & Monitoring**
- ? Logging dettagliato (Information, Warning, Error)
- ? Visibility timeout configurabile
- ? Max dequeue count per poison messages
- ? Graceful shutdown
- ? Exception handling completo

## ?? Setup Rapido

### Scenario 1: Azure App Service (Produzione)

#### Step 1: Abilita Managed Identity

```bash
# Abilita System-Assigned Managed Identity sull'App Service
az webapp identity assign \
  --name app-secureboot-api-prod \
  --resource-group rg-secureboot-prod

# Ottieni il Principal ID
PRINCIPAL_ID=$(az webapp identity show \
  --name app-secureboot-api-prod \
  --resource-group rg-secureboot-prod \
  --query principalId -o tsv)

echo "Principal ID: $PRINCIPAL_ID"
```

#### Step 2: Assegna Permessi RBAC

```bash
# Assegna il ruolo "Storage Queue Data Contributor"
az role assignment create \
  --role "Storage Queue Data Contributor" \
  --assignee $PRINCIPAL_ID \
  --scope "/subscriptions/{subscription-id}/resourceGroups/{resource-group}/providers/Microsoft.Storage/storageAccounts/{storage-account}"

# Verifica assegnazione
az role assignment list --assignee $PRINCIPAL_ID --output table
```

#### Step 3: Configura App Service Settings

```bash
# Abilita Queue Processor
az webapp config appsettings set \
  --name app-secureboot-api-prod \
  --resource-group rg-secureboot-prod \
  --settings \
    QueueProcessor__Enabled=true \
    QueueProcessor__QueueServiceUri=https://stsecurebootprod.queue.core.windows.net \
    QueueProcessor__QueueName=secureboot-reports \
    QueueProcessor__AuthenticationMethod=ManagedIdentity
```

#### Step 4: Deploy API

```bash
# Build e publish
cd SecureBootDashboard.Api
dotnet publish -c Release -o ./publish

# Deploy
az webapp deploy \
  --resource-group rg-secureboot-prod \
  --name app-secureboot-api-prod \
  --src-path ./publish.zip \
  --type zip
```

#### Step 5: Verifica Logs

```bash
# Stream logs in tempo reale
az webapp log tail \
  --name app-secureboot-api-prod \
  --resource-group rg-secureboot-prod

# Cerca questi log:
# ? "Queue processor starting. Queue: secureboot-reports, AuthMethod: ManagedIdentity"
# ? "Using Managed Identity authentication"
# ? "Queue processor started successfully"
# ? "Received N message(s) from queue secureboot-reports"
```

### Scenario 2: Sviluppo Locale

#### Step 1: Configura Authentication

```bash
# Autenticati con Azure CLI
az login

# Assegna ruolo al tuo account
az role assignment create \
  --role "Storage Queue Data Contributor" \
  --assignee "your-email@domain.com" \
  --scope "/subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Storage/storageAccounts/{storage}"
```

#### Step 2: Configura appsettings.Development.json

```json
{
  "QueueProcessor": {
    "Enabled": true,
    "QueueServiceUri": "https://stsecurebootprod.queue.core.windows.net",
  "QueueName": "secureboot-reports",
    "AuthenticationMethod": "DefaultAzureCredential"
  }
}
```

#### Step 3: Esegui API

```bash
cd SecureBootDashboard.Api
dotnet run
```

#### Step 4: Verifica Logs

Cerca nel console output:
```
info: SecureBootDashboard.Api.Services.QueueProcessorService[0]
      Queue processor starting. Queue: secureboot-reports, AuthMethod: DefaultAzureCredential
info: SecureBootDashboard.Api.Services.QueueProcessorService[0]
      Using DefaultAzureCredential authentication
info: SecureBootDashboard.Api.Services.QueueProcessorService[0]
      Queue processor started successfully
```

## ?? Configurazione Completa

### appsettings.json (Produzione Azure)

```json
{
  "Logging": {
    "LogLevel": {
 "Default": "Information",
    "Microsoft.AspNetCore": "Warning",
 "SecureBootDashboard.Api.Services.QueueProcessorService": "Information"
    }
  },
  "ConnectionStrings": {
    "SqlServer": "Server=tcp:sql-secureboot-prod.database.windows.net,1433;..."
  },
  "Storage": {
    "Provider": "EfCore"
  },
  "QueueProcessor": {
    "Enabled": true,
    "QueueServiceUri": "https://stsecurebootprod.queue.core.windows.net",
    "QueueName": "secureboot-reports",
    "AuthenticationMethod": "ManagedIdentity",
    "MaxMessages": 10,
    "ProcessingInterval": "00:00:05",
    "EmptyQueuePollInterval": "00:00:30",
    "VisibilityTimeout": "00:05:00",
    "MaxDequeueCount": 5
  }
}
```

### Parametri Spiegati

| Parametro | Tipo | Default | Descrizione |
|-----------|------|---------|-------------|
| `Enabled` | bool | `false` | Abilita/disabilita il queue processor |
| `QueueServiceUri` | Uri | - | URL del queue service (es. https://...queue.core.windows.net) |
| `QueueName` | string | `"secureboot-reports"` | Nome della queue |
| `AuthenticationMethod` | string | `"ManagedIdentity"` | Metodo auth: ManagedIdentity, Certificate, AppRegistration, DefaultAzureCredential, ConnectionString |
| `MaxMessages` | int | `10` | Messaggi per batch (1-32) |
| `ProcessingInterval` | TimeSpan | `00:00:05` | Delay quando ci sono messaggi |
| `EmptyQueuePollInterval` | TimeSpan | `00:00:30` | Delay quando queue vuota |
| `VisibilityTimeout` | TimeSpan | `00:05:00` | Tempo per processare prima che messaggio ritorni visibile |
| `MaxDequeueCount` | int | `5` | Max retry prima di considerare poison |

## ?? Performance Tuning

### Volume Basso (<100 report/ora)

```json
{
  "QueueProcessor": {
    "MaxMessages": 5,
    "ProcessingInterval": "00:00:10",
    "EmptyQueuePollInterval": "00:01:00"
  }
}
```

### Volume Medio (100-1000 report/ora) - DEFAULT

```json
{
  "QueueProcessor": {
    "MaxMessages": 10,
    "ProcessingInterval": "00:00:05",
    "EmptyQueuePollInterval": "00:00:30"
  }
}
```

### Volume Alto (>1000 report/ora)

```json
{
  "QueueProcessor": {
    "MaxMessages": 32,
    "ProcessingInterval": "00:00:02",
    "EmptyQueuePollInterval": "00:00:10",
    "VisibilityTimeout": "00:10:00"
  }
}
```

## ?? Metodi di Autenticazione

### 1. Managed Identity (RACCOMANDATO per Azure)

```json
{
  "QueueProcessor": {
    "AuthenticationMethod": "ManagedIdentity"
  }
}
```

**Setup**: Vedi Step 1-2 sopra

**Vantaggi**:
- ? ZERO secrets
- ? Rotazione automatica
- ? Massima sicurezza

### 2. Certificate (RACCOMANDATO per On-Premises)

```json
{
  "QueueProcessor": {
    "AuthenticationMethod": "Certificate",
  "TenantId": "your-tenant-id",
  "ClientId": "your-app-id",
    "CertificateThumbprint": "ABC123...",
    "CertificateStoreLocation": "LocalMachine",
    "CertificateStoreName": "My"
  }
}
```

**Setup**: Vedi `docs/APP_REGISTRATION_GUIDE.md`

### 3. App Registration + Client Secret

```json
{
  "QueueProcessor": {
    "AuthenticationMethod": "AppRegistration",
    "TenantId": "your-tenant-id",
    "ClientId": "your-app-id"
    // ClientSecret in variabili d'ambiente!
  }
}
```

**Setup Secret** (Azure App Service):
```bash
az webapp config appsettings set \
  --settings QueueProcessor__ClientSecret="your-secret"
```

### 4. DefaultAzureCredential (Sviluppo)

```json
{
  "QueueProcessor": {
    "AuthenticationMethod": "DefaultAzureCredential"
  }
}
```

**Setup**: `az login` + assegna RBAC

## ??? Troubleshooting

### Problema: Queue Processor non parte

**Sintomi**: Nessun log "Queue processor starting"

**Soluzione**:
1. Verifica `Enabled: true` in configurazione
2. Verifica che l'API sia avviata correttamente
3. Controlla logs per errori di configurazione

### Problema: "Failed to create queue client"

**Sintomi**: Errore all'avvio del servizio

**Soluzione**:
1. Verifica `QueueServiceUri` sia corretto
2. Per Managed Identity: verifica sia abilitata
3. Per Certificate: verifica certificato installato
4. Per App Registration: verifica TenantId, ClientId, ClientSecret

### Problema: "AuthenticationFailed" o "403 Forbidden"

**Sintomi**: Servizio parte ma non riesce a leggere messaggi

**Soluzione**:
```bash
# Verifica ruolo RBAC assegnato
az role assignment list --assignee {principal-id} --output table

# Assegna ruolo se manca
az role assignment create \
  --role "Storage Queue Data Contributor" \
  --assignee {principal-id} \
  --scope {storage-scope}

# Attendi fino a 5 minuti per propagazione
```

### Problema: "Queue does not exist"

**Sintomi**: Log "Queue secureboot-reports does not exist"

**Soluzione**:
```bash
# Verifica queue esista
az storage queue exists \
  --name secureboot-reports \
  --account-name {storage-account}

# Crea queue se non esiste
az storage queue create \
  --name secureboot-reports \
  --account-name {storage-account}
```

### Problema: Messaggi non vengono processati

**Sintomi**: Messaggi in queue ma non salvati in database

**Soluzione**:
1. Verifica logs per errori deserializzazione
2. Controlla che il formato messaggio sia `SecureBootQueueEnvelope`
3. Verifica connection string database
4. Controlla logs database per errori SQL

### Problema: "Failed to deserialize message"

**Sintomi**: Log "Failed to deserialize message {id}"

**Soluzione**:
1. Verifica che Client invii formato corretto
2. Controlla compatibilità versioni Client/API
3. Esamina messaggio raw in queue per debug

## ?? Monitoring

### Logs Chiave da Monitorare

#### Startup Logs ?
```
[Information] Queue processor starting. Queue: secureboot-reports, AuthMethod: ManagedIdentity
[Information] Using Managed Identity authentication
[Information] Queue processor started successfully
```

#### Processing Logs ?
```
[Information] Received 10 message(s) from queue secureboot-reports
[Information] Processing report for device MACHINE01 (MessageId: {guid})
[Information] Saved report {guid} for device MACHINE01 from queue message {guid}
[Information] Successfully processed and deleted message {guid}
```

#### Error Logs ?
```
[Error] Failed to create queue client
[Error] Failed to receive messages from queue secureboot-reports
[Error] Failed to deserialize message {id}. Invalid JSON format
[Warning] Message {id} exceeded max dequeue count (5). Consider moving to poison queue
```

### Application Insights Query

```kusto
traces
| where message contains "QueueProcessor"
| project timestamp, severityLevel, message
| order by timestamp desc
```

## ? Deployment Checklist

### Azure App Service

- [ ] Abilita Managed Identity
- [ ] Assegna ruolo "Storage Queue Data Contributor"
- [ ] Configura `QueueProcessor__Enabled=true`
- [ ] Deploy API
- [ ] Verifica logs startup
- [ ] Testa invio messaggio dal Client
- [ ] Verifica messaggio salvato in database

### On-Premises / VM

- [ ] Crea App Registration
- [ ] Genera e installa certificato
- [ ] Configura appsettings con TenantId, ClientId, Thumbprint
- [ ] Assegna ruolo RBAC al Service Principal
- [ ] Deploy API
- [ ] Verifica logs startup
- [ ] Testa connessione alla queue

### Sviluppo Locale

- [ ] `az login` con account con permessi
- [ ] Assegna ruolo RBAC al tuo account
- [ ] Crea `appsettings.Development.json` con config
- [ ] Avvia API: `dotnet run`
- [ ] Verifica logs startup

## ?? Riferimenti

- **Setup Guide Completo**: `docs/APP_REGISTRATION_GUIDE.md`
- **Client Authentication**: `docs/AZURE_QUEUE_ENTRA_ID_AUTH.md`
- **Config Examples**: `appsettings.queueprocessor.json`
- **Architecture**: `docs/ARCHITECTURE_DIAGRAM.md`

---

**L'API è ora pronta per processare report dalla queue in produzione! ??**
