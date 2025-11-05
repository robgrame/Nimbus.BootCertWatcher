# Azure Queue Authentication - App Registration Guide

## ?? Perché App Registration?

L'**App Registration** è il metodo raccomandato per autenticazione **service-to-service** perché offre:

? **Gestione centralizzata** - Configurazione in un unico punto (Entra ID Portal)  
? **API Permissions granulari** - Controllo preciso degli accessi  
? **Certificate-based auth** - Più sicuro di Client Secret  
? **Audit completo** - Sign-in logs dettagliati in Entra ID  
? **Conditional Access** - Policy di sicurezza applicabili  
? **Supporto multi-tenant** - Se necessario in futuro

## ?? Setup Completo Step-by-Step

### Step 1: Crea App Registration in Entra ID

```bash
# Crea App Registration
az ad app create \
  --display-name "SecureBoot Watcher Service" \
  --sign-in-audience "AzureADMyOrg"

# Ottieni l'Application (Client) ID
APP_ID=$(az ad app list --display-name "SecureBoot Watcher Service" --query "[0].appId" -o tsv)
echo "Application (Client) ID: $APP_ID"

# Ottieni l'Object ID dell'app
OBJECT_ID=$(az ad app list --display-name "SecureBoot Watcher Service" --query "[0].id" -o tsv)

# Crea Service Principal per l'app
az ad sp create --id $APP_ID

# Ottieni il Service Principal Object ID
SP_OBJECT_ID=$(az ad sp list --filter "appId eq '$APP_ID'" --query "[0].id" -o tsv)
```

### Step 2: Assegna Permissions allo Storage Account

```bash
# Ottieni il Tenant ID
TENANT_ID=$(az account show --query tenantId -o tsv)
echo "Tenant ID: $TENANT_ID"

# Assegna il ruolo "Storage Queue Data Contributor"
az role assignment create \
  --role "Storage Queue Data Contributor" \
  --assignee $SP_OBJECT_ID \
  --scope "/subscriptions/{subscription-id}/resourceGroups/{resource-group}/providers/Microsoft.Storage/storageAccounts/{storage-account-name}"

# Verifica l'assegnazione
az role assignment list \
  --assignee $SP_OBJECT_ID \
  --scope "/subscriptions/{subscription-id}/resourceGroups/{resource-group}/providers/Microsoft.Storage/storageAccounts/{storage-account-name}" \
  --output table
```

### Step 3A: Opzione 1 - Client Secret (Più Semplice)

```bash
# Crea Client Secret (valido 1 anno)
az ad app credential reset \
  --id $APP_ID \
  --append \
  --display-name "SecureBoot Client Secret" \
  --years 1

# Output conterrà:
# {
#   "appId": "your-app-id",
#   "password": "your-client-secret",  ? Salva questo in modo sicuro!
#   "tenant": "your-tenant-id"
# }
```

#### Configurazione con Client Secret

```json
{
  "SecureBootWatcher": {
    "Sinks": {
      "EnableAzureQueue": true,
      "AzureQueue": {
     "QueueServiceUri": "https://yourstorageaccount.queue.core.windows.net",
        "QueueName": "secureboot-reports",
        "AuthenticationMethod": "AppRegistration",
        "TenantId": "00000000-0000-0000-0000-000000000000",
        "ClientId": "11111111-1111-1111-1111-111111111111"
        // ClientSecret va in variabili d'ambiente o Key Vault!
      }
    }
  }
}
```

**Salva il Client Secret in modo sicuro:**

```powershell
# Variabile d'ambiente (Windows)
$env:SECUREBOOT_Sinks__AzureQueue__ClientSecret = "your-client-secret"

# Azure Key Vault
az keyvault secret set \
  --vault-name your-keyvault \
  --name "SecureBootWatcher--Sinks--AzureQueue--ClientSecret" \
  --value "your-client-secret"
```

### Step 3B: Opzione 2 - Certificate (PIÙ SICURO - Raccomandato)

#### Genera Certificato Self-Signed

```powershell
# Windows PowerShell - Genera certificato per sviluppo
$cert = New-SelfSignedCertificate `
  -Subject "CN=SecureBoot Watcher Service" `
  -CertStoreLocation "Cert:\CurrentUser\My" `
  -KeyExportPolicy Exportable `
  -KeySpec Signature `
  -KeyLength 2048 `
  -KeyAlgorithm RSA `
  -HashAlgorithm SHA256 `
  -NotAfter (Get-Date).AddYears(2)

# Visualizza il thumbprint
$cert.Thumbprint

# Esporta il certificato pubblico (.cer)
Export-Certificate -Cert $cert -FilePath "SecureBootWatcher.cer"

# Esporta certificato con chiave privata (.pfx) - per deployment
$password = ConvertTo-SecureString -String "YourPfxPassword123!" -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath "SecureBootWatcher.pfx" -Password $password
```

#### Carica il Certificato in App Registration

```bash
# Upload del certificato pubblico (.cer) in App Registration
az ad app credential reset \
  --id $APP_ID \
  --cert @SecureBootWatcher.cer \
  --append

# Verifica che sia stato caricato
az ad app credential list --id $APP_ID --output table
```

#### Configurazione con Certificate (da file)

```json
{
  "SecureBootWatcher": {
    "Sinks": {
    "EnableAzureQueue": true,
   "AzureQueue": {
    "QueueServiceUri": "https://yourstorageaccount.queue.core.windows.net",
        "QueueName": "secureboot-reports",
        "AuthenticationMethod": "Certificate",
 "TenantId": "00000000-0000-0000-0000-000000000000",
    "ClientId": "11111111-1111-1111-1111-111111111111",
    "CertificatePath": "C:\\Program Files\\SecureBootWatcher\\SecureBootWatcher.pfx"
        // CertificatePassword va in variabili d'ambiente o Key Vault!
      }
  }
  }
}
```

#### Configurazione con Certificate (da Windows Certificate Store) - RACCOMANDATO

```json
{
  "SecureBootWatcher": {
    "Sinks": {
    "EnableAzureQueue": true,
      "AzureQueue": {
    "QueueServiceUri": "https://yourstorageaccount.queue.core.windows.net",
  "QueueName": "secureboot-reports",
     "AuthenticationMethod": "Certificate",
        "TenantId": "00000000-0000-0000-0000-000000000000",
     "ClientId": "11111111-1111-1111-1111-111111111111",
        "CertificateThumbprint": "ABC123DEF456789...",
        "CertificateStoreLocation": "LocalMachine",
   "CertificateStoreName": "My"
      }
    }
  }
}
```

**Installa il certificato nel Certificate Store:**

```powershell
# Importa il .pfx nel LocalMachine\My store
$password = ConvertTo-SecureString -String "YourPfxPassword123!" -Force -AsPlainText
Import-PfxCertificate -FilePath "SecureBootWatcher.pfx" -CertStoreLocation "Cert:\LocalMachine\My" -Password $password

# Verifica che sia installato
Get-ChildItem -Path "Cert:\LocalMachine\My" | Where-Object {$_.Subject -like "*SecureBoot*"}
```

## ?? Quale Metodo Scegliere?

| Scenario | Metodo Raccomandato | Sicurezza | Complessità |
|----------|---------------------|-----------|-------------|
| **Sviluppo locale** | DefaultAzureCredential | ???? | Bassa |
| **Azure VM/App Service** | Managed Identity | ????? | Bassa |
| **Server on-premises** | Certificate | ????? | Media |
| **CI/CD Pipeline** | Certificate | ????? | Media |
| **Quick test** | Client Secret | ??? | Bassa |
| **Multi-tenant** | App Registration + Certificate | ????? | Alta |

### Regola d'Oro

```
?? On Azure (VM/App Service) ? Managed Identity
?? On-premises con elevata sicurezza ? Certificate-based App Registration
?? On-premises standard ? Client Secret App Registration
?? Sviluppo locale ? DefaultAzureCredential (con az login)
? Mai in produzione ? Connection String
```

## ?? Sicurezza: Client Secret vs Certificate

### Client Secret ??

**Vantaggi:**
- ? Semplice da configurare
- ? Facile rotazione

**Svantaggi:**
- ?? Secret statico che può essere compromesso
- ?? Deve essere ruotato manualmente
- ?? Richiede storage sicuro (Key Vault)

### Certificate ? (RACCOMANDATO)

**Vantaggi:**
- ? Più sicuro - chiave privata protetta
- ? Standard industry per service-to-service auth
- ? Non espone secrets in configurazione
- ? Può usare Windows Certificate Store (Hardware Security Module support)
- ? Scadenza automatica (forza rotazione)

**Svantaggi:**
- ?? Setup più complesso
- ?? Richiede gestione certificati

## ?? Confronto: Service Principal CLI vs App Registration Portal

| Aspetto | `az ad sp create-for-rbac` | App Registration Portal |
|---------|----------------------------|-------------------------|
| **Gestione** | Solo CLI | Portal + CLI |
| **Visibilità** | Minore | Completa |
| **API Permissions** | Implicite | Esplicite |
| **Audit logs** | Limitati | Completi |
| **Certificate support** | Limitato | Completo |
| **Conditional Access** | No | Sì |
| **Recommended for** | Quick tests | Produzione |

## ??? Troubleshooting

### Errore: "AADSTS700016: Application not found"

**Causa**: App ID o Tenant ID errato

**Soluzione**:
```bash
# Verifica l'App ID
az ad app list --display-name "SecureBoot Watcher Service" --query "[0].appId" -o tsv

# Verifica il Tenant ID
az account show --query tenantId -o tsv
```

### Errore: "AuthenticationFailed" o "403 Forbidden"

**Causa**: Manca il ruolo RBAC

**Soluzione**:
```bash
# Verifica le assegnazioni di ruolo
az role assignment list \
  --assignee $SP_OBJECT_ID \
  --scope "/subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Storage/storageAccounts/{storage}" \
  --output table

# Assegna il ruolo se manca
az role assignment create \
  --role "Storage Queue Data Contributor" \
  --assignee $SP_OBJECT_ID \
  --scope "/subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Storage/storageAccounts/{storage}"
```

### Errore: "Certificate not found in store"

**Causa**: Certificato non installato o thumbprint errato

**Soluzione**:
```powershell
# Lista certificati installati
Get-ChildItem -Path "Cert:\LocalMachine\My" | Format-Table Subject, Thumbprint

# Verifica il thumbprint (no spazi o `:`)
# Corretto: ABC123DEF456
# Errato: AB:C1:23:DE:F4:56
```

### Errore: "The certificate chain was issued by an authority that is not trusted"

**Causa**: Certificato self-signed non trusted

**Soluzione**:
```powershell
# Per certificati self-signed, aggiungi al Trusted Root
$cert = Get-ChildItem -Path "Cert:\LocalMachine\My\{thumbprint}"
Export-Certificate -Cert $cert -FilePath "cert.cer"
Import-Certificate -FilePath "cert.cer" -CertStoreLocation "Cert:\LocalMachine\Root"
```

## ?? Checklist Deployment Produzione

### Pre-Deployment

- [ ] Crea App Registration in Entra ID Portal
- [ ] Genera certificato (non self-signed per produzione)
- [ ] Upload certificato pubblico in App Registration
- [ ] Assegna ruolo "Storage Queue Data Contributor" al Service Principal
- [ ] Installa certificato su server target (LocalMachine\My store)
- [ ] Configura appsettings con Tenant ID, Client ID, e Thumbprint
- [ ] **NON** committare certificati o secrets nel repository
- [ ] Testa connessione in ambiente di sviluppo

### Post-Deployment

- [ ] Verifica nei logs: "Using Certificate-based authentication"
- [ ] Verifica report inviati alla queue
- [ ] Monitora Entra ID Sign-in logs per l'app
- [ ] Configura alert per certificate expiration
- [ ] Documenta thumbprint e localizzazione certificato
- [ ] Pianifica rotazione certificato (prima della scadenza)

## ?? Rotazione Certificato

### Setup Alert per Scadenza

```bash
# Azure Monitor alert per certificato in scadenza (90 giorni)
az monitor metrics alert create \
--name "SecureBootWatcher Certificate Expiring" \
  --resource-group {rg} \
  --scopes {app-registration-resource-id} \
  --condition "avg certificate_age > 90" \
  --description "Certificate will expire in 90 days"
```

### Rotazione Step-by-Step

1. **Genera nuovo certificato** (stesso Subject, nuovo Thumbprint)
2. **Upload in App Registration** (`az ad app credential reset --append`)
3. **Deploy nuovo certificato** sui server
4. **Aggiorna configurazione** con nuovo Thumbprint
5. **Riavvia servizio**
6. **Verifica** che funzioni con nuovo certificato
7. **Rimuovi vecchio certificato** da App Registration (dopo grace period)

```bash
# Lista certificati nell'App Registration
az ad app credential list --id $APP_ID --cert --output table

# Rimuovi vecchio certificato (dopo rotazione)
az ad app credential delete --id $APP_ID --key-id {old-cert-key-id}
```

## ?? Best Practices

? **Usa Certificate invece di Client Secret** per produzione  
? **Salva certificati in LocalMachine\My store** (non file)  
? **Non usare certificati self-signed** in produzione  
? **Ruota certificati regolarmente** (es. ogni anno)  
? **Monitora Sign-in logs** in Entra ID  
? **Usa Conditional Access policies** per restrizioni geografiche  
? **Assegna solo "Storage Queue Data Contributor"** (least privilege)  
? **Documenta thumbprint e scadenza** certificato  
? **MAI committare certificati** (.pfx) nel repository  
? **MAI usare certificati scaduti**  
? **MAI condividere chiavi private** tra ambienti

## ?? Riferimenti

- [App Registration Overview](https://learn.microsoft.com/en-us/entra/identity-platform/quickstart-register-app)
- [Certificate Credentials](https://learn.microsoft.com/en-us/entra/identity-platform/certificate-credentials)
- [ClientCertificateCredential Class](https://learn.microsoft.com/en-us/dotnet/api/azure.identity.clientcertificatecredential)
- [Azure RBAC Built-in Roles](https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles#storage-queue-data-contributor)
- [Certificate Best Practices](https://learn.microsoft.com/en-us/entra/identity-platform/howto-create-self-signed-certificate)
