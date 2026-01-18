# CLR Worker Thread Error - Quick Fix Guide

## Errore Identificato

```
Application '/LM/W3SVC/3/ROOT' with physical root 'C:\inetpub\SecureBootDashboard.Web\' 
failed to load coreclr. Exception message: 
CLR worker thread exited prematurely
```

## Causa

Il **CLR (Common Language Runtime)** non riesce ad avviarsi. Cause comuni:

1. ? .NET Runtime versione errata o mancante
2. ? AspNetCoreModuleV2 versione incompatibile  
3. ? Hosting Model errato (inprocess vs outofprocess)
4. ? DLL corrotte o dipendenze mancanti

---

## ? Soluzione Rapida

### Opzione 1: Script Automatico (Raccomandato)

```powershell
# Run come Administrator
.\scripts\Fix-CLRError.ps1
```

Lo script:
- ? Verifica .NET 10 Runtime
- ? Controlla AspNetCoreModuleV2
- ? Modifica web.config (hostingModel=outofprocess)
- ? Imposta permessi corretti
- ? Riavvia servizi
- ? Testa il sito
- ? Mostra log recenti

---

### Opzione 2: Fix Manuale

#### 1. Verifica .NET Runtime

```powershell
dotnet --list-runtimes

# Devi vedere:
# Microsoft.AspNetCore.App 10.0.x [...]
# Microsoft.NETCore.App 10.0.x [...]
```

**Se mancano**, installa .NET 10 Hosting Bundle:
- https://aka.ms/dotnet/10.0/dotnet-hosting-win.exe

```powershell
# Dopo installazione
iisreset
```

#### 2. Modifica web.config

Cambia `hostingModel` da `inprocess` a `outofprocess`:

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
        <!-- ? CHANGE: inprocess ? outofprocess -->
        <environmentVariables>
          <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
        </environmentVariables>
      </aspNetCore>
    </system.webServer>
  </location>
</configuration>
```

#### 3. Riavvia Servizi

```powershell
Import-Module WebAdministration
Restart-WebAppPool "SecureBootDashboard.Web"
Restart-WebSite "SecureBootDashboard.Web"
Start-Sleep -Seconds 5
```

#### 4. Testa

```powershell
Invoke-WebRequest -Uri "https://secbootsrv.mslabs.local" `
    -UseBasicParsing `
    -SkipCertificateCheck
```

---

## ?? Debugging Avanzato

### Se ancora non funziona

#### 1. Controlla Event Viewer

```powershell
Get-EventLog -LogName Application -Source "*AspNetCore*" -Newest 10 |
    Where-Object { $_.EntryType -eq "Error" } |
    Format-List TimeGenerated, Message
```

#### 2. Controlla Stdout Logs

```powershell
Get-Content "C:\Logs\SecureBootDashboard\stdout-*.log" -Tail 50
```

#### 3. Verifica Versione Module

```powershell
$modulePath = "$env:SystemRoot\System32\inetsrv\aspnetcorev2.dll"
(Get-Item $modulePath).VersionInfo.FileVersion

# Dovrebbe essere 20.0.x o superiore
```

#### 4. Redeploy Pulito

```powershell
# Backup
$timestamp = Get-Date -Format "yyyyMMddHHmmss"
Move-Item "C:\inetpub\SecureBootDashboard.Web" `
          "C:\inetpub\SecureBootDashboard.Web.backup_$timestamp"

# Redeploy
Copy-Item -Path "SOURCE_PATH\*" `
          -Destination "C:\inetpub\SecureBootDashboard.Web\" `
          -Recurse -Force
```

---

## ?? Differenza Hosting Models

| Aspetto | InProcess | OutOfProcess |
|---------|-----------|--------------|
| Processo | w3wp.exe | dotnet.exe |
| Performance | ? Più veloce | ?? Leggermente più lento |
| Isolation | ? Meno isolato | ? Più isolato |
| Debugging | ?? Più difficile | ? Più facile |
| Stabilità | ? Più sensibile | ? Più stabile |
| **Raccomandazione** | Produzione (se stabile) | **Debug / Troubleshoot** |

**Per risolvere errori CLR, usa sempre OutOfProcess prima.**

---

## ? Checklist Verifica

- [ ] .NET 10 Runtime installato (`dotnet --list-runtimes`)
- [ ] AspNetCoreModuleV2 versione 20.0.x+ presente
- [ ] web.config con `hostingModel="outofprocess"`
- [ ] web.config con `stdoutLogEnabled="true"`
- [ ] Permessi write su `C:\Logs\SecureBootDashboard`
- [ ] App Pool in stato "Started"
- [ ] Website in stato "Started"
- [ ] Stdout logs creati dopo restart
- [ ] Nessun errore in Event Viewer

---

## ?? Comandi Quick

```powershell
# Fix rapido completo
.\scripts\Fix-CLRError.ps1

# Solo diagnostica
.\scripts\Diagnose-WebDashboard.ps1

# Solo restart
Restart-WebAppPool "SecureBootDashboard.Web"
Restart-WebSite "SecureBootDashboard.Web"

# Check logs
Get-Content "C:\Logs\SecureBootDashboard\stdout-*.log" -Tail 50

# Check Event Viewer
Get-EventLog -LogName Application -Source "*AspNetCore*" -Newest 5
```

---

## ?? File Correlati

- `scripts/Fix-CLRError.ps1` - Fix automatico
- `scripts/Diagnose-WebDashboard.ps1` - Diagnostica completa
- `TROUBLESHOOT_HTTP_500_30.md` - Guida generale 500.30

---

## ?? Prossimi Passi

1. **Esegui**: `.\scripts\Fix-CLRError.ps1`
2. **Aspetta** 10 secondi
3. **Testa**: Vai a `https://secbootsrv.mslabs.local`
4. **Se funziona**: ? Risolto!
5. **Se non funziona**: Controlla stdout logs e condividi l'errore

---

## ?? Note Importanti

- **OutOfProcess** è più lento ma più stabile per debug
- Dopo aver risolto, puoi provare a tornare a **InProcess**
- Sempre fare **iisreset** dopo aver installato .NET Runtime
- I log stdout sono **essenziali** per il troubleshooting

