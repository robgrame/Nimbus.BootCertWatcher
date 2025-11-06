# Troubleshooting: Port Already in Use

## Problema

Errore all'avvio dell'API:
```
System.Net.Sockets.SocketException (10013): An attempt was made to access a socket in a way forbidden by its access permissions.
```

Questo significa che la porta (es. 5000) è già in uso da un altro processo.

---

## ? Soluzione Rapida

### 1. Usa le Porte Configurate

L'API è configurata per usare:
- **HTTPS**: `https://localhost:7120`
- **HTTP**: `http://localhost:5027`

**Avvia con:**
```powershell
cd SecureBootDashboard.Api
dotnet run --launch-profile https
```

**Oppure semplicemente:**
```powershell
dotnet run
```

Ora usa automaticamente le porte specificate in `appsettings.Development.json`.

---

## ?? Verifica Quale Processo Sta Usando la Porta

### PowerShell

```powershell
# Trova quale processo usa la porta 5000
Get-NetTCPConnection -LocalPort 5000 | Select-Object -Property State, OwningProcess, LocalAddress, LocalPort

# Ottieni il nome del processo
Get-Process -Id (Get-NetTCPConnection -LocalPort 5000).OwningProcess
```

### CMD

```cmd
netstat -ano | findstr :5000
```

Poi identifica il processo con il PID:
```cmd
tasklist | findstr <PID>
```

---

## ?? Termina il Processo che Usa la Porta

### PowerShell

```powershell
# Termina il processo sulla porta 5000
$processId = (Get-NetTCPConnection -LocalPort 5000).OwningProcess
Stop-Process -Id $processId -Force
```

### Task Manager

1. Apri **Task Manager** (Ctrl+Shift+Esc)
2. Vai su **Details**
3. Trova il processo con il PID trovato sopra
4. Tasto destro ? **End Task**

---

## ?? Cambia Porta Manualmente

Se vuoi usare una porta specifica diversa, modifica:

### launchSettings.json

```json
"https": {
  "applicationUrl": "https://localhost:7120;http://localhost:5027"
}
```

### appsettings.Development.json

```json
{
  "Urls": "https://localhost:7120;http://localhost:5027"
}
```

### Comando CLI

```powershell
dotnet run --urls "https://localhost:7120;http://localhost:5027"
```

---

## ?? Porte Configurate nella Soluzione

| Componente | HTTP | HTTPS |
|------------|------|-------|
| **API** | 5027 | 7120 |
| **Web** | 5174 | 7001 |

### Verifica launchSettings.json

**API:**
```powershell
Get-Content SecureBootDashboard.Api\Properties\launchSettings.json
```

**Web:**
```powershell
Get-Content SecureBootDashboard.Web\Properties\launchSettings.json
```

---

## ?? Porte Comuni che Potrebbero Essere Bloccate

- **5000/5001**: Default ASP.NET Core
- **80/443**: HTTP/HTTPS standard (richiedono admin)
- **8080**: Proxy/alternative web servers
- **3000**: Node.js/React dev servers

---

## ?? Test Rapido

### 1. Avvia API

```powershell
cd SecureBootDashboard.Api
dotnet run
```

Dovresti vedere:
```
[20:15:30 INF] Starting SecureBootDashboard.Api application
[20:15:31 INF] Now listening on: https://localhost:7120
[20:15:31 INF] Now listening on: http://localhost:5027
```

### 2. Verifica API Funziona

```powershell
Invoke-WebRequest https://localhost:7120/health -SkipCertificateCheck
```

**Output atteso:** `200 OK`

### 3. Avvia Web

```powershell
cd SecureBootDashboard.Web
dotnet run
```

### 4. Apri Browser

```
https://localhost:7001
```

---

## ?? Errori Comuni

### "The SSL connection could not be established"

**Soluzione:** Trust il certificato di sviluppo
```powershell
dotnet dev-certs https --trust
```

### "Port 7120 is already in use"

**Soluzione:** Verifica se hai un'altra istanza in esecuzione
```powershell
Get-Process | Where-Object {$_.ProcessName -like "*dotnet*"}
```

Termina le istanze duplicate:
```powershell
Get-Process dotnet | Stop-Process
```

### "Cannot assign requested address"

**Soluzione:** Usa `localhost` invece di `127.0.0.1` o viceversa, oppure usa `0.0.0.0` per ascoltare su tutte le interfacce:

```json
{
  "Urls": "https://0.0.0.0:7120;http://0.0.0.0:5027"
}
```

---

## ?? Riferimenti

- [ASP.NET Core Kestrel Configuration](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel)
- [Configure Endpoints](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/endpoints)
- [launchSettings.json Schema](https://json.schemastore.org/launchsettings.json)
