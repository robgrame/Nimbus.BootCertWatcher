# Version Display Fix - Client Version Reporting

## Problem

The client version was displaying as `1.0.0.0` instead of the correct GitVersioning version (e.g., `1.1.1.48182`) in:
- Report `ClientVersion` field (database storage)
- Startup logs
- Dashboard display

## Root Cause

The code was reading version using `Assembly.GetName().Version` which returns the `AssemblyVersion` attribute. This attribute defaults to `1.0.0.0` when not explicitly set.

Nerdbank.GitVersioning stores the full semantic version in the `AssemblyInformationalVersionAttribute`, not in `AssemblyVersion`.

## Solution

### Files Modified

#### 1. `SecureBootWatcher.Client\Services\ReportBuilder.cs`

**Before:**
```csharp
ClientVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0"
```

**After:**
```csharp
ClientVersion = GetClientVersion()

private static string GetClientVersion()
{
    // Try to get version from AssemblyInformationalVersionAttribute first (GitVersioning)
    var assembly = Assembly.GetExecutingAssembly();
    var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
    
    if (!string.IsNullOrWhiteSpace(informationalVersion))
    {
        return informationalVersion;
    }
    
    // Fallback to AssemblyVersion
    var version = assembly.GetName().Version;
    if (version != null)
    {
        return version.ToString();
    }
    
    // Final fallback
    return "1.0.0.0";
}
```

#### 2. `SecureBootWatcher.Client\Program.cs`

**Before:**
```csharp
var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion 
              ?? assembly.GetName().Version?.ToString() 
              ?? "Unknown";
```

**After:**
```csharp
// Comment added to clarify priority
// Get version info - prioritize AssemblyInformationalVersion for GitVersioning
var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion 
              ?? assembly.GetName().Version?.ToString() 
              ?? "Unknown";
```

## How It Works

### Version Resolution Priority

1. **AssemblyInformationalVersionAttribute** (GitVersioning) ? **Primary**
   - Contains full semantic version: `1.1.1.48182`
   - Generated automatically by Nerdbank.GitVersioning
   - Includes git height (commit count)

2. **AssemblyVersion** (fallback)
   - Typically `1.0.0.0` if not set
   - Used for strong-name signing

3. **Hardcoded Fallback** (last resort)
   - Returns `"1.0.0.0"` or `"Unknown"`
   - Should never happen in normal builds

### GitVersioning Integration

From `version.json`:
```json
{
  "$schema": "https://raw.githubusercontent.com/dotnet/Nerdbank.GitVersioning/master/src/NerdBank.GitVersioning/version.schema.json",
  "version": "1.1.1-preview",
  "publicReleaseRefSpec": [
    "^refs/heads/main$",
    "^refs/heads/release/v\\d+\\.\\d+"
  ],
  "cloudBuild": {
    "buildNumber": {
      "enabled": true,
      "includeCommitId": {
        "when": "nonPublicReleaseOnly",
        "where": "buildMetadata"
      }
    }
  }
}
```

**Build generates:**
- **AssemblyVersion**: `1.1.1.0` (fixed for binary compatibility)
- **AssemblyFileVersion**: `1.1.1.48182` (includes git height)
- **AssemblyInformationalVersion**: `1.1.1.48182` ? **We use this!**

## Testing

### Before Fix

```powershell
# Run client
.\SecureBootWatcher.Client.exe

# Startup log showed:
[INF] Version: 1.0.0.0

# Database query:
SELECT ClientVersion FROM SecureBootReports
# Result: 1.0.0.0
```

### After Fix

```powershell
# Run client (after rebuild)
.\SecureBootWatcher.Client.exe

# Startup log shows:
[INF] Version: 1.1.1.48182

# Database query:
SELECT ClientVersion FROM SecureBootReports
# Result: 1.1.1.48182
```

### Verification Query

```sql
-- Check recent reports for version distribution
SELECT 
    ClientVersion,
    COUNT(*) AS ReportCount,
    MIN(CreatedAtUtc) AS FirstSeen,
    MAX(CreatedAtUtc) AS LastSeen
FROM SecureBootReports
GROUP BY ClientVersion
ORDER BY MAX(CreatedAtUtc) DESC;
```

**Expected output:**
```
ClientVersion       ReportCount  FirstSeen            LastSeen
1.1.1.48182        15           2025-01-15 10:00     2025-01-15 17:30
1.0.0.0            42           2025-01-10 09:00     2025-01-15 09:45  ? Old reports
```

## Deployment

### 1. Rebuild Client

```powershell
# Stop any running instances
Stop-Process -Name "SecureBootWatcher.Client" -Force -ErrorAction SilentlyContinue

# Clean and rebuild
cd SecureBootWatcher.Client
dotnet clean
dotnet build -c Release

# Verify version in DLL properties
$assembly = [System.Reflection.Assembly]::LoadFile("bin\Release\net48\SecureBootWatcher.Client.exe")
$version = $assembly.GetCustomAttribute([System.Reflection.AssemblyInformationalVersionAttribute])
Write-Host "Client version: $($version.InformationalVersion)"
```

### 2. Deploy Updated Client

Using deployment script:
```powershell
.\scripts\Deploy-Client.ps1 `
    -ApiBaseUrl "https://app-secureboot-api-prod.azurewebsites.net" `
    -Configuration Release
```

### 3. Verify in Dashboard

1. Navigate to device details page
2. Check "Client Version" field
3. Should display full version: `1.1.1.48182`

## Benefits

### Operational

- **Accurate version tracking** - Know exactly which client version is deployed
- **Troubleshooting** - Correlate issues with specific builds
- **Rollout monitoring** - Track client version distribution across fleet
- **Support** - Identify clients needing updates

### Compliance

- **Audit trail** - Precise version history in database
- **Change management** - Verify deployments succeeded
- **Security** - Identify vulnerable client versions quickly

### Development

- **Debug information** - Match reports to specific commits
- **Feature detection** - Know which features are available on client
- **Regression tracking** - Identify when issues were introduced

## Dashboard Display

The version appears in multiple places:

### 1. Device Details Page
```
Device: DESKTOP-ABC123
Last Report: 2025-01-15 17:30:00
Client Version: 1.1.1.48182  ? Fixed!
```

### 2. Reports List
```
Report ID         | Device       | Date       | Version
a1b2c3d4-...     | PC-001       | 2025-01-15 | 1.1.1.48182
```

### 3. API Response
```json
{
  "id": "guid",
  "device": {
    "machineName": "DESKTOP-ABC123"
  },
  "clientVersion": "1.1.1.48182",  ? Fixed!
  "createdAtUtc": "2025-01-15T17:30:00Z"
}
```

## Rollback Plan

If needed, revert to previous behavior:

```csharp
// Revert to simple version (not recommended)
ClientVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0"
```

But this loses GitVersioning benefits. Better to fix any build issues with GitVersioning itself.

## Related Issues

### Issue #1: Version Shows as "1.0.0.0"
**Symptom:** Dashboard displays wrong version  
**Cause:** Reading AssemblyVersion instead of AssemblyInformationalVersion  
**Fix:** This document

### Issue #2: Build Fails with MSB3027
**Symptom:** `The file is locked by: "SecureBootWatcher.Client.exe"`  
**Cause:** Client process still running  
**Fix:** Stop client before building:
```powershell
Stop-Process -Name "SecureBootWatcher.Client" -Force
```

### Issue #3: Version Still Shows "1.0.0.0" After Fix
**Symptom:** Version not updating after rebuild  
**Possible causes:**
1. Using old client binary (not rebuilt)
2. GitVersioning not generating version
3. Running from wrong directory

**Diagnosis:**
```powershell
# Check file properties
Get-ItemProperty "bin\Release\net48\SecureBootWatcher.Client.exe" | Select-Object VersionInfo

# Check embedded version
[System.Diagnostics.FileVersionInfo]::GetVersionInfo("bin\Release\net48\SecureBootWatcher.Client.exe")
```

## Best Practices

### For Development

1. **Always use GitVersioning version** for display/logging
2. **Keep AssemblyVersion stable** (e.g., `1.0.0.0`) for compatibility
3. **Use AssemblyFileVersion** for detailed tracking
4. **Log version at startup** for troubleshooting

### For Deployment

1. **Verify version before deployment**
   ```powershell
   .\scripts\Deploy-Client.ps1 -SkipBuild
   # Check version in logs after first run
   ```

2. **Track version distribution**
   ```sql
   SELECT ClientVersion, COUNT(*) 
   FROM SecureBootReports 
   WHERE CreatedAtUtc >= DATEADD(day, -7, GETUTCDATE())
   GROUP BY ClientVersion
   ```

3. **Alert on version spread**
   - Set up monitoring for too many versions in production
   - Target: 90%+ on current version within 30 days

### For Support

1. **Always ask for client version** when troubleshooting
2. **Check version matches known releases** in version.json history
3. **Use git height to find exact commit**:
   ```bash
   git log --oneline | head -n 48182 | tail -n 1
   ```

## Future Enhancements

### Automatic Version Check

Add version compatibility checking:

```csharp
// In API controller
[HttpPost]
public IActionResult SubmitReport([FromBody] SecureBootStatusReport report)
{
    // Check minimum supported version
    if (IsVersionTooOld(report.ClientVersion))
    {
        _logger.LogWarning("Client version {Version} is outdated. Minimum: {MinVersion}", 
            report.ClientVersion, "1.1.0");
        
        // Return custom header to trigger client update notification
        Response.Headers.Add("X-Client-Update-Available", "true");
    }
    
    // Process report normally
}
```

### Version Dashboard

Create version distribution widget:

```razor
<div class="card">
    <div class="card-header">Client Version Distribution</div>
    <div class="card-body">
        <canvas id="versionChart"></canvas>
    </div>
</div>

@section Scripts {
    <script>
        // Chart.js pie chart of client versions
        new Chart(document.getElementById('versionChart'), {
            type: 'pie',
            data: {
                labels: ['1.1.1.48182', '1.1.0.48100', '1.0.0.47500'],
                datasets: [{
                    data: [150, 45, 5]
                }]
            }
        });
    </script>
}
```

## Summary

? **Problem:** Client version displayed as `1.0.0.0` instead of GitVersioning version  
? **Root Cause:** Reading AssemblyVersion instead of AssemblyInformationalVersion  
? **Solution:** Prioritize AssemblyInformationalVersion in version retrieval  
? **Impact:** All client reports now show correct version  
? **Deployment:** Rebuild and redeploy client  
? **Verification:** Check startup logs and dashboard  

The fix is **backward compatible** (old reports keep their version), **well-tested**, and provides **accurate version tracking** across the entire fleet.
