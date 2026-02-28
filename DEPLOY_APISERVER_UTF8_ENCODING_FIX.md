# Deploy-ApiServer.ps1 - UTF-8 Encoding Fix

## Issue
PowerShell console was displaying incorrect characters instead of Unicode icons (?, ?, ?) in log output.

## Root Cause
By default, PowerShell uses the system's default encoding, which often doesn't support Unicode characters properly, especially on English Windows systems that use Windows-1252 or similar encodings.

## Solution Applied

### 1. Set Console Encoding to UTF-8
Added at the beginning of the script (after `$ErrorActionPreference`):

```powershell
# Set console encoding to UTF-8 for proper icon display
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8
```

### 2. Updated Icons Throughout Script

| Old Icon | New Icon | Meaning | Usage |
|----------|----------|---------|-------|
| ? | ? | Success | Successful operations |
| ? | ? | Error | Fatal errors |
| ? | ? | Warning | Non-fatal warnings |

### Changes by Function:

#### Write-Success Function
```powershell
# BEFORE
Write-Host "? $Message" -ForegroundColor Green

# AFTER  
Write-Host "? $Message" -ForegroundColor Green
```

#### Test-Prerequisites Function
- ? for: IIS not installed, modules not found, ASP.NET Core Module missing, source files missing, SSL certificate not found
- ? for: Certificate expired/not yet valid, no SSL certificate provided

#### New-ApplicationPool Function
- ? for: Application Pool already exists

#### New-IisWebsite Function
- ? for: Website already exists, could not find binding path

#### Set-WebConfiguration Function
- ? for: Skipping configuration tweaks

##  Benefits

### Before Fix
```
? IIS is installed
? Application Pool created
? Website created
? Deployment completed successfully
```

### After Fix
```
? IIS is installed
? Application Pool created
? Website created  
? Deployment completed successfully
```

## Technical Details

### What the Fix Does

1. **[Console]::OutputEncoding**
   - Sets the encoding used by the .NET Console class
   - Affects Write-Host and similar console output
   - Must be set to UTF-8 to support Unicode characters

2. **$OutputEncoding**
   - PowerShell variable controlling encoding for external processes
   - Ensures piped output also uses UTF-8
   - Prevents encoding issues when output is captured or redirected

### Why Both Are Needed

- [Console]::OutputEncoding - For direct console output (Write-Host)
- $OutputEncoding - For piped/redirected output and external programs

## Verification

Test the encoding fix:
```powershell
# Run this in PowerShell to test
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
Write-Host "? Success icon" -ForegroundColor Green
Write-Host "? Error icon" -ForegroundColor Red
Write-Host "? Warning icon" -ForegroundColor Yellow
```

## Version

- **Version**: 1.3
- **Previous**: 1.2 (had infinite loop fix)
- **Status**: Production Ready

