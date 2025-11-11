# ?? Guida Verifica SignalR Attivo

## Come Sapere se SignalR Viene Utilizzato

### Metodo 1: ? Verifica Visiva (30 secondi)

**Indicatore Visivo**: Guarda in alto a destra nella pagina web

| Icona | Colore | Significato | SignalR Attivo? |
|-------|--------|-------------|-----------------|
| ?? | Rosso pulsante | Errore connessione | ? Problema |
| ?? | Giallo pulsante | Riconnessione in corso | ? Temporaneo |
| ?? | Verde fisso | Connesso e funzionante | ? **SI** |
| ? | Grigio | Disconnesso | ? No |
| ? | Non visibile | Script non caricato | ? No |

**Come testare**:
1. Apri la dashboard: `https://localhost:7001`
2. Guarda in alto a destra (vicino al nome utente)
3. Passa il mouse sul pallino colorato
4. Leggi il tooltip:
   - ? "Real-time updates: Connected" ? **FUNZIONA**
   - ? "Real-time updates: Disconnected" ? Non connesso

---

### Metodo 2: ??? Console Browser (1 minuto)

**Passo 1**: Apri Developer Tools
- Premi `F12` oppure
- Click destro ? "Ispeziona"

**Passo 2**: Vai alla tab **"Console"**

**Passo 3**: Cerca questi messaggi

**? SignalR ATTIVO** - Dovresti vedere:
```javascript
[SignalR] Initializing connection to: https://localhost:5001/dashboardHub
[SignalR] Connected successfully with ID: 8a7f2c3d-1234-5678-90ab-cdef12345678
[SignalR] Subscribed to dashboard updates
```

**? SignalR NON ATTIVO** - Vedrai:
```javascript
[SignalR] SignalR library not loaded
// oppure nessun messaggio [SignalR]
```

---

### Metodo 3: ?? Network Tab (WebSocket)

**Passo 1**: Developer Tools ? Tab **"Network"**

**Passo 2**: Filtra per **"WS"** (WebSocket)

**Passo 3**: Cerca una connessione a `dashboardHub`

**? SignalR ATTIVO**:
```
Name: dashboardHub?id=8a7f2c3d...
Status: 101 Switching Protocols
Type: websocket
Size: (pending)
Time: (pending)
```

**Clicca sulla connessione** per vedere:
- **Headers**: `Upgrade: websocket`, `Connection: Upgrade`
- **Messages**: Messaggi in tempo reale tra client e server
- **Frames**: Dati binari WebSocket

**? SignalR NON ATTIVO**: Nessuna connessione WebSocket visibile

---

### Metodo 4: ?? Test Real-time (5 minuti)

Questo � il test definitivo per verificare che SignalR funzioni end-to-end.

**Passo 1**: Apri la dashboard nel browser

**Passo 2**: Apri Console Browser (F12 ? Console)

**Passo 3**: Invia un report dal client

Sul dispositivo client (o localmente):
```powershell
cd "C:\Program Files\SecureBootWatcher"
.\SecureBootWatcher.Client.exe
```

**Passo 4**: Guarda la Console Browser

**? SignalR FUNZIONA** - Vedrai:
```javascript
[SignalR] New report received: {
  deviceId: "a1b2c3d4-...",
  reportId: "e5f6g7h8-...",
  machineName: "DESKTOP-ABC123",
  timestamp: "2025-01-20T10:30:00Z"
}
```

**Bonus**: Dovresti anche vedere:
- Un **toast notification** pop-up in basso a destra
- Le **statistiche** sulla dashboard aggiornarsi automaticamente
- L'**elenco dispositivi** aggiornarsi senza refresh

---

### Metodo 5: ?? Verifica File Sorgenti

Controlla che i file SignalR siano presenti e inclusi:

#### A. File JavaScript Client

**File**: `SecureBootDashboard.Web/wwwroot/js/dashboard-realtime.js`

```powershell
# Verifica esistenza file
Test-Path "SecureBootDashboard.Web/wwwroot/js/dashboard-realtime.js"
# Output: True = OK, False = MANCANTE
```

#### B. File CSS Indicator

**File**: `SecureBootDashboard.Web/wwwroot/css/signalr.css`

```powershell
Test-Path "SecureBootDashboard.Web/wwwroot/css/signalr.css"
# Output: True = OK
```

#### C. Layout Include SignalR

**File**: `SecureBootDashboard.Web/Pages/Shared/_Layout.cshtml`

**Cerca queste righe**:
```html
<!-- SignalR CDN -->
<script src="https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/8.0.0/signalr.min.js" 
        integrity="sha512-P2X0sbc4zKJMoBK42bCLBLbltkGehjd+GQVsG7EEmVike6caqXWve+EWV+Tgmzx4qQ5YXQNpOJQKsXQy9Vthvg=="
        crossorigin="anonymous"></script>

<!-- SignalR CSS -->
<link rel="stylesheet" href="~/css/signalr.css" asp-append-version="true" />

<!-- SignalR Client -->
<script src="~/js/dashboard-realtime.js" asp-append-version="true"></script>

<!-- Indicatore connessione -->
<div id="signalr-status-indicator" class="signalr-status-indicator"></div>
```

**Se mancano** ? SignalR non � attivo

#### D. Hub nel Backend

**File**: `SecureBootDashboard.Api/Hubs/DashboardHub.cs`

```powershell
Test-Path "SecureBootDashboard.Api/Hubs/DashboardHub.cs"
# Output: True = OK
```

#### E. Configurazione in Program.cs

**File**: `SecureBootDashboard.Api/Program.cs`

**Cerca queste righe**:
```csharp
// SignalR configuration
builder.Services.AddSignalR(options =>
{
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
});

// Hub mapping
app.MapHub<DashboardHub>("/dashboardHub");
```

---

### Metodo 6: ?? Test Ping Diretto

Usa il metodo `Ping()` per testare la connessione:

**Passo 1**: Apri Console Browser (F12)

**Passo 2**: Esegui questo comando:
```javascript
// Verifica che il client sia inizializzato
if (window.dashboardClient) {
    // Invia ping
    window.dashboardClient.ping().then(result => {
        console.log('Ping result:', result);
    });
} else {
    console.error('SignalR client not initialized!');
}
```

**? SignalR ATTIVO**:
```
Ping result: "pong"
```

**? SignalR NON ATTIVO**:
```
SignalR client not initialized!
```

---

### Metodo 7: ?? Verifica Endpoint API

Controlla che l'endpoint SignalR risponda:

**Passo 1**: Apri browser e vai a:
```
https://localhost:5001/dashboardHub
```

**? SignalR ATTIVO**:
- Status: `405 Method Not Allowed` oppure
- Risposta JSON con errore (normale, perch� il browser non pu� connettersi direttamente)

**? SignalR NON ATTIVO**:
- Status: `404 Not Found`
- Endpoint non esiste

---

## ?? Checklist Rapida

Usa questa checklist per una verifica completa:

- [ ] **Indicatore visivo** verde in alto a destra
- [ ] **Console log** `[SignalR] Connected successfully`
- [ ] **Network tab** mostra connessione WebSocket
- [ ] **File JavaScript** `dashboard-realtime.js` presente
- [ ] **File CSS** `signalr.css` presente
- [ ] **Layout** include script SignalR
- [ ] **Hub backend** `DashboardHub.cs` presente
- [ ] **Program.cs** configura SignalR
- [ ] **Ping test** restituisce "pong"
- [ ] **Toast notification** appare quando arriva un report

**Se tutti i punti sono ? ? SignalR � PIENAMENTE FUNZIONANTE!**

---

## ?? Troubleshooting

### Problema 1: Indicatore Non Visibile

**Sintomo**: Nessun pallino colorato in alto a destra

**Causa Possibile**:
- Script non caricato
- CSS mancante
- Errore JavaScript

**Soluzione**:
```javascript
// 1. Verifica nella Console se ci sono errori
// 2. Controlla che i file siano caricati (Network tab)
// 3. Verifica che l'elemento esista:
document.getElementById('signalr-status-indicator')
// Output: <div id="signalr-status-indicator" ...> = OK
// Output: null = MANCANTE
```

### Problema 2: Connessione Fallisce

**Sintomo**: Indicatore rosso o grigio

**Causa Possibile**:
- API non avviata
- Porta sbagliata
- CORS issue
- Certificato HTTPS non valido

**Soluzione**:
```powershell
# 1. Verifica che l'API sia avviata
curl https://localhost:5001/health
# Output: "Healthy" = OK

# 2. Verifica endpoint SignalR
curl https://localhost:5001/dashboardHub
# Output: 405 = OK (non supporta GET diretto)
# Output: 404 = PROBLEMA (endpoint non esiste)

# 3. Controlla configurazione CORS (se necessario)
```

### Problema 3: Nessun Messaggio Real-time

**Sintomo**: Connesso ma nessun messaggio ricevuto

**Causa Possibile**:
- Non sottoscritto a gruppi
- Report non arriva all'API
- Broadcast non chiamato

**Soluzione**:
```javascript
// 1. Verifica sottoscrizioni nella Console
[SignalR] Subscribed to dashboard updates

// 2. Verifica che il report arrivi all'API (API logs)
// 3. Verifica broadcast in SecureBootReportsController
```

---

## ?? Documentazione di Riferimento

Per informazioni complete su SignalR:

- **Implementazione**: `docs/SIGNALR_REALTIME_COMPLETE.md`
- **Planning**: `docs/Q1_2025_FEATURES_PLAN.md`
- **Session Summary**: `docs/Q1_2025_SESSION_SUMMARY.md`

---

## ? Verifica Veloce (30 secondi)

**Comando rapido**:

1. Apri browser: `https://localhost:7001`
2. Premi `F12`
3. Digita nella Console:
```javascript
window.dashboardClient?.isConnected
```

**Output**:
- `true` ? ? **SignalR ATTIVO E CONNESSO**
- `false` ? ?? Client inizializzato ma non connesso
- `undefined` ? ? SignalR non caricato

---

**Fatto! Ora sai esattamente come verificare se SignalR � utilizzato! ??**