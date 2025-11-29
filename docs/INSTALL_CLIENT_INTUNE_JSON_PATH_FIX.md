# Install-Client-Intune.ps1 JSON Path Fix

## Date
2025-01-24 (Updated)

## Issue
Installation via Intune was failing with the following error:
```
ERROR: Installation failed - The property 'BaseAddress' cannot be found on this object. Verify that the property exists
```

## Root Cause

The `Install-Client-Intune.ps1` script was using incorrect JSON paths when configuring `appsettings.json`:

**WRONG (Before Fix)**:
```powershell
# Missing SecureBootWatcher parent object!
$config.Sinks.WebApi.BaseAddress = $ApiBaseUrl
$config.Sinks.EnableWebApi = $true
```

**Problem**: The `Sinks` object is **under `SecureBootWatcher`**, not at the root level.

## Solution

Updated the script to use correct JSON paths:

**CORRECT (After Fix)**:
```powershell
# FIX: Correct JSON path - SecureBootWatcher.Sinks.WebApi.BaseAddress
$config.SecureBootWatcher.Sinks.WebApi.BaseAddress = $ApiBaseUrl
$config.SecureBootWatcher.Sinks.EnableWebApi = $true

# FleetId is correctly under SecureBootWatcher
$config.SecureBootWatcher.FleetId = $FleetId
```

## appsettings.json Structure

For reference, here is the actual structure:

```json
{
  "Logging": { ... },
  "SecureBootWatcher": {              // ? Parent object
    "FleetId": "mslabs",              // ? Correct path: SecureBootWatcher.FleetId
    "RunMode": "Once",
    "Sinks": {                        // ? Sinks is UNDER SecureBootWatcher!
      "ExecutionStrategy": "StopOnFirstSuccess",
      "EnableWebApi": true,           // ? Correct path: SecureBootWatcher.Sinks.EnableWebApi
      "WebApi": {
        "BaseAddress": "https://...", // ? Correct path: SecureBootWatcher.Sinks.WebApi.BaseAddress
        "IngestionRoute": "/api/SecureBootReports",
        "HttpTimeout": "00:00:30"
      },
      "EnableAzureQueue": true,
      ...
    },
    "ClientUpdate": { ... },
    "Commands": { ... }
  }
}
```

**Correct Paths:**
- ? Fleet ID: `SecureBootWatcher.FleetId`
- ? Web API Enabled: `SecureBootWatcher.Sinks.EnableWebApi`  
- ? Base Address: `SecureBootWatcher.Sinks.WebApi.BaseAddress`  
- ? Ingestion Route: `SecureBootWatcher.Sinks.WebApi.IngestionRoute`

**Incorrect Paths (OLD):**
- ? `Sinks.WebApi.BaseAddress` - Missing `SecureBootWatcher` parent
- ? `Sinks.EnableWebApi` - Missing `SecureBootWatcher` parent

## Changes Made

### File Modified
- `scripts/Install-Client-Intune.ps1`

### Code Changed

**Lines ~139-149** (approximate):

**Before (WRONG)**:
```powershell
if (-not [string]::IsNullOrEmpty($ApiBaseUrl)) {
    Write-InstallLog "Configure WebApi $ApiBaseUrl"
    
    # WRONG: Missing SecureBootWatcher parent
    $config.Sinks.WebApi.BaseAddress = $ApiBaseUrl
    $config.Sinks.EnableWebApi = $true
    Write-InstallLog "Set API Base URL: $ApiBaseUrl"
}

if (-not [string]::IsNullOrEmpty($FleetId)) {
    $config.SecureBootWatcher.FleetId = $FleetId
    Write-InstallLog "Set Fleet ID: $FleetId"
}
```

**After (CORRECT)**:
```powershell
if (-not [string]::IsNullOrEmpty($ApiBaseUrl)) {
    Write-InstallLog "Configure WebApi $ApiBaseUrl"
    
    # FIX: Correct JSON path - SecureBootWatcher.Sinks.WebApi.BaseAddress
    $config.SecureBootWatcher.Sinks.WebApi.BaseAddress = $ApiBaseUrl
    $config.SecureBootWatcher.Sinks.EnableWebApi = $true
    Write-InstallLog "Set API Base URL: $ApiBaseUrl"
}

if (-not [string]::IsNullOrEmpty($FleetId)) {
    # Correct JSON path - SecureBootWatcher.FleetId
    $config.SecureBootWatcher.FleetId = $FleetId
    Write-InstallLog "Set Fleet ID: $FleetId"
}
```

## Testing

### Verification Command

Run this PowerShell command to verify the structure:

```powershell
# Verify Sinks is under SecureBootWatcher
Get-Content "SecureBootWatcher.Client\appsettings.json" -Raw | `
    ConvertFrom-Json | `
    Select-Object -ExpandProperty SecureBootWatcher | `
    Select-Object -ExpandProperty Sinks | `
    ConvertTo-Json -Depth 2
```

**Output shows** that Sinks is indeed under SecureBootWatcher! ?

### Test Script Created
Created `scripts/Test-AppsettingsJsonPath.ps1` to verify the fix.

**Run Test:**
```powershell
.\scripts\Test-AppsettingsJsonPath.ps1
```

**Expected Output**:
```
Testing appsettings.json configuration paths

Current JSON structure:
  SecureBootWatcher.FleetId = mslabs
  SecureBootWatcher.Sinks.WebApi.BaseAddress = https://SRVCM00.MSINTUNE.LAB:5001
  SecureBootWatcher.Sinks.EnableWebApi = True

Test: Setting configuration values
  Setting: $config.SecureBootWatcher.Sinks.WebApi.BaseAddress = 'https://newapi.contoso.com'
  Setting: $config.SecureBootWatcher.Sinks.EnableWebApi = $true
  Setting: $config.SecureBootWatcher.FleetId = 'test-fleet'
  Result: SUCCESS ?

Verification:
  SecureBootWatcher.FleetId = test-fleet
  SecureBootWatcher.Sinks.WebApi.BaseAddress = https://newapi.contoso.com
  SecureBootWatcher.Sinks.EnableWebApi = True

========================================
ALL TESTS PASSED ?
========================================

Summary:
  ? Correct path for BaseAddress: $config.SecureBootWatcher.Sinks.WebApi.BaseAddress
  ? Correct path for EnableWebApi: $config.SecureBootWatcher.Sinks.EnableWebApi
  ? Correct path for FleetId: $config.SecureBootWatcher.FleetId
```

## Verification

### Manual Install Test

```powershell
# Test install script with parameters
.\scripts\Install-Client-Intune.ps1 `
    -ApiBaseUrl "https://test-api.contoso.com:5001" `
    -FleetId "test-fleet"

# Verify configuration
$config = Get-Content "C:\Program Files\SecureBootWatcher\appsettings.json" -Raw | ConvertFrom-Json
Write-Host "BaseAddress: $($config.SecureBootWatcher.Sinks.WebApi.BaseAddress)"
Write-Host "FleetId: $($config.SecureBootWatcher.FleetId)"
```

**Expected Output**:
```
BaseAddress: https://test-api.contoso.com:5001
FleetId: test-fleet
```

## Impact

### Before Fix
- ? Installation fails with "BaseAddress cannot be found" error
- ? Intune deployment broken
- ? Client cannot connect to API
- ? Manual intervention required

### After Fix
- ? Installation succeeds
- ? BaseAddress configured correctly
- ? EnableWebApi configured correctly
- ? FleetId configured correctly
- ? Client can connect to API
- ? Intune deployment works

## Related Files

### Scripts
- `scripts/Install-Client-Intune.ps1` - ? Fixed installation script
- `scripts/Test-AppsettingsJsonPath.ps1` - ? Test script (new)

### Configuration
- `SecureBootWatcher.Client/appsettings.json` - Reference configuration file

### Documentation
- `docs/INTUNE_WIN32_DEPLOYMENT.md` - Intune deployment guide
- `docs/INSTALL_CLIENT_INTUNE_JSON_PATH_FIX.md` - This document

## Deployment Notes

### For Existing Deployments

If you have deployed the client via Intune with the broken script:

1. **Re-create Intune package** with fixed script:
   ```powershell
   .\scripts\Prepare-IntunePackage.ps1
   ```

2. **Update Win32 app** in Intune with new `.intunewin` file

3. **Re-deploy** to affected devices

4. **Manual fix** (if needed on already-installed devices):
   ```powershell
   # On affected devices
   $appsettingsPath = "C:\Program Files\SecureBootWatcher\appsettings.json"
   $config = Get-Content $appsettingsPath -Raw | ConvertFrom-Json

   # Set correct values
   $config.SecureBootWatcher.Sinks.WebApi.BaseAddress = "https://your-api.contoso.com:5001"
   $config.SecureBootWatcher.Sinks.EnableWebApi = $true
   $config.SecureBootWatcher.FleetId = "your-fleet-id"

   # Save
   $config | ConvertTo-Json -Depth 10 | Set-Content $appsettingsPath -Encoding UTF8
   ```

### For New Deployments

The fix is included automatically when creating new packages:

```powershell
# Create new package with fix included
.\scripts\Prepare-IntunePackage.ps1

# Deploy to Intune as normal
```

---

**Status**: ? Fixed and Tested  
**Last Updated**: 2025-01-24  
**Version**: v1.11.3

---

**Made with ?? for IT Operations Teams**
