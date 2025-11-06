# Configurazione Porte - Secure Boot Dashboard

## ?? Porte Configurate

| Componente | HTTP | HTTPS | Note |
|------------|------|-------|------|
| **API** | 5027 | 7120 | Configurate in Program.cs (Kestrel) |
| **Web** | 5174 | 7001 | Configurate in launchSettings.json |

---

## ?? Come Funziona la Configurazione

### API (SecureBootDashboard.Api)

Le porte sono **hardcoded in Program.cs** tramite `ConfigureKestrel`:

```csharp
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenLocalhost(5027); // HTTP
    serverOptions.ListenLocalhost(7120, listenOptions =>
    {
        listenOptions.UseHttps(); // HTTPS
    });
});
```

**Questo significa che:**
- ? Le porte sono **sempre** 5027 (HTTP) e 7120 (HTTPS)
- ? Funziona sia con `dotnet run` che `dotnet SecureBootDashboard.Api.dll`
- ? Non dipende da `launchSettings.json` o `appsettings.json`
- ? Non può essere accidentalmente cambiato da configurazione

### Web (SecureBootDashboard.Web)

Le porte sono configurate in `Properties/launchSettings.json`:

```json
"https": {
  "applicationUrl": "https://localhost:7001;http://localhost:5174"
}
```

**Questo significa che:**
- ? Le porte sono 5174 (HTTP) e 7001 (HTTPS) quando usi `dotnet run`
- ?? Se avvii con `dotnet SecureBootDashboard.Web.dll`, usa porte di default (5000/5001)

---

## ?? Avvio e Test

### Test API

```powershell
# Avvia API
cd SecureBootDashboard.Api
dotnet run
```

**Output atteso:**
```
[20:30:15 INF] Starting SecureBootDashboard.Api application
[20:30:16 INF] SecureBootDashboard.Api started successfully
[20:30:16 INF] Listening on: http://localhost:5027 and https://localhost:7120
```

**Test health check:**
```powershell
Invoke-WebRequest https://localhost:7120/health -SkipCertificateCheck
# Output: StatusCode: 200
```

**Test Swagger:**
```
https://localhost:7120/swagger
```

### Test Web

```powershell
# Avvia Web
cd SecureBootDashboard.Web
dotnet run
```

**Apri browser:**
```
https://localhost:7001
```

---

## ?? Verifica Porte in Uso

### PowerShell

```powershell
# Verifica porta 7120 (API HTTPS)
Get-NetTCPConnection -LocalPort 7120 -ErrorAction SilentlyContinue | 
    Select-Object State, OwningProcess, LocalAddress, LocalPort

# Verifica porta 5027 (API HTTP)
Get-NetTCPConnection -LocalPort 5027 -ErrorAction SilentlyContinue | 
    Select-Object State, OwningProcess, LocalAddress, LocalPort

# Verifica porta 7001 (Web HTTPS)
Get-NetTCPConnection -LocalPort 7001 -ErrorAction SilentlyContinue | 
    Select-Object State, OwningProcess, LocalAddress, LocalPort
```

### Netstat

```cmd
netstat -ano | findstr :7120
netstat -ano | findstr :5027
netstat -ano | findstr :7001
```

---

## ?? Cambiare le Porte (Se Necessario)

### Per l'API

Modifica `SecureBootDashboard.Api/Program.cs`:

```csharp
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenLocalhost(5028); // Nuova porta HTTP
    serverOptions.ListenLocalhost(7121, listenOptions =>
    {
        listenOptions.UseHttps(); // Nuova porta HTTPS
    });
});
```

**Ricordati di aggiornare:**
- `SecureBootDashboard.Web/appsettings.json` ? `ApiSettings:BaseUrl`
- `SecureBootDashboard.Web/appsettings.Development.json` ? `ApiSettings:BaseUrl`

### Per il Web

Modifica `SecureBootDashboard.Web/Properties/launchSettings.json`:

```json
"https": {
  "applicationUrl": "https://localhost:7002;http://localhost:5175"
}
```

---

## ?? Troubleshooting

### Problema: "Port already in use"

**Soluzione 1: Termina il processo**
```powershell
# Trova il processo
$processId = (Get-NetTCPConnection -LocalPort 7120).OwningProcess
Get-Process -Id $processId

# Termina il processo
Stop-Process -Id $processId -Force
```

**Soluzione 2: Cambia porta** (vedi sezione sopra)

### Problema: "Cannot assign requested address"

**Causa:** Probabilmente stai usando `0.0.0.0` o un IP non valido.

**Soluzione:** Usa `localhost` o `127.0.0.1`:

```csharp
serverOptions.ListenLocalhost(7120, listenOptions =>
{
    listenOptions.UseHttps();
});
```

### Problema: "SSL certificate error"

**Soluzione:** Trust del certificato di sviluppo
```powershell
dotnet dev-certs https --trust
```

### Problema: API si avvia ma Web non la raggiunge

**Verifica configurazione Web:**
```powershell
# Verifica appsettings.Development.json
Get-Content SecureBootDashboard.Web\appsettings.Development.json
```

Deve contenere:
```json
{
  "ApiSettings": {
    "BaseUrl": "https://localhost:7120"
  }
}
```

---

## ?? Checklist Deployment

### Development

- [x] API configurata su porte 5027 (HTTP) e 7120 (HTTPS)
- [x] Web configurata su porte 5174 (HTTP) e 7001 (HTTPS)
- [x] Web punta a `https://localhost:7120` per l'API
- [x] Certificato HTTPS trusted: `dotnet dev-certs https --trust`

### Production (IIS)

- [ ] API configurata su binding IIS (es. `https://api.domain.com:443`)
- [ ] Web configurata su binding IIS (es. `https://dashboard.domain.com:443`)
- [ ] Web appsettings.Production.json punta all'URL corretto dell'API
- [ ] Certificato SSL valido configurato in IIS
- [ ] Firewall aperto per le porte necessarie

---

## ?? Best Practices

### 1. **Usa HTTPS sempre in Production**

```csharp
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
    app.UseHsts();
}
```

### 2. **Non Hardcodare Porte in Production**

Per IIS deployment, lascia che IIS gestisca le porte tramite i binding.

### 3. **Usa Environment Variables per URL dinamici**

```json
{
  "ApiSettings": {
    "BaseUrl": "#{API_BASE_URL}#"
  }
}
```

Sostituito durante il deployment con Azure DevOps, Octopus Deploy, etc.

### 4. **Testa Connettività Prima del Deployment**

```powershell
# Verifica API raggiungibile
Test-NetConnection -ComputerName localhost -Port 7120

# Test HTTP request
Invoke-WebRequest https://localhost:7120/health -SkipCertificateCheck
```

---

## ?? Riferimenti

- [Kestrel Endpoint Configuration](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/endpoints)
- [ASP.NET Core Configuration](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/)
- [IIS Deployment](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/)

---

## ? Test Finale

```powershell
# 1. Avvia API
cd SecureBootDashboard.Api
dotnet run

# 2. In un altro terminale, test API
Invoke-WebRequest https://localhost:7120/health -SkipCertificateCheck

# 3. Avvia Web
cd SecureBootDashboard.Web
dotnet run

# 4. Apri browser
Start-Process https://localhost:7001

# ? Se tutto funziona, dovresti vedere la dashboard!
```
