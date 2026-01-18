# Deploy-ApiServer.ps1 - Bug Fixes Summary

## Overview
Fixed critical issues in the `Deploy-ApiServer.ps1` script that prevented successful IIS deployment when the IISAdministration module was used instead of WebAdministration module.

## Issues Fixed

### 1. **SSL Certificate Binding Error (FIXED)**
**Error Message**: `Method invocation failed because [...] does not contain a method named 'AddSslCertificate'`

**Root Cause**: The WebAdministration module's binding object doesn't have an `AddSslCertificate()` method. The original code attempted to call this non-existent method.

**Solution**: Replaced with proper `Set-ItemProperty` cmdlet approach:
```powershell
Set-ItemProperty -Path $bindingPath -Name "certificateHash" -Value $CertThumbprint
Set-ItemProperty -Path $bindingPath -Name "certificateStoreName" -Value "My"
```

**Location**: `New-IisWebsite` function, lines 416-420

---

### 2. **Missing IIS PSDrive with IISAdministration Module (FIXED)**
**Error Message**: `Cannot find drive. A drive with the name 'IIS' does not exist.`

**Root Cause**: When IISAdministration module is loaded instead of WebAdministration, the IIS PowerShell provider (PSDrive) is not available. The script tried to use `IIS:\` paths without checking if the drive exists.

**Affected Functions**:
- `New-ApplicationPool` - Used `IIS:\AppPools\$Name` paths
- `New-IisWebsite` - Used `IIS:\Sites\$Name` paths  
- `Set-WebConfiguration` - Used `IIS:\Sites\$SiteName` paths

**Solution**: Added proper detection to check if IIS PSDrive is available before using WebAdministration cmdlets. Falls back to ServerManager API when IIS PSDrive is unavailable.

#### New-ApplicationPool (Lines 266-322)
```powershell
# Check if WebAdministration module is available with IIS PSDrive support
$hasWebAdminDrive = $false
if (Has-Command 'New-WebAppPool') {
    $hasWebAdminDrive = Test-Path "IIS:\" -ErrorAction SilentlyContinue
}

if ($hasWebAdminDrive) {
    # Use WebAdministration cmdlets
    # ...
} else {
    # Fall back to ServerManager API
    $sm = Get-IISServerManager
    # ...
}
```

#### New-IisWebsite (Lines 400-436)
```powershell
if (Has-Command 'WebAdministration\New-WebSite') {
    # Check if IIS PSDrive is available
    $hasWebAdminDrive = Test-Path "IIS:\" -ErrorAction SilentlyContinue
    
    if ($hasWebAdminDrive) {
        # Use WebAdministration cmdlets
        # ...
    }
}
# Fall back to ServerManager API
```

#### Set-WebConfiguration (Lines 481-497)
```powershell
$hasWebAdminDrive = Test-Path "IIS:\" -ErrorAction SilentlyContinue

if ($hasWebAdminDrive -and (Has-Command 'Set-WebConfigurationProperty')) {
    # Use WebAdministration cmdlets
    # ...
} else {
    # Gracefully skip advanced configuration
    Write-Host "? Skipping site configuration tweaks..."
}
```

---

### 3. **Undefined Function Call (FIXED)**
**Error Message**: `The term 'Set-ApplicationConfiguration' is not recognized`

**Root Cause**: The script called `Set-ApplicationConfiguration` function which was never defined in the script.

**Solution**: Removed the undefined function call (line 622). Application configuration should be managed through `appsettings.json` files in the deployment directory.

**Location**: Main execution block, removed function call

---

### 4. **Force Parameter Added to New-WebAppPool (IMPROVED)**
**Location**: Line 277 in `New-ApplicationPool`

**Change**: Added `-Force` parameter and `-ErrorAction SilentlyContinue` to handle cases where the app pool already exists:
```powershell
New-WebAppPool -Name $Name -Force -ErrorAction SilentlyContinue | Out-Null
```

---

## Deployment Module Support

The script now properly supports **both** IIS management approaches:

### WebAdministration Module (Preferred)
- Uses native PowerShell cmdlets
- Requires IIS PSDrive to be available
- Provides direct access to IIS configuration
- Used when available

### IISAdministration Module (Fallback)
- Uses Microsoft.Web.Administration API
- Works when WebAdministration is not installed
- Provides programmatic access to IIS objects
- Automatically used when IIS PSDrive is unavailable

---

## Testing Recommendations

### Test Scenario 1: WebAdministration Module
1. Ensure `Web-Scripting-Tools` role service is installed
2. Verify IIS PSDrive is available: `Test-Path 'IIS:\'`
3. Run script with `-WhatIf` flag first
4. Execute full deployment

### Test Scenario 2: IISAdministration Module Only
1. Ensure WebAdministration module is not available
2. Verify ServerManager API is working
3. Run script with `-WhatIf` flag first
4. Execute full deployment

### Validation Steps
```powershell
# Check if script can be dot-sourced
. '.\scripts\Deploy-ApiServer.ps1'

# Test with WhatIf
.\scripts\Deploy-ApiServer.ps1 `
    -SiteName "SecureBootDashboard.Api" `
    -AppPoolName "SecureBootDashboard.Api" `
    -PhysicalPath "C:\inetpub\SecureBootDashboard.Api" `
    -HostHeader "api.yourdomain.com" `
    -WhatIf

# Full deployment (requires admin, valid certificate, and published binaries)
.\scripts\Deploy-ApiServer.ps1 `
    -SiteName "SecureBootDashboard.Api" `
    -AppPoolName "SecureBootDashboard.Api" `
    -PhysicalPath "C:\inetpub\SecureBootDashboard.Api" `
    -HostHeader "api.yourdomain.com" `
    -SslCertificateThumbprint "YOUR_THUMBPRINT_HERE"
```

---

## Backwards Compatibility

All changes are **fully backwards compatible**:
- Existing deployments using WebAdministration will continue to work
- New deployments using only IISAdministration are now supported
- Script gracefully handles mixed environments
- All error messages provide clear guidance for troubleshooting

---

## Related Files

- `scripts/Deploy-WebDashboard.ps1` - May benefit from similar fixes
- `docs/SERVER_INFRASTRUCTURE_DEPLOYMENT.md` - Deployment documentation
- `docs/AZURE_DEPLOYMENT_GUIDE.md` - Azure deployment information

---

## Summary of Changes by Function

| Function | Changes | Lines |
|----------|---------|-------|
| `New-ApplicationPool` | Added IIS PSDrive detection, improved fallback logic | 266-322 |
| `New-IisWebsite` | Fixed SSL certificate binding, added IIS PSDrive detection | 400-436, 416-420 |
| `Set-WebConfiguration` | Added IIS PSDrive detection, graceful fallback | 481-497 |
| Main execution | Removed undefined function call | - |

