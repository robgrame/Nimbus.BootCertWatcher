# Azure Queue Authentication con Entra ID (Azure AD)

Questa guida descrive come configurare l'autenticazione sicura per Azure Storage Queue usando **Microsoft Entra ID** (precedentemente Azure Active Directory).

## ?? Metodi di Autenticazione Supportati

| Metodo | Uso Consigliato | Sicurezza | Configurazione |
|--------|-----------------|-----------|----------------|
| **DefaultAzureCredential** ? | Sviluppo e produzione | ????? | Automatica |
| **Managed Identity** | Azure VMs, App Services | ????? | Semplice |
| **Service Principal** | On-premises, CI/CD | ???? | Moderata |
| **Connection String** ? | Solo sviluppo locale | ? | Semplice |

## 1?? DefaultAzureCredential (Raccomandato)

**Quando usarlo**: Sviluppo locale e produzione. Prova automaticamente più metodi nell'ordine.

### Configurazione

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

### Come Funziona

`DefaultAzureCredential` prova questi metodi nell'ordine:

1. **Environment Variables** - Credenziali configurate tramite variabili d'ambiente
2. **Managed Identity** - Se eseguito su Azure VM/App Service
3. **Visual Studio** - Credenziali dell'account VS
4. **Azure CLI** - Se sei autenticato con `az login`
5. **Azure PowerShell** - Se sei autenticato con `Connect-AzAccount`

### Setup per Sviluppo Locale

```bash
# Opzione 1: Usa Azure CLI (raccomandato)
az login

# Opzione 2: Usa Visual Studio
# Accedi con il tuo account Azure in Visual Studio
```

### Permessi Necessari

Assegna il ruolo **"Storage Queue Data Contributor"** all'utente/identità:

```bash
# Per il tuo utente (sviluppo)
az role assignment create \
  --role "Storage Queue Data Contributor" \
  --assignee "your-email@domain.com" \
  --scope "/subscriptions/{subscription-id}/resourceGroups/{resource-group}/providers/Microsoft.Storage/storageAccounts/{storage-account}"
```

---

## 2?? Managed Identity (Produzione su Azure)

**Quando usarlo**: Quando il client è deployato su Azure VM, App Service, Container Instance, o AKS.

### System-Assigned Managed Identity

```json
{
  "SecureBootWatcher": {
    "Sinks": {
      "AzureQueue": {
        "QueueServiceUri": "https://yourstorageaccount.queue.core.windows.net",
  "QueueName": "secureboot-reports",
        "AuthenticationMethod": "ManagedIdentity"
      }
    }
  }
}
```

### Setup su Azure VM

```bash
# 1. Abilita System-Assigned Identity sulla VM
az vm identity assign \
  --name my-vm \
  --resource-group my-rg

# 2. Ottieni il Principal ID
PRINCIPAL_ID=$(az vm identity show --name my-vm --resource-group my-rg --query principalId -o tsv)

# 3. Assegna il ruolo Storage Queue Data Contributor
az role assignment create \
  --role "Storage Queue Data Contributor" \
  --assignee $PRINCIPAL_ID \
  --scope "/subscriptions/{subscription-id}/resourceGroups/{resource-group}/providers/Microsoft.Storage/storageAccounts/{storage-account}"
```

### User-Assigned Managed Identity

Se hai più identità gestite:

```json
{
  "SecureBootWatcher": {
    "Sinks": {
      "AzureQueue": {
        "QueueServiceUri": "https://yourstorageaccount.queue.core.windows.net",
        "QueueName": "secureboot-reports",
 "AuthenticationMethod": "ManagedIdentity",
      "ClientId": "00000000-0000-0000-0000-000000000000"
      }
    }
  }
}
```

Setup:

```bash
# 1. Crea User-Assigned Identity
az identity create \
  --name secureboot-identity \
  --resource-group my-rg

# 2. Ottieni Client ID e Principal ID
CLIENT_ID=$(az identity show --name secureboot-identity --resource-group my-rg --query clientId -o tsv)
PRINCIPAL_ID=$(az identity show --name secureboot-identity --resource-group my-rg --query principalId -o tsv)

# 3. Assegna l'identità alla VM
az vm identity assign \
  --name my-vm \
  --resource-group my-rg \
  --identities /subscriptions/{subscription-id}/resourceGroups/my-rg/providers/Microsoft.ManagedIdentity/userAssignedIdentities/secureboot-identity

# 4. Assegna il ruolo Storage
az role assignment create \
  --role "Storage Queue Data Contributor" \
  --assignee $PRINCIPAL_ID \
  --scope "/subscriptions/{subscription-id}/resourceGroups/{resource-group}/providers/Microsoft.Storage/storageAccounts/{storage-account}"
```

---

## 3?? Service Principal (On-Premises / CI/CD)

**Quando usarlo**: Dispositivi on-premises, server non-Azure, o pipeline CI/CD.

### Configurazione

```json
{
  "SecureBootWatcher": {
    "Sinks": {
    "AzureQueue": {
        "QueueServiceUri": "https://yourstorageaccount.queue.core.windows.net",
        "QueueName": "secureboot-reports",
   "AuthenticationMethod": "ServicePrincipal",
        "TenantId": "00000000-0000-0000-0000-000000000000",
        "ClientId": "11111111-1111-1111-1111-111111111111",
     "ClientSecret": "your-secret-value"
      }
    }
  }
}
```

### Setup Service Principal

```bash
# 1. Crea Service Principal
az ad sp create-for-rbac \
  --name "secureboot-watcher-sp" \
  --role "Storage Queue Data Contributor" \
  --scopes "/subscriptions/{subscription-id}/resourceGroups/{resource-group}/providers/Microsoft.Storage/storageAccounts/{storage-account}"

# Output:
# {
#   "appId": "11111111-1111-1111-1111-111111111111",  <- ClientId
#   "displayName": "secureboot-watcher-sp",
#   "password": "secret-value",  <- ClientSecret
#   "tenant": "00000000-0000-0000-0000-000000000000"  <- TenantId
# }
```

### ?? Sicurezza Client Secret

**MAI committare il Client Secret nel repository!** Usa:

#### Opzione A: Variabili d'Ambiente

```powershell
# Windows
$env:SECUREBOOT_Sinks__AzureQueue__ClientSecret = "your-secret-value"

# Linux/Mac
export SECUREBOOT_Sinks__AzureQueue__ClientSecret="your-secret-value"
```

#### Opzione B: Azure Key Vault

```csharp
// In Program.cs
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddEnvironmentVariables(prefix: "SECUREBOOT_")
    .AddAzureKeyVault(new Uri("https://your-keyvault.vault.azure.net/"), new DefaultAzureCredential())
    .Build();
```

Salva il secret in Key Vault:

```bash
az keyvault secret set \
  --vault-name your-keyvault \
  --name "SecureBootWatcher--Sinks--AzureQueue--ClientSecret" \
  --value "your-secret-value"
```

---

## 4?? Connection String (Solo Sviluppo)

**?? NON RACCOMANDATO per produzione!**

```json
{
  "SecureBootWatcher": {
    "Sinks": {
      "AzureQueue": {
        "AuthenticationMethod": "ConnectionString",
   "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=yourstorageaccount;AccountKey=...;EndpointSuffix=core.windows.net",
      "QueueName": "secureboot-reports"
      }
    }
  }
}
```

---

## ??? Troubleshooting

### Errore: "AuthenticationFailed" o "403 Forbidden"

**Causa**: Manca il ruolo "Storage Queue Data Contributor"

**Soluzione**:

```bash
# Verifica le assegnazioni di ruolo
az role assignment list \
  --scope "/subscriptions/{subscription-id}/resourceGroups/{resource-group}/providers/Microsoft.Storage/storageAccounts/{storage-account}" \
  --output table

# Aggiungi il ruolo mancante
az role assignment create \
  --role "Storage Queue Data Contributor" \
  --assignee {principal-id-or-email} \
  --scope "/subscriptions/{subscription-id}/resourceGroups/{resource-group}/providers/Microsoft.Storage/storageAccounts/{storage-account}"
```

### Errore: "ManagedIdentityCredential authentication unavailable"

**Causa**: Managed Identity non abilitata o non disponibile

**Soluzione per VM**:

```bash
az vm identity assign --name my-vm --resource-group my-rg
```

**Soluzione per App Service**:

```bash
az webapp identity assign --name my-app --resource-group my-rg
```

### Errore: "DefaultAzureCredential failed to retrieve a token"

**Causa**: Nessuna credenziale valida trovata

**Soluzione per sviluppo locale**:

```bash
# Autenticati con Azure CLI
az login

# Oppure imposta variabili d'ambiente per Service Principal
$env:AZURE_TENANT_ID = "your-tenant-id"
$env:AZURE_CLIENT_ID = "your-client-id"
$env:AZURE_CLIENT_SECRET = "your-client-secret"
```

### Test della Connessione

```bash
# Test con Azure CLI (usa le stesse credenziali di DefaultAzureCredential)
az storage message put \
  --queue-name secureboot-reports \
  --content "test message" \
  --account-name yourstorageaccount \
  --auth-mode login
```

---

## ?? Checklist Deployment

### Sviluppo Locale

- [ ] Autenticati con `az login`
- [ ] Assegna ruolo "Storage Queue Data Contributor" al tuo account
- [ ] Configura `AuthenticationMethod: "DefaultAzureCredential"`
- [ ] Testa il client localmente

### Produzione su Azure VM

- [ ] Abilita System-Assigned Managed Identity sulla VM
- [ ] Assegna ruolo "Storage Queue Data Contributor" all'identità
- [ ] Configura `AuthenticationMethod: "ManagedIdentity"`
- [ ] Deploy client sulla VM
- [ ] Verifica logs per conferma autenticazione

### Produzione On-Premises

- [ ] Crea Service Principal
- [ ] Assegna ruolo "Storage Queue Data Contributor" al Service Principal
- [ ] Salva Client Secret in Azure Key Vault o variabili d'ambiente
- [ ] Configura `AuthenticationMethod: "ServicePrincipal"`
- [ ] Non committare credenziali nel repository
- [ ] Deploy con configurazione sicura

---

## ?? Best Practices

1. ? **Usa sempre DefaultAzureCredential o Managed Identity quando possibile**
2. ? **Assegna i ruoli RBAC più restrittivi possibili** (Storage Queue Data Contributor, non Owner)
3. ? **Ruota regolarmente i Client Secrets** (se usi Service Principal)
4. ? **Usa Azure Key Vault** per i secrets, non file di configurazione
5. ? **Abilita diagnostic logs** sullo Storage Account per audit
6. ? **Usa User-Assigned Managed Identity** se hai più identità
7. ? **MAI committare ConnectionString o Client Secret** nel repository
8. ? **MAI usare ConnectionString in produzione**

---

## ?? Riferimenti

- [Azure Identity SDK](https://learn.microsoft.com/en-us/dotnet/api/overview/azure/identity-readme)
- [DefaultAzureCredential](https://learn.microsoft.com/en-us/dotnet/api/azure.identity.defaultazurecredential)
- [Managed Identity](https://learn.microsoft.com/en-us/azure/active-directory/managed-identities-azure-resources/overview)
- [Azure RBAC Roles](https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles)
- [Storage Queue Data Contributor Role](https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles#storage-queue-data-contributor)
