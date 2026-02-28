# Summary - Azure Certificate Integration for Client Package

## Overview
Esteso lo script `Create-DeploymentPackage.ps1` per includere automaticamente il certificato Azure App Registration nello ZIP del client, con installazione automatica tramite `Deploy-Client.ps1`.

## Data Modifica
2025-01-XX

## Problema Risolto
In precedenza, il certificato Azure per l'autenticazione su Azure Storage Queue doveva essere distribuito manualmente su ogni workstation. Questo processo era:
- Manuale e soggetto a errori
- Complesso da automatizzare
- Richiedeva distribuzione separata del certificato
- Problematico per deployment via Intune/SCCM

## Soluzione Implementata

### 1. Inclusione Certificato nel Client ZIP

Lo script `Create-DeploymentPackage.ps1` ora:
- Crea una directory di staging temporanea
- Copia i binari del client
- **NUOVO**: Include `AzureAppRegistration.pfx` se `-GenerateAzureCertificate` è specificato
- **NUOVO**: Crea file `INSTALL-CERTIFICATE.txt` con istruzioni dettagliate
- Comprime tutto in un unico ZIP
- Rimuove la directory di staging

**Struttura Client ZIP (con certificato)**:
```
SecureBootWatcher-Client-v1.5.2.zip
??? SecureBootWatcher.Client.exe
??? appsettings.json
??? *.dll (dependencies)
??? certificates/
    ??? AzureAppRegistration.pfx
    ??? INSTALL-CERTIFICATE.txt
```

### 2. Installazione Automatica Certificato

Lo script `Deploy-Client.ps1` ora:
- Rileva automaticamente se `certificates/` esiste nel pacchetto
- Legge la password del certificato da `INSTALL-CERTIFICATE.txt`
- Installa il certificato in `Cert:\LocalMachine\My`
- Aggiorna `appsettings.json` con il thumbprint del certificato
- **SICUREZZA**: Rimuove il file .pfx dal disco dopo l'installazione
- **SICUREZZA**: Rimuove le istruzioni dopo l'uso
- Gestisce errori con fallback a installazione manuale

## File Modificati

### `scripts/Create-DeploymentPackage.ps1`

**Funzione**: `Create-ClientPackageZip` (linea ~264)

**Modifiche principali**:
1. Aggiunta directory di staging temporanea
2. Copia binari del client in staging
3. **NUOVO**: Verifica se `-GenerateAzureCertificate` è attivo
4. **NUOVO**: Copia `AzureAppRegistration.pfx` in `staging/certificates/`
5. **NUOVO**: Crea `INSTALL-CERTIFICATE.txt` con:
   - Password del certificato
   - Istruzioni di installazione automatica
   - Istruzioni di installazione manuale
   - Note di sicurezza
6. Comprime da staging invece che direttamente da binaries
7. Cleanup staging directory

**Codice chiave**:
```powershell
if ($GenerateAzureCertificate) {
    $certSourcePath = Join-Path $packagePath "certificates\AzureAppRegistration.pfx"
    if (Test-Path $certSourcePath) {
        # Create certificates subfolder
        $certStagingPath = Join-Path $tempStagingPath "certificates"
        New-Item -ItemType Directory -Path $certStagingPath -Force | Out-Null
        
        # Copy certificate
        Copy-Item -Path $certSourcePath -Destination $certStagingPath -Force
        
        # Create installation instructions
        Set-Content -Path (Join-Path $certStagingPath "INSTALL-CERTIFICATE.txt") -Value $instructions
    }
}
```

### `scripts/Deploy-Client.ps1`

**Sezione**: Step 4 - Install Client (linea ~230)

**Modifiche principali**:
1. **NUOVO**: Verifica presenza di `certificates/AzureAppRegistration.pfx`
2. **NUOVO**: Legge password da `INSTALL-CERTIFICATE.txt`
3. **NUOVO**: Installa certificato con `Import-PfxCertificate`
4. **NUOVO**: Aggiorna `appsettings.json` con thumbprint
5. **NUOVO**: Rimuove file .pfx e istruzioni dal disco
6. **NUOVO**: Cleanup cartella `certificates/` se vuota
7. Logging dettagliato di ogni step
8. Gestione errori con fallback

**Codice chiave**:
```powershell
$certPath = Join-Path $InstallPath "certificates\AzureAppRegistration.pfx"
if (Test-Path $certPath) {
    # Read password from instructions
    if ($instructions -match "Password:\s*(.+)") {
        $certPassword = $matches[1].Trim()
    }
    
    # Install certificate
    $securePassword = ConvertTo-SecureString -String $certPassword -Force -AsPlainText
    $cert = Import-PfxCertificate `
        -FilePath $certPath `
        -CertStoreLocation "Cert:\LocalMachine\My" `
        -Password $securePassword `
        -Exportable
    
    # Update appsettings.json with thumbprint
    $appsettings.SecureBootWatcher.Sinks.AzureQueue.CertificateThumbprint = $cert.Thumbprint
    
    # Remove certificate file (security)
    Remove-Item -Path $certPath -Force
}
```

## Flusso di Deployment Completo

### Scenario: Client con Azure Queue Storage

```powershell
# 1. Genera pacchetto con certificato
.\Create-DeploymentPackage.ps1 `
    -Version "1.5.2" `
    -GenerateAzureCertificate `
    -AzureCertificatePassword "AzureP@ss123"

# Output:
# - deploy/packages/SecureBootDashboard-Deploy-v1.5.2.zip (pacchetto completo)
# - deploy/packages/SecureBootWatcher-Client-v1.5.2.zip (client + certificato)
# - deploy/packages/SecureBootWatcher-Client-v1.5.2.zip.sha256
# - deploy/packages/SecureBootWatcher-Client-v1.5.2-README.txt

# 2. Carica certificato pubblico su Azure Portal
# File: certificates/AzureAppRegistration.cer (dal pacchetto principale)
# Azure Portal ? Entra ID ? App registrations ? Certificates & secrets

# 3. Distribuisci client (esempio: Intune)
# - Carica SecureBootWatcher-Client-v1.5.2.zip su Intune
# - Crea Win32 app con comando di installazione:
powershell.exe -ExecutionPolicy Bypass -File "Deploy-Client.ps1" `
    -PackageZipPath "SecureBootWatcher-Client-v1.5.2.zip" `
    -ApiBaseUrl "https://api.contoso.com" `
    -CreateScheduledTask

# 4. Deploy automatico su workstation
# - ZIP estratto in temp
# - Certificato installato in LocalMachine\My
# - appsettings.json configurato con thumbprint
# - File .pfx rimosso dal disco
# - Client installato in C:\Program Files\SecureBootWatcher
# - Scheduled task creato
```

## Vantaggi

### ? Distribuzione Semplificata
- **Un singolo ZIP** contiene tutto il necessario
- Nessuna distribuzione separata del certificato
- Compatibile con Intune/SCCM/GPO

### ? Sicurezza Migliorata
- Certificato rimosso dal disco dopo l'installazione
- Solo SYSTEM account ha accesso (tramite scheduled task)
- Password documentata ma file .pfx eliminato
- Nessun file sensibile lasciato su disco

### ? Automazione Completa
- Zero intervento manuale richiesto
- Deploy-Client.ps1 gestisce tutto automaticamente
- Rollout rapido su centinaia/migliaia di workstation
- Adatto per CI/CD pipeline

### ? Tracciabilità
- SHA256 checksum per verifica integrità
- Logging dettagliato di ogni step
- Versioning chiaro (nome file include versione)
- Documenta thumbprint del certificato installato

### ? Gestione Errori
- Fallback a installazione manuale se automatica fallisce
- Istruzioni sempre disponibili durante installazione
- Warning chiari senza bloccare il deployment
- Retry-friendly

## Considerazioni di Sicurezza

### ?? Password del Certificato
- Password hardcoded in `INSTALL-CERTIFICATE.txt`
- File rimosso dopo installazione automatica
- **RACCOMANDAZIONE**: Usare Azure Key Vault in produzione
- Cambiare password default prima del deployment

### ?? Distribuzione Certificato
- Certificato .pfx distribuito su ogni workstation (temporaneamente)
- **MITIGAZIONE**: File eliminato immediatamente dopo installazione
- **MITIGAZIONE**: Solo in directory protetta (Program Files)
- **MITIGAZIONE**: Scheduled task eseguito come SYSTEM

### ?? Rotazione Certificati
- Certificato valido 2 anni (default)
- Necessaria rotazione prima della scadenza
- **RACCOMANDAZIONE**: Monitorare data di scadenza
- **RACCOMANDAZIONE**: Automatizzare processo di rotazione

### ? Best Practices Implementate
- Certificato installato con flag `Exportable` (per backup)
- Store: `LocalMachine\My` (accessibile da SYSTEM)
- File .pfx cancellato dopo installazione
- Istruzioni cancellate dopo uso
- Logging completo per audit

## Test

Vedere `scripts/TEST-ClientPackageZip.md` per:
- **Test 6**: Package con Azure Certificate
- **Test 7**: Automatic Certificate Installation
- Verifica checksums
- Verifica installazione certificato
- Verifica aggiornamento appsettings.json
- Verifica rimozione file sensibili

## Compatibilità

### ? Backward Compatibility
- Funziona senza `-GenerateAzureCertificate` (come prima)
- Deploy-Client.ps1 gestisce sia ZIP con che senza certificato
- Nessuna breaking change
- Script precedenti continuano a funzionare

### ? Forward Compatibility
- Struttura ZIP estendibile per futuri certificati
- Supporto per multiple certificates (folder certificates/)
- Istruzioni separate per ogni certificato

## Prossimi Passi

### Documentazione
- [ ] Aggiornare `docs/CLIENT_DEPLOYMENT.md`
- [ ] Aggiornare `docs/AZURE_QUEUE_CONFIGURATION.md`
- [ ] Creare `docs/CERTIFICATE_MANAGEMENT.md`
- [ ] Aggiornare `README.md`

### Testing
- [ ] Test su Windows 10
- [ ] Test su Windows 11
- [ ] Test deployment Intune
- [ ] Test deployment SCCM
- [ ] Test con Azure Queue reale
- [ ] Test rotazione certificato

### Miglioramenti Futuri (Opzionali)
- [ ] Supporto Azure Key Vault per password certificati
- [ ] Automatic certificate renewal via scheduled task
- [ ] Certificate expiration monitoring e alerting
- [ ] Supporto multiple certificates (dev/test/prod)
- [ ] Digital signature del client ZIP
- [ ] Certificate installation retry logic

## Rollback Plan

Se necessario rollback:

1. **Usa vecchia versione ZIP** (senza certificato):
   ```powershell
   .\Deploy-Client.ps1 -SkipBuild -PackageZipPath "old-package.zip"
   ```

2. **Installa certificato manualmente**:
   ```powershell
   Import-PfxCertificate -FilePath "cert.pfx" -CertStoreLocation "Cert:\LocalMachine\My"
   ```

3. **Aggiorna appsettings.json manualmente**:
   - Edita `C:\Program Files\SecureBootWatcher\appsettings.json`
   - Imposta `CertificateThumbprint` manualmente

## Riferimenti

- **Script modificati**:
  - `scripts/Create-DeploymentPackage.ps1`
  - `scripts/Deploy-Client.ps1`

- **Documentazione**:
  - `scripts/TEST-ClientPackageZip.md`
  - `scripts/CHANGES-ClientPackageZip.md`

- **Azure Docs**:
  - [Import-PfxCertificate](https://docs.microsoft.com/powershell/module/pki/import-pfxcertificate)
  - [Azure Storage Authentication with certificates](https://docs.microsoft.com/azure/storage/common/storage-auth-aad-app)
  - [Azure App Registration certificates](https://docs.microsoft.com/azure/active-directory/develop/howto-create-service-principal-portal)

## Domande Frequenti

### Q: Il certificato è sicuro nello ZIP?
**A**: Il certificato .pfx è protetto da password. Il file viene rimosso immediatamente dopo l'installazione. Per massima sicurezza, considera Azure Key Vault in produzione.

### Q: Cosa succede se l'installazione automatica fallisce?
**A**: Lo script mostra un warning e lascia le istruzioni in `certificates/INSTALL-CERTIFICATE.txt` per installazione manuale.

### Q: Posso usare un certificato diverso?
**A**: Sì, basta sostituire `AzureAppRegistration.pfx` nella cartella `certificates/` prima di creare lo ZIP.

### Q: Il certificato funziona con Azure Queue?
**A**: Sì, il certificato è configurato per autenticazione su Azure Storage Queue tramite Azure App Registration.

### Q: Come roto il certificato quando scade?
**A**: Genera un nuovo package con nuovo certificato, deploy come update. Il vecchio certificato può rimanere installato durante la transizione.

## Contatti

Per domande o problemi: [Your Contact Info]

---

**Status**: ? Implementato e pronto per testing
**Versione**: 1.5.2+
**Data Ultima Modifica**: 2025-01-XX
