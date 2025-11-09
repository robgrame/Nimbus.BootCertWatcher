# Example: Publishing Version 1.2.0

## Scenario

You've fixed a bug and want to release version 1.2.0 of the SecureBootWatcher client.

## Step-by-Step Example

### 1. Prepare for Release

```powershell
# Navigate to project root
cd C:\Projects\SecureBootWatcher

# Ensure you're on the correct branch
git checkout main
git pull origin main

# Create a release branch (optional)
git checkout -b release/1.2.0
```

### 2. Run the Publish Script

```powershell
# Option A: Local build only (no Azure upload)
.\scripts\Publish-ClientVersion.ps1 -Version "1.2.0"

# Option B: Full automated release with Azure upload
.\scripts\Publish-ClientVersion.ps1 `
    -Version "1.2.0" `
    -UploadToAzure `
    -AzureStorageAccount "secbootcert" `
    -AzureContainer "client-packages" `
    -UpdateApiConfig
```

### 3. Expected Output

```
========================================
  SecureBootWatcher Client Publisher
========================================

Version: 1.2.0
Configuration: Release

[1/6] Updating project version...
  Project version updated to: 1.2.0

[2/6] Building client...
  Build successful

[3/6] Publishing client...
  Publish successful
     Output: C:\Projects\SecureBootWatcher\SecureBootWatcher.Client\bin\Release\net48\win-x86\publish

[4/6] Creating package...
  Package created
     Path: .\release\1.2.0\SecureBootWatcher-Client-1.2.0.zip
     Size: 2.45 MB

[5/6] Calculating checksum...
  SHA256: E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855
     Saved to: .\release\1.2.0\SecureBootWatcher-Client-1.2.0.sha256

[6/6] Uploading to Azure Blob Storage...
  Uploading package...
  Uploading checksum...
  Upload successful
     URL: https://secbootcert.blob.core.windows.net/client-packages/SecureBootWatcher-Client-1.2.0.zip

[BONUS] Updating API configuration...
  API configuration updated
     Latest Version: 1.2.0.0
     Release Date: 2025-01-15T10:30:00Z

========================================
  Release Summary
========================================

Version: 1.2.0 (1.2.0.0)
Package: .\release\1.2.0\SecureBootWatcher-Client-1.2.0.zip
Checksum: E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855

? Package published to Azure Storage
  URL: https://secbootcert.blob.core.windows.net/client-packages/SecureBootWatcher-Client-1.2.0.zip

Next Steps:
1. Test the package locally
2. Update API configuration if not done automatically
3. Deploy to Intune/GPO/SCCM
4. Monitor dashboard for version adoption

Done! ??
```

### 4. Verify the Package

```powershell
# Navigate to release folder
cd .\release\1.2.0\

# List files
Get-ChildItem

# Expected output:
# SecureBootWatcher-Client-1.2.0.zip
# SecureBootWatcher-Client-1.2.0.sha256
```

### 5. Test the Package Locally

```powershell
# Extract to test folder
Expand-Archive SecureBootWatcher-Client-1.2.0.zip -DestinationPath .\test-1.2.0

# Navigate to extracted folder
cd .\test-1.2.0

# Run the client
.\SecureBootWatcher.Client.exe
```

**Expected log output:**
```
[10:30:00 INF] ========================================
[10:30:00 INF] SecureBootWatcher Client Starting
[10:30:00 INF] ========================================
[10:30:00 INF] Version: 1.2.0
...
```

### 6. Verify API Configuration

```powershell
# Check API configuration was updated
$apiUrl = "https://your-api.azurewebsites.net"
$versionInfo = Invoke-RestMethod -Uri "$apiUrl/api/ClientUpdate/version"

# Display version info
$versionInfo | Format-List

# Expected output:
# latestVersion       : 1.2.0.0
# releaseDate         : 2025-01-15T10:30:00Z
# downloadUrl         : https://secbootcert.blob.core.windows.net/client-packages/SecureBootWatcher-Client-1.2.0.zip
# isUpdateRequired    : False
# minimumVersion      : 1.0.0.0
# releaseNotes        : Bug fixes and performance improvements
# checksum            : E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855
# fileSize            : 2571264
```

### 7. Test Update Detection

```powershell
# Run an old client (version 1.1.0)
cd C:\Program Files\SecureBootWatcher
.\SecureBootWatcher.Client.exe

# Check logs for update alert
Get-Content "C:\ProgramData\SecureBootWatcher\logs\client-*.log" | Select-String "update"
```

**Expected log entry:**
```
[10:35:00 WRN] Update available: Version 1.2.0.0 (current: 1.1.0.0)
```

### 8. Deploy to Test Devices

```powershell
# Option A: Manual copy to test device
Copy-Item .\release\1.2.0\SecureBootWatcher-Client-1.2.0.zip `
    -Destination \\TEST-PC\C$\Temp\

# On TEST-PC:
Expand-Archive C:\Temp\SecureBootWatcher-Client-1.2.0.zip -DestinationPath "C:\Program Files\SecureBootWatcher"
```

### 9. Monitor Dashboard

Navigate to: `https://your-dashboard.com/ClientVersions`

**Before deployment:**
```
???????????????????????????????????????
? Version 1.1.0 [Outdated] (150 dev) ?
? Version 1.0.0 [Unsupported] (5 dev)?
???????????????????????????????????????
```

**After test deployment:**
```
???????????????????????????????????????
? Version 1.2.0 [Up-to-Date] (1 dev) ?
? Version 1.1.0 [Outdated] (149 dev) ?
? Version 1.0.0 [Unsupported] (5 dev)?
???????????????????????????????????????
```

### 10. Deploy to Production

#### Via Intune:

```powershell
# 1. Prepare Intune package
cd scripts
.\Prepare-IntunePackage.ps1 -PackageZipPath "..\release\1.2.0\SecureBootWatcher-Client-1.2.0.zip"

# 2. Upload to Intune via portal
# 3. Assign to production device groups
# 4. Monitor deployment status
```

#### Via GPO:

```powershell
# 1. Copy to NETLOGON
Copy-Item .\release\1.2.0\SecureBootWatcher-Client-1.2.0.zip `
    -Destination "\\domain.com\NETLOGON\SecureBootWatcher\"

# 2. Update GPO startup script
# 3. Wait for next group policy refresh
```

### 11. Monitor Adoption

```powershell
# Check version distribution via API
$devices = Invoke-RestMethod -Uri "$apiUrl/api/Devices"
$versionStats = $devices | Group-Object ClientVersion | 
    Select-Object @{N='Version';E={$_.Name}}, Count | 
    Sort-Object Count -Descending

$versionStats | Format-Table

# Output:
# Version  Count
# -------  -----
# 1.2.0.0    120
# 1.1.0.0     28
# 1.0.0.0      2
```

### 12. Complete Release

```powershell
# 1. Merge release branch
git checkout main
git merge release/1.2.0

# 2. Tag the release
git tag -a v1.2.0 -m "Release version 1.2.0 - Bug fixes"
git push origin v1.2.0

# 3. Update CHANGELOG
echo "## [1.2.0] - 2025-01-15" >> CHANGELOG.md
echo "### Fixed" >> CHANGELOG.md
echo "- Bug fix XYZ" >> CHANGELOG.md

git commit -am "Update CHANGELOG for v1.2.0"
git push origin main

# 4. Create GitHub release (optional)
gh release create v1.2.0 `
    .\release\1.2.0\SecureBootWatcher-Client-1.2.0.zip `
    --title "Release v1.2.0" `
    --notes "Bug fixes and improvements"
```

## Files Created

After running the script, you'll have:

```
release/
??? 1.2.0/
    ??? SecureBootWatcher-Client-1.2.0.zip      (Package)
    ??? SecureBootWatcher-Client-1.2.0.sha256   (Checksum)
```

## API Configuration Updated

The `SecureBootDashboard.Api/appsettings.json` will contain:

```json
{
  "ClientUpdate": {
    "LatestVersion": "1.2.0.0",
    "ReleaseDate": "2025-01-15T10:30:00Z",
    "DownloadUrl": "https://secbootcert.blob.core.windows.net/client-packages/SecureBootWatcher-Client-1.2.0.zip",
    "MinimumVersion": "1.0.0.0",
    "IsUpdateRequired": false,
    "ReleaseNotes": "Bug fixes and performance improvements",
    "Checksum": "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855",
    "FileSize": 2571264
  }
}
```

## Timeline

| Time | Action | Status |
|------|--------|--------|
| 10:00 | Start release process | ? |
| 10:05 | Build and package complete | ? |
| 10:10 | Upload to Azure Storage | ? |
| 10:15 | API config updated | ? |
| 10:20 | Test on 1 device | ? |
| 10:30 | Deploy to pilot group (10 devices) | ? |
| 11:00 | Monitor pilot for issues | ? |
| 14:00 | Deploy to production (150 devices) | ? |
| Day 2 | 80% adoption | ? |
| Day 3 | 95% adoption | ? |
| Day 5 | 100% adoption | ? |

## Rollback Example (If Needed)

```powershell
# Revert API config to previous version
$apiConfigPath = "SecureBootDashboard.Api\appsettings.json"
$config = Get-Content $apiConfigPath -Raw | ConvertFrom-Json
$config.ClientUpdate.LatestVersion = "1.1.0.0"
$config.ClientUpdate.DownloadUrl = "https://secbootcert.blob.core.windows.net/client-packages/SecureBootWatcher-Client-1.1.0.zip"
$config | ConvertTo-Json -Depth 10 | Set-Content $apiConfigPath

# Restart API
iisreset

# Redeploy previous version via Intune
```

## Summary

? **Total Time:** ~15 minutes  
? **Manual Steps:** 3 (run script, test, deploy)  
? **Automated Steps:** 7 (build, package, upload, config, checksum, etc.)  
? **Deployment Time:** 2-3 days for full fleet  

---

**This example demonstrates the complete workflow from version bump to production deployment.**
