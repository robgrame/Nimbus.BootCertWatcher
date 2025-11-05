# Client Deployment con App Registration - Guida Completa

## ?? Obiettivo

Deployare il **SecureBootWatcher Client** su **dispositivi Windows** (workstation/server) che scrivono report su **Azure Storage Queue** usando **App Registration + Certificate** per autenticazione sicura.

## ?? Architettura Deployment

```
????????????????????????????????????????????????????????
?  Azure Entra ID            ?
?  ?? App Registration: "SecureBoot Client Service"   ?
?  ?  ?? Client ID      ?
?  ?  ?? Certificate (CN=SecureBoot Client Service)   ?
?  ?  ?? RBAC: Storage Queue Data Contributor  ?
?  ?? Service Principal                 ?
????????????????????????????????????????????????????????
 ? (certificate)
????????????????????????????????????????????????????????
?  Group Policy Object (GPO) / Intune                  ?
?  ?? Startup Script: Install-Certificate.ps1  ?
?  ?? Source: \\domain\SYSVOL\...\SecureBootClient.pfx?
?  ?? Target: Cert:\LocalMachine\My            ?
????????????????????????????????????????????????????????
         ?
????????????????????????????????????????????????????????
?  Windows Devices (Workstations/Servers)        ?
?  ?? Certificate in LocalMachine\My store    ?
?  ?? Client: C:\Program Files\SecureBootWatcher\     ?
?  ?  ?? appsettings.json (TenantId, ClientId, Thumbprint) ?
?  ?? Scheduled Task (runs as SYSTEM, daily)          ?
????????????????????????????????????????????????????????
          ? (authenticate with certificate)
????????????????????????????????????????????????????????
?  Azure Storage Queue       ?
?  ?? Queue: secureboot-reports       ?
?  ?? Auth: App Registration + Certificate            ?
????????????????????????????????????????????????????????
```

## ? Vantaggi Soluzione

| Aspetto | Vantaggio |
|---------|-----------|
| **Sicurezza** | Certificate-based (massima sicurezza) |
| **Distribuzione** | Automatica via GPO/Intune |
| **Gestione** | Centralizzata in Entra ID |
| **Scalabilità** | Funziona per 10-10,000+ dispositivi |
| **Audit** | Completo in Entra ID Sign-in logs |
| **Secrets** | Zero secrets in configurazione |
| **Rotazione** | Automatica via GPO update |

## ?? Deployment Step-by-Step

### Phase 1: Azure Setup (Una Volta)

#### 1.1 Crea App Registration

```powershell
# Login
az login

# Crea App Registration
az ad app create \
  --display-name "SecureBoot Client Service" \
  --sign-in-audience "AzureADMyOrg"

# Ottieni IDs
$APP_ID = (az ad app list --display-name "SecureBoot Client Service" --query "[0].appId" -o tsv)
$TENANT_ID = (az account show --query tenantId -o tsv)

Write-Host "Application (Client) ID: $APP_ID" -ForegroundColor Green
Write-Host "Tenant ID: $TENANT_ID" -ForegroundColor Green

# ?? SALVA QUESTI VALORI - Serviranno per la configurazione client
```

#### 1.2 Genera Certificato Master

```powershell
# Genera certificato su workstation admin sicura
$cert = New-SelfSignedCertificate `
  -Subject "CN=SecureBoot Client Service" `
  -CertStoreLocation "Cert:\LocalMachine\My" `
  -KeyExportPolicy Exportable `
  -KeySpec Signature `
  -KeyLength 2048 `
  -KeyAlgorithm RSA `
  -HashAlgorithm SHA256 `
  -NotAfter (Get-Date).AddYears(2) `
  -Provider "Microsoft Enhanced RSA and AES Cryptographic Provider"

# Ottieni thumbprint
$THUMBPRINT = $cert.Thumbprint
Write-Host "Certificate Thumbprint: $THUMBPRINT" -ForegroundColor Green

# ?? SALVA THUMBPRINT - Servirà per appsettings.json
```

#### 1.3 Upload Certificato in Azure

```powershell
# Esporta certificato pubblico
$cerPath = "$env:TEMP\SecureBootClient.cer"
Export-Certificate -Cert $cert -FilePath $cerPath

# Upload in App Registration
az ad app credential reset --id $APP_ID --cert "@$cerPath" --append

# Verifica
az ad app credential list --id $APP_ID --cert --output table
```

#### 1.4 Crea Service Principal e Assegna RBAC

```powershell
# Crea Service Principal
az ad sp create --id $APP_ID

# Ottieni Object ID
$SP_OBJECT_ID = (az ad sp list --filter "appId eq '$APP_ID'" --query "[0].id" -o tsv)
Write-Host "Service Principal Object ID: $SP_OBJECT_ID" -ForegroundColor Green

# Assegna ruolo Storage Queue Data Contributor
$SUBSCRIPTION_ID = (az account show --query id -o tsv)
$RESOURCE_GROUP = "rg-secureboot-prod"
$STORAGE_ACCOUNT = "stsecurebootprod"

$STORAGE_SCOPE = "/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Storage/storageAccounts/$STORAGE_ACCOUNT"

az role assignment create `
  --role "Storage Queue Data Contributor" `
  --assignee $SP_OBJECT_ID `
  --scope $STORAGE_SCOPE

# Verifica assegnazione
az role assignment list --assignee $SP_OBJECT_ID --output table
```

#### 1.5 Esporta .pfx per Distribuzione

```powershell
# ?? CAMBIA LA PASSWORD CON UNA FORTE!
$pfxPassword = Read-Host -AsSecureString "Enter strong password for .pfx"
$pfxPath = "$env:TEMP\SecureBootClient.pfx"

Export-PfxCertificate `
  -Cert $cert `
  -FilePath $pfxPath `
  -Password $pfxPassword

Write-Host "Certificate exported to: $pfxPath" -ForegroundColor Green
Write-Host "?? PROTEGGI QUESTO FILE - Contiene la chiave privata!" -ForegroundColor Yellow
```

---

### Phase 2: Group Policy Setup (Domain Controller)

#### 2.1 Copia .pfx in SYSVOL

```powershell
# Su Domain Controller
$domain = (Get-ADDomain).DNSRoot
$sysvol = "\\$domain\SYSVOL\$domain\Policies\SecureBootWatcher"

# Crea directory
New-Item -ItemType Directory -Path $sysvol -Force

# Copia .pfx (da workstation admin a DC)
Copy-Item "\\admin-workstation\C$\Temp\SecureBootClient.pfx" -Destination "$sysvol\SecureBootClient.pfx"

# Imposta ACL restrittive
icacls "$sysvol\SecureBootClient.pfx" /inheritance:r
icacls "$sysvol\SecureBootClient.pfx" /grant "Domain Computers:(R)"
icacls "$sysvol\SecureBootClient.pfx" /grant "Administrators:(F)"
```

#### 2.2 Crea GPO Startup Script

```powershell
# Crea script Install-Certificate.ps1
$scriptContent = @'
# Install SecureBoot Client Certificate
# Runs at computer startup as SYSTEM

$ErrorActionPreference = 'Stop'
$domain = (Get-WmiObject Win32_ComputerSystem).Domain
$pfxPath = "\\$domain\SYSVOL\$domain\Policies\SecureBootWatcher\SecureBootClient.pfx"
$password = ConvertTo-SecureString -String "YOUR-STRONG-PASSWORD-HERE" -Force -AsPlainText

try {
    # Crea Event Source se non esiste
    if (-not ([System.Diagnostics.EventLog]::SourceExists("SecureBootWatcher"))) {
   New-EventLog -LogName Application -Source "SecureBootWatcher"
    }

    # Check if certificate già installato
 $existingCert = Get-ChildItem -Path "Cert:\LocalMachine\My" | 
        Where-Object { $_.Subject -eq "CN=SecureBoot Client Service" }
    
    if ($existingCert) {
 Write-EventLog -LogName Application -Source "SecureBootWatcher" `
            -EntryType Information -EventId 1001 `
        -Message "SecureBoot certificate already installed. Thumbprint: $($existingCert.Thumbprint)"
        exit 0
    }

    # Import certificate
    $cert = Import-PfxCertificate `
        -FilePath $pfxPath `
 -CertStoreLocation "Cert:\LocalMachine\My" `
     -Password $password `
        -Exportable:$false

    Write-EventLog -LogName Application -Source "SecureBootWatcher" `
        -EntryType Information -EventId 1000 `
        -Message "SecureBoot certificate installed successfully. Thumbprint: $($cert.Thumbprint), Expires: $($cert.NotAfter)"
}
catch {
    Write-EventLog -LogName Application -Source "SecureBootWatcher" `
        -EntryType Error -EventId 1002 `
        -Message "Failed to install SecureBoot certificate: $($_.Exception.Message)"
    exit 1
}
'@

# Salva script in SYSVOL
$scriptContent | Out-File -FilePath "$sysvol\Install-Certificate.ps1" -Encoding UTF8
```

#### 2.3 Crea e Configura GPO

```powershell
# Importa modulo Group Policy
Import-Module GroupPolicy

# Crea GPO
$gpoName = "SecureBoot Certificate Deployment"
New-GPO -Name $gpoName

# Link a OU target (modifica con il tuo OU)
$targetOU = "OU=Workstations,DC=domain,DC=com"
New-GPLink -Name $gpoName -Target $targetOU -LinkEnabled Yes

Write-Host "GPO '$gpoName' creato e linkato a $targetOU" -ForegroundColor Green
Write-Host "?? MANUALE: Aggiungi Startup Script in GPMC:" -ForegroundColor Yellow
Write-Host "  Computer Configuration ? Policies ? Windows Settings ? Scripts ? Startup" -ForegroundColor Yellow
Write-Host "  Script: $sysvol\Install-Certificate.ps1" -ForegroundColor Yellow
```

**Oppure configura manualmente**:
1. Apri **Group Policy Management Console** (`gpmc.msc`)
2. Crea nuovo GPO: **"SecureBoot Certificate Deployment"**
3. Edit GPO ? Computer Configuration ? Policies ? Windows Settings ? Scripts ? Startup
4. Add script: `\\domain\SYSVOL\domain\Policies\SecureBootWatcher\Install-Certificate.ps1`
5. Link GPO alla OU con i computer target
6. Security Filtering: Aggiungi **"Domain Computers"**

---

### Phase 3: Client Build & Package

#### 3.1 Build Client

```powershell
# Clone repository
cd C:\Source
git clone https://github.com/robgrame/Nimbus.BootCertWatcher SecureBootCertificate
cd SecureBootCertificate

# Build e publish
cd SecureBootWatcher.Client
dotnet publish -c Release -r win-x86 --self-contained false -o "C:\Deploy\SecureBootWatcher"
```

#### 3.2 Configura appsettings.json

```powershell
# Sostituisci i valori con quelli reali ottenuti in Phase 1
$appsettings = @"
{
  "Logging": {
    "LogLevel": {
  "Default": "Information",
      "Microsoft": "Warning"
 }
  },
  "SecureBootWatcher": {
    "FleetId": "production-fleet",
    "RegistryPollInterval": "00:30:00",
    "EventQueryInterval": "00:30:00",
    "EventLookbackPeriod": "1.00:00:00",
    "EventChannels": [
      "Microsoft-Windows-DeviceManagement-Enterprise-Diagnostics-Provider/Admin",
      "Microsoft-Windows-CodeIntegrity/Operational"
    ],
    "Sinks": {
      "EnableAzureQueue": true,
      "AzureQueue": {
 "QueueServiceUri": "https://stsecurebootprod.queue.core.windows.net",
    "QueueName": "secureboot-reports",
        "AuthenticationMethod": "Certificate",
        "TenantId": "$TENANT_ID",
        "ClientId": "$APP_ID",
   "CertificateThumbprint": "$THUMBPRINT",
        "CertificateStoreLocation": "LocalMachine",
        "CertificateStoreName": "My",
        "VisibilityTimeout": "00:05:00",
        "MaxSendRetryCount": 5
  }
  }
  }
}
"@

$appsettings | Out-File -FilePath "C:\Deploy\SecureBootWatcher\appsettings.json" -Encoding UTF8
```

#### 3.3 Crea Installation Script

```powershell
$installScript = @'
# Install SecureBootWatcher Client
$ErrorActionPreference = 'Stop'

$destination = "C:\Program Files\SecureBootWatcher"
$zipPath = "$PSScriptRoot\SecureBootWatcher.zip"

try {
    # Extract files
    Write-Host "Installing SecureBootWatcher to $destination..."
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    Expand-Archive -Path $zipPath -DestinationPath $destination -Force

    # Create Scheduled Task
    Write-Host "Creating scheduled task..."
    $action = New-ScheduledTaskAction -Execute "$destination\SecureBootWatcher.Client.exe"
    $trigger = New-ScheduledTaskTrigger -Daily -At "09:00AM"
    $principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest
    $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable

    Register-ScheduledTask `
        -TaskName "SecureBootWatcher" `
        -Action $action `
        -Trigger $trigger `
        -Principal $principal `
        -Settings $settings `
      -Description "Monitors Secure Boot certificate status and reports to Azure" `
        -Force | Out-Null

    Write-Host "SecureBootWatcher installed successfully!" -ForegroundColor Green
    Write-Host "Scheduled task created: runs daily at 9:00 AM as SYSTEM" -ForegroundColor Green
}
catch {
    Write-Error "Installation failed: $($_.Exception.Message)"
    exit 1
}
'@

$installScript | Out-File -FilePath "C:\Deploy\Install.ps1" -Encoding UTF8
```

#### 3.4 Crea Pacchetto

```powershell
# Package tutto
Compress-Archive -Path "C:\Deploy\SecureBootWatcher\*" -DestinationPath "C:\Deploy\SecureBootWatcher.zip" -Force

# Crea deployment package finale
$packagePath = "C:\Deploy\SecureBootWatcher-Package"
New-Item -ItemType Directory -Path $packagePath -Force
Copy-Item "C:\Deploy\SecureBootWatcher.zip" -Destination $packagePath
Copy-Item "C:\Deploy\Install.ps1" -Destination $packagePath

Write-Host "Deployment package ready at: $packagePath" -ForegroundColor Green
```

---

### Phase 4: Deployment (SCCM / Intune / Manual)

#### Opzione A: SCCM Deployment

```plaintext
1. SCCM Console ? Software Library ? Application Management ? Applications
2. Create Application
   - Type: Script Installer
   - Content Location: \\server\share\SecureBootWatcher-Package
   - Installation Program: PowerShell.exe -ExecutionPolicy Bypass -File Install.ps1
   - Detection Method: File exists: C:\Program Files\SecureBootWatcher\SecureBootWatcher.Client.exe
   - Install for: System
   - Run: Whether or not user logged on
3. Distribute to Distribution Points
4. Deploy to Collection "SecureBootWatcher - Production"
```

#### Opzione B: Intune Deployment

```plaintext
1. Intune Portal ? Apps ? Windows ? Add ? Windows app (Win32)
2. Upload SecureBootWatcher-Package.intunewin
3. Program:
   - Install command: PowerShell.exe -ExecutionPolicy Bypass -File Install.ps1
   - Uninstall command: PowerShell.exe -Command "Unregister-ScheduledTask -TaskName SecureBootWatcher -Confirm:$false; Remove-Item 'C:\Program Files\SecureBootWatcher' -Recurse -Force"
4. Requirements:
   - OS: Windows 10 1607+
   - Architecture: x86, x64
5. Detection: File exists: C:\Program Files\SecureBootWatcher\SecureBootWatcher.Client.exe
6. Assign to group: "SG-SecureBootWatcher-Production"
```

#### Opzione C: Manual Deployment (Small Scale)

```powershell
# Su ogni computer (come Administrator)
# 1. Copia package
Copy-Item "\\server\share\SecureBootWatcher-Package\*" -Destination "C:\Temp\SecureBootWatcher" -Recurse

# 2. Esegui installer
cd C:\Temp\SecureBootWatcher
PowerShell.exe -ExecutionPolicy Bypass -File Install.ps1

# 3. Forza GPO update (installa certificato)
gpupdate /force

# 4. Reboot (per applicare GPO startup script)
Restart-Computer
```

---

### Phase 5: Testing & Validation

#### 5.1 Test su Macchina Pilota

```powershell
# 1. Verifica certificato installato
Get-ChildItem -Path "Cert:\LocalMachine\My" | Where-Object { $_.Subject -eq "CN=SecureBoot Client Service" }

# Output atteso:
#   Thumbprint     Subject
#   -----------------
#   1A2B3C4D5E6F7G8H9I0J1K2L3M4N5O6P7Q8R9S0T  CN=SecureBoot Client Service

# 2. Verifica client installato
Test-Path "C:\Program Files\SecureBootWatcher\SecureBootWatcher.Client.exe"

# 3. Verifica scheduled task
Get-ScheduledTask -TaskName "SecureBootWatcher"

# 4. Test manuale client
cd "C:\Program Files\SecureBootWatcher"
.\SecureBootWatcher.Client.exe

# Cerca nei logs:
# [Information] Using Certificate-based authentication with Client ID: ...
# [Information] Loaded certificate from store. Thumbprint: ..., Subject: CN=SecureBoot Client Service
# [Information] Secure Boot report enqueued to secureboot-reports using Certificate authentication
```

#### 5.2 Verifica in Azure

```powershell
# Verifica messaggio in queue
az storage message peek `
  --queue-name secureboot-reports `
  --account-name stsecurebootprod `
  --auth-mode login `
  --num-messages 5
```

**Azure Portal**:
1. **Entra ID ? Sign-in logs**
   - Filtra: Application = "SecureBoot Client Service"
   - Status = Success
 - User type = Service Principal
   
2. **Storage Account ? Queues ? secureboot-reports**
   - Verifica message count > 0

#### 5.3 Event Log Monitoring

```powershell
# Su client, verifica eventi certificate installation
Get-EventLog -LogName Application -Source "SecureBootWatcher" -Newest 10 | Format-List
```

---

## ?? Rollout Strategy

### Pilot Phase (1 settimana)

- **Scope**: 10-50 dispositivi in OU di test
- **Obiettivo**: Validare deployment e identificare issues
- **Monitoraggio**: Daily review logs e queue

### Phased Rollout (4 settimane)

| Settimana | % Dispositivi | Azione |
|-----------|---------------|--------|
| 1 | 10% | Deploy a OU o gruppo pilota espanso |
| 2 | 25% | Expand a altre OU/gruppi |
| 3 | 50% | Metà ambiente |
| 4 | 100% | Rollout completo |

### Monitoraggio Durante Rollout

```kusto
// Application Insights / Log Analytics Query
// Dispositivi attivi (ultimi 7 giorni)
traces
| where timestamp > ago(7d)
| where message contains "Secure Boot report enqueued"
| summarize ReportCount = count() by tostring(customDimensions.MachineName)
| order by ReportCount desc
```

---

## ?? Troubleshooting

### Problema: Certificato Non Installato

**Sintomi**: `Get-ChildItem Cert:\LocalMachine\My` non mostra certificato

**Diagnosi**:
```powershell
# Verifica GPO applicata
gpresult /r | Select-String "SecureBoot"

# Verifica Event Log
Get-EventLog -LogName Application -Source "SecureBootWatcher" -After (Get-Date).AddDays(-1)
```

**Soluzione**:
1. Forza GPO: `gpupdate /force` + reboot
2. Esegui script manualmente: `PowerShell.exe -ExecutionPolicy Bypass -File "\\domain\...\Install-Certificate.ps1"`
3. Verifica ACL su .pfx: `icacls "\\domain\...\SecureBootClient.pfx"`

---

### Problema: Client Fallisce Autenticazione

**Sintomi**: Log "AuthenticationFailed" o "403 Forbidden"

**Diagnosi**:
```powershell
# Verifica Thumbprint in config
$configThumbprint = (Get-Content "C:\Program Files\SecureBootWatcher\appsettings.json" | ConvertFrom-Json).SecureBootWatcher.Sinks.AzureQueue.CertificateThumbprint

# Verifica Thumbprint certificato reale
$certThumbprint = (Get-ChildItem Cert:\LocalMachine\My | Where Subject -eq "CN=SecureBoot Client Service").Thumbprint

if ($configThumbprint -ne $certThumbprint) {
    Write-Host "MISMATCH! Config: $configThumbprint, Cert: $certThumbprint" -ForegroundColor Red
}
```

**Soluzione**:
1. Verifica TenantId e ClientId corretti in appsettings.json
2. Verifica RBAC: `az role assignment list --assignee {sp-object-id}`
3. Controlla Entra ID Sign-in logs per errori specifici

---

### Problema: Nessun Messaggio in Queue

**Sintomi**: Client esegue ma queue vuota

**Diagnosi**:
```powershell
# Abilita debug logging
# In appsettings.json: "Default": "Debug"

# Esegui client manualmente e cattura output
cd "C:\Program Files\SecureBootWatcher"
.\SecureBootWatcher.Client.exe > output.log 2>&1
Get-Content output.log
```

**Soluzione**:
1. Verifica QueueServiceUri corretto
2. Test connettività: `Test-NetConnection stsecurebootprod.queue.core.windows.net -Port 443`
3. Verifica certificato valido (non scaduto)

---

## ?? Security Checklist

### Certificate Protection
- ? Certificato in `LocalMachine\My` (non `CurrentUser`)
- ? Eseguito come SYSTEM (non user account)
- ? `Exportable:$false` dopo installazione
- ? Password .pfx forte e salvata in Key Vault

### RBAC Permissions
- ? Solo "Storage Queue Data Contributor" (non "Owner" o "Contributor")
- ? Scope limitato allo storage account specifico
- ? Service Principal dedicato (non shared)

### Deployment Security
- ? .pfx in SYSVOL con ACL restrittive (solo Domain Computers read)
- ? Scheduled Task esegue come SYSTEM
- ? GPO applied solo a OU specifici (non "All Computers")
- ? .pfx locale eliminato dopo export in SYSVOL

### Monitoring & Audit
- ? Entra ID Sign-in logs monitoring
- ? Failed auth attempts alerts
- ? Certificate expiration alerts (90 giorni prima)
- ? Queue message rate monitoring

---

## ?? Certificate Rotation (Annuale)

### 90 Giorni Prima Scadenza

```powershell
# 1. Genera nuovo certificato (stesso Subject)
$newCert = New-SelfSignedCertificate `
  -Subject "CN=SecureBoot Client Service" `
  -CertStoreLocation "Cert:\LocalMachine\My" `
  -KeyLength 2048 `
  -NotAfter (Get-Date).AddYears(2)

$newThumbprint = $newCert.Thumbprint

# 2. Upload in App Registration (APPEND, non replace)
Export-Certificate -Cert $newCert -FilePath "C:\Temp\SecureBootClient-New.cer"
az ad app credential reset --id $APP_ID --cert "@C:\Temp\SecureBootClient-New.cer" --append

# 3. Export nuovo .pfx
$password = Read-Host -AsSecureString "Enter password for new .pfx"
Export-PfxCertificate -Cert $newCert -FilePath "C:\Temp\SecureBootClient-New.pfx" -Password $password

# 4. Update SYSVOL
Copy-Item "C:\Temp\SecureBootClient-New.pfx" "\\domain\SYSVOL\...\SecureBootClient.pfx" -Force

# 5. Update appsettings.json con nuovo Thumbprint
# Deploy via SCCM/Intune

# 6. Forza GPO update (certificato sarà reinstallato al prossimo startup)

# 7. Dopo 30 giorni grace period, rimuovi vecchio certificato da App Registration
az ad app credential list --id $APP_ID --cert --query "[?endDateTime<'2026-XX-XX'].keyId" -o tsv
az ad app credential delete --id $APP_ID --key-id {old-cert-key-id}
```

---

## ?? Deployment Checklist

### Pre-Deployment
- [ ] App Registration creata in Entra ID
- [ ] Certificato master generato e salvato
- [ ] Certificato uploaded in App Registration
- [ ] Service Principal creato
- [ ] RBAC "Storage Queue Data Contributor" assegnato
- [ ] .pfx esportato e password salvata in vault
- [ ] Certificate expiration alert configurato (90 giorni)

### Group Policy
- [ ] .pfx copiato in SYSVOL
- [ ] ACL configurate (Domain Computers: Read, Admins: Full)
- [ ] Install-Certificate.ps1 creato e testato
- [ ] GPO creato e configurato
- [ ] GPO linkato a OU target
- [ ] Security Filtering verificato

### Client Package
- [ ] Client built (dotnet publish Release)
- [ ] appsettings.json configurato (TenantId, ClientId, Thumbprint)
- [ ] Install.ps1 script creato
- [ ] Deployment package creato (.zip + script)
- [ ] Package testato su macchina di sviluppo

### Pilot Deployment (10-50 dispositivi)
- [ ] Pilot OU identificata e configurata
- [ ] Client deployato via SCCM/Intune/Manual
- [ ] GPO forzato su pilot devices
- [ ] Certificato installato verificato su sample devices
- [ ] Client execution testato manualmente
- [ ] Messaggi in queue verificati
- [ ] Entra ID Sign-in logs verificati
- [ ] Issue identificati e risolti

### Production Rollout
- [ ] Rollout phasing plan definito (4 settimane)
- [ ] Monitoring configurato (Application Insights)
- [ ] Alerts configurati (auth failures, cert expiry)
- [ ] Communication plan per IT e utenti
- [ ] Rollback plan documentato
- [ ] Settimana 1: 10% deployed e monitorato
- [ ] Settimana 2: 25% deployed e monitorato
- [ ] Settimana 3: 50% deployed e monitorato
- [ ] Settimana 4: 100% deployed

### Post-Deployment
- [ ] Daily monitoring attivo (first week)
- [ ] Weekly reporting configurato
- [ ] Certificate rotation reminder (90 giorni prima)
- [ ] Runbook troubleshooting documentato
- [ ] Backup .pfx e password salvati in vault
- [ ] Success metrics raccolti e reported

---

## ?? Support & Resources

### Documentazione
- **Architecture**: `docs/ARCHITECTURE_DIAGRAM.md`
- **App Registration Guide**: `docs/APP_REGISTRATION_GUIDE.md`
- **Entra ID Auth**: `docs/AZURE_QUEUE_ENTRA_ID_AUTH.md`
- **Production Config**: `appsettings.production.json`

### Monitoring Queries

```kusto
// Application Insights - Dispositivi attivi
traces
| where timestamp > ago(7d)
| where message contains "Secure Boot report enqueued"
| summarize count() by tostring(customDimensions.MachineName)
| order by count_ desc

// Errori autenticazione
traces
| where timestamp > ago(24h)
| where severityLevel >= 3
| where message contains "Authentication" or message contains "Certificate"
| project timestamp, severityLevel, message, customDimensions.MachineName
```

### Azure CLI Quick Commands

```bash
# Verifica App Registration
az ad app show --id {app-id}

# Verifica Service Principal RBAC
az role assignment list --assignee {sp-object-id} --output table

# Verifica certificati in App Registration
az ad app credential list --id {app-id} --cert --output table

# Peek messages in queue
az storage message peek --queue-name secureboot-reports --account-name {storage} --auth-mode login
```

---

**Deployment completato! Il client è ora pronto per produzione con massima sicurezza enterprise-grade.** ?????
