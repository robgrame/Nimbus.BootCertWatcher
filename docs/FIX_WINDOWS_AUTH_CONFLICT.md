# Windows Authentication Conflict - SOLUZIONE

## ?? Errore Identificato

```
System.InvalidOperationException: The Negotiate Authentication handler cannot be used 
on a server that directly supports Windows Authentication. Enable Windows Authentication 
for the server and the Negotiate Authentication handler will defer to it.
```

**Posizione**: `Program.cs:line 453`

---

## ?? Causa del Problema

La tua applicazione **SecureBootDashboard.Web** sta usando:
```csharp
services.AddAuthentication(NegotiateDefaults.AuthenticationScheme)
    .AddNegotiate();
```

Ma **IIS ha già Windows Authentication abilitata** a livello di sito/server. Questo crea un **conflitto** perché:
- ASP.NET Core vuole gestire l'autenticazione Windows via Negotiate handler
- IIS sta già gestendo l'autenticazione Windows
- Non possono coesistere nello stesso processo

---

## ? SOLUZIONE RAPIDA

### **Esegui lo Script Automatico**

```powershell
# Run come Administrator
.\scripts\Fix-WindowsAuthConflict.ps1
```

Lo script:
1. ? Disabilita Windows Authentication in IIS per il sito
2. ? Abilita Anonymous Authentication in IIS
3. ? Permette a ASP.NET Core di gestire l'autenticazione
4. ? Riavvia App Pool
5. ? Testa il sito
6. ? Mostra log se ci sono ancora problemi

---

## ?? Fix Manuale (Se Preferisci)

### Opzione 1: Disabilita Windows Auth in IIS (Raccomandato)

```powershell
Import-Module WebAdministration

# Disabilita Windows Authentication
Set-WebConfigurationProperty `
    -Filter /system.webServer/security/authentication/windowsAuthentication `
    -Name enabled `
    -Value false `
    -PSPath "IIS:\Sites\SecureBootDashboard.Web"

# Abilita Anonymous Authentication  
Set-WebConfigurationProperty `
    -Filter /system.webServer/security/authentication/anonymousAuthentication `
    -Name enabled `
    -Value true `
    -PSPath "IIS:\Sites\SecureBootDashboard.Web"

# Restart
Restart-WebAppPool "SecureBootDashboard.Web"
```

### Opzione 2: Rimuovi Negotiate dal Codice

Se vuoi che IIS gestisca tutto, modifica `Program.cs`:

```csharp
// COMMENTA QUESTE RIGHE:
// services.AddAuthentication(NegotiateDefaults.AuthenticationScheme)
//     .AddNegotiate();

// E lascia che IIS gestisca Windows Auth
```

Poi **ricompila e redeploy** l'applicazione.

---

## ?? Spiegazione Tecnica

### Come Funziona l'Autenticazione in IIS + ASP.NET Core

| Scenario | IIS Windows Auth | ASP.NET Negotiate | Risultato |
|----------|------------------|-------------------|-----------|
| **Entrambi ON** | ? Enabled | ? Enabled | ? **CONFLITTO** (errore attuale) |
| **Solo IIS** | ? Enabled | ? Disabled | ? IIS gestisce tutto |
| **Solo ASP.NET** | ? Disabled | ? Enabled | ? **APP gestisce** (raccomandato) |
| **Nessuno** | ? Disabled | ? Disabled | ? Nessuna autenticazione |

### Perché Opzione 1 è Migliore

Quando disabiliti Windows Auth in IIS:
- ? IIS passa **richieste anonime** all'app
- ? ASP.NET Core **Negotiate handler** gestisce l'autenticazione Windows
- ? Hai **pieno controllo** nel codice (policy, claims, ecc.)
- ? **Più flessibile** per testing e debugging
- ? **Compatibile** con hosting InProcess o OutOfProcess

---

## ?? Verifica Configurazione

### Prima del Fix
```
IIS Site Settings:
  Windows Authentication: TRUE ?
  Anonymous Authentication: FALSE ?

ASP.NET Core:
  Negotiate Handler: TRUE ?

Result: CONFLICT ?
```

### Dopo il Fix
```
IIS Site Settings:
  Windows Authentication: FALSE ?
  Anonymous Authentication: TRUE ?

ASP.NET Core:
  Negotiate Handler: TRUE ?

Result: SUCCESS ?
```

---

## ?? Test Post-Fix

### 1. Verifica Settings IIS

```powershell
$sitePath = "IIS:\Sites\SecureBootDashboard.Web"

Get-WebConfigurationProperty `
    -Filter /system.webServer/security/authentication/windowsAuthentication `
    -Name enabled `
    -PSPath $sitePath

# Expected: False

Get-WebConfigurationProperty `
    -Filter /system.webServer/security/authentication/anonymousAuthentication `
    -Name enabled `
    -PSPath $sitePath

# Expected: True
```

### 2. Verifica App Funziona

```powershell
Invoke-WebRequest -Uri "https://secbootsrv.mslabs.local" `
    -UseBasicParsing `
    -SkipCertificateCheck

# Expected: HTTP 200 OK
```

### 3. Verifica Autenticazione Windows

Quando accedi al sito dal browser:
- ? Dovrebbe chiederti credenziali Windows
- ? Dovrebbe mostrare il tuo username nel sito
- ? Non dovrebbe mostrare errori 401 o 500

---

## ?? Log Output Atteso

### Prima (Errore)
```
[20:37:42 FTL] Application startup exception
System.InvalidOperationException: The Negotiate Authentication handler cannot be used...
```

### Dopo (Success)
```
[XX:XX:XX INF] Starting SecureBootDashboard.Web application
[XX:XX:XX INF] Windows authentication configured
[XX:XX:XX INF] SecureBootDashboard.Web started successfully
[XX:XX:XX DBG] Hosting starting
```

---

## ?? Troubleshooting

### Se Ancora Non Funziona

#### 1. Verifica IIS Manager

Apri IIS Manager e controlla:
1. Seleziona il sito "SecureBootDashboard.Web"
2. Doppio click su "Authentication"
3. Verifica:
   - Windows Authentication: **Disabled**
   - Anonymous Authentication: **Enabled**

#### 2. Controlla web.config

Il file `web.config` NON dovrebbe contenere:
```xml
<!-- NON DEVE ESSERCI QUESTO: -->
<windowsAuthentication enabled="true" />
```

#### 3. Verifica App Pool Identity

```powershell
$appPool = Get-Item "IIS:\AppPools\SecureBootDashboard.Web"
$appPool.processModel.identityType

# Expected: ApplicationPoolIdentity
```

#### 4. Controlla Event Viewer

```powershell
Get-EventLog -LogName Application -Source "*AspNetCore*" -Newest 5
```

---

## ?? File Correlati

- `scripts/Fix-WindowsAuthConflict.ps1` - Fix automatico
- `scripts/Diagnose-WebDashboard.ps1` - Diagnostica generale
- `FIX_CLR_WORKER_THREAD_ERROR.md` - Fix CLR error (precedente)

---

## ? Checklist

- [ ] Script eseguito: `Fix-WindowsAuthConflict.ps1`
- [ ] IIS Windows Auth: Disabled
- [ ] IIS Anonymous Auth: Enabled
- [ ] App Pool riavviato
- [ ] Sito testa OK (HTTP 200)
- [ ] Autenticazione Windows funziona dal browser
- [ ] Nessun errore nei log

---

## ?? Comandi Quick

```powershell
# Fix completo
.\scripts\Fix-WindowsAuthConflict.ps1

# Solo verifica settings
Get-WebConfigurationProperty -Filter /system.webServer/security/authentication/* -Name enabled -PSPath "IIS:\Sites\SecureBootDashboard.Web"

# Solo restart
Restart-WebAppPool "SecureBootDashboard.Web"

# Test sito
Invoke-WebRequest -Uri "https://secbootsrv.mslabs.local" -UseBasicParsing -SkipCertificateCheck
```

---

## ?? Risultato Atteso

Dopo aver applicato il fix:
1. ? Sito si avvia senza errori
2. ? Autenticazione Windows funziona via ASP.NET Core
3. ? Log mostrano "started successfully"
4. ? Browser chiede credenziali Windows
5. ? Dashboard mostra utente autenticato

**Versione**: 1.3.3 (Windows Auth Conflict Fix)

