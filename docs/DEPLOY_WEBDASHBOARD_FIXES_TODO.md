# Deploy-WebDashboard.ps1 - Fixes Required

## Summary
The `Deploy-WebDashboard.ps1` script needs the same fixes that were applied to `Deploy-ApiServer.ps1`.

## Fixes to Apply

### 1. UTF-8 Encoding (? APPLIED)
- Added `[Console]::OutputEncoding` and `$OutputEncoding` to UTF-8
- **Status**: APPLIED

### 2. Guard Variable (? APPLIED)
- Added `$global:DeployWebDashboardRunning` to prevent re-execution
- **Status**: APPLIED

### 3. Unicode Icons (?? IN PROGRESS)
Need to replace all `?` icons with proper Unicode:
- `?` for success (? DONE in Write-Success)
- `?` for errors  
- `?` for warnings

**Locations to fix**:
- Line 98: `? IIS is not installed` ? `? IIS is not installed`
- Line 117: `? Using IISAdministration` ? `? Using IISAdministration`
- Line 120: `? WebAdministration or IISAdministration module not found` ? `? ...`
- Line 182: `? ASP.NET Core Module V2 not found` ? `? ...`
- All other `?` occurrences for errors/warnings

### 4. IIS PSDrive Detection (?? NEEDED)
Add proper detection for IIS: PSDrive in functions:
- `New-ApplicationPool`
- `New-IisWebsite`
- `Set-WebConfiguration`
- Similar to Deploy-ApiServer.ps1 implementation

### 5. Infinite Loop Protection (?? NEEDED)
Add to `Start-WebSite` function:
```powershell
if ($script:WebSiteStartAttempted) {
    Write-Host "? Start-WebSite already attempted. Skipping to prevent loop." -ForegroundColor Red
    return
}
$script:WebSiteStartAttempted = $true
```

### 6. App Pool Stopping Improvements (?? NEEDED)
In `Copy-WebFiles` function, add:
- Sleep delay after stopping (500ms)
- Better error handling
- State checking

### 7. Finally Block (?? NEEDED)
Add to main try-catch:
```powershell
} finally {
    # Ensure guard is always cleared
    $global:DeployWebDashboardRunning = $false
}
```

## Implementation Plan

### Phase 1: Quick Fixes (Done)
- ? UTF-8 encoding
- ? Guard variable
- ? Write-Success icon

### Phase 2: Icon Updates (Next)
- Replace all `?` with `?` or `?` based on context

### Phase 3: IIS PSDrive Detection
- Update all IIS-related functions
- Add Has-Command helper if missing
- Add fallback logic

### Phase 4: Loop Prevention
- Add guards to Start-WebSite
- Add state checking to start operations
- Improve app pool stopping

### Phase 5: Finalization
- Add finally block
- Test script
- Create documentation

## Comparison with Deploy-ApiServer.ps1

| Feature | Deploy-ApiServer.ps1 | Deploy-WebDashboard.ps1 | Status |
|---------|---------------------|------------------------|--------|
| UTF-8 Encoding | ? v1.3 | ? Applied | Done |
| Guard Variable | ? v1.2 | ? Applied | Done |
| Unicode Icons | ? All updated | ?? Partial | In Progress |
| IIS PSDrive Detection | ? All functions | ? Missing | Needed |
| Loop Protection | ? v1.2 | ? Missing | Needed |
| App Pool Improvements | ? v1.2 | ? Missing | Needed |
| Finally Block | ? v1.2 | ? Missing | Needed |

## Next Steps

1. Complete icon updates throughout the file
2. Add Has-Command function
3. Update New-ApplicationPool with PSDrive detection
4. Update New-IisWebsite with PSDrive detection  
5. Update Set-WebConfiguration with PSDrive detection
6. Add loop protection to Start-WebSite
7. Improve Copy-WebFiles
8. Add finally block
9. Test deployment
10. Create documentation

## Files to Reference

- `scripts/Deploy-ApiServer.ps1` - Source of all fixes
- `DEPLOY_APISERVER_INFINITE_LOOP_FIX.md` - Loop fix documentation
- `DEPLOY_APISERVER_UTF8_ENCODING_FIX.md` - UTF-8 fix documentation

## Target Version

Deploy-WebDashboard.ps1 v1.3 (matching Deploy-ApiServer.ps1 v1.3)

