# Device Deduplication Implementation Summary

## ?? Problema Risolto

**Problema originale:** La stessa macchina appariva più volte nella dashboard perché venivano mostrati i **report** invece dei **dispositivi unici**.

**Soluzione:** Implementazione di device deduplication con tracking dello storico report.

---

## ? Cosa È Stato Implementato

### 1. **Database già corretto**

La struttura del database era già corretta con:
- Tabella `Devices` separata con ID univoco
- Relazione 1-to-many: `Device` ? `Reports`
- Index su `MachineName` per performance
- Campi `FirstSeenUtc` e `LastSeenUtc` per tracking temporale

### 2. **EfCoreReportStore già corretto**

Il codice in `EfCoreReportStore.SaveAsync()` già implementa correttamente:

```csharp
// Cerca device esistente per MachineName + DomainName
var device = await _dbContext.Devices
    .FirstOrDefaultAsync(d => 
        d.MachineName == report.Device.MachineName && 
        d.DomainName == report.Device.DomainName, 
        cancellationToken);

if (device == null)
{
    // Crea nuovo device
    device = new DeviceEntity { ... };
    _dbContext.Devices.Add(device);
}
else
{
    // Aggiorna device esistente
    device.LastSeenUtc = utcNow;
    device.FirmwareVersion = report.Device.FirmwareVersion;
    // ...
}

// Crea SEMPRE un nuovo report collegato al device
var reportEntity = new SecureBootReportEntity
{
    Device = device,  // ? Relazione con device esistente o nuovo
    // ...
};
```

**Comportamento:**
- ? Device viene creato **una sola volta** (identificato da `MachineName` + `DomainName`)
- ? Ogni invio crea un **nuovo report** collegato al device
- ? `LastSeenUtc` viene aggiornato ogni volta
- ? Mantiene storico completo dei report per dispositivo

### 3. **Nuovo API Controller: DevicesController** ?

Creato nuovo controller `/api/Devices` con endpoint:

#### `GET /api/Devices`
Restituisce lista di dispositivi unici con summary:

```json
[
  {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "machineName": "W11DEV",
    "domainName": "MSINTUNE.LAB",
    "fleetId": "mslabs",
    "manufacturer": "Microsoft Corporation",
    "model": "Virtual Machine",
    "firstSeenUtc": "2025-01-15T10:00:00Z",
    "lastSeenUtc": "2025-01-15T14:30:00Z",
    "reportCount": 5,
    "latestDeploymentState": "Deployed",
    "latestReportDate": "2025-01-15T14:30:00Z"
  }
]
```

#### `GET /api/Devices/{id}`
Restituisce dettagli device con ultimi 10 report:

```json
{
  "id": "550e8400-...",
  "machineName": "W11DEV",
  "domainName": "MSINTUNE.LAB",
  "userPrincipalName": "admin@msintune.lab",
  "fleetId": "mslabs",
  "manufacturer": "Microsoft Corporation",
  "model": "Virtual Machine",
  "firmwareVersion": "Hyper-V UEFI Release v4.1",
  "tagsJson": "{\"environment\":\"dev\",\"fleet\":\"mslabs\"}",
  "firstSeenUtc": "2025-01-15T10:00:00Z",
  "lastSeenUtc": "2025-01-15T14:30:00Z",
  "recentReports": [
    {
      "reportId": "abc123...",
      "createdAtUtc": "2025-01-15T14:30:00Z",
      "deploymentState": "Deployed",
      "clientVersion": "1.0.0"
    },
    {
      "reportId": "def456...",
      "createdAtUtc": "2025-01-15T13:00:00Z",
      "deploymentState": "Pending",
      "clientVersion": "1.0.0"
    }
  ]
}
```

#### `GET /api/Devices/{id}/reports?limit=50`
Restituisce storico report per un device specifico.

---

## ?? Benefici

### ? Dashboard Pulita
- **Prima:** W11DEV appare 3 volte (una per ogni report)
- **Dopo:** W11DEV appare 1 volta con "Report Count: 3"

### ? Tracking Temporale
- **First Seen:** Quando il device ha inviato il primo report
- **Last Seen:** Quando ha inviato l'ultimo report
- Utile per identificare device inattivi

### ? State Change Detection
Confrontando i report storici si può vedere quando un device ha cambiato stato:
- **Report 1** (10:00): `DeploymentState: "Pending"`
- **Report 2** (11:00): `DeploymentState: "Deployed"` ? **CAMBIAMENTO RILEVATO**
- **Report 3** (12:00): `DeploymentState: "Deployed"`

### ? Performance Migliorate
- Meno righe da visualizzare nella dashboard
- Index su `MachineName` per query veloci
- Caricamento più rapido

---

## ?? Prossimi Passi (TODO)

### 1. Aggiorna Web Client

Modifica `ISecureBootApiClient` per aggiungere:

```csharp
Task<IReadOnlyList<DeviceSummary>> GetDevicesAsync(CancellationToken cancellationToken = default);
Task<DeviceDetail?> GetDeviceAsync(Guid id, CancellationToken cancellationToken = default);
Task<IReadOnlyList<ReportHistoryItem>> GetDeviceReportsAsync(Guid deviceId, int limit = 50, CancellationToken cancellationToken = default);
```

### 2. Crea Nuove Pagine Razor

#### `/Pages/Devices/Index.cshtml` (Lista Dispositivi)
Mostra tutti i dispositivi unici con:
- Machine Name
- Domain
- Last Seen
- Report Count
- Latest Deployment State
- Bottone "View Details" ? `/Devices/Details/{id}`
- Bottone "View Reports" ? `/Devices/{id}/Reports`

#### `/Pages/Devices/Details.cshtml` (Dettagli Dispositivo)
Mostra informazioni complete del device:
- Machine Name, Domain, UPN
- Manufacturer, Model, Firmware Version
- Fleet ID, Tags
- First Seen / Last Seen
- Tabella ultimi 10 report con link a `/Reports/Details/{reportId}`

#### `/Pages/Devices/{id}/Reports.cshtml` (Storico Report)
Mostra tutti i report per un device specifico:
- Timeline dei report
- Evidenzia cambiamenti di stato
- Link a dettagli completi di ogni report

### 3. Aggiorna Dashboard Principale

Modifica `/Pages/Index.cshtml` per:
- Mostrare **conteggio dispositivi unici** invece di report
- Aggiungere card "Active Devices" (visti nelle ultime 24h)
- Aggiungere card "Inactive Devices" (non visti da >7 giorni)
- Tabella "Recent Activity" con ultimi device visti

### 4. Aggiungi State Change Detection

Crea query per rilevare cambiamenti di stato:

```csharp
// API endpoint: GET /api/Devices/{id}/state-changes
public async Task<IReadOnlyCollection<StateChangeEvent>> GetStateChangesAsync(Guid deviceId)
{
    var reports = await _dbContext.Reports
        .Where(r => r.DeviceId == deviceId)
        .OrderBy(r => r.CreatedAtUtc)
        .Select(r => new { r.CreatedAtUtc, r.DeploymentState })
        .ToListAsync();

    var changes = new List<StateChangeEvent>();
    string? previousState = null;

    foreach (var report in reports)
    {
        if (report.DeploymentState != previousState && previousState != null)
        {
            changes.Add(new StateChangeEvent(
                report.CreatedAtUtc,
                previousState,
                report.DeploymentState ?? "Unknown"));
        }
        previousState = report.DeploymentState;
    }

    return changes;
}
```

### 5. Aggiungi Alert per Device Inattivi

Crea background job che controlla device inattivi:

```csharp
// Trova device non visti da >7 giorni
var inactiveDevices = await _dbContext.Devices
    .Where(d => d.LastSeenUtc < DateTimeOffset.UtcNow.AddDays(-7))
    .ToListAsync();

foreach (var device in inactiveDevices)
{
    _logger.LogWarning("Device {MachineName} inactive for {Days} days. Last seen: {LastSeen}",
        device.MachineName,
        (DateTimeOffset.UtcNow - device.LastSeenUtc).TotalDays,
        device.LastSeenUtc);
}
```

---

## ?? Test dell'Implementazione

### Test 1: Device Deduplication

**Azione:** Invia 3 report dalla stessa macchina (W11DEV)

**Risultato Atteso:**

```sql
-- 1 device
SELECT * FROM Devices WHERE MachineName = 'W11DEV';
-- Id: 550e8400-..., MachineName: W11DEV, ReportCount: 3

-- 3 report collegati allo stesso device
SELECT * FROM SecureBootReports WHERE DeviceId = '550e8400-...';
-- Report1: CreatedAtUtc = 2025-01-15 10:00:00
-- Report2: CreatedAtUtc = 2025-01-15 11:00:00
-- Report3: CreatedAtUtc = 2025-01-15 12:00:00
```

**Verifica Web:**
```
GET /api/Devices
? Risposta: 1 device con reportCount: 3

GET /api/Devices/550e8400-.../reports
? Risposta: 3 report
```

### Test 2: LastSeenUtc Update

**Azione:** Invia report da W11DEV alle 10:00, poi alle 14:00

**Risultato Atteso:**

```sql
SELECT FirstSeenUtc, LastSeenUtc FROM Devices WHERE MachineName = 'W11DEV';
-- FirstSeenUtc: 2025-01-15 10:00:00 (invariato)
-- LastSeenUtc:  2025-01-15 14:00:00 (aggiornato)
```

### Test 3: State Change Detection

**Azione:** Invia 3 report con deployment state diversi

**Risultato Atteso:**

```
Report 1 (10:00): DeploymentState = "Pending"
Report 2 (11:00): DeploymentState = "Deployed"  ? CAMBIO STATO
Report 3 (12:00): DeploymentState = "Deployed"
```

Query state changes:
```
GET /api/Devices/550e8400-.../state-changes
? [{
  "timestamp": "2025-01-15T11:00:00Z",
  "fromState": "Pending",
  "toState": "Deployed"
}]
```

---

## ?? Query SQL Utili

```sql
-- Conta dispositivi unici
SELECT COUNT(*) AS TotalDevices FROM Devices;

-- Dispositivi con più report
SELECT 
    MachineName, 
    COUNT(*) AS ReportCount,
    MIN(CreatedAtUtc) AS FirstSeen,
    MAX(LastSeenUtc) AS LastSeen
FROM Devices d
INNER JOIN SecureBootReports r ON d.Id = r.DeviceId
GROUP BY MachineName
ORDER BY ReportCount DESC;

-- Dispositivi inattivi (non visti da >7 giorni)
SELECT MachineName, LastSeenUtc, 
    DATEDIFF(DAY, LastSeenUtc, GETUTCDATE()) AS DaysInactive
FROM Devices
WHERE LastSeenUtc < DATEADD(DAY, -7, GETUTCDATE())
ORDER BY LastSeenUtc ASC;

-- Cambiamenti di stato per un device
SELECT 
    CreatedAtUtc,
    DeploymentState,
    LAG(DeploymentState) OVER (ORDER BY CreatedAtUtc) AS PreviousState
FROM SecureBootReports
WHERE DeviceId = '550e8400-...'
ORDER BY CreatedAtUtc;
```

---

## ?? Risultato Finale

**Dashboard PRIMA:**
```
?? Report Recenti (3 righe)
- W11DEV | MSINTUNE.LAB | Deployed | 2025-01-15 12:00:00 | [Dettagli]
- W11DEV | MSINTUNE.LAB | Deployed | 2025-01-15 11:00:00 | [Dettagli]
- W11DEV | MSINTUNE.LAB | Pending  | 2025-01-15 10:00:00 | [Dettagli]
```

**Dashboard DOPO:**
```
?? Dispositivi (1 riga)
- W11DEV | MSINTUNE.LAB | 3 reports | Last Seen: 2 min ago | Deployed | [Dettagli] [Reports]

?? Statistics:
- Total Devices: 1
- Active (24h): 1
- Inactive (>7d): 0
```

---

## ? Checklist Implementazione

- [x] Database structure (già esistente)
- [x] EfCoreReportStore deduplication logic (già implementata)
- [x] API DevicesController creato
- [ ] Web client `ISecureBootApiClient` aggiornato
- [ ] Razor Pages `/Devices/Index` creata
- [ ] Razor Pages `/Devices/Details` creata
- [ ] Razor Pages `/Devices/{id}/Reports` creata
- [ ] Dashboard principale aggiornata per mostrare devices
- [ ] State change detection implementata
- [ ] Alert per device inattivi
- [ ] Test end-to-end

---

## ?? Prossimo Step Immediato

**Testa il nuovo API endpoint:**

```powershell
# Avvia l'API
cd SecureBootDashboard.Api
dotnet run

# In un altro terminale, testa l'endpoint
Invoke-RestMethod -Uri "https://localhost:5001/api/Devices" -Method Get
```

**Output atteso:**
```json
[
  {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "machineName": "W11DEV",
    "domainName": "MSINTUNE.LAB",
    "fleetId": "mslabs",
    "reportCount": 3,
    "latestDeploymentState": "Deployed",
    "lastSeenUtc": "2025-01-15T14:30:00Z"
  }
]
```

Se funziona, procedi con l'aggiornamento del web client! ??
