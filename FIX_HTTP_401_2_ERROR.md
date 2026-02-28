# Fix HTTP 401.2 - Unauthorized Error

## ?? Errore

```
HTTP Error 401.2 - Unauthorized
You are not authorized to view this page due to invalid authentication headers.

Most likely cause:
No authentication protocol (including anonymous) is selected in IIS.
```

---

## ? SOLUZIONE RAPIDA

### Script Automatico

```powershell
# Run come Administrator
.\scripts\Fix-401Error.ps1
```

Lo script:
1. ? Verifica configurazione autenticazione corrente
2. ? Disabilita Windows Authentication in IIS
3. ? **Abilita Anonymous Authentication in IIS**
4. ? Verifica le modifiche
5. ? Controlla web.config per override
6. ? Riavvia App Pool
7. ? Testa il sito

---

### Fix Manuale Veloce

```powershell
Import-Module WebAdministration

# Abilita Anonymous Authentication
Set-WebConfigurationProperty `
    -Filter /system.webServer/security/authentication/anonymousAuthentication `
    -Name enabled `
    -Value true `
    -PSPath "IIS:\Sites\SecureBootDashboard.Web"

# Disabilita Windows Authentication
Set-WebConfigurationProperty `
    -Filter /system.webServer/security/authentication/windowsAuthentication `
    -Name enabled `
    -Value false `
    -PSPath "IIS:\Sites\SecureBootDashboard.Web"

# Verifica
Get-WebConfigurationProperty `
    -Filter /system.webServer/security/authentication/* `
    -Name enabled `
    -PSPath "IIS:\Sites\SecureBootDashboard.Web"

# Restart
Restart-WebAppPool "SecureBootDashboard.Web"
Start-Sleep -Seconds 5

# Test
Invoke-WebRequest -Uri "https://secbootsrv.mslabs.local" -UseBasicParsing -SkipCertificateCheck
```

---

### Fix via IIS Manager (GUI)

1. Apri **IIS Manager**
2. Naviga a **Sites** ? **SecureBootDashboard.Web**
3. Doppio click su **"Authentication"**
4. Seleziona **"Anonymous Authentication"**
5. Click su **"Enable"** nel pannello Actions
6. Seleziona **"Windows Authentication"**
7. Click su **"Disable"** nel pannello Actions
8. Riavvia il sito

---

## ?? Spiegazione

### Cosa È Successo

| Passo | Azione | Risultato |
|-------|--------|-----------|
| 1 | Disabilitato Windows Auth per risolvere conflitto | ? App si avvia |
| 2 | Ma Anonymous Auth non abilitata | ? 401.2 Error |
| 3 | IIS dice "Non so come autenticare!" | ? Errore |

### Come Funziona Ora

```
Browser Request
    ?
IIS (Anonymous Auth ENABLED)
    ?
Passa richiesta anonima a ASP.NET Core
    ?
ASP.NET Core Negotiate Handler
    ?
Chiede credenziali Windows al browser
    ?
Browser invia credenziali NTLM/Kerberos
    ?
ASP.NET Core autentica utente
    ?
? SUCCESS
```

---

## ?? Configurazione Corretta

### Stato Finale Desiderato

| Autenticazione | IIS | ASP.NET Core |
|----------------|-----|--------------|
| Windows | ? DISABLED | ? ENABLED (Negotiate) |
| Anonymous | ? ENABLED | ? (Pass-through) |
| Basic | ? DISABLED | ? DISABLED |

### Verifica Configurazione

```powershell
# Controlla IIS settings
$sitePath = "IIS:\Sites\SecureBootDashboard.Web"

Write-Host "Windows Auth:" (Get-WebConfigurationProperty `
    -Filter /system.webServer/security/authentication/windowsAuthentication `
    -Name enabled -PSPath $sitePath).Value

Write-Host "Anonymous Auth:" (Get-WebConfigurationProperty `
    -Filter /system.webServer/security/authentication/anonymousAuthentication `
    -Name enabled -PSPath $sitePath).Value

# Expected Output:
# Windows Auth: False
# Anonymous Auth: True
```

---

## ?? Test Autenticazione

### Test 1: Site Risponde

```powershell
$response = Invoke-WebRequest -Uri "https://secbootsrv.mslabs.local" `
    -UseBasicParsing `
    -SkipCertificateCheck

Write-Host "Status: $($response.StatusCode)"
# Expected: 200 (or 302 redirect to login)
```

### Test 2: Windows Auth Funziona

Apri il browser e vai a: `https://secbootsrv.mslabs.local`

**Comportamento Atteso**:
1. Browser chiede credenziali Windows (popup o automatico)
2. Dopo login, vedi la dashboard
3. Dashboard mostra il tuo username Windows

**Se vedi**:
- ? HTTP 200 + Dashboard ? SUCCESS
- ? HTTP 302 Redirect ? Normal (redirect a login)
- ? HTTP 401 ? Anonymous Auth non abilitata correttamente

---

## ?? Troubleshooting

### Ancora 401 Dopo il Fix?

#### 1. Verifica in IIS Manager

```
IIS Manager ? Sites ? SecureBootDashboard.Web ? Authentication

Dovrebbe mostrare:
  Anonymous Authentication: ? Enabled
  Windows Authentication: ? Disabled
```

#### 2. Controlla web.config Override

```powershell
$webConfig = "C:\inetpub\SecureBootDashboard.Web\web.config"
Select-String -Path $webConfig -Pattern "authentication|windowsAuthentication|anonymousAuthentication"
```

Se trovi tag `<authentication>` o `<windowsAuthentication>`, potrebbero sovrascrivere le impostazioni IIS.

#### 3. Riavvia IIS Completamente

```powershell
iisreset
Start-Sleep -Seconds 10
```

#### 4. Controlla Logs

```powershell
# Stdout logs
Get-Content "C:\Logs\SecureBootDashboard\stdout-*.log" -Tail 30

# Event Viewer
Get-EventLog -LogName Application -Source "*AspNetCore*" -Newest 5
```

---

## ?? Cronologia Errori Risolti

| Errore | Causa | Fix |
|--------|-------|-----|
| **HTTP 500.30** | CLR worker thread | ? Changed hostingModel |
| **InvalidOperationException** | Windows Auth conflict | ? Disabled IIS Windows Auth |
| **HTTP 401.2** | No auth protocol | ? **Enabled Anonymous Auth** |

---

## ? Checklist Completa

- [ ] IIS Windows Authentication: **Disabled**
- [ ] IIS Anonymous Authentication: **Enabled**
- [ ] App Pool riavviato
- [ ] Site testa OK (HTTP 200 o 302)
- [ ] Browser chiede credenziali Windows
- [ ] Dashboard mostra username autenticato
- [ ] Nessun errore in stdout logs
- [ ] Nessun errore in Event Viewer

---

## ?? Comandi Quick

```powershell
# Fix completo
.\scripts\Fix-401Error.ps1

# Solo verifica
Get-WebConfigurationProperty -Filter /system.webServer/security/authentication/* -Name enabled -PSPath "IIS:\Sites\SecureBootDashboard.Web"

# Solo restart
Restart-WebAppPool "SecureBootDashboard.Web"

# Test
Invoke-WebRequest -Uri "https://secbootsrv.mslabs.local" -UseBasicParsing -SkipCertificateCheck
```

---

## ?? Risultato Finale

Dopo il fix:
1. ? Sito accessibile
2. ? Windows Authentication funziona
3. ? Utenti vedono prompt login Windows
4. ? Dashboard mostra dati con autenticazione
5. ? Nessun errore 401 o 500

**Versione**: 1.3.4 (401.2 Fix)

