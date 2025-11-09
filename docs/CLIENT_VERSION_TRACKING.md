# Client Version Tracking & Update Notification Feature

## Overview

This feature adds **client version tracking** and **update notification** capabilities to the SecureBootWatcher solution. It enables IT teams to:

- Track which version of the client is deployed on each device
- Receive alerts when newer versions are available
- Optionally enable auto-download and auto-install of updates (opt-in)
- Maintain visibility of the fleet's update status via the dashboard

## Architecture

### Notification-First Approach (Recommended)

The default configuration uses a **notification-only** approach:

```
Client ? Check Version API ? Alert if Update Available ? Dashboard Shows Outdated Devices
```

IT teams retain full control over update deployment via Intune/GPO/SCCM.

### Optional Auto-Update (Opt-In)

If enabled, clients can:
1. **Auto-download**: Download update package to temp folder
2. **Auto-install**: Schedule update to run after current execution
3. **Rollback**: Automatically restore previous version if update fails

## Configuration

### API Configuration (`appsettings.json`)

```json
{
  "ClientUpdate": {
    "LatestVersion": "1.0.0.0",
    "ReleaseDate": "2025-01-08T00:00:00Z",
    "MinimumVersion": "1.0.0.0",
    "DownloadUrl": "https://yourstorageaccount.blob.core.windows.net/client-packages/SecureBootWatcher-Client-latest.zip",
    "IsUpdateRequired": false,
    "ReleaseNotes": "Initial release",
    "Checksum": "sha256-hash-here",
    "FileSize": 1024000,
    "PackagePath": ""
  }
}
```

**Configuration Fields:**

| Field | Description | Example |
|-------|-------------|---------|
| `LatestVersion` | Current latest version available | `"1.2.0.0"` |
| `ReleaseDate` | Release date of latest version | `"2025-01-15T10:00:00Z"` |
| `MinimumVersion` | Minimum supported version | `"1.0.0.0"` |
| `DownloadUrl` | URL to download update package | Azure Blob Storage URL |
| `IsUpdateRequired` | Force update flag | `false` |
| `ReleaseNotes` | Human-readable release notes | `"Bug fixes and improvements"` |
| `Checksum` | SHA256 hash for integrity validation | (optional) |
| `FileSize` | Package size in bytes | `1024000` |
| `PackagePath` | Local path for direct download | (optional) |

### Client Configuration (`appsettings.json`)

```json
{
  "SecureBootWatcher": {
    "ClientUpdate": {
      "CheckForUpdates": true,
      "AutoDownloadEnabled": false,
      "AutoInstallEnabled": false,
      "NotifyOnUpdateAvailable": true
    }
  }
}
```

**Configuration Fields:**

| Field | Description | Default | Recommendation |
|-------|-------------|---------|----------------|
| `CheckForUpdates` | Enable version checking | `true` | ? Keep enabled |
| `AutoDownloadEnabled` | Automatically download updates | `false` | ?? Use with caution |
| `AutoInstallEnabled` | Automatically install updates | `false` | ? Not recommended for production |
| `NotifyOnUpdateAvailable` | Add alert to report | `true` | ? Recommended |

## How It Works

### 1. Version Tracking

Every report now includes the client version:

```json
{
  "device": {
    "machineName": "DESKTOP-123",
    "clientVersion": "1.0.0.0"
  }
}
```

### 2. Update Check Flow

```
???????????????????
?  Client Starts  ?
???????????????????
         ?
         ?
???????????????????????????
? ReportBuilder.BuildAsync?
???????????????????????????
         ?
         ?
????????????????????????????????
? IClientUpdateService.Check   ?
? API/ClientUpdate/check?ver=  ?
????????????????????????????????
         ?
         ?
    ???????????
    ? Update  ?
    ?Available?
    ???????????
         ?
    ???????????????
    ?   Yes       ?   No
    ?             ?
???????????   ????????????
? Add     ?   ? Continue ?
? Alert   ?   ? Normally ?
???????????   ????????????
```

### 3. Alert Types

**Update Available (Informational):**
```
?? Client update available: Version 1.2.0 (current: 1.0.0)
```

**Update Required (Warning):**
```
?? CLIENT UPDATE REQUIRED: Version 1.2.0 is available (current: 1.0.0). Update is mandatory.
```

### 4. Auto-Download (Optional)

If `AutoDownloadEnabled = true`:

1. Client downloads package to `%TEMP%\SecureBootWatcher-Update`
2. Validates checksum (if provided)
3. Stores path for later installation

### 5. Auto-Install (Optional)

If `AutoInstallEnabled = true` (requires `AutoDownloadEnabled = true`):

1. Creates PowerShell update script
2. Creates one-time scheduled task to run 10 seconds after client exits
3. Task performs:
   - Waits for client process to exit
   - Backs up current version
   - Copies new files
   - Restarts scheduled task
   - Self-deletes

## API Endpoints

### GET /api/ClientUpdate/version

Returns latest version information.

**Response:**
```json
{
  "latestVersion": "1.2.0.0",
  "releaseDate": "2025-01-15T10:00:00Z",
  "downloadUrl": "https://...",
  "isUpdateRequired": false,
  "minimumVersion": "1.0.0.0",
  "releaseNotes": "Bug fixes",
  "checksum": "sha256-hash",
  "fileSize": 1024000
}
```

### GET /api/ClientUpdate/check?currentVersion=1.0.0

Checks if update is available for a specific version.

**Response:**
```json
{
  "currentVersion": "1.0.0.0",
  "latestVersion": "1.2.0.0",
  "updateAvailable": true,
  "updateRequired": false,
  "downloadUrl": "https://...",
  "releaseNotes": "Bug fixes"
}
```

### GET /api/ClientUpdate/download (Optional)

Downloads the latest client package directly from the API.

**Response:** `application/zip` file

## Database Changes

### Migration: AddClientVersionToDevice

Adds `ClientVersion` column to `Devices` table:

```sql
ALTER TABLE Devices ADD ClientVersion nvarchar(50) NULL;
```

**Apply Migration:**
```bash
dotnet ef database update --project SecureBootDashboard.Api
```

## Deployment Scenarios

### Scenario 1: Notification Only (Recommended)

**Configuration:**
```json
{
  "CheckForUpdates": true,
  "AutoDownloadEnabled": false,
  "AutoInstallEnabled": false,
  "NotifyOnUpdateAvailable": true
}
```

**Workflow:**
1. Client checks for updates on each run
2. Alert added to report if update available
3. Dashboard shows devices with outdated versions
4. IT deploys updates via Intune/GPO/SCCM

**Benefits:**
- ? Full IT control
- ? Tested deployment process
- ? Audit trail via Intune
- ? Rollback capabilities via Intune

### Scenario 2: Auto-Download (Advanced)

**Configuration:**
```json
{
  "CheckForUpdates": true,
  "AutoDownloadEnabled": true,
  "AutoInstallEnabled": false,
  "NotifyOnUpdateAvailable": true
}
```

**Workflow:**
1. Client checks for updates
2. If update available, downloads to temp folder
3. Alert indicates "Update downloaded, pending installation"
4. IT triggers installation via script or Intune

**Benefits:**
- ? Faster deployment (pre-downloaded)
- ? Reduced network load during deployment window
- ?? Requires manual installation trigger

### Scenario 3: Full Auto-Update (Not Recommended for Production)

**Configuration:**
```json
{
  "CheckForUpdates": true,
  "AutoDownloadEnabled": true,
  "AutoInstallEnabled": true,
  "NotifyOnUpdateAvailable": true
}
```

**Workflow:**
1. Client checks for updates
2. Downloads update
3. Schedules installation
4. Installs after current execution completes

**Risks:**
- ? No testing before rollout
- ? Potential for fleet-wide failures
- ? Loss of deployment control
- ? Compliance issues

**Use Cases:**
- Development/testing environments
- Small fleets (<10 devices)
- Hotfix deployment with known good package

## Publishing a New Version

### Step 1: Update API Configuration

Edit `appsettings.json`:

```json
{
  "ClientUpdate": {
    "LatestVersion": "1.2.0.0",
    "ReleaseDate": "2025-01-15T10:00:00Z",
    "DownloadUrl": "https://yourstorageaccount.blob.core.windows.net/client-packages/SecureBootWatcher-Client-1.2.0.zip",
    "ReleaseNotes": "Bug fixes and performance improvements"
  }
}
```

### Step 2: Upload Package to Azure Blob Storage

```powershell
# Build client
.\scripts\Deploy-Client.ps1 -Configuration Release

# Upload to Azure Blob Storage
$ctx = New-AzStorageContext -StorageAccountName "yourstorageaccount" -UseConnectedAccount
Set-AzStorageBlobContent `
    -File ".\client-package\SecureBootWatcher-Client.zip" `
    -Container "client-packages" `
    -Blob "SecureBootWatcher-Client-1.2.0.zip" `
    -Context $ctx
```

### Step 3: (Optional) Calculate Checksum

```powershell
$hash = Get-FileHash ".\client-package\SecureBootWatcher-Client.zip" -Algorithm SHA256
Write-Host "Checksum: $($hash.Hash)"
```

Add to `appsettings.json`:
```json
{
  "Checksum": "ABC123DEF456..."
}
```

### Step 4: Restart API

```bash
# If running in IIS
iisreset

# If running as service
Restart-Service SecureBootDashboardApi
```

## Dashboard Integration

### Viewing Outdated Devices

The dashboard can display devices with outdated client versions:

```sql
SELECT 
    MachineName,
    ClientVersion,
    LastSeenUtc,
    DATEDIFF(day, LastSeenUtc, GETUTCDATE()) AS DaysSinceLastSeen
FROM Devices
WHERE ClientVersion < '1.2.0.0'  -- Latest version
ORDER BY LastSeenUtc DESC;
```

### Example Dashboard View

| Machine Name | Client Version | Last Seen | Status |
|--------------|----------------|-----------|--------|
| DESKTOP-001  | 1.0.0.0        | 2 days ago | ?? Outdated |
| DESKTOP-002  | 1.2.0.0        | 1 hour ago | ? Up-to-date |
| DESKTOP-003  | 1.1.0.0        | 5 days ago | ?? Outdated |

## Security Considerations

### Checksum Validation

Always provide a SHA256 checksum for update packages:

```powershell
Get-FileHash "SecureBootWatcher-Client.zip" -Algorithm SHA256
```

Client verifies checksum before installation (if provided).

### HTTPS Only

The `DownloadUrl` MUST use HTTPS to prevent man-in-the-middle attacks:

```
? https://yourstorageaccount.blob.core.windows.net/...
? http://yourstorageaccount.blob.core.windows.net/...
```

### Package Signing

Consider signing the client package with Authenticode:

```powershell
Set-AuthenticodeSignature -FilePath "SecureBootWatcher.Client.exe" `
    -Certificate (Get-ChildItem Cert:\CurrentUser\My\THUMBPRINT)
```

### Access Control

Use Azure Blob Storage with:
- Private containers
- SAS tokens with expiration
- IP restrictions (if possible)

## Troubleshooting

### Issue: "Update check failed with status code: 404"

**Cause:** API endpoint not configured or not accessible.

**Fix:**
1. Verify `WebApi.BaseAddress` is correct in client config
2. Check API is running and accessible
3. Verify firewall rules

### Issue: "Download failed"

**Cause:** Invalid `DownloadUrl` or network issue.

**Fix:**
1. Verify URL is accessible from client
2. Check Azure Storage account permissions
3. Review client logs for detailed error

### Issue: "Update scheduled but not applied"

**Cause:** Scheduled task failed to run.

**Fix:**
1. Check Task Scheduler for `SecureBootWatcher-Update` task
2. Review `C:\ProgramData\SecureBootWatcher\update.log`
3. Verify SYSTEM account has permissions

### Issue: "Client version not appearing in dashboard"

**Cause:** Migration not applied or old client version.

**Fix:**
1. Apply migration: `dotnet ef database update`
2. Redeploy client with updated version
3. Wait for next report

## Best Practices

### ? DO

- Use notification-only mode for production
- Test updates in dev/test environments first
- Provide checksums for all packages
- Use HTTPS for download URLs
- Keep Azure Blob Storage private
- Monitor dashboard for outdated devices
- Document version changes in release notes

### ? DON'T

- Enable auto-install in production without testing
- Use HTTP for download URLs
- Skip checksum validation
- Deploy updates without testing
- Mix update channels (Intune + auto-update)
- Forget to update API configuration

## Migration from Manual Deployment

### Phase 1: Enable Tracking (Week 1)

```json
{
  "CheckForUpdates": true,
  "AutoDownloadEnabled": false,
  "AutoInstallEnabled": false,
  "NotifyOnUpdateAvailable": true
}
```

- Redeploy clients with version tracking
- Monitor dashboard for version distribution

### Phase 2: Test Notifications (Week 2)

- Publish test version to API
- Verify alerts appear in reports
- Confirm dashboard shows outdated devices

### Phase 3: Production Rollout (Week 3+)

- Continue using Intune/GPO for deployment
- Use dashboard to track deployment progress
- Alerts help identify devices that missed updates

## Summary

The Client Version Tracking feature provides:

? **Visibility**: Know which version is running on each device  
? **Alerting**: Automatic notification when updates are available  
? **Control**: IT retains full control over deployment  
? **Flexibility**: Opt-in auto-update for specific scenarios  
? **Safety**: Rollback capabilities and checksum validation  

**Recommended Configuration:** Notification-only mode with manual deployment via Intune.

**Not Recommended:** Auto-install in production without extensive testing.

---

**Version:** 1.0  
**Last Updated:** 2025-01-09  
**Related Documentation:**
- `INTUNE_WIN32_DEPLOYMENT.md`
- `DEPLOYMENT_GUIDE.md`
- `README.md`
