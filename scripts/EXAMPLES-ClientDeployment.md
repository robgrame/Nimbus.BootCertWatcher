# Esempi Pratici - Client Deployment con Certificato Azure

## Scenario 1: Deployment Locale per Test

### Step 1: Genera Pacchetto con Certificato
```powershell
cd C:\Users\nefario\source\repos\robgrame\Nimbus.BootCertWatcher\scripts

.\Create-DeploymentPackage.ps1 `
    -Version "1.5.2-test" `
    -Configuration "Release" `
    -GenerateAzureCertificate `
    -AzureCertificatePassword "Test123!@#" `
    -SkipTests
```

**Output Atteso**:
```
[Step 5/13] Creating standalone client package ZIP...
Copying client binaries to staging directory...
  Azure certificate included in client package
  Certificate will be installed by Deploy-Client.ps1
  Certificate installation instructions created
? Client package ZIP created
  Path: .\deploy\packages\SecureBootWatcher-Client-v1.5.2-test.zip
  Size: X.XX MB
  SHA256: [checksum]
  Includes: Azure certificate for Storage Account authentication
```

### Step 2: Verifica Contenuto ZIP
```powershell
$zipPath = ".\deploy\packages\SecureBootWatcher-Client-v1.5.2-test.zip"
$extractPath = ".\deploy\test-verify"

# Estrai per verifica
Expand-Archive -Path $zipPath -DestinationPath $extractPath -Force

# Verifica file
Get-ChildItem $extractPath -Recurse | Select-Object FullName

# Output atteso:
# SecureBootWatcher.Client.exe
# appsettings.json
# *.dll
# certificates\AzureAppRegistration.pfx
# certificates\INSTALL-CERTIFICATE.txt

# Verifica contenuto istruzioni
Get-Content "$extractPath\certificates\INSTALL-CERTIFICATE.txt"

# Cleanup
Remove-Item $extractPath -Recurse -Force
```

### Step 3: Deploy Locale con Installazione Automatica
```powershell
.\Deploy-Client.ps1 `
    -PackageZipPath ".\deploy\packages\SecureBootWatcher-Client-v1.5.2-test.zip" `
    -ApiBaseUrl "https://test-api.contoso.local" `
    -FleetId "test-workstations" `
    -CreateScheduledTask `
    -ScheduleType Daily `
    -TaskTime "08:00AM" `
    -InstallPath "C:\Program Files\SecureBootWatcher-Test"
```

**Output Atteso**:
```
[1/4] Extracting package to temporary directory...
  Package extracted

[2/4] Configuring appsettings.json...
  API Base URL: https://test-api.contoso.local
  Fleet ID: test-workstations
  Configuration updated

[3/4] Using precompiled package (configuration applied to temp copy)...

[4/4] Installing client to: C:\Program Files\SecureBootWatcher-Test
  Client installed
     Location: C:\Program Files\SecureBootWatcher-Test

  Installing Azure Storage authentication certificate...
  Azure certificate installed successfully
     Thumbprint: XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
     Subject: CN=SecureBootDashboard-AzureAppReg, O=Your Organization, C=IT
     Store: LocalMachine\My
  appsettings.json updated with certificate thumbprint
  Removing certificate file from disk (security)...

  Creating scheduled task...
  Random delay: XX minutes (to prevent API flooding)
  Scheduled task created
     Task Name: SecureBootWatcher
     Run As: SYSTEM
     Schedule: Daily at 08:00AM (±XX min)
     Executable: C:\Program Files\SecureBootWatcher-Test\SecureBootWatcher.Client.exe

  Cleaning up temporary files...
```

### Step 4: Verifica Installazione
```powershell
# 1. Verifica scheduled task
Get-ScheduledTask -TaskName "SecureBootWatcher" | Format-List *

# 2. Verifica certificato installato
Get-ChildItem Cert:\LocalMachine\My | Where-Object { $_.Subject -like "*SecureBootDashboard*" } | Format-List *

# 3. Verifica appsettings.json
$settings = Get-Content "C:\Program Files\SecureBootWatcher-Test\appsettings.json" -Raw | ConvertFrom-Json
$settings.SecureBootWatcher.Sinks.WebApi.BaseAddress
$settings.SecureBootWatcher.FleetId
$settings.SecureBootWatcher.Sinks.AzureQueue.CertificateThumbprint

# 4. Verifica che file .pfx sia stato rimosso
Test-Path "C:\Program Files\SecureBootWatcher-Test\certificates\AzureAppRegistration.pfx"
# Deve essere False

# 5. Test esecuzione manuale
cd "C:\Program Files\SecureBootWatcher-Test"
.\SecureBootWatcher.Client.exe

# 6. Test esecuzione tramite scheduled task
Start-ScheduledTask -TaskName "SecureBootWatcher"
Start-Sleep -Seconds 5
Get-ScheduledTaskInfo -TaskName "SecureBootWatcher"
```

### Step 5: Cleanup Test
```powershell
# Rimuovi scheduled task
Unregister-ScheduledTask -TaskName "SecureBootWatcher" -Confirm:$false

# Rimuovi certificato
$cert = Get-ChildItem Cert:\LocalMachine\My | Where-Object { $_.Subject -like "*SecureBootDashboard*" }
Remove-Item "Cert:\LocalMachine\My\$($cert.Thumbprint)" -Force

# Rimuovi installazione
Remove-Item "C:\Program Files\SecureBootWatcher-Test" -Recurse -Force

# Rimuovi package di test
Remove-Item ".\deploy\packages\*-test*" -Force -ErrorAction SilentlyContinue
```

---

## Scenario 2: Deployment Produzione via Intune

### Step 1: Preparazione Pacchetto

```powershell
# Genera pacchetto produzione con certificato
.\Create-DeploymentPackage.ps1 `
    -Version "1.5.2" `
    -Configuration "Release" `
    -GenerateAzureCertificate `
    -AzureCertificatePassword "Prod!SecureP@ss123#"

# Verifica checksum
$zipPath = ".\deploy\packages\SecureBootWatcher-Client-v1.5.2.zip"
$storedHash = (Get-Content "$zipPath.sha256").Split()[0]
$actualHash = (Get-FileHash -Path $zipPath -Algorithm SHA256).Hash

if ($storedHash -eq $actualHash) {
    Write-Host "? Package integrity verified" -ForegroundColor Green
} else {
    Write-Host "? Package integrity check FAILED" -ForegroundColor Red
    exit 1
}
```

### Step 2: Upload Certificato su Azure Portal

```powershell
# 1. Estrai pacchetto principale
$mainPackage = ".\deploy\packages\SecureBootDashboard-Deploy-v1.5.2.zip"
$extractPath = ".\deploy\azure-cert"
Expand-Archive -Path $mainPackage -DestinationPath $extractPath -Force

# 2. Certificato pubblico da caricare su Azure
$cerPath = "$extractPath\certificates\AzureAppRegistration.cer"

Write-Host "Upload this certificate to Azure Portal:" -ForegroundColor Cyan
Write-Host "  File: $cerPath" -ForegroundColor Yellow
Write-Host ""
Write-Host "Steps:" -ForegroundColor White
Write-Host "  1. Open Azure Portal" -ForegroundColor Gray
Write-Host "  2. Navigate to: Entra ID ? App registrations" -ForegroundColor Gray
Write-Host "  3. Select your app registration" -ForegroundColor Gray
Write-Host "  4. Go to: Certificates & secrets ? Certificates" -ForegroundColor Gray
Write-Host "  5. Click 'Upload certificate'" -ForegroundColor Gray
Write-Host "  6. Select: $cerPath" -ForegroundColor Gray
Write-Host "  7. Add description: 'SecureBootDashboard Client v1.5.2'" -ForegroundColor Gray
Write-Host "  8. Click 'Add'" -ForegroundColor Gray
Write-Host ""
Write-Host "Press any key after uploading to Azure Portal..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

# Cleanup
Remove-Item $extractPath -Recurse -Force
```

### Step 3: Creazione Win32 App Intune

```powershell
# Preparazione file per Intune

# 1. Crea directory di staging per Intune
$intuneStaging = ".\deploy\intune-package"
New-Item -ItemType Directory -Path $intuneStaging -Force | Out-Null

# 2. Copia ZIP client
$clientZip = ".\deploy\packages\SecureBootWatcher-Client-v1.5.2.zip"
Copy-Item $clientZip -Destination $intuneStaging

# 3. Copia Deploy-Client.ps1 nella staging
Copy-Item ".\Deploy-Client.ps1" -Destination $intuneStaging

# 4. Crea install.ps1 wrapper per Intune
$installScript = @"
# SecureBootWatcher Intune Installation Script
# Version: 1.5.2

`$ErrorActionPreference = "Stop"

# Log file
`$logFile = "`$env:TEMP\SecureBootWatcher-Install.log"

function Write-Log {
    param([string]`$Message)
    `$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    "[`$timestamp] `$Message" | Out-File `$logFile -Append
    Write-Host `$Message
}

try {
    Write-Log "Starting SecureBootWatcher installation via Intune"
    
    # Get script directory
    `$scriptDir = Split-Path -Parent `$MyInvocation.MyCommand.Path
    
    # Deploy with production settings
    & "`$scriptDir\Deploy-Client.ps1" ``
        -PackageZipPath "`$scriptDir\SecureBootWatcher-Client-v1.5.2.zip" ``
        -ApiBaseUrl "https://api-secureboot.contoso.com" ``
        -FleetId "production-workstations" ``
        -CreateScheduledTask ``
        -ScheduleType Daily ``
        -TaskTime "09:00AM" ``
        -InstallPath "C:\Program Files\SecureBootWatcher"
    
    if (`$LASTEXITCODE -eq 0) {
        Write-Log "Installation completed successfully"
        exit 0
    } else {
        throw "Deploy-Client.ps1 failed with exit code `$LASTEXITCODE"
    }
}
catch {
    Write-Log "Installation failed: `$_"
    exit 1
}
"@

Set-Content -Path "$intuneStaging\install.ps1" -Value $installScript

# 5. Crea uninstall.ps1 per Intune
$uninstallScript = @"
# SecureBootWatcher Intune Uninstallation Script

`$ErrorActionPreference = "Stop"

try {
    # Remove scheduled task
    Unregister-ScheduledTask -TaskName "SecureBootWatcher" -Confirm:`$false -ErrorAction SilentlyContinue
    
    # Remove installation directory
    Remove-Item "C:\Program Files\SecureBootWatcher" -Recurse -Force -ErrorAction SilentlyContinue
    
    # Remove certificate (optional - comment out if you want to keep it)
    # `$cert = Get-ChildItem Cert:\LocalMachine\My | Where-Object { `$_.Subject -like "*SecureBootDashboard*" }
    # if (`$cert) {
    #     Remove-Item "Cert:\LocalMachine\My\`$(`$cert.Thumbprint)" -Force
    # }
    
    Write-Host "Uninstallation completed"
    exit 0
}
catch {
    Write-Host "Uninstallation failed: `$_"
    exit 1
}
"@

Set-Content -Path "$intuneStaging\uninstall.ps1" -Value $uninstallScript

# 6. Crea detection.ps1 per Intune
$detectionScript = @"
# SecureBootWatcher Intune Detection Script

`$installPath = "C:\Program Files\SecureBootWatcher\SecureBootWatcher.Client.exe"
`$scheduledTask = Get-ScheduledTask -TaskName "SecureBootWatcher" -ErrorAction SilentlyContinue

if ((Test-Path `$installPath) -and `$scheduledTask) {
    Write-Host "SecureBootWatcher is installed"
    exit 0
} else {
    exit 1
}
"@

Set-Content -Path "$intuneStaging\detection.ps1" -Value $detectionScript

Write-Host ""
Write-Host "? Intune package prepared in: $intuneStaging" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "1. Package with IntuneWinAppUtil.exe:" -ForegroundColor White
Write-Host "   IntuneWinAppUtil.exe -c `"$intuneStaging`" -s install.ps1 -o `"$intuneStaging\output`"" -ForegroundColor Gray
Write-Host ""
Write-Host "2. Upload to Intune:" -ForegroundColor White
Write-Host "   - Endpoint Manager ? Apps ? Windows ? Add" -ForegroundColor Gray
Write-Host "   - App type: Windows app (Win32)" -ForegroundColor Gray
Write-Host "   - Upload .intunewin file" -ForegroundColor Gray
Write-Host "   - Install command: powershell.exe -ExecutionPolicy Bypass -File install.ps1" -ForegroundColor Gray
Write-Host "   - Uninstall command: powershell.exe -ExecutionPolicy Bypass -File uninstall.ps1" -ForegroundColor Gray
Write-Host "   - Detection: Use custom detection script (detection.ps1)" -ForegroundColor Gray
Write-Host "   - Requirements: Windows 10 1809+ or Windows 11" -ForegroundColor Gray
Write-Host ""
```

### Step 4: Verifica Deployment su Workstation di Test

```powershell
# Su workstation di test (dopo deploy Intune)

# 1. Verifica installazione
Test-Path "C:\Program Files\SecureBootWatcher\SecureBootWatcher.Client.exe"

# 2. Verifica scheduled task
Get-ScheduledTask -TaskName "SecureBootWatcher" | Format-List State,LastRunTime,NextRunTime

# 3. Verifica certificato
$cert = Get-ChildItem Cert:\LocalMachine\My | Where-Object { $_.Subject -like "*SecureBootDashboard*" }
if ($cert) {
    Write-Host "? Certificate installed" -ForegroundColor Green
    Write-Host "Thumbprint: $($cert.Thumbprint)"
    Write-Host "Expires: $($cert.NotAfter)"
} else {
    Write-Host "? Certificate not found" -ForegroundColor Red
}

# 4. Verifica configurazione
$config = Get-Content "C:\Program Files\SecureBootWatcher\appsettings.json" -Raw | ConvertFrom-Json
$config.SecureBootWatcher.Sinks.WebApi.BaseAddress
$config.SecureBootWatcher.Sinks.AzureQueue.CertificateThumbprint

# 5. Test esecuzione client
cd "C:\Program Files\SecureBootWatcher"
.\SecureBootWatcher.Client.exe

# 6. Verifica log Intune
Get-Content "$env:TEMP\SecureBootWatcher-Install.log"
```

---

## Scenario 3: Deployment SCCM

### Step 1: Preparazione Package SCCM

```powershell
# 1. Crea directory package SCCM
$sccmPackage = "\\sccm-server\sources\Apps\SecureBootWatcher\v1.5.2"
New-Item -ItemType Directory -Path $sccmPackage -Force | Out-Null

# 2. Copia file necessari
Copy-Item ".\deploy\packages\SecureBootWatcher-Client-v1.5.2.zip" -Destination $sccmPackage
Copy-Item ".\Deploy-Client.ps1" -Destination $sccmPackage

# 3. Crea install.cmd per SCCM
$installCmd = @"
@echo off
REM SecureBootWatcher SCCM Installation Script

SET LOGFILE=%TEMP%\SecureBootWatcher-Install.log

echo Starting installation... > %LOGFILE%

powershell.exe -ExecutionPolicy Bypass -NoProfile -File "%~dp0Deploy-Client.ps1" -PackageZipPath "%~dp0SecureBootWatcher-Client-v1.5.2.zip" -ApiBaseUrl "https://api-secureboot.contoso.com" -FleetId "production-workstations" -CreateScheduledTask >> %LOGFILE% 2>&1

IF %ERRORLEVEL% EQU 0 (
    echo Installation successful >> %LOGFILE%
    exit /b 0
) ELSE (
    echo Installation failed with error %ERRORLEVEL% >> %LOGFILE%
    exit /b %ERRORLEVEL%
)
"@

Set-Content -Path "$sccmPackage\install.cmd" -Value $installCmd

# 4. Crea uninstall.cmd per SCCM
$uninstallCmd = @"
@echo off
REM SecureBootWatcher SCCM Uninstallation Script

schtasks /Delete /TN "SecureBootWatcher" /F
rd /s /q "C:\Program Files\SecureBootWatcher"

exit /b 0
"@

Set-Content -Path "$sccmPackage\uninstall.cmd" -Value $uninstallCmd

Write-Host "? SCCM package ready at: $sccmPackage" -ForegroundColor Green
```

### Step 2: Creazione Application in SCCM Console

```
1. Open SCCM Console
2. Software Library ? Application Management ? Applications
3. Right-click ? Create Application

Application Properties:
- Name: SecureBootWatcher Client
- Version: 1.5.2
- Publisher: Your Organization
- Software Version: 1.5.2

Deployment Type:
- Type: Script Installer
- Content location: \\sccm-server\sources\Apps\SecureBootWatcher\v1.5.2
- Installation program: install.cmd
- Uninstall program: uninstall.cmd

Detection Method:
- Type: File System
- Path: C:\Program Files\SecureBootWatcher
- File: SecureBootWatcher.Client.exe

User Experience:
- Installation behavior: Install for system
- Logon requirement: Whether or not a user is logged on
- Installation program visibility: Hidden
- Maximum allowed run time: 15 minutes

Requirements:
- Operating system: Windows 10 (All), Windows 11 (All)
- Disk space: 100 MB

Dependencies:
- .NET Framework 4.8

Return Codes:
- 0 = Success
- 1 = Failure
- 3010 = Soft reboot
```

---

## Scenario 4: Troubleshooting

### Problema: Certificato non installato

```powershell
# Diagnosi
Write-Host "Verifica 1: Certificato presente nello ZIP?" -ForegroundColor Cyan
$zipPath = ".\deploy\packages\SecureBootWatcher-Client-v1.5.2.zip"
$tempPath = "$env:TEMP\cert-check"
Expand-Archive -Path $zipPath -DestinationPath $tempPath -Force
Test-Path "$tempPath\certificates\AzureAppRegistration.pfx"

Write-Host "Verifica 2: Password corretta?" -ForegroundColor Cyan
$instructions = Get-Content "$tempPath\certificates\INSTALL-CERTIFICATE.txt" -Raw
if ($instructions -match "Password:\s*(.+)") {
    Write-Host "Password trovata: $($matches[1].Trim())"
}

Write-Host "Verifica 3: Permessi corretti?" -ForegroundColor Cyan
whoami /priv

# Soluzione: Installazione manuale
$pfxPath = "$tempPath\certificates\AzureAppRegistration.pfx"
$password = ConvertTo-SecureString -String "YOUR_PASSWORD" -Force -AsPlainText

try {
    $cert = Import-PfxCertificate `
        -FilePath $pfxPath `
        -CertStoreLocation "Cert:\LocalMachine\My" `
        -Password $password `
        -Exportable
    
    Write-Host "? Certificato installato manualmente" -ForegroundColor Green
    Write-Host "Thumbprint: $($cert.Thumbprint)"
} catch {
    Write-Host "? Errore installazione: $_" -ForegroundColor Red
}

# Cleanup
Remove-Item $tempPath -Recurse -Force
```

### Problema: appsettings.json non aggiornato

```powershell
# Aggiornamento manuale thumbprint
$appsettingsPath = "C:\Program Files\SecureBootWatcher\appsettings.json"

# Get certificate thumbprint
$cert = Get-ChildItem Cert:\LocalMachine\My | Where-Object { $_.Subject -like "*SecureBootDashboard*" }

if ($cert) {
    # Update appsettings.json
    $settings = Get-Content $appsettingsPath -Raw | ConvertFrom-Json
    
    # Update Azure Queue sink
    if ($settings.SecureBootWatcher.Sinks.PSObject.Properties.Name -contains "AzureQueue") {
        $settings.SecureBootWatcher.Sinks.AzureQueue.CertificateThumbprint = $cert.Thumbprint
        $settings | ConvertTo-Json -Depth 10 | Set-Content $appsettingsPath -Encoding UTF8
        
        Write-Host "? appsettings.json updated" -ForegroundColor Green
    } else {
        Write-Host "??  AzureQueue sink not configured in appsettings.json" -ForegroundColor Yellow
    }
} else {
    Write-Host "? Certificate not found" -ForegroundColor Red
}
```

### Problema: Scheduled Task non eseguito

```powershell
# Verifica scheduled task
$task = Get-ScheduledTask -TaskName "SecureBootWatcher" -ErrorAction SilentlyContinue

if ($task) {
    # Controlla stato
    Write-Host "Task State: $($task.State)" -ForegroundColor Cyan
    
    # Controlla ultima esecuzione
    $taskInfo = Get-ScheduledTaskInfo -TaskName "SecureBootWatcher"
    Write-Host "Last Run Time: $($taskInfo.LastRunTime)" -ForegroundColor Cyan
    Write-Host "Last Task Result: $($taskInfo.LastTaskResult)" -ForegroundColor Cyan
    
    # Test esecuzione manuale
    Write-Host "Testing manual execution..." -ForegroundColor Yellow
    Start-ScheduledTask -TaskName "SecureBootWatcher"
    Start-Sleep -Seconds 10
    
    $taskInfo = Get-ScheduledTaskInfo -TaskName "SecureBootWatcher"
    Write-Host "Result: $($taskInfo.LastTaskResult)" -ForegroundColor Cyan
    
    # Se LastTaskResult non è 0, c'è un errore
    if ($taskInfo.LastTaskResult -ne 0) {
        Write-Host "? Task failed with error code: $($taskInfo.LastTaskResult)" -ForegroundColor Red
        Write-Host "Check Event Viewer: Task Scheduler ? Microsoft ? Windows ? TaskScheduler" -ForegroundColor Yellow
    }
} else {
    Write-Host "? Scheduled task not found" -ForegroundColor Red
}
```

---

## Riferimenti Rapidi

### Comandi Utili

```powershell
# Lista certificati LocalMachine\My
Get-ChildItem Cert:\LocalMachine\My | Format-Table Subject, Thumbprint, NotAfter

# Trova certificato SecureBootDashboard
Get-ChildItem Cert:\LocalMachine\My | Where-Object { $_.Subject -like "*SecureBootDashboard*" }

# Verifica thumbprint in appsettings.json
$settings = Get-Content "C:\Program Files\SecureBootWatcher\appsettings.json" -Raw | ConvertFrom-Json
$settings.SecureBootWatcher.Sinks.AzureQueue.CertificateThumbprint

# Verifica scheduled task
Get-ScheduledTask -TaskName "SecureBootWatcher"
Get-ScheduledTaskInfo -TaskName "SecureBootWatcher"

# Test esecuzione client
cd "C:\Program Files\SecureBootWatcher"
.\SecureBootWatcher.Client.exe

# View client logs
Get-Content "C:\Program Files\SecureBootWatcher\logs\*.log" -Tail 50
```

### File Locations

| File | Path |
|------|------|
| Client ZIP | `.\deploy\packages\SecureBootWatcher-Client-v1.5.2.zip` |
| Checksum | `.\deploy\packages\SecureBootWatcher-Client-v1.5.2.zip.sha256` |
| README | `.\deploy\packages\SecureBootWatcher-Client-v1.5.2-README.txt` |
| Deploy Script | `.\Deploy-Client.ps1` |
| Installation | `C:\Program Files\SecureBootWatcher\` |
| Config | `C:\Program Files\SecureBootWatcher\appsettings.json` |
| Certificate | `Cert:\LocalMachine\My` |
| Scheduled Task | Task Scheduler ? SecureBootWatcher |

---

**Note**: Adatta gli URL, password e percorsi secondo il tuo ambiente.
