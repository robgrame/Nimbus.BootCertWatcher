# ?? Client Versions Page - API Integration Fix

## ?? Problema Rilevato

### Sintomi
La pagina "Client Versions" (`/ClientVersions`) mostrava sempre versioni hardcoded obsolete:

```
Latest version: 1.0.0.0 | Minimum supported: 1.0.0.0
```

Anche dopo aver aggiornato `SecureBootDashboard.Api\appsettings.json` con:
```json
{
  "ClientUpdate": {
    "LatestVersion": "1.5.0.0",
    "MinimumVersion": "1.0.0.0"
  }
}
```

### Causa Root
Il `ClientVersionsModel.OnGetAsync()` leggeva la configurazione **dalla Web app locale**:

```csharp
// ? SBAGLIATO: Legge dalla Web app, non dall'API
LatestVersion = _configuration["ClientUpdate:LatestVersion"] ?? "1.0.0.0";
MinimumVersion = _configuration["ClientUpdate:MinimumVersion"] ?? "1.0.0.0";
```

La sezione `ClientUpdate` **non esiste** in `SecureBootDashboard.Web\appsettings.json`, quindi venivano usati i valori di fallback hardcoded (`1.0.0.0`).

---

## ? Soluzione Implementata

### Modifiche a `ClientVersions.cshtml.cs`

**File**: `SecureBootDashboard.Web\Pages\ClientVersions.cshtml.cs`

#### 1. Aggiunta Dependency Injection per `IHttpClientFactory`

```csharp
private readonly IHttpClientFactory _httpClientFactory;

public ClientVersionsModel(
    ISecureBootApiClient apiClient, 
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory)  // ? Nuovo parametro
{
    _apiClient = apiClient;
    _configuration = configuration;
    _httpClientFactory = httpClientFactory;  // ? Nuovo campo
}
```

#### 2. Nuovo Metodo per Chiamare l'API

```csharp
private async Task LoadVersionInfoFromApiAsync()
{
    try
    {
        var apiBaseUrl = _configuration["ApiSettings:BaseUrl"];
        if (string.IsNullOrEmpty(apiBaseUrl))
        {
            // Fallback to hardcoded values if API URL not configured
            return;
        }

        var httpClient = _httpClientFactory.CreateClient();
        var response = await httpClient.GetFromJsonAsync<ClientUpdateVersionInfo>(
            $"{apiBaseUrl}/api/ClientUpdate/version",
            HttpContext.RequestAborted);

        if (response != null)
        {
            LatestVersion = response.LatestVersion;
            MinimumVersion = response.MinimumVersion;
        }
    }
    catch (Exception)
    {
        // If API call fails, use fallback values (already set)
    }
}
```

#### 3. Chiamata nel Metodo `OnGetAsync()`

```csharp
public async Task OnGetAsync()
{
    try
    {
        // ? CORRETTO: Recupera da API endpoint
        await LoadVersionInfoFromApiAsync();

        var latestVer = Version.Parse(LatestVersion);
        var minimumVer = Version.Parse(MinimumVersion);

        // ... resto del codice
    }
    catch (Exception ex)
    {
        ErrorMessage = $"Error loading client versions: {ex.Message}";
    }
}
```

#### 4. Nuovo DTO per la Risposta API

```csharp
// DTO for API response
internal class ClientUpdateVersionInfo
{
    public string LatestVersion { get; set; } = "1.0.0.0";
    public string MinimumVersion { get; set; } = "1.0.0.0";
    public DateTime ReleaseDate { get; set; }
    public string? DownloadUrl { get; set; }
    public bool IsUpdateRequired { get; set; }
    public string? ReleaseNotes { get; set; }
    public string? Checksum { get; set; }
    public long FileSize { get; set; }
}
```

---

## ?? Comportamento Atteso Dopo la Fix

### Scenario 1: API Configurata e Disponibile

**Configurazione Web** (`SecureBootDashboard.Web\appsettings.json`):
```json
{
  "ApiSettings": {
    "BaseUrl": "https://localhost:5001"
  }
}
```

**Configurazione API** (`SecureBootDashboard.Api\appsettings.json`):
```json
{
  "ClientUpdate": {
    "LatestVersion": "1.5.0.0",
    "MinimumVersion": "1.0.0.0"
  }
}
```

**Risultato**:
```
Latest version: 1.5.0.0 | Minimum supported: 1.0.0.0  ?
```

**Flusso**:
1. Utente naviga a `/ClientVersions`
2. `OnGetAsync()` chiama `LoadVersionInfoFromApiAsync()`
3. HTTP GET a `https://localhost:5001/api/ClientUpdate/version`
4. API restituisce `{ "latestVersion": "1.5.0.0", "minimumVersion": "1.0.0.0" }`
5. Versioni aggiornate nella pagina

---

### Scenario 2: API Non Disponibile (Fallback)

**Se l'API è offline o non risponde**:

**Risultato**:
```
Latest version: 1.0.0.0 | Minimum supported: 1.0.0.0  ?? Fallback
```

**Flusso**:
1. `LoadVersionInfoFromApiAsync()` tenta la chiamata
2. Timeout o errore HTTP
3. Catch block cattura l'eccezione
4. Valori di fallback rimangono (`1.0.0.0`)
5. Pagina funziona comunque (graceful degradation)

---

### Scenario 3: API URL Non Configurato

**Se `ApiSettings:BaseUrl` è vuoto**:

```json
{
  "ApiSettings": {
    "BaseUrl": ""  // ?? Vuoto
  }
}
```

**Risultato**:
```
Latest version: 1.0.0.0 | Minimum supported: 1.0.0.0  ?? Fallback
```

**Flusso**:
1. `LoadVersionInfoFromApiAsync()` controlla `apiBaseUrl`
2. È `null` o vuoto ? `return` anticipato
3. Nessuna chiamata API
4. Valori di fallback usati

---

## ?? Testing

### Test Case 1: API Online e Configurata

**Prerequisiti**:
- API in esecuzione su `https://localhost:5001`
- `appsettings.json` API con `LatestVersion: "1.5.0.0"`
- Web app configurata con `ApiSettings:BaseUrl: "https://localhost:5001"`

**Passi**:
```powershell
# 1. Avvia API
cd SecureBootDashboard.Api
dotnet run

# 2. Avvia Web
cd SecureBootDashboard.Web
dotnet run

# 3. Naviga a https://localhost:7001/ClientVersions
```

**Verifica**:
- [ ] Header mostra "Latest version: **1.5.0.0**"
- [ ] Header mostra "Minimum supported: **1.0.0.0**"
- [ ] Nessun errore nella console browser
- [ ] Nessun errore nei log Web app

**Verifica Network (F12 ? Network)**:
```
Request: GET https://localhost:5001/api/ClientUpdate/version
Status: 200 OK
Response: { "latestVersion": "1.5.0.0", "minimumVersion": "1.0.0.0", ... }
```

---

### Test Case 2: API Offline (Fallback)

**Prerequisiti**:
- API **non** in esecuzione
- Web app in esecuzione

**Passi**:
```powershell
# 1. Ferma API (se in esecuzione)
# Ctrl+C nel terminale API

# 2. Avvia solo Web
cd SecureBootDashboard.Web
dotnet run

# 3. Naviga a https://localhost:7001/ClientVersions
```

**Verifica**:
- [ ] Pagina carica senza crash
- [ ] Header mostra "Latest version: **1.0.0.0**" (fallback)
- [ ] Nessun errore visibile all'utente
- [ ] Log Web app mostra errore di connessione (normale)

**Verifica Network (F12 ? Network)**:
```
Request: GET https://localhost:5001/api/ClientUpdate/version
Status: Failed (ERR_CONNECTION_REFUSED o Timeout)
```

---

### Test Case 3: Cambio Versione API

**Passi**:
```powershell
# 1. Modifica API appsettings.json
# "LatestVersion": "1.6.0.0"  (da 1.5.0.0)

# 2. Riavvia API
# Ctrl+C, poi dotnet run

# 3. Refresh pagina Client Versions (F5)
```

**Verifica**:
- [ ] Header mostra "Latest version: **1.6.0.0**" (aggiornato)
- [ ] Device con versione `1.5.0.48182` ora mostrati come "Outdated" (giallo)

---

### Test Case 4: Verifica Calcolo Status

**Scenario**: Client con versioni diverse

**Devices nel DB**:
```sql
INSERT INTO Devices (MachineName, ClientVersion, ...)
VALUES 
  ('PC-001', '1.5.0.48182', ...),  -- Outdated (< 1.5.0.0 API latest? No, >= quindi Up-to-Date se latest fosse 1.5.0.0)
  ('PC-002', '1.0.0.47500', ...),  -- Unsupported (< 1.0.0.0 minimum? No, quindi Outdated se minimum fosse 1.5.0.0)
  ('PC-003', NULL, ...);           -- Unknown
```

Con API config:
```json
{
  "LatestVersion": "1.5.0.0",
  "MinimumVersion": "1.0.0.0"
}
```

**Risultato Atteso**:
```
???????????????????????????????????????????????
? Total: 3 | Up-to-Date: 1 | Outdated: 1 | Unsupported: 0 | Unknown: 1
???????????????????????????????????????????????

Version: 1.5.0.48182 [Up-to-Date ?]
  - PC-001

Version: 1.0.0.47500 [Outdated ??]
  - PC-002

Version: Unknown [Unsupported ?]
  - PC-003
```

**Verifica**:
- [ ] PC-001 con versione `1.5.0.48182` è **Up-to-Date** (>= 1.5.0.0)
- [ ] PC-002 con versione `1.0.0.47500` è **Outdated** (< 1.5.0.0 ma >= 1.0.0.0)
- [ ] PC-003 con versione `null` è **Unknown** (e considerato Unsupported)

---

## ?? Comparazione Versioni

### Logica di Confronto

```csharp
var currentVer = Version.Parse(group.Version);  // Es. "1.5.0.48182"

group.IsLatest = currentVer >= latestVer;        // >= 1.5.0.0 ? true
group.IsOutdated = currentVer < latestVer;      // < 1.5.0.0 ? false
group.IsUnsupported = currentVer < minimumVer;  // < 1.0.0.0 ? false
```

### Tabella di Verità

| Device Version | Latest (API) | Minimum (API) | IsLatest | IsOutdated | IsUnsupported | Badge |
|----------------|--------------|---------------|----------|------------|---------------|-------|
| 1.6.0.50000 | 1.5.0.0 | 1.0.0.0 | ? True | ? False | ? False | Green (Up-to-Date) |
| 1.5.0.48182 | 1.5.0.0 | 1.0.0.0 | ? True | ? False | ? False | Green (Up-to-Date) |
| 1.4.0.48000 | 1.5.0.0 | 1.0.0.0 | ? False | ? True | ? False | Yellow (Outdated) |
| 1.0.0.47500 | 1.5.0.0 | 1.0.0.0 | ? False | ? True | ? False | Yellow (Outdated) |
| 0.9.0.47000 | 1.5.0.0 | 1.0.0.0 | ? False | ? True | ? True | Red (Unsupported) |
| NULL | 1.5.0.0 | 1.0.0.0 | ? False | ? True | ? True | Red (Unknown) |

---

## ?? Configurazione Necessaria

### Web App (`SecureBootDashboard.Web\appsettings.json`)

**Aggiungere (se non presente)**:
```json
{
  "ApiSettings": {
    "BaseUrl": "https://localhost:5001"
  }
}
```

**Produzione** (Azure App Service):
```json
{
  "ApiSettings": {
    "BaseUrl": "https://your-api.azurewebsites.net"
  }
}
```

### API (`SecureBootDashboard.Api\appsettings.json`)

**Già configurato** (dalla fix precedente):
```json
{
  "ClientUpdate": {
    "LatestVersion": "1.5.0.0",
    "ReleaseDate": "2025-01-11T00:00:00Z",
    "MinimumVersion": "1.0.0.0",
    "DownloadUrl": "https://yourstorageaccount.blob.core.windows.net/client-packages/SecureBootWatcher-Client-latest.zip",
    "IsUpdateRequired": false,
    "ReleaseNotes": "Version 1.5.0 - WebAPI Sink Retry & Failover Fix...",
    "Checksum": "",
    "FileSize": 0,
    "PackagePath": ""
  }
}
```

---

## ?? Workflow di Aggiornamento Versione

### 1. Rilascio Nuova Versione Client (es. 1.6.0)

**a) Build e Package**:
```powershell
cd SecureBootWatcher.Client
dotnet publish -c Release -o ./publish
Compress-Archive -Path ./publish/* -DestinationPath "./SecureBootWatcher-Client-1.6.0.zip"
```

**b) Upload a Storage (opzionale)**:
```powershell
az storage blob upload `
    --account-name "yourstorageaccount" `
    --container-name "client-packages" `
    --name "SecureBootWatcher-Client-1.6.0.zip" `
    --file "./SecureBootWatcher-Client-1.6.0.zip"
```

**c) Aggiorna API `appsettings.json`**:
```json
{
  "ClientUpdate": {
    "LatestVersion": "1.6.0.0",  // ? Aggiornato
    "ReleaseDate": "2025-01-15T00:00:00Z",
    "DownloadUrl": "https://yourstorageaccount.blob.core.windows.net/client-packages/SecureBootWatcher-Client-1.6.0.zip",
    "ReleaseNotes": "Version 1.6.0 - New Features: XYZ"
  }
}
```

**d) Riavvia API**:
```powershell
# Se su IIS
iisreset

# Se servizio Windows
Restart-Service "SecureBootDashboard.Api"

# Se in sviluppo
# Ctrl+C, poi dotnet run
```

---

### 2. Verifica Pagina Client Versions

**a) Naviga a `/ClientVersions`**:
```
https://localhost:7001/ClientVersions
```

**b) Verifica Header**:
```
Latest version: 1.6.0.0 ?  (aggiornato automaticamente)
```

**c) Verifica Badge Device**:
- Device con `1.5.0.48182` ? Ora mostrati come **Outdated** (?? giallo)
- Device con `1.6.0.50000` ? Mostrati come **Up-to-Date** (? verde)

---

## ?? Best Practices

### 1. Gestione Errori API

**Implementazione Attuale**:
```csharp
catch (Exception)
{
    // If API call fails, use fallback values (already set)
}
```

**Logging Avanzato** (opzionale):
```csharp
catch (Exception ex)
{
    _logger.LogWarning(ex, "Failed to load version info from API. Using fallback values.");
    // Continua con valori di fallback
}
```

### 2. Caching (Opzionale per Performance)

Se l'API è molto lenta, considera il caching:

```csharp
// In Program.cs della Web app
builder.Services.AddMemoryCache();

// In ClientVersionsModel
private readonly IMemoryCache _cache;

private async Task LoadVersionInfoFromApiAsync()
{
    const string cacheKey = "ClientVersionInfo";
    
    if (_cache.TryGetValue(cacheKey, out ClientUpdateVersionInfo? cached))
    {
        LatestVersion = cached.LatestVersion;
        MinimumVersion = cached.MinimumVersion;
        return;
    }

    // Chiamata API...
    if (response != null)
    {
        _cache.Set(cacheKey, response, TimeSpan.FromMinutes(5));
        LatestVersion = response.LatestVersion;
        MinimumVersion = response.MinimumVersion;
    }
}
```

### 3. Resilience (Opzionale con Polly)

Per retry automatici su errori transitori:

```csharp
// In Program.cs della Web app
builder.Services.AddHttpClient()
    .AddTransientHttpErrorPolicy(builder =>
        builder.WaitAndRetryAsync(3, retryAttempt =>
            TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

// La chiamata API userà automaticamente retry
```

### 4. Health Check

Verifica che l'API sia raggiungibile:

```csharp
// In Program.cs della Web app
builder.Services.AddHealthChecks()
    .AddUrlGroup(new Uri($"{apiBaseUrl}/health"), "API Health");
```

---

## ?? Riferimenti

### File Modificati
- ? `SecureBootDashboard.Web\Pages\ClientVersions.cshtml.cs`

### Endpoint API Usato
- `GET /api/ClientUpdate/version` (definito in `ClientUpdateController.cs`)

### Configurazione
- **Web App**: `ApiSettings:BaseUrl` in `appsettings.json`
- **API**: `ClientUpdate` section in `appsettings.json`

### Documentazione Correlata
- `docs\CLIENT_VERSION_API_CONFIG_FIX.md` - Fix precedente configurazione API
- `docs\CLIENT_VERSION_TRACKING.md` - Sistema tracking versioni
- `docs\CLIENT_VERSIONS_DASHBOARD.md` - Documentazione pagina dashboard

---

## ? Checklist Verifica

**Build & Compile**:
- [x] Build riuscita senza errori
- [x] Nessun warning di compilazione
- [x] Dependency injection `IHttpClientFactory` registrata

**Funzionalità**:
- [ ] Pagina `/ClientVersions` carica senza errori
- [ ] Header mostra versione corretta dall'API
- [ ] Fallback funziona se API offline
- [ ] Badge versione corretti (verde/giallo/rosso)

**Testing**:
- [ ] Test Case 1: API Online e Configurata
- [ ] Test Case 2: API Offline (Fallback)
- [ ] Test Case 3: Cambio Versione API
- [ ] Test Case 4: Verifica Calcolo Status

**Configurazione**:
- [ ] `ApiSettings:BaseUrl` configurato in Web `appsettings.json`
- [ ] `ClientUpdate:LatestVersion` aggiornato in API `appsettings.json`
- [ ] API in esecuzione e raggiungibile

---

## ?? Summary

? **Problema Risolto**: Pagina Client Versions ora mostra versione corretta dall'API  

**Prima**: Hardcoded `1.0.0.0` (lettura da config locale Web)  
**Dopo**: Dinamico `1.5.0.0` (lettura da endpoint API `/api/ClientUpdate/version`)  

**Modifiche**:
- Aggiunto `IHttpClientFactory` a dependency injection
- Creato metodo `LoadVersionInfoFromApiAsync()`
- Aggiunto DTO `ClientUpdateVersionInfo` per risposta API
- Implementato graceful degradation (fallback se API offline)

**Vantaggi**:
- ? Versione sempre sincronizzata con API
- ? Single source of truth (API `appsettings.json`)
- ? Nessun deployment Web app necessario per cambio versione
- ? Fallback funzionale se API temporaneamente offline

**Testing Necessario**: Verificare con API online e offline

---

**Fix Applicata**: 2025-01-11  
**Status**: ? **COMPLETO E PRONTO PER TESTING**  
**Breaking Changes**: ? None  
**Backward Compatible**: ? Yes (fallback a `1.0.0.0`)

