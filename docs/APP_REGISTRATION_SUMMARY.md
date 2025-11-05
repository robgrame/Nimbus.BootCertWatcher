# App Registration Authentication - Implementazione Completa ?

## ?? Soluzione Implementata

L'autenticazione tramite **Azure App Registration** con supporto per **Client Secret** e **Certificate-based authentication** è ora completamente implementata per Azure Storage Queue.

## ? Novità Rispetto alla Versione Precedente

### Prima (Solo Service Principal via CLI)
- ? Service Principal creato con `az ad sp create-for-rbac`
- ? Solo Client Secret supportato
- ? Gestione limitata tramite CLI
- ? Nessun supporto per certificati

### Ora (App Registration Completa)
- ? App Registration gestita tramite Azure Portal
- ? Client Secret **E** Certificate-based authentication
- ? Certificati da file (.pfx) o Windows Certificate Store
- ? Gestione granulare permissions in Entra ID
- ? Supporto Conditional Access policies
- ? Audit completo in Sign-in logs

## ?? Metodi di Autenticazione Supportati

| # | Metodo | Sicurezza | Uso Raccomandato |
|---|--------|-----------|------------------|
| 1?? | **Certificate (Store)** | ????? | Produzione (BEST) |
| 2?? | **Certificate (File)** | ???? | Deployment alternativo |
| 3?? | **App Registration (Secret)** | ??? | Quick setup |
| 4?? | **Managed Identity** | ????? | Azure VMs/App Services |
| 5?? | **DefaultAzureCredential** | ???? | Dev + Produzione universale |

## ?? Perché Certificate-based Auth è Migliore

### Client Secret ??
```
??????????????????????????????????????
?  Client Secret        ?
??????????????????????????????????????
? ? Secret statico in testo chiaro ?
? ? Può essere compromesso      ?
? ? Rotazione manuale richiesta ?
? ? Storage sicuro complesso     ?
? ?? Valido per max 2 anni      ?
??????????????????????????????????????
```

### Certificate ? (RACCOMANDATO)
```
??????????????????????????????????????
?  Certificate-based Auth  ?
??????????????????????????????????????
? ? Chiave privata protetta ?
? ? Standard industry  ?
? ? Supporto HSM/TPM    ?
? ? Windows Certificate Store       ?
? ? Scadenza automatica (sicurezza) ?
? ? Revoca immediata ?
??????????????????????????????????????
```

## ?? Setup Rapido

### Metodo 1: Certificate da Windows Store (RACCOMANDATO)

```powershell
# 1. Genera certificato
$cert = New-SelfSignedCertificate `
  -Subject "CN=SecureBoot Watcher Service" `
  -CertStoreLocation "Cert:\LocalMachine\My" `
  -KeyLength 2048 `
  -NotAfter (Get-Date).AddYears(2)

# 2. Esporta certificato pubblico
Export-Certificate -Cert $cert -FilePath "SecureBootWatcher.cer"

# 3. Crea App Registration
az ad app create --display-name "SecureBoot Watcher Service"
$APP_ID = (az ad app list --display-name "SecureBoot Watcher Service" --query "[0].appId" -o tsv)

# 4. Upload certificato in App Registration
az ad app credential reset --id $APP_ID --cert @SecureBootWatcher.cer --append

# 5. Crea Service Principal
az ad sp create --id $APP_ID
$SP_ID = (az ad sp list --filter "appId eq '$APP_ID'" --query "[0].id" -o tsv)

# 6. Assegna ruolo RBAC
az role assignment create `
  --role "Storage Queue Data Contributor" `
  --assignee $SP_ID `
--scope "/subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Storage/storageAccounts/{storage}"
```

### Configurazione

```json
{
  "SecureBootWatcher": {
    "Sinks": {
      "EnableAzureQueue": true,
      "AzureQueue": {
     "QueueServiceUri": "https://yourstorageaccount.queue.core.windows.net",
        "QueueName": "secureboot-reports",
      "AuthenticationMethod": "Certificate",
        "TenantId": "your-tenant-id",
        "ClientId": "your-app-id",
 "CertificateThumbprint": "ABC123DEF456...",
        "CertificateStoreLocation": "LocalMachine",
    "CertificateStoreName": "My"
      }
    }
  }
}
```

## ?? Confronto Implementazioni

### Service Principal (CLI) vs App Registration (Portal)

| Aspetto | SP via CLI | App Registration |
|---------|------------|------------------|
| **Creazione** | `az ad sp create-for-rbac` | Azure Portal + CLI |
| **Gestione** | Solo CLI | Portal + CLI + PowerShell |
| **Visibilità** | Limitata | Completa (Entra ID) |
| **Certificates** | Solo upload base | Gestione completa |
| **Permissions** | Implicite | Esplicite (API Permissions) |
| **Audit** | Limitato | Sign-in logs completi |
| **Conditional Access** | No | Sì |
| **Multi-tenant** | No | Sì |
| **Secret Rotation** | Manuale | Manuale + Monitoring |
| **Certificate Store** | No | Sì (Windows Store) |
| **Raccomandato per** | Quick tests | Produzione |

## ?? File Modificati

### 1. `SecureBootWatcherOptions.cs`
Aggiunti parametri:
- `CertificatePath` - Path a file .pfx
- `CertificatePassword` - Password del .pfx
- `CertificateThumbprint` - Thumbprint per Certificate Store
- `CertificateStoreLocation` - `CurrentUser` o `LocalMachine`
- `CertificateStoreName` - `My`, `Root`, ecc.

### 2. `AzureQueueReportSink.cs`
Implementati metodi:
- **AppRegistration** - Client Secret authentication
- **Certificate** - Certificate-based authentication con 2 opzioni:
  - Da file (.pfx) con password
  - Da Windows Certificate Store con thumbprint

### 3. Documentazione
- `docs/APP_REGISTRATION_GUIDE.md` - Guida completa setup
- `appsettings.app-registration.json` - Esempi configurazione

## ?? Quale Metodo Usare?

```
???????????????????????????????????????????????????????????????
?    DECISION TREE        ?
???????????????????????????????????????????????????????????????

Il client è deployato su Azure?
?? SÌ
?  ?? VM, App Service, Container Instance, AKS
?     ?? USA: Managed Identity ?????
?        ?? Zero secrets, zero config, massima sicurezza
?
?? NO (on-premises)
   ?? Requisiti elevata sicurezza?
   ?  ?? SÌ
   ?  ??? USA: App Registration + Certificate (Store) ?????
   ?  ?     ?? Certificato protetto da Windows/HSM
   ?  ?
   ?  ?? NO
   ?     ?? USA: App Registration + Client Secret ???
   ?        ?? Più semplice ma richiede Key Vault
   ?
   ?? Codice deve girare ovunque?
      ?? USA: DefaultAzureCredential ????
         ?? Prova tutto automaticamente
          ?? Azure ? Managed Identity
         ?? Locale ? Azure CLI
          ?? On-prem ? Environment Variables
```

## ? Vantaggi dell'Implementazione

### 1. Massima Flessibilità
- 5 metodi di autenticazione supportati
- Funziona su Azure, on-premises, e sviluppo locale
- Certificate Store Windows integrato

### 2. Sicurezza Enterprise
- Certificate-based auth (più sicuro)
- Supporto HSM/TPM tramite Certificate Store
- Zero secrets in configurazione (se usi Certificate Store)
- Audit completo in Entra ID

### 3. Gestibilità
- App Registration centralizzata in Entra ID Portal
- Monitoring e alerting scadenza certificati
- Conditional Access policies applicabili
- Revoca immediata accessi

### 4. Production-Ready
- Logging dettagliato metodo auth usato
- Error handling completo
- Null-safety checks
- Warning per metodi non sicuri

## ?? Best Practices Implementate

? **Certificate Store preferito su file** - Più sicuro  
? **Supporto HSM** - Tramite Windows Certificate Store  
? **Thumbprint senza formattazione** - Accetta `AB:C1:23` o `ABC123`  
? **Warning per Connection String** - Scoraggia uso in produzione  
? **Logging dettagliato** - Debug facilitato  
? **Null-safety** - Nessun warning di compilazione  
? **Backward compatible** - Tutti i metodi precedenti ancora supportati

## ??? Testing

```powershell
# Test 1: Certificate da Store
dotnet run --project SecureBootWatcher.Client
# Cerca nei logs: "Using Certificate-based authentication with Client ID: ..."
# Cerca: "Loaded certificate from store. Thumbprint: ..."

# Test 2: App Registration con Secret
$env:SECUREBOOT_Sinks__AzureQueue__ClientSecret = "your-secret"
dotnet run --project SecureBootWatcher.Client
# Cerca nei logs: "Using App Registration authentication with Client ID: ..."

# Test 3: Managed Identity (solo su Azure VM)
# Nessuna configurazione secret necessaria
dotnet run --project SecureBootWatcher.Client
# Cerca nei logs: "Using System-Assigned Managed Identity"
```

## ?? Migrazione da Service Principal CLI

### Prima
```bash
az ad sp create-for-rbac \
  --name "secureboot-sp" \
  --role "Storage Queue Data Contributor" \
  --scopes "/subscriptions/{sub}/..."
```

### Dopo (Raccomandato)
```bash
# 1. Crea App Registration (più controllo)
az ad app create --display-name "SecureBoot Watcher Service"

# 2. Genera certificato
$cert = New-SelfSignedCertificate -Subject "CN=SecureBoot Watcher" ...

# 3. Upload certificato
az ad app credential reset --id $APP_ID --cert @cert.cer

# 4. Assegna permissions esplicite
az ad sp create --id $APP_ID
az role assignment create --role "Storage Queue Data Contributor" ...
```

### Vantaggi Migrazione
- ? Gestione centralizzata in Portal
- ? Certificate support completo
- ? Conditional Access
- ? Audit logs dettagliati
- ? API Permissions granulari

## ?? Risultato Finale

**Hai ora 5 opzioni di autenticazione enterprise-grade:**

1. ????? **Certificate (Windows Store)** - Massima sicurezza produzione
2. ????? **Managed Identity** - Zero config su Azure
3. ???? **DefaultAzureCredential** - Universale dev+prod
4. ???? **Certificate (File)** - Deployment alternativo
5. ??? **App Registration (Secret)** - Quick setup

**La soluzione è production-ready con enterprise-grade security! ??**

---

## ?? Documentazione

- **Setup Guide**: `docs/APP_REGISTRATION_GUIDE.md`
- **Esempi Config**: `appsettings.app-registration.json`
- **Entra ID Auth**: `docs/AZURE_QUEUE_ENTRA_ID_AUTH.md`
- **Quick Summary**: Questo file

**Ready to deploy con massima sicurezza!** ???
