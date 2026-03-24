# Fix Stdout Redirection Error - Quick Guide

## ?? Errore

```
Could not start stdout file redirection to '.\logs\stdout' 
with application base 'C:\inetpub\SecureBootDashboard.Web\'. 
ios_base::failbit set: iostream stream error.
```

---

## ? SOLUZIONE RAPIDA (1 minuto)

### Script Automatico

```powershell
# Run come Administrator
.\scripts\Fix-StdoutRedirection.ps1
```

Lo script:
1. ? Crea `C:\inetpub\SecureBootDashboard.Web\logs`
2. ? Imposta permessi App Pool
3. ? Crea `C:\Logs\SecureBootDashboard`
4. ? **Aggiorna web.config con path assoluto**
5. ? Verifica Anonymous Authentication
6. ? Riavvia App Pool
7. ? Controlla log creati
8. ? Testa il sito

---

### Fix Manuale (2 minuti)

#### Opzione A: Path Relativo (Fix Veloce)

```powershell
# 1. Crea directory
$logsDir = "C:\inetpub\SecureBootDashboard.Web\logs"
New-Item -Path $logsDir -ItemType Directory -Force

# 2. Permessi
$acl = Get-Acl $logsDir
$rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
    "IIS AppPool\SecureBootDashboard.Web", 
    "FullControl", 
    "ContainerInherit,ObjectInherit", 
    "None", 
    "Allow"
)
$acl.AddAccessRule($rule)
Set-Acl $logsDir $acl

# 3. Restart
Restart-WebAppPool "SecureBootDashboard.Web"
```

#### Opzione B: Path Assoluto (Raccomandato)

Modifica `C:\inetpub\SecureBootDashboard.Web\web.config`:

```xml
<aspNetCore processPath="dotnet"
            arguments=".\SecureBootDashboard.Web.dll"
            stdoutLogEnabled="true"
            stdoutLogFile="C:\Logs\SecureBootDashboard\stdout"
            hostingModel="outofprocess">
            <!-- ? PATH ASSOLUTO: C:\Logs\... invece di .\logs\... -->
```

Poi:
```powershell
# Crea directory centrale
New-Item -Path "C:\Logs\SecureBootDashboard" -ItemType Directory -Force

# Permessi
$acl = Get-Acl "C:\Logs\SecureBootDashboard"
$rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
    "IIS AppPool\SecureBootDashboard.Web", 
    "FullControl", 
    "ContainerInherit,ObjectInherit", 
    "None", 
    "Allow"
)
$acl.AddAccessRule($rule)
Set-Acl "C:\Logs\SecureBootDashboard" $acl

# Restart
Restart-WebAppPool "SecureBootDashboard.Web"
```

---

## ?? Spiegazione

### Perché Succede

| Causa | Descrizione |
|-------|-------------|
| **Path Relativo** | `.\logs\stdout` è relativo alla app directory |
| **Directory Mancante** | `C:\inetpub\SecureBootDashboard.Web\logs` non esiste |
| **Permessi** | App Pool non può creare la directory |
| **IIS Redirect** | IIS non può scrivere se la directory non esiste |

### Path Relativi vs Assoluti

| Path Type | Esempio | Risoluzione |
|-----------|---------|-------------|
| **Relativo** | `.\logs\stdout` | `C:\inetpub\SecureBootDashboard.Web\logs\stdout-*.log` |
| **Assoluto** | `C:\Logs\SecureBootDashboard\stdout` | `C:\Logs\SecureBootDashboard\stdout-*.log` |

**Raccomandazione**: Usa sempre **path assoluti** per evitare problemi.

---

## ?? Configurazione Corretta

### web.config Completo

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <location path="." inheritInChildApplications="false">
    <system.webServer>
      <handlers>
        <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
      </handlers>
      <aspNetCore processPath="dotnet"
                  arguments=".\SecureBootDashboard.Web.dll"
                  stdoutLogEnabled="true"
                  stdoutLogFile="C:\Logs\SecureBootDashboard\stdout"
                  hostingModel="outofprocess">
        <environmentVariables>
          <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
        </environmentVariables>
      </aspNetCore>
    </system.webServer>
  </location>
</configuration>
```

**Key Points**:
- ? `stdoutLogEnabled="true"` - Logging abilitato
- ? Path assoluto: `C:\Logs\SecureBootDashboard\stdout`
- ? `hostingModel="outofprocess"` - Per evitare conflitti CLR
- ? Environment: `Production`

---

## ?? Verifica

### 1. Directory Esiste

```powershell
Test-Path "C:\Logs\SecureBootDashboard"
# Expected: True
```

### 2. Permessi Corretti

```powershell
$acl = Get-Acl "C:\Logs\SecureBootDashboard"
$acl.Access | Where-Object { $_.IdentityReference -like "*SecureBootDashboard.Web*" }

# Expected: IdentityReference con FileSystemRights FullControl
```

### 3. Log Files Creati

```powershell
Get-ChildItem "C:\Logs\SecureBootDashboard\stdout-*.log" | 
    Sort-Object LastWriteTime -Descending | 
    Select-Object -First 1 Name, LastWriteTime

# Expected: Almeno 1 file stdout
```

### 4. Contenuto Log

```powershell
Get-Content "C:\Logs\SecureBootDashboard\stdout-*.log" -Tail 20

# Expected: Log di startup dell'applicazione
```

---

## ?? Cronologia Errori Risolti

| # | Errore | Fix | Status |
|---|--------|-----|--------|
| 1 | HTTP 500.30 (CLR) | Changed hostingModel | ? |
| 2 | Windows Auth Conflict | Disabled IIS Windows Auth | ? |
| 3 | HTTP 401.2 | Enabled Anonymous Auth | ? |
| 4 | **Stdout Redirection** | **Created logs + absolute path** | ?? **Fixing** |

---

## ?? Troubleshooting

### Ancora Errore Dopo il Fix?

#### 1. Verifica Directory Exists

```powershell
Test-Path "C:\inetpub\SecureBootDashboard.Web\logs"
Test-Path "C:\Logs\SecureBootDashboard"

# Entrambi dovrebbero essere True
```

#### 2. Verifica Permessi

```powershell
# Test scrittura
$testFile = "C:\Logs\SecureBootDashboard\test.txt"
"test" | Out-File $testFile -ErrorAction Stop
Remove-Item $testFile

Write-Host "? Write permissions OK" -ForegroundColor Green
```

#### 3. Controlla Event Viewer

```powershell
Get-EventLog -LogName Application -Source "*AspNetCore*" -Newest 3
```

#### 4. Controlla App Pool Identity

```powershell
$appPool = Get-Item "IIS:\AppPools\SecureBootDashboard.Web"
Write-Host "Identity: $($appPool.processModel.identityType)"

# Expected: ApplicationPoolIdentity
```

#### 5. Try Different Path

Se ancora problemi, prova un path ancora più semplice:

```xml
<aspNetCore stdoutLogFile="C:\Temp\stdout" ... >
```

Crea `C:\Temp` e dai permessi Everyone.

---

## ? Checklist Completa

- [ ] Directory `C:\Logs\SecureBootDashboard` creata
- [ ] Permessi App Pool impostati (FullControl)
- [ ] web.config aggiornato con path assoluto
- [ ] Anonymous Authentication abilitata
- [ ] Windows Authentication disabilitata
- [ ] App Pool riavviato
- [ ] Stdout log files creati
- [ ] Site testa OK (HTTP 200)

---

## ?? Comandi Quick

```powershell
# Fix completo
.\scripts\Fix-StdoutRedirection.ps1

# Solo verifica
Test-Path "C:\Logs\SecureBootDashboard"
Get-ChildItem "C:\Logs\SecureBootDashboard" | Select Name, LastWriteTime

# Solo restart
Restart-WebAppPool "SecureBootDashboard.Web"

# Test site
Invoke-WebRequest -Uri "https://secbootsrv.mslabs.local" -UseBasicParsing -SkipCertificateCheck

# Check logs
Get-Content "C:\Logs\SecureBootDashboard\stdout-*.log" -Tail 30
```

---

## ?? Risultato Atteso

Dopo il fix:
```
C:\Logs\SecureBootDashboard\
  stdout-20250101-123456.log  ? Log creato automaticamente
  web-20250101.log            ? Serilog log (se app parte)
```

Log conterrà:
```
[HH:MM:SS INF] Starting SecureBootDashboard.Web application
[HH:MM:SS INF] Environment: Production
[HH:MM:SS INF] Windows authentication configured
[HH:MM:SS INF] SecureBootDashboard.Web started successfully
```

**Versione**: 1.3.5 (Stdout Redirection Fix)

