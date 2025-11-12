# ?? Client Version Configuration Fix

## ?? Problema Rilevato

### Sintomi
L'API restituiva la versione client `1.0.0.0` invece della versione corrente `1.5.0.0`, causando:
- ? Dashboard che mostra versione errata
- ? Update check che non rileva la versione corretta
- ? Metriche di versione client non accurate

### Log dell'API
```
Version check: Current=1.5.0.0, Latest=1.0.0.0, UpdateAvailable=false, Required=false
```

**Comportamento Errato**:
- Client invia report con versione `1.5.0.0`
- API risponde che la "Latest Version" è `1.0.0.0`
- Client pensa di essere **più recente** del server ?

---

## ?? Causa Root

Nel file `SecureBootDashboard.Api\appsettings.json`, la sezione `ClientUpdate` aveva valori hardcoded obsoleti:

```json
"ClientUpdate": {
    "LatestVersion": "1.0.0.0",  // ? Versione obsoleta
    "ReleaseDate": "2025-01-08T00:00:00Z",  // ? Data obsoleta
    "ReleaseNotes": "Initial release",  // ? Note obsolete
    // ...
}
```

Questa configurazione viene letta dal `ClientUpdateController`:

```csharp
var latestVersion = _configuration["ClientUpdate:LatestVersion"] ?? "1.0.0.0";
```

**Risultato**: L'API dichiarava `1.0.0.0` come ultima versione disponibile, anche se il client era alla `1.5.0.0`.

---

## ? Soluzione Implementata

### Modifica a `appsettings.json`

**Prima**:
```json
"ClientUpdate": {
    "LatestVersion": "1.0.0.0",
    "ReleaseDate": "2025-01-08T00:00:00Z",
    "MinimumVersion": "1.0.0.0",
    "DownloadUrl": "https://yourstorageaccount.blob.core.windows.net/client-packages/SecureBootWatcher-Client-latest.zip",
    "IsUpdateRequired": false,
    "ReleaseNotes": "Initial release",
    "Checksum": "",
    "FileSize": 0,
    "PackagePath": ""
}
```

**Dopo**:
```json
"ClientUpdate": {
    "LatestVersion": "1.5.0.0",
    "ReleaseDate": "2025-01-11T00:00:00Z",
    "MinimumVersion": "1.0.0.0",
    "DownloadUrl": "https://yourstorageaccount.blob.core.windows.net/client-packages/SecureBootWatcher-Client-latest.zip",
    "IsUpdateRequired": false,
    "ReleaseNotes": "Version 1.5.0 - WebAPI Sink Retry & Failover Fix: Fixed critical issue where HTTP errors (503, 500, etc.) were not triggering retry logic or failover to backup sinks (Azure Queue). Client now properly retries on failure and uses backup sinks as configured.",
    "Checksum": "",
    "FileSize": 0,
    "PackagePath": ""
}
```

**Modifiche Apportate**:
1. ? `LatestVersion`: `1.0.0.0` ? `1.5.0.0`
2. ? `ReleaseDate`: `2025-01-08` ? `2025-01-11`
3. ? `ReleaseNotes`: Aggiornate con dettagli della fix v1.5.0

---

## ?? Comportamento Atteso Dopo la Fix

### Scenario 1: Client v1.5.0.0 (Aggiornato)

**Request**:
```
GET /api/ClientUpdate/check?currentVersion=1.5.0.0
```

**Response**:
```json
{
  "currentVersion": "1.5.0.0",
  "latestVersion": "1.5.0.0",
  "updateAvailable": false,
  "updateRequired": false,
  "downloadUrl": "https://...",
  "releaseNotes": "Version 1.5.0 - WebAPI Sink Retry & Failover Fix..."
}
```

**Log API**:
```
Version check: Current=1.5.0.0, Latest=1.5.0.0, UpdateAvailable=false, Required=false
```

? **Corretto**: Client è aggiornato

### Scenario 2: Client v1.0.0.0 (Obsoleto)

**Request**:
```
GET /api/ClientUpdate/check?currentVersion=1.0.0.0
```

**Response**:
```json
{
  "currentVersion": "1.0.0.0",
  "latestVersion": "1.5.0.0",
  "updateAvailable": true,
  "updateRequired": false,
  "downloadUrl": "https://...",
  "releaseNotes": "Version 1.5.0 - WebAPI Sink Retry & Failover Fix..."
}
```

**Log API**:
```
Version check: Current=1.0.0.0, Latest=1.5.0.0, UpdateAvailable=true, Required=false
```

? **Corretto**: Client riceve notifica di aggiornamento disponibile

### Scenario 3: Client v1.4.0.0 (Vecchio)

**Request**:
```
GET /api/ClientUpdate/check?currentVersion=1.4.0.0
```

**Response**:
```json
{
  "currentVersion": "1.4.0.0",
  "latestVersion": "1.5.0.0",
  "updateAvailable": true,
  "updateRequired": false,
  "downloadUrl": "https://...",
  "releaseNotes": "Version 1.5.0 - WebAPI Sink Retry & Failover Fix..."
}
```

? **Corretto**: Client riceve notifica di aggiornamento disponibile

---

## ?? Testing

### Test Case 1: Verifica API Endpoint

```powershell
# Testa l'endpoint di versione
$apiUrl = "https://localhost:5001"
$versionInfo = Invoke-RestMethod -Uri "$apiUrl/api/ClientUpdate/version"

# Visualizza le informazioni
$versionInfo | Format-List

# Output atteso:
# latestVersion       : 1.5.0.0
# releaseDate         : 2025-01-11T00:00:00Z
# downloadUrl         : https://yourstorageaccount.blob.core.windows.net/client-packages/SecureBootWatcher-Client-latest.zip
# isUpdateRequired    : False
# minimumVersion      : 1.0.0.0
# releaseNotes        : Version 1.5.0 - WebAPI Sink Retry & Failover Fix...
# checksum            : 
# fileSize            : 0
```

**Verifica Successo**:
- [ ] `latestVersion` è `1.5.0.0`
- [ ] `releaseDate` è `2025-01-11`
- [ ] `releaseNotes` contengono dettagli della v1.5.0

### Test Case 2: Update Check con Client v1.5.0.0

```powershell
# Testa update check con versione corrente
$checkResult = Invoke-RestMethod -Uri "$apiUrl/api/ClientUpdate/check?currentVersion=1.5.0.0"

$checkResult | Format-List

# Output atteso:
# currentVersion      : 1.5.0.0
# latestVersion       : 1.5.0.0
# updateAvailable     : False
# updateRequired      : False
# downloadUrl         : https://...
# releaseNotes        : Version 1.5.0...
```

**Verifica Successo**:
- [ ] `updateAvailable` è `false`
- [ ] `updateRequired` è `false`
- [ ] Log API mostra `UpdateAvailable=false`

### Test Case 3: Update Check con Client Obsoleto (v1.0.0.0)

```powershell
# Testa update check con versione obsoleta
$checkResult = Invoke-RestMethod -Uri "$apiUrl/api/ClientUpdate/check?currentVersion=1.0.0.0"

$checkResult | Format-List

# Output atteso:
# currentVersion      : 1.0.0.0
# latestVersion       : 1.5.0.0
# updateAvailable     : True  <-- ? Deve essere TRUE
# updateRequired      : False
# downloadUrl         : https://...
# releaseNotes        : Version 1.5.0...
```

**Verifica Successo**:
- [ ] `updateAvailable` è `true`
- [ ] `latestVersion` è `1.5.0.0`
- [ ] Log API mostra `UpdateAvailable=true`

### Test Case 4: Client Logs per Update Notification

```powershell
# Esegui un client con versione vecchia (se disponibile)
# Oppure modifica temporaneamente il client per riportare una versione più vecchia

# Controlla i log del client
Get-Content "C:\ProgramData\SecureBootWatcher\logs\client-*.log" | Select-String -Pattern "update"

# Output atteso (se NotifyOnUpdateAvailable=true):
# [WRN] Update available: Version 1.5.0.0 (current: 1.0.0.0)
# oppure
# [INF] ?? Client update available: Version 1.5.0.0 (current: 1.0.0.0)
```

---

## ?? Impatto della Fix

### Prima della Fix

| Versione Client | Latest API | UpdateAvailable | Comportamento |
|-----------------|------------|-----------------|---------------|
| 1.5.0.0 | 1.0.0.0 | ? False | Client pensa di essere "più nuovo" |
| 1.0.0.0 | 1.0.0.0 | ? False | Nessuna notifica di update |
| 1.4.0.0 | 1.0.0.0 | ? False | Client pensa di essere "più nuovo" |

**Problema**: Tutti i client pensano di essere aggiornati o più recenti del server!

### Dopo la Fix

| Versione Client | Latest API | UpdateAvailable | Comportamento |
|-----------------|------------|-----------------|---------------|
| 1.5.0.0 | 1.5.0.0 | ? False | Corretto - client è aggiornato |
| 1.0.0.0 | 1.5.0.0 | ? True | Corretto - update disponibile |
| 1.4.0.0 | 1.5.0.0 | ? True | Corretto - update disponibile |

**Risultato**: I client ricevono correttamente le notifiche di aggiornamento!

---

## ?? Gestione Versioni Future

### Processo Consigliato

Quando viene rilasciata una nuova versione client (es. `1.6.0.0`):

#### 1. Build e Pacchettizzazione

```powershell
# Pubblica il client
cd SecureBootWatcher.Client
dotnet publish -c Release -o ./publish

# Crea il pacchetto ZIP
Compress-Archive -Path ./publish/* -DestinationPath "./SecureBootWatcher-Client-1.6.0.zip"

# Calcola checksum SHA256
$hash = Get-FileHash "./SecureBootWatcher-Client-1.6.0.zip" -Algorithm SHA256
$hash.Hash  # Copia questo valore
```

#### 2. Upload ad Azure Storage (opzionale)

```powershell
# Upload a Azure Blob Storage
az storage blob upload `
    --account-name "yourstorageaccount" `
    --container-name "client-packages" `
    --name "SecureBootWatcher-Client-1.6.0.zip" `
    --file "./SecureBootWatcher-Client-1.6.0.zip"

# Ottieni l'URL pubblico
az storage blob url `
    --account-name "yourstorageaccount" `
    --container-name "client-packages" `
    --name "SecureBootWatcher-Client-1.6.0.zip"
```

#### 3. Aggiorna `appsettings.json`

```json
"ClientUpdate": {
    "LatestVersion": "1.6.0.0",
    "ReleaseDate": "2025-01-15T00:00:00Z",
    "MinimumVersion": "1.0.0.0",
    "DownloadUrl": "https://yourstorageaccount.blob.core.windows.net/client-packages/SecureBootWatcher-Client-1.6.0.zip",
    "IsUpdateRequired": false,
    "ReleaseNotes": "Version 1.6.0 - New Features: ...",
    "Checksum": "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855",
    "FileSize": 5242880,
    "PackagePath": ""
}
```

#### 4. Riavvia l'API

```powershell
# Se ospitato su IIS
iisreset

# Se eseguito come servizio Windows
Restart-Service "SecureBootDashboard.Api"

# Se in sviluppo
# Ctrl+C per fermare, poi riavvia con:
dotnet run --project SecureBootDashboard.Api
```

#### 5. Verifica

```powershell
# Controlla che l'API restituisca la nuova versione
Invoke-RestMethod -Uri "https://localhost:5001/api/ClientUpdate/version" | Format-List
```

---

## ?? Script di Automazione (Futuro Enhancement)

### Script PowerShell per Aggiornamento Automatico

```powershell
# scripts\Update-ClientVersion.ps1

param(
    [Parameter(Mandatory = $true)]
    [string]$NewVersion,
    
    [Parameter(Mandatory = $false)]
    [string]$ReleaseNotes = "",
    
    [Parameter(Mandatory = $false)]
    [string]$PackagePath = "",
    
    [Parameter(Mandatory = $false)]
    [switch]$UpdateApiConfig
)

Write-Host "Updating client version to: $NewVersion" -ForegroundColor Cyan

# 1. Calcola checksum se PackagePath è fornito
$checksum = ""
$fileSize = 0

if (-not [string]::IsNullOrEmpty($PackagePath) -and (Test-Path $PackagePath)) {
    Write-Host "Calculating checksum for: $PackagePath"
    $hash = Get-FileHash $PackagePath -Algorithm SHA256
    $checksum = $hash.Hash
    $fileSize = (Get-Item $PackagePath).Length
    
    Write-Host "  Checksum: $checksum"
    Write-Host "  File Size: $fileSize bytes"
}

# 2. Aggiorna appsettings.json
if ($UpdateApiConfig) {
    $appsettingsPath = "SecureBootDashboard.Api\appsettings.json"
    
    Write-Host "Updating API configuration: $appsettingsPath"
    
    $config = Get-Content $appsettingsPath -Raw | ConvertFrom-Json
    $config.ClientUpdate.LatestVersion = $NewVersion
    $config.ClientUpdate.ReleaseDate = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    
    if (-not [string]::IsNullOrEmpty($ReleaseNotes)) {
        $config.ClientUpdate.ReleaseNotes = $ReleaseNotes
    }
    
    if (-not [string]::IsNullOrEmpty($checksum)) {
        $config.ClientUpdate.Checksum = $checksum
        $config.ClientUpdate.FileSize = $fileSize
    }
    
    $config | ConvertTo-Json -Depth 10 | Set-Content $appsettingsPath
    
    Write-Host "  ? API configuration updated" -ForegroundColor Green
}

Write-Host ""
Write-Host "Version update complete!" -ForegroundColor Green
Write-Host "  New Version: $NewVersion"
Write-Host "  Release Date: $(Get-Date -Format 'yyyy-MM-dd')"
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Restart API service"
Write-Host "  2. Verify version endpoint: GET /api/ClientUpdate/version"
Write-Host "  3. Deploy updated client to devices"
```

**Uso**:
```powershell
# Aggiorna alla versione 1.6.0
.\scripts\Update-ClientVersion.ps1 `
    -NewVersion "1.6.0.0" `
    -ReleaseNotes "Version 1.6.0 - New Features: XYZ" `
    -PackagePath ".\release\SecureBootWatcher-Client-1.6.0.zip" `
    -UpdateApiConfig
```

---

## ?? Best Practices

### 1. Controllo Versioni

- ? Usa **Semantic Versioning** (MAJOR.MINOR.PATCH.BUILD)
- ? Incrementa **MAJOR** per breaking changes
- ? Incrementa **MINOR** per nuove funzionalità
- ? Incrementa **PATCH** per bug fix
- ? **BUILD** è gestito automaticamente da GitVersioning

### 2. Documentazione Release

- ? Mantieni note di rilascio dettagliate in `ReleaseNotes`
- ? Includi informazioni su bug fix critici
- ? Menziona eventuali breaking changes
- ? Elenca nuove funzionalità aggiunte

### 3. Gestione Pacchetti

- ? Calcola sempre il checksum SHA256
- ? Conserva pacchetti precedenti per rollback
- ? Usa Azure Blob Storage per hosting centralizzato
- ? Implementa versionamento pacchetti (es. `-1.5.0.zip`)

### 4. Testing

- ? Testa update check con versioni vecchie
- ? Verifica notifiche client
- ? Controlla log API
- ? Valida checksum dopo download

### 5. Deployment

- ? Aggiorna API prima del deployment client
- ? Testa in ambiente di staging prima di produzione
- ? Monitora dashboard per adoption rate
- ? Mantieni documentazione di deployment aggiornata

---

## ?? Configurazione Avanzata

### MinimumVersion (Versione Minima Obbligatoria)

Per rendere un aggiornamento **obbligatorio**, imposta `MinimumVersion`:

```json
"ClientUpdate": {
    "LatestVersion": "1.5.0.0",
    "MinimumVersion": "1.5.0.0",  // ?? Versioni < 1.5.0.0 sono obbligate ad aggiornare
    "IsUpdateRequired": true
}
```

**Risultato**: Client con versione `1.4.0.0` riceverà:
```json
{
  "updateAvailable": true,
  "updateRequired": true  // ?? Update obbligatorio!
}
```

### Alerting sui Client Obsoleti

Nel report client, se `UpdateRequired = true`:

```
?? CLIENT UPDATE REQUIRED: Version 1.5.0.0 is available (current: 1.4.0.0). Update is mandatory.
```

---

## ?? Riferimenti

### File Modificati
- ? `SecureBootDashboard.Api\appsettings.json`

### File Correlati
- `SecureBootDashboard.Api\Controllers\ClientUpdateController.cs` - Controller che legge la configurazione
- `SecureBootWatcher.Client\Services\ClientUpdateService.cs` - Servizio client per update check
- `SecureBootWatcher.Client\Services\ReportBuilder.cs` - Integrazione alerting
- `docs\CLIENT_VERSION_TRACKING.md` - Documentazione tracking versioni
- `docs\PUBLISHING_CLIENT_VERSIONS.md` - Guida pubblicazione versioni

---

## ? Checklist Verifica

**Pre-Deployment**:
- [x] `LatestVersion` aggiornata a `1.5.0.0`
- [x] `ReleaseDate` aggiornata a data corrente
- [x] `ReleaseNotes` aggiornate con dettagli v1.5.0
- [x] Build API riuscita
- [ ] Test endpoint `/api/ClientUpdate/version`
- [ ] Test endpoint `/api/ClientUpdate/check?currentVersion=1.0.0.0`

**Post-Deployment**:
- [ ] Restart API service
- [ ] Verifica log API per update checks
- [ ] Controlla dashboard client versions
- [ ] Monitora adoption rate nuova versione

---

## ?? Summary

? **Problema**: API restituiva versione `1.0.0.0` invece di `1.5.0.0`  
? **Causa**: Configurazione hardcoded obsoleta in `appsettings.json`  
? **Soluzione**: Aggiornamento manuale configurazione `ClientUpdate`  
? **Impatto**: Tutti i client ora ricevono correttamente le notifiche di aggiornamento  
? **Testing**: Verificare endpoint API e log client  
? **Deployment**: Riavviare API dopo la modifica  

**Status**: ? **RISOLTO E PRONTO PER DEPLOY**

---

**Fix Applicata**: 2025-01-11  
**Versione Corretta**: `1.5.0.0`  
**Prossimo Rilascio**: `1.6.0.0` (TBD)

