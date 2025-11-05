# Autenticazione Entra ID per Azure Queue - Implementazione Completa ?

## ?? Cosa è stato Implementato

L'autenticazione con **Microsoft Entra ID (Azure AD)** è ora completamente supportata per Azure Storage Queue, eliminando la necessità di usare connection string o SAS token non sicuri.

## ?? Modifiche Effettuate

### 1. **Modello di Configurazione Aggiornato**
   - `SecureBootWatcher.Shared\Configuration\SecureBootWatcherOptions.cs`
   - Aggiunti parametri per tutti i metodi di autenticazione Entra ID
   - Supporto per Managed Identity, Service Principal, e DefaultAzureCredential

### 2. **AzureQueueReportSink Migliorato**
   - `SecureBootWatcher.Client\Sinks\AzureQueueReportSink.cs`
   - Implementazione completa di 4 metodi di autenticazione
   - Gestione automatica delle credenziali
   - Logging dettagliato del metodo di autenticazione usato

### 3. **Configurazione di Esempio**
   - `SecureBootWatcher.Client\appsettings.json` - Configurazione predefinita con DefaultAzureCredential
   - `SecureBootWatcher.Client\appsettings.examples.json` - Esempi per tutti gli scenari

### 4. **Documentazione Completa**
   - `docs\AZURE_QUEUE_ENTRA_ID_AUTH.md` - Guida dettagliata con esempi pratici

## ?? Metodi di Autenticazione Supportati

| # | Metodo | Quando Usarlo | Comando |
|---|--------|---------------|---------|
| 1?? | **DefaultAzureCredential** ? | Sviluppo e produzione (automatico) | Nessuna config extra |
| 2?? | **Managed Identity** | Azure VM, App Service, AKS | `az vm identity assign` |
| 3?? | **Service Principal** | On-premises, CI/CD | `az ad sp create-for-rbac` |
| 4?? | **Connection String** ? | Solo sviluppo locale | Non raccomandato |

## ?? Quick Start

### Configurazione Raccomandata (Production-Ready)

```json
{
  "SecureBootWatcher": {
    "Sinks": {
   "EnableAzureQueue": true,
      "AzureQueue": {
        "QueueServiceUri": "https://yourstorageaccount.queue.core.windows.net",
"QueueName": "secureboot-reports",
   "AuthenticationMethod": "DefaultAzureCredential"
      }
    }
  }
}
```

### Setup Permessi (Una volta)

```bash
# Per sviluppo locale (usa il tuo account)
az login
az role assignment create \
  --role "Storage Queue Data Contributor" \
  --assignee "your-email@domain.com" \
  --scope "/subscriptions/{sub-id}/resourceGroups/{rg}/providers/Microsoft.Storage/storageAccounts/{storage}"

# Per Azure VM (usa Managed Identity)
az vm identity assign --name my-vm --resource-group my-rg
PRINCIPAL_ID=$(az vm identity show --name my-vm --resource-group my-rg --query principalId -o tsv)
az role assignment create \
  --role "Storage Queue Data Contributor" \
  --assignee $PRINCIPAL_ID \
  --scope "/subscriptions/{sub-id}/resourceGroups/{rg}/providers/Microsoft.Storage/storageAccounts/{storage}"
```

### Test

```bash
# Build
dotnet build SecureBootWatcher.Client

# Run
.\SecureBootWatcher.Client\bin\Debug\net48\SecureBootWatcher.Client.exe

# Cerca nei logs:
# "Using DefaultAzureCredential authentication"
# "Secure Boot report enqueued to secureboot-reports using DefaultAzureCredential authentication"
```

## ?? Matrice di Decisione

```
???????????????????????????????????????????????????????????????
? Dove È Deployato     ? Metodo Raccomandato ? Setup Richiesto?
???????????????????????????????????????????????????????????????
? ?? Sviluppo locale   ? DefaultAzure...     ? az login       ?
? ??  Azure VM      ? ManagedIdentity     ? Abilita MI     ?
? ??  Azure App Service ? ManagedIdentity     ? Abilita MI     ?
? ??Azure AKS         ? ManagedIdentity     ? Abilita MI     ?
? ?? Server on-premises? ServicePrincipal    ? Crea SP  ?
? ?? CI/CD Pipeline    ? ServicePrincipal  ? Crea SP        ?
???????????????????????????????????????????????????????????????

Regola d'oro: Su Azure ? Managed Identity
        On-premises ? Service Principal
     Non sei sicuro? ? DefaultAzureCredential
```

## ? Vantaggi della Soluzione Entra ID

| Caratteristica | Connection String | Entra ID |
|----------------|-------------------|----------|
| **Sicurezza** | ? Bassa | ????? Alta |
| **Rotazione chiavi** | ? Manuale | ? Automatica |
| **Audit logs** | ? Limitati | ? Completi |
| **Principio least privilege** | ? Full access | ? Solo queue |
| **Revoca immediata** | ? Cambia key | ? Revoca ruolo |
| **Zero secrets nel codice** | ? No | ? Sì |

## ?? Best Practices Implementate

? **DefaultAzureCredential come default** - Funziona ovunque  
? **Logging del metodo di auth** - Debugging facilitato  
? **Warning per ConnectionString** - Scoraggia uso in produzione  
? **Supporto User-Assigned MI** - Scenari complessi  
? **Gestione errori dettagliata** - Troubleshooting facile
? **Documentazione completa** - Guida per ogni scenario  

## ?? File Importanti

```
SecureBootWatcher.Client/
??? appsettings.json                 # Config predefinita (DefaultAzureCredential)
??? appsettings.examples.json            # Esempi per tutti gli scenari
??? Sinks/
    ??? AzureQueueReportSink.cs        # Implementazione completa Entra ID

SecureBootWatcher.Shared/
??? Configuration/
    ??? SecureBootWatcherOptions.cs      # Modello config esteso

docs/
??? AZURE_QUEUE_ENTRA_ID_AUTH.md         # Guida completa con esempi CLI
```

## ??? Troubleshooting Rapido

### Errore: "AuthenticationFailed"
```bash
# Manca il ruolo RBAC
az role assignment create --role "Storage Queue Data Contributor" --assignee {email-or-principal-id} --scope {storage-scope}
```

### Errore: "ManagedIdentityCredential authentication unavailable"
```bash
# Managed Identity non abilitata
az vm identity assign --name my-vm --resource-group my-rg
```

### Errore: "DefaultAzureCredential failed to retrieve a token"
```bash
# Nessuna credenziale valida trovata - autenticati con Azure CLI
az login
```

## ?? Migration da Connection String

### Prima (? Non Sicuro)
```json
{
  "AzureQueue": {
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...;",
    "QueueName": "secureboot-reports"
  }
}
```

### Dopo (? Sicuro)
```json
{
  "AzureQueue": {
    "QueueServiceUri": "https://yourstorageaccount.queue.core.windows.net",
    "QueueName": "secureboot-reports",
    "AuthenticationMethod": "DefaultAzureCredential"
  }
}
```

### Steps Migration
1. Rimuovi `ConnectionString` dalla configurazione
2. Aggiungi `QueueServiceUri` e `AuthenticationMethod`
3. Assegna ruolo RBAC: `az role assignment create ...`
4. Riavvia il client
5. Verifica nei logs: "Using DefaultAzureCredential authentication"

## ?? Risultato Finale

- ? **Zero secrets nel codice** - Nessuna connection string committata
- ? **Autenticazione Entra ID** - Sicurezza enterprise-grade
- ? **Flessibilità** - 4 metodi per ogni scenario
- ? **Production-ready** - Configurazione default sicura
- ? **Backward compatible** - Connection string ancora supportata (solo dev)
- ? **Documentazione completa** - Guida per ogni scenario

**La soluzione è pronta per il deployment in produzione con autenticazione sicura! ??**
