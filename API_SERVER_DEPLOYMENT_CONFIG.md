# API Server Deployment Configuration

## ?? **Modifiche Necessarie per Deploy-ApiServer.ps1**

### **Contesto**

L'API Server usa **Mutual TLS** (certificate-based authentication) invece di Windows Authentication.  
Tuttavia, per **consistenza** con il Web Dashboard e per evitare potenziali problemi, dobbiamo applicare alcune modifiche.

---

## ? **1. App Pool .NET CLR Version**

### **Modifica Richiesta**

**File**: `scripts/Deploy-ApiServer.ps1`  
**Linea**: ~292

**Prima**:
```powershell
Set-ItemProperty "IIS:\AppPools\$Name" -Name "managedRuntimeVersion" -Value ""
Write-Info "Runtime version: No Managed Code (.NET Core)"
```

**Dopo**:
```powershell
Set-ItemProperty "IIS:\AppPools\$Name" -Name "managedRuntimeVersion" -Value "v4.0"
Write-Info "Runtime version: .NET CLR v4.0 (for consistency and compatibility)"
```

### **Motivo**

- ? **Consistenza** con Web Dashboard
- ? **Compatibilità** con eventuali moduli IIS
- ? **Best Practice** per deployment production

---

## ? **2. web.config - OutOfProcess Hosting**

### **Configurazione Necessaria**

L'API deve usare **`hostingModel="outofprocess"`** come il Web Dashboard.

**Template web.config per API**:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <location path="." inheritInChildApplications="false">
    <system.webServer>
      <handlers>
        <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
      </handlers>
      <aspNetCore processPath="dotnet"
                  arguments=".\SecureBootDashboard.Api.dll"
                  stdoutLogEnabled="true"
                  stdoutLogFile="C:\Logs\SecureBootDashboard\api-stdout"
                  hostingModel="outofprocess"
                  forwardWindowsAuthToken="false">
        <environmentVariables>
          <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
        </environmentVariables>
      </aspNetCore>
    </system.webServer>
  </location>
</configuration>
```

### **Key Settings**

| Setting | Value | Motivo |
|---------|-------|--------|
| **hostingModel** | `outofprocess` | Processo separato, isolamento |
| **forwardWindowsAuthToken** | `false` | Non usa Windows Auth |
| **stdoutLogEnabled** | `true` | Logging per troubleshooting |
| **stdoutLogFile** | `C:\Logs\SecureBootDashboard\api-stdout` | Path log stdout |

---

## ?? **3. Autenticazione IIS**

### **IMPORTANTE: NO Windows Authentication per API**

A differenza del Web Dashboard, l'API **NON usa Windows Authentication**.

**Configurazione Corretta**:
- ? **Windows Authentication**: DISABLED (default)
- ? **Anonymous Authentication**: ENABLED (default)
- ? **Mutual TLS**: Configurato in ASP.NET Core (non in IIS)

### **Motivo**

L'API usa **certificate-based authentication** (Mutual TLS):
- Client presenta certificate
- ASP.NET Core valida certificate thumbprint
- Autorizzazione basata su configurazione in database

---

## ?? **Modifiche Codice Deploy-ApiServer.ps1**

### **Modifica 1: App Pool .NET CLR**

```powershell
# Linea ~292
Set-ItemProperty "IIS:\AppPools\$Name" -Name "managedRuntimeVersion" -Value "v4.0"
Write-Info "Runtime version: .NET CLR v4.0 (for consistency and compatibility)"
```

### **Modifica 2: Sezione Set-ApplicationConfiguration**

Aggiungere creazione/aggiornamento web.config (simile a Web Dashboard, ma con path diversi):

```powershell
function Set-ApplicationConfiguration {
    param(
        [string]$PhysicalPath
    )
    
    Write-Step "Configuring application settings"
    
    if ($WhatIf) {
        Write-Info "Would configure application settings"
        return
    }
    
    # Create or update web.config with correct settings
    $webConfig = Join-Path $PhysicalPath "web.config"
    
    if (-not (Test-Path $webConfig)) {
        Write-Host "web.config not found - creating with correct OutOfProcess configuration..." -ForegroundColor Yellow
        
        $webConfigContent = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <location path="." inheritInChildApplications="false">
    <system.webServer>
      <handlers>
        <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
      </handlers>
      <aspNetCore processPath="dotnet"
                  arguments=".\SecureBootDashboard.Api.dll"
                  stdoutLogEnabled="true"
                  stdoutLogFile="C:\Logs\SecureBootDashboard\api-stdout"
                  hostingModel="outofprocess"
                  forwardWindowsAuthToken="false">
        <environmentVariables>
          <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
        </environmentVariables>
      </aspNetCore>
    </system.webServer>
  </location>
</configuration>
"@
        
        Set-Content -Path $webConfig -Value $webConfigContent -Encoding UTF8
        Write-Success "web.config created with OutOfProcess configuration"
        Write-Info "  hostingModel: outofprocess"
        Write-Info "  stdoutLogEnabled: true"
    } else {
        Write-Info "Updating existing web.config..."
        
        # (Codice simile a Web Dashboard per update)
        # ...
    }
}
```

---

## ?? **Confronto Web vs API**

| Aspetto | Web Dashboard | API Server |
|---------|---------------|------------|
| **App Pool CLR** | v4.0 | v4.0 ? |
| **hostingModel** | outofprocess | outofprocess ? |
| **Windows Auth IIS** | ENABLED | **DISABLED** ? |
| **Anonymous Auth IIS** | DISABLED | **ENABLED** ? |
| **Autenticazione App** | Negotiate (Windows) | **Mutual TLS** (Certificates) |
| **Autorizzazione** | User/Group based | **Certificate Thumbprint** based |

---

## ?? **Script di Applicazione Modifiche**

### **Quick Fix per App Pool Esistente**

```powershell
# Quick-Fix-ApiServer-AppPool.ps1
Import-Module WebAdministration

$appPoolName = "SecureBootDashboard.Api"
$appPoolPath = "IIS:\AppPools\$appPoolName"

if (-not (Test-Path $appPoolPath)) {
    Write-Host "? App Pool not found!" -ForegroundColor Red
    exit 1
}

# Get current
$appPool = Get-Item $appPoolPath
Write-Host "Current .NET CLR Version: $($appPool.managedRuntimeVersion)" -ForegroundColor Gray

# Change to v4.0
Stop-WebAppPool $appPoolName -ErrorAction SilentlyContinue
Start-Sleep -Seconds 3

Set-ItemProperty $appPoolPath -Name "managedRuntimeVersion" -Value "v4.0"
Write-Host "? Changed to v4.0" -ForegroundColor Green

Start-WebAppPool $appPoolName
Write-Host "? App Pool restarted" -ForegroundColor Green
```

---

## ? **Checklist Finale API**

- [ ] **Deploy-ApiServer.ps1**: App Pool CLR = v4.0
- [ ] **web.config**: hostingModel = outofprocess
- [ ] **web.config**: stdoutLogEnabled = true
- [ ] **IIS Anonymous Auth**: ENABLED (default, OK)
- [ ] **IIS Windows Auth**: DISABLED (default, OK)
- [ ] **Mutual TLS config**: In database (non toccare)

---

## ?? **Note Importanti**

1. ? **Non modificare autenticazione IIS** - API usa Mutual TLS, non Windows Auth
2. ? **App Pool CLR v4.0** - Per consistenza con Web Dashboard
3. ? **OutOfProcess** - Isolamento e stabilità
4. ?? **Mutual TLS** - Configurato in ASP.NET Core, non in IIS

---

**Version**: 1.0.0 (API Server Configuration Guide)  
**Related**: FINAL_SOLUTION_WINDOWS_AUTH_CONFLICT.md, Deploy-WebDashboard.ps1

