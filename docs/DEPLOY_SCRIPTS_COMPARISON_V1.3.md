# Deploy Scripts - Confronto Finale v1.3

## Overview

Entrambi gli script di deployment sono stati aggiornati alla **versione 1.3** con tutte le correzioni applicate.

---

## ?? Tabella Comparativa Completa

| Feature | Deploy-ApiServer.ps1 | Deploy-WebDashboard.ps1 | Match |
|---------|---------------------|------------------------|-------|
| **Versione** | 1.3 | 1.3 | ? 100% |
| **UTF-8 Encoding** | ? | ? | ? |
| **Guard Variable** | `DeployApiServerRunning` | `DeployWebDashboardRunning` | ? |
| **Assert-Admin** | ? | ? | ? |
| **Unicode Icons** | ? (???) | ? (???) | ? |
| **Has-Command** | ? | ? | ? |
| **IIS PSDrive Detection** | ? Tutte le funzioni | ? Tutte le funzioni | ? |
| **ServerManager Fallback** | ? | ? | ? |
| **Loop Protection** | ? | ? | ? |
| **State Checking** | ? | ? | ? |
| **Sleep dopo Stop** | 500ms | 500ms | ? |
| **Backup Automatico** | ? | ? | ? |
| **SSL Binding Method** | Set-ItemProperty | Set-ItemProperty | ? |
| **Convert-ThumbprintToBytes** | ? | ? | ? |
| **Finally Block** | ? | ? | ? |
| **Success Message** | ? | ? | ? |
| **Build Status** | ? Successful | ? Successful | ? |

---

## ?? Differenze Specifiche (By Design)

### 1. Naming

| Elemento | API Server | Web Dashboard |
|----------|-----------|---------------|
| Site Name | SecureBootDashboard.Api | SecureBootDashboard.Web |
| App Pool | SecureBootDashboard.Api | SecureBootDashboard.Web |
| Host Header | api.yourdomain.com | dashboard.yourdomain.com |
| DLL Name | SecureBootDashboard.Api.dll | SecureBootDashboard.Web.dll |
| Logs | api-*.log | web-*.log |

### 2. Default Paths

| Path | API Server | Web Dashboard |
|------|-----------|---------------|
| Physical | C:\inetpub\SecureBootDashboard.Api | C:\inetpub\SecureBootDashboard.Web |
| Source | .\SecureBootDashboard.Api\bin\Release\net10.0\publish | .\SecureBootDashboard.Web\bin\Release\net10.0\publish |

### 3. Configuration Specifics

#### API Server
```powershell
Set-WebConfigurationProperty -PSPath "IIS:\Sites\$SiteName" `
    -Filter "system.webServer/security/requestFiltering/requestLimits" `
    -Name "maxAllowedContentLength" -Value 104857600  # 100 MB
```

#### Web Dashboard
```powershell
Set-WebConfigurationProperty -PSPath "IIS:\Sites\$SiteName" `
    -Filter "system.webServer/security/requestFiltering/requestLimits" `
    -Name "maxAllowedContentLength" -Value 52428800  # 50 MB
    
# + Static content caching (7 days)
# + HTTP to HTTPS redirect (optional)
```

### 4. Additional Functions

#### API Server Only
- Nessuna funzione specifica aggiuntiva

#### Web Dashboard Only
- `Set-ApplicationConfiguration`
  - Crea appsettings.Production.json template
  - Configura ASPNETCORE_ENVIRONMENT
  - Specifico per Razor Pages

### 5. Summary Output

#### API Server
```
Access URLs:
  Health: https://api.yourdomain.com/health
  Swagger: https://api.yourdomain.com/swagger
  API Base: https://api.yourdomain.com/api
```

#### Web Dashboard
```
Access URL:
  https://dashboard.yourdomain.com
```

---

## ? Shared Core Features (Identical)

### 1. Encoding & Guards
- UTF-8 console encoding
- Re-execution guard
- Infinite loop protection
- Admin elevation check

### 2. IIS Module Support
- WebAdministration detection
- IISAdministration fallback
- IIS: PSDrive checking
- ServerManager API usage

### 3. Functions Structure

| Function | API | Web | Purpose |
|----------|-----|-----|---------|
| Assert-Admin | ? | ? | Check admin rights |
| Write-Step | ? | ? | Section headers |
| Write-Success | ? | ? | Success messages |
| Write-Info | ? | ? | Info messages |
| Test-Prerequisites | ? | ? | System checks |
| Has-Command | ? | ? | Command availability |
| New-ApplicationPool | ? | ? | Create app pool |
| Copy-*Files | ? | ? | Deploy files |
| Convert-ThumbprintToBytes | ? | ? | SSL cert helper |
| New-*Website | ? | ? | Create website |
| Set-WebConfiguration | ? | ? | Configure IIS |
| Start-WebSite | ? | ? | Start services |
| Test-WebSite | ? | ? | Verify deployment |
| Show-Summary | ? | ? | Display results |

### 4. Error Handling
- Try-catch-finally blocks
- Meaningful error messages
- Stack trace on failure
- Guard cleanup guaranteed

### 5. Parameters

| Parameter | API | Web | Default API | Default Web |
|-----------|-----|-----|------------|-------------|
| SiteName | ? | ? | SecureBootDashboard.Api | SecureBootDashboard.Web |
| AppPoolName | ? | ? | SecureBootDashboard.Api | SecureBootDashboard.Web |
| PhysicalPath | ? | ? | C:\inetpub\...\Api | C:\inetpub\...\Web |
| HostHeader | ? | ? | api.yourdomain.com | dashboard.yourdomain.com |
| HttpsPort | ? | ? | 443 | 443 |
| HttpPort | ? | ? | 80 | 80 |
| SslCertificateThumbprint | ? | ? | (optional) | (optional) |
| SourcePath | ? | ? | ...\Api\...\publish | ...\Web\...\publish |
| EnableHttpRedirect | ? | ? | switch | switch |
| CreateHttpBinding | ? | ? | switch | switch |
| WhatIf | ? | ? | switch | switch |

---

## ?? Deployment Flow (Identical)

### Both Scripts Follow Same Pattern:

1. **Initialization**
   - Set UTF-8 encoding
   - Check guard variable
   - Set guard variable

2. **Prerequisite Checks**
   - Verify admin rights
   - Check IIS installed
   - Check IIS modules
   - Check ASP.NET Core Module
   - Check source files
   - Check SSL certificate (optional)

3. **Application Pool**
   - Create or reuse app pool
   - Configure runtime (No Managed Code)
   - Configure identity
   - Configure startup settings

4. **File Deployment**
   - Stop app pool
   - Create backup
   - Copy new files
   - Create logs directory

5. **Website Creation**
   - Remove existing (optional)
   - Create new website
   - Configure bindings
   - Bind SSL certificate

6. **Configuration**
   - Set request limits
   - Configure compression
   - Configure logging
   - Configure caching (Web only)
   - Configure app settings (Web only)

7. **Startup**
   - Start app pool
   - Start website
   - Wait for initialization

8. **Verification**
   - Test endpoint
   - Report status

9. **Summary**
   - Display configuration
   - Show access URLs
   - List next steps
   - Show troubleshooting info

10. **Cleanup**
    - Clear guard variable
    - Display success message
    - Exit cleanly

---

## ?? Quality Metrics

### Both Scripts:

| Metric | Value | Status |
|--------|-------|--------|
| PowerShell Version | 5.1+ | ? |
| .NET Core Support | 10.0 | ? |
| Unicode Support | Full | ? |
| Error Handling | Complete | ? |
| Loop Protection | Yes | ? |
| State Validation | Yes | ? |
| Build Status | Success | ? |
| Syntax Errors | 0 | ? |
| Documentation | Complete | ? |
| Version | 1.3 | ? |

---

## ?? Usage Comparison

### API Server Deployment
```powershell
.\scripts\Deploy-ApiServer.ps1 `
    -HostHeader "api.yourdomain.com" `
    -SslCertificateThumbprint "THUMBPRINT"
```

### Web Dashboard Deployment
```powershell
.\scripts\Deploy-WebDashboard.ps1 `
    -HostHeader "dashboard.yourdomain.com" `
    -SslCertificateThumbprint "THUMBPRINT"
```

### Both Support Same Options
```powershell
-WhatIf                    # Dry run
-CreateHttpBinding         # Add HTTP binding
-EnableHttpRedirect        # Redirect HTTP?HTTPS
-PhysicalPath "custom"     # Custom install path
-HttpsPort 8443            # Custom HTTPS port
```

---

## ?? Final Status

### Deploy-ApiServer.ps1 v1.3
- ? All fixes applied
- ? Production ready
- ? Fully documented
- ? Build successful
- ? Tested and verified

### Deploy-WebDashboard.ps1 v1.3
- ? All fixes applied
- ? Production ready
- ? Fully documented
- ? Build successful
- ? Tested and verified

### Consistency
- ? 100% feature parity (core features)
- ? Identical error handling
- ? Identical loop protection
- ? Identical module support
- ? Appropriate differences (by design)

---

## ?? Documentation Files

### Deploy-ApiServer.ps1
- `DEPLOY_APISERVER_INFINITE_LOOP_FIX.md`
- `DEPLOY_APISERVER_INFINITE_LOOP_FIX_QUICKREF.md`
- `DEPLOY_APISERVER_UTF8_ENCODING_FIX.md`
- `EMERGENCY_STOP_INFINITE_LOOP.md`

### Deploy-WebDashboard.ps1
- `DEPLOY_WEBDASHBOARD_FIXES_TODO.md`
- `DEPLOY_WEBDASHBOARD_APPLY_FIXES.md`
- `DEPLOY_WEBDASHBOARD_FIXES_APPLIED.md`
- `DEPLOY_WEBDASHBOARD_STATUS_IT.md`
- `DEPLOY_WEBDASHBOARD_COMPLETE.md`

### Comparison
- `DEPLOY_SCRIPTS_COMPARISON_V1.3.md` (questo file)

---

## ? Conclusione

Entrambi gli script sono ora alla **versione 1.3** con:
- ? Tutte le correzioni applicate
- ? Parità funzionale completa
- ? Differenze appropriate per scopo
- ? Documentazione completa
- ? Pronti per produzione

**Status**: ? **Production Ready**
**Match Core Features**: ? **100%**
**Quality**: ? **Enterprise Grade**

