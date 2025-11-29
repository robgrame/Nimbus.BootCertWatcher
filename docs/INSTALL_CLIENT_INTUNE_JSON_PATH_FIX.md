# Install-Client-Intune.ps1 JSON Path Fix

## Date
November 24, 2025

## Issue
Installation via Intune was failing with the following error:
```
ERROR: Installation failed - The property 'BaseAddress' cannot be found on this object. Verify that the property exists
```

## Root Cause

The `Install-Client-Intune.ps1` script was using incorrect JSON paths when configuring `appsettings.json`:

**WRONG (Before Fix)**:
```powershell
$config.SecureBootWatcher.Sinks.WebApi.BaseAddress = $ApiBaseUrl
$config.SecureBootWatcher.Sinks.EnableWebApi = $true
$config.SecureBootWatcher.FleetId = $FleetId
```

**Problem**: The `Sinks` object is at the **root level** of `appsettings.json`, not under `SecureBootWatcher`.

## Solution

Updated the script to use correct JSON paths:

**CORRECT (After Fix)**:
```powershell
# FIX: Sinks is at root level, not under SecureBootWatcher
$config.Sinks.WebApi.BaseAddress = $ApiBaseUrl
$config.Sinks.EnableWebApi = $true

# FleetId is correctly under SecureBootWatcher
$config.SecureBootWatcher.FleetId = $FleetId
```

## appsettings.json Structure

For reference, here is the actual structure:

```json
{
  "Logging": { ... },
  "SecureBootWatcher": {
    "FleetId": "mslabs",         // ? Correct path
    "RunMode": "Once",
    ...
  },
  "Sinks": {                      // ? At ROOT level, not under SecureBootWatcher!
    "ExecutionStrategy": "StopOnFirstSuccess",
    "EnableWebApi": true,         // ? Correct path
    "WebApi": {
      "BaseAddress": "https://SRVCM00.MSINTUNE.LAB:5001",  // ? Correct path
      "IngestionRoute": "/api/SecureBootReports",
      "HttpTimeout": "00:00:30"
    },
    "EnableAzureQueue": true,
    ...
  },
  "ClientUpdate": { ... },
  "Commands": { ... }
}
```

## Changes Made

### File Modified
- `scripts/Install-Client-Intune.ps1`

### Code Changed

**Lines ~139-149** (approximate):

**Before**:
```powershell
if (-not [string]::IsNullOrEmpty($ApiBaseUrl)) {
    Write-InstallLog "Configure WebApi $ApiBaseUrl"
    
    $config.SecureBootWatcher.Sinks.WebApi.BaseAddress = $ApiBaseUrl  // ? WRONG
    $config.SecureBootWatcher.Sinks.EnableWebApi = $true              // ? WRONG
    Write-InstallLog "Set API Base URL: $ApiBaseUrl"
}

if (-not [string]::IsNullOrEmpty($FleetId)) {
    $config.SecureBootWatcher.FleetId = $FleetId                      // ? CORRECT
    Write-InstallLog "Set Fleet ID: $FleetId"
}
```

**After**:
```powershell
if (-not [string]::IsNullOrEmpty($ApiBaseUrl)) {
    Write-InstallLog "Configure WebApi $ApiBaseUrl"
    
    # FIX: Correct JSON path - Sinks.WebApi.BaseAddress (not SecureBootWatcher.Sinks)
    $config.Sinks.WebApi.BaseAddress = $ApiBaseUrl                    // ? CORRECT
    $config.Sinks.EnableWebApi = $true                                // ? CORRECT
    Write-InstallLog "Set API Base URL: $ApiBaseUrl"
}

if (-not [string]::IsNullOrEmpty($FleetId)) {
    # FIX: Correct JSON path - SecureBootWatcher.FleetId
    $config.SecureBootWatcher.FleetId = $FleetId                      // ? CORRECT
    Write-InstallLog "Set Fleet ID: $FleetId"
}
```

## Testing

### Test Script Created
Created `scripts/Test-AppsettingsJsonPath.ps1` to verify the fix.

**Test Results**:
```
? Current JSON structure:
  SecureBootWatcher.FleetId = mslabs
  Sinks.WebApi.BaseAddress = https://SRVCM00.MSINTUNE.LAB:5001
  Sinks.EnableWebApi = True

? Test 1: OLD METHOD (WRONG)
  Result: ERROR - The property 'BaseAddress' cannot be found on this object.

? Test 2: NEW METHOD (CORRECT)
  Result: SUCCESS ?
  
? Verification:
  SecureBootWatcher.FleetId = test-fleet
  Sinks.WebApi.BaseAddress = https://newapi.contoso.com
  Sinks.EnableWebApi = True

? Test 3: JSON Serialization
  Reading back...
    FleetId = test-fleet
    BaseAddress = https://newapi.contoso.com
    EnableWebApi = True

========================================
ALL TESTS PASSED ?
========================================
```

## Verification

To verify the fix works:

### 1. Manual Test

```powershell
# Run test script
.\scripts\Test-AppsettingsJsonPath.ps1
```

**Expected Output**: "ALL TESTS PASSED ?"

### 2. Intune Package Test

```powershell
# Test install script with parameters
cd "C:\Temp\SecureBootWatcher-Intune"

.\Install-Client-Intune.ps1 `
    -ApiBaseUrl "https://test-api.contoso.com" `
    -FleetId "test-fleet"

# Check the appsettings.json was updated correctly
$config = Get-Content "C:\Program Files\SecureBootWatcher\appsettings.json" | ConvertFrom-Json
Write-Host "BaseAddress: $($config.Sinks.WebApi.BaseAddress)"
Write-Host "FleetId: $($config.SecureBootWatcher.FleetId)"
```

**Expected Output**:
```
BaseAddress: https://test-api.contoso.com
FleetId: test-fleet
```

## Impact

### Before Fix
- ? Installation fails with error
- ? BaseAddress and EnableWebApi not set
- ? Client cannot connect to API
- ? Intune deployment broken

### After Fix
- ? Installation succeeds
- ? BaseAddress and EnableWebApi set correctly
- ? FleetId set correctly
- ? Client can connect to API
- ? Intune deployment works

## Related Files

### Scripts
- `scripts/Install-Client-Intune.ps1` - Fixed installation script
- `scripts/Test-AppsettingsJsonPath.ps1` - Test script (new)

### Configuration
- `SecureBootWatcher.Client/appsettings.json` - Client configuration file

### Documentation
- `docs/INTUNE_WIN32_DEPLOYMENT.md` - Intune deployment guide
- `scripts/README.md` - Scripts documentation

## Deployment Notes

### For Existing Deployments

If you have already deployed the client via Intune with the broken script:

1. **Update the Intune package** with the fixed script
2. **Re-deploy to affected devices** (or manually fix appsettings.json)
3. **Manual fix** (if needed):

```powershell
# On affected devices
$appsettingsPath = "C:\Program Files\SecureBootWatcher\appsettings.json"
$config = Get-Content $appsettingsPath -Raw | ConvertFrom-Json

# Set correct values
$config.Sinks.WebApi.BaseAddress = "https://your-api.contoso.com"
$config.Sinks.EnableWebApi = $true
$config.SecureBootWatcher.FleetId = "your-fleet-id"

# Save
$config | ConvertTo-Json -Depth 10 | Set-Content $appsettingsPath -Encoding UTF8

# Restart scheduled task
Restart-ScheduledTask -TaskName "SecureBootWatcher"
```

### For New Deployments

The fix is included in:
- ? `scripts/Install-Client-Intune.ps1` (current version)
- ? Future Intune packages created with `scripts/Prepare-IntunePackage.ps1`

No additional action needed for new deployments.

## Commit Information

**Files Changed**:
- `scripts/Install-Client-Intune.ps1` - Fixed JSON paths
- `scripts/Test-AppsettingsJsonPath.ps1` - Added test script
- `docs/INSTALL_CLIENT_INTUNE_JSON_PATH_FIX.md` - This documentation

**Commit Message**:
```
fix: correct JSON paths in Install-Client-Intune.ps1

- Fix BaseAddress path from $config.SecureBootWatcher.Sinks.WebApi.BaseAddress 
  to $config.Sinks.WebApi.BaseAddress (Sinks is at root level)
- Fix EnableWebApi path from $config.SecureBootWatcher.Sinks.EnableWebApi 
  to $config.Sinks.EnableWebApi
- Add test script to verify JSON path correctness
- Update inline documentation with correct paths

Fixes installation error: "The property 'BaseAddress' cannot be found on this object"

Tested with Test-AppsettingsJsonPath.ps1 - all tests pass
```

## References

### PowerShell JSON Manipulation
- [ConvertFrom-Json](https://learn.microsoft.com/powershell/module/microsoft.powershell.utility/convertfrom-json)
- [ConvertTo-Json](https://learn.microsoft.com/powershell/module/microsoft.powershell.utility/convertto-json)

### Related Issues
- Intune deployment failing with "BaseAddress not found" error
- Client not connecting to API after Intune installation

---

**Status**: ? Fixed and Tested  
**Last Updated**: November 24, 2025  
**Author**: GitHub Copilot
