# SOLUZIONE FINALE - Windows Authentication Conflict

## ?? Problema

**Windows Authentication in IIS NON può essere disabilitata** con script PowerShell.

L'errore persiste:
```
System.InvalidOperationException: The Negotiate Authentication handler cannot be used 
on a server that directly supports Windows Authentication.
```

---

## ? SOLUZIONE 1: Fix Manuale in IIS Manager (RACCOMANDATO)

### Procedura Completa

1. **Apri IIS Manager**
   - Premi `Win + R`
   - Digita `inetmgr`
   - Premi `Enter`

2. **Naviga al Sito**
   - Espandi il nodo del server (es. SECBOOTSRV)
   - Espandi **Sites**
   - **Click** su **SecureBootDashboard.Web**

3. **Apri Authentication**
   - Nel pannello centrale (Features View)
   - **Doppio click** su **"Authentication"**

4. **Modifica Impostazioni**
   
   **DISABILITA Windows Authentication:**
   - **Click destro** su **"Windows Authentication"**
   - Click su **"Disable"**
   
   **ABILITA Anonymous Authentication:**
   - **Click destro** su **"Anonymous Authentication"**
   - Click su **"Enable"**

5. **Chiudi IIS Manager**

6. **Riavvia App Pool**
   ```powershell
   Restart-WebAppPool "SecureBootDashboard.Web"
   ```

7. **Testa il Sito**
   ```powershell
   Invoke-WebRequest -Uri "https://secbootsrv.mslabs.local" -UseBasicParsing -SkipCertificateCheck
   ```

---

## ? SOLUZIONE 2: Usa IIS Windows Authentication (ALTERNATIVA)

Se **non puoi disabilitare Windows Auth**, usa direttamente IIS invece di Negotiate handler.

### Step 1: Modifica Program.cs

Nel file `SecureBootDashboard.Web\Program.cs`, cerca (circa linea 165):

```csharp
// PRIMA (Negotiate handler)
builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme)
    .AddNegotiate();
```

Sostituisci con:

```csharp
// DOPO (IIS Windows Auth)
builder.Services.AddAuthentication(IISDefaults.AuthenticationScheme);
```

### Step 2: Rebuild

```powershell
cd SecureBootDashboard.Web
dotnet build -c Release
```

### Step 3: Redeploy

```powershell
# Stop services
Stop-WebAppPool "SecureBootDashboard.Web"
Stop-Website "SecureBootDashboard.Web"

# Copy new binaries
$source = "bin\Release\net10.0"
$dest = "C:\inetpub\SecureBootDashboard.Web"
Copy-Item "$source\*" $dest -Recurse -Force

# Start services
Start-WebAppPool "SecureBootDashboard.Web"
Start-Website "SecureBootDashboard.Web"
```

### Step 4: Configura IIS

Ora **ABILITA** Windows Auth in IIS (invece di disabilitarla):

**In IIS Manager:**
1. Site ? SecureBootDashboard.Web
2. Authentication
3. **ENABLE** Windows Authentication
4. **DISABLE** Anonymous Authentication

**In PowerShell:**
```powershell
Import-Module WebAdministration

# ENABLE Windows Auth
Set-WebConfigurationProperty `
    -Filter /system.webServer/security/authentication/windowsAuthentication `
    -Name enabled `
    -Value true `
    -PSPath "IIS:\Sites\SecureBootDashboard.Web"

# DISABLE Anonymous Auth
Set-WebConfigurationProperty `
    -Filter /system.webServer/security/authentication/anonymousAuthentication `
    -Name enabled `
    -Value false `
    -PSPath "IIS:\Sites\SecureBootDashboard.Web"

# Restart
Restart-WebAppPool "SecureBootDashboard.Web"
```

---

## ?? Confronto Soluzioni

| Soluzione | Pros | Cons | Raccomandazione |
|-----------|------|------|-----------------|
| **1. Disabilita Windows Auth in IIS** | ? No code changes<br>? Più flessibile<br>? Controllo completo | ?? Richiede fix manuale IIS | ? **BEST** se riesci |
| **2. Usa IIS Windows Auth** | ? Funziona subito<br>? No fix IIS richiesto<br>? Configurazione standard | ? Richiede code change<br>? Rebuild e redeploy | ? **OK** se fix IIS impossibile |

---

## ?? Troubleshooting

### Verifica Configurazione Corrente

```powershell
# Check Windows Auth status
Import-Module WebAdministration
$sitePath = "IIS:\Sites\SecureBootDashboard.Web"

$winAuth = Get-WebConfigurationProperty `
    -Filter /system.webServer/security/authentication/windowsAuthentication `
    -Name enabled `
    -PSPath $sitePath

$anonAuth = Get-WebConfigurationProperty `
    -Filter /system.webServer/security/authentication/anonymousAuthentication `
    -Name enabled `
    -PSPath $sitePath

Write-Host "Windows Auth: $($winAuth.Value)"
Write-Host "Anonymous Auth: $($anonAuth.Value)"
```

### Per Soluzione 1 (Negotiate)
```
Expected:
  Windows Auth: False
  Anonymous Auth: True
```

### Per Soluzione 2 (IIS Windows Auth)
```
Expected:
  Windows Auth: True
  Anonymous Auth: False
```

---

## ?? Checklist Finale

### Per Soluzione 1
- [ ] IIS Manager aperto
- [ ] Windows Authentication: **Disabled**
- [ ] Anonymous Authentication: **Enabled**
- [ ] App Pool riavviato
- [ ] Site testa OK

### Per Soluzione 2
- [ ] Program.cs modificato (Negotiate ? IIS)
- [ ] Progetto rebuilded
- [ ] Binaries redeployed
- [ ] IIS: Windows Auth **Enabled**
- [ ] IIS: Anonymous Auth **Disabled**
- [ ] App Pool riavviato
- [ ] Site testa OK

---

## ?? Comandi Quick Test

```powershell
# Test site
Invoke-WebRequest -Uri "https://secbootsrv.mslabs.local" -UseBasicParsing -SkipCertificateCheck

# Check logs
Get-Content "C:\Logs\SecureBootDashboard\stdout-*.log" -Tail 30

# Check for Negotiate error
$log = Get-Content "C:\Logs\SecureBootDashboard\stdout-*.log" -Tail 50
if ($log -match "Negotiate Authentication handler cannot be used") {
    Write-Host "? Windows Auth conflict still present!" -ForegroundColor Red
} else {
    Write-Host "? No Negotiate error found!" -ForegroundColor Green
}
```

---

## ?? Prossimi Passi

1. **Prova Soluzione 1** (manualmente in IIS Manager)
2. **Se fallisce**, usa **Soluzione 2** (modifica codice)
3. **Testa il sito**
4. **Verifica autenticazione Windows funziona**

**Entrambe le soluzioni sono valide e supportate!**

---

**Versione**: 1.3.6 (Windows Auth Conflict - Final Solution)

