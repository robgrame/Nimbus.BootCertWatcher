# Client Version Publishing - Quick Reference Guide

## Overview

This guide explains how to publish a new version of the SecureBootWatcher client and make it available for deployment.

## Prerequisites

- PowerShell 5.1+
- .NET SDK installed
- (Optional) Azure CLI for uploading to Azure Storage
- (Optional) Access to Azure Storage Account

## Publishing Workflow

```
???????????????????????????????????????????????????????????????
? 1. Update Version ? 2. Build ? 3. Package ? 4. Publish     ?
?                                                             ?
? Local Build         Create ZIP    Upload to Azure          ?
? .csproj update     + Checksum     Update API Config        ?
???????????????????????????????????????????????????????????????
```

## Step-by-Step Instructions

### Option A: Publish Locally (No Azure)

```powershell
# 1. Navigate to scripts directory
cd scripts

# 2. Run publish script
.\Publish-ClientVersion.ps1 -Version "1.2.0"

# 3. Package will be created in: .\release\1.2.0\
# 4. Manually update API appsettings.json (see below)
```

### Option B: Publish to Azure Storage

```powershell
# 1. Ensure you're logged into Azure CLI
az login

# 2. Run publish script with Azure upload
.\Publish-ClientVersion.ps1 `
    -Version "1.2.0" `
    -UploadToAzure `
    -AzureStorageAccount "yourstorageaccount" `
    -AzureContainer "client-packages" `
    -UpdateApiConfig

# 3. Script will:
#    - Build and package client
#    - Upload to Azure Blob Storage
#    - Update API appsettings.json automatically
```

### Option C: Full Automated Release

```powershell
# Complete release with all options
.\Publish-ClientVersion.ps1 `
    -Version "1.2.0" `
    -Configuration "Release" `
    -OutputPath ".\release" `
    -UploadToAzure `
    -AzureStorageAccount "secbootcert" `
    -AzureContainer "client-packages" `
    -UpdateApiConfig
```

## Manual Configuration Update

If you don't use `-UpdateApiConfig`, manually update `SecureBootDashboard.Api/appsettings.json`:

```json
{
  "ClientUpdate": {
    "LatestVersion": "1.2.0.0",
    "ReleaseDate": "2025-01-15T10:00:00Z",
    "DownloadUrl": "https://yourstorageaccount.blob.core.windows.net/client-packages/SecureBootWatcher-Client-1.2.0.zip",
    "MinimumVersion": "1.0.0.0",
    "IsUpdateRequired": false,
    "ReleaseNotes": "Bug fixes and performance improvements",
    "Checksum": "ABC123DEF456...",
    "FileSize": 1048576
  }
}
```

### Configuration Fields

| Field | Description | Example |
|-------|-------------|---------|
| `LatestVersion` | New version number (X.Y.Z.0) | `"1.2.0.0"` |
| `ReleaseDate` | Release date in ISO 8601 format | `"2025-01-15T10:00:00Z"` |
| `DownloadUrl` | Full URL to download package | Azure Blob Storage URL |
| `MinimumVersion` | Minimum supported version | `"1.0.0.0"` |
| `IsUpdateRequired` | Force update flag | `false` |
| `ReleaseNotes` | Human-readable notes | `"Bug fixes"` |
| `Checksum` | SHA256 hash of package | From `.sha256` file |
| `FileSize` | Package size in bytes | From package properties |

## Azure Storage Setup

### Create Storage Account (One-time)

```powershell
# 1. Create storage account
az storage account create `
    --name secbootcert `
    --resource-group YourResourceGroup `
    --location westeurope `
    --sku Standard_LRS

# 2. Create container
az storage container create `
    --name client-packages `
    --account-name secbootcert `
    --auth-mode login

# 3. (Optional) Make container publicly accessible for downloads
az storage container set-permission `
    --name client-packages `
    --account-name secbootcert `
    --public-access blob
```

### Configure RBAC (Recommended)

```powershell
# Grant yourself Storage Blob Data Contributor role
az role assignment create `
    --role "Storage Blob Data Contributor" `
    --assignee your@email.com `
    --scope /subscriptions/{subscription-id}/resourceGroups/{rg}/providers/Microsoft.Storage/storageAccounts/secbootcert
```

## Version Numbering

### Semantic Versioning

Use [Semantic Versioning](https://semver.org/): `MAJOR.MINOR.PATCH`

- **MAJOR**: Incompatible API changes
- **MINOR**: New functionality (backward compatible)
- **PATCH**: Bug fixes (backward compatible)

### Examples

| Version | Description |
|---------|-------------|
| `1.0.0` | Initial release |
| `1.0.1` | Bug fix release |
| `1.1.0` | New features added |
| `2.0.0` | Breaking changes |

### Version in .csproj

The script updates these properties:

```xml
<Version>1.2.0</Version>
<AssemblyVersion>1.2.0.0</AssemblyVersion>
<FileVersion>1.2.0.0</FileVersion>
<InformationalVersion>1.2.0</InformationalVersion>
```

## Testing Before Release

### 1. Test Build Locally

```powershell
# Build without publishing
dotnet build SecureBootWatcher.Client -c Release
```

### 2. Test Published Package

```powershell
# After running Publish-ClientVersion.ps1
cd .\release\1.2.0\

# Extract package
Expand-Archive SecureBootWatcher-Client-1.2.0.zip -DestinationPath .\test

# Run client
cd test
.\SecureBootWatcher.Client.exe

# Verify version in logs
```

### 3. Test Update Check

```powershell
# Update API config with new version
# Run old client
# Verify alert appears: "Client update available: Version 1.2.0"
```

## Deployment After Publishing

### Option 1: Intune Deployment

See: [INTUNE_WIN32_DEPLOYMENT.md](INTUNE_WIN32_DEPLOYMENT.md)

```powershell
# 1. Prepare Intune package
.\scripts\Prepare-IntunePackage.ps1 -Version "1.2.0"

# 2. Upload to Intune portal
# 3. Assign to device groups
# 4. Monitor deployment
```

### Option 2: GPO Deployment

```powershell
# 1. Copy package to NETLOGON share
Copy-Item .\release\1.2.0\SecureBootWatcher-Client-1.2.0.zip `
    -Destination \\domain\NETLOGON\SecureBootWatcher\

# 2. Create GPO startup script
# 3. Link to OU
```

### Option 3: SCCM Deployment

1. Create application in SCCM
2. Import package
3. Distribute to distribution points
4. Deploy to collection

## Rollback Procedure

If new version has issues:

### 1. Revert API Configuration

```json
{
  "ClientUpdate": {
    "LatestVersion": "1.1.0.0",  // Previous version
    "MinimumVersion": "1.0.0.0"
  }
}
```

### 2. Stop Auto-Updates (if enabled)

```json
{
  "SecureBootWatcher": {
    "ClientUpdate": {
      "CheckForUpdates": false  // Temporarily disable
    }
  }
}
```

### 3. Redeploy Previous Version

Use Intune/GPO to push previous version.

## Monitoring Adoption

### Dashboard View

Navigate to: `/ClientVersions`

- View devices by version
- Track adoption percentage
- Identify stragglers

### SQL Query

```sql
SELECT 
    ClientVersion,
    COUNT(*) AS DeviceCount,
    CAST(COUNT(*) * 100.0 / SUM(COUNT(*)) OVER () AS DECIMAL(5,2)) AS Percentage
FROM Devices
WHERE ClientVersion IS NOT NULL
GROUP BY ClientVersion
ORDER BY ClientVersion DESC;
```

### PowerShell Query

```powershell
# Query API
$apiUrl = "https://your-api.azurewebsites.net"
$response = Invoke-RestMethod -Uri "$apiUrl/api/Devices"

$response | Group-Object ClientVersion | 
    Select-Object Name, Count | 
    Sort-Object Count -Descending
```

## Troubleshooting

### Issue: Build Fails

**Cause:** Missing dependencies or syntax errors

**Fix:**
```powershell
# Clean and rebuild
dotnet clean SecureBootWatcher.Client
dotnet restore SecureBootWatcher.Client
dotnet build SecureBootWatcher.Client -c Release
```

### Issue: Azure Upload Fails

**Cause:** Not logged in or insufficient permissions

**Fix:**
```powershell
# Re-login to Azure
az login

# Verify access
az storage blob list `
    --account-name secbootcert `
    --container-name client-packages `
    --auth-mode login
```

### Issue: Checksum Mismatch

**Cause:** File corrupted during transfer

**Fix:**
```powershell
# Recalculate checksum
$hash = Get-FileHash .\SecureBootWatcher-Client-1.2.0.zip -Algorithm SHA256
Write-Host $hash.Hash

# Update API config with correct checksum
```

### Issue: Version Not Detected by Clients

**Cause:** API configuration not updated or API not restarted

**Fix:**
```powershell
# Verify API config
Invoke-RestMethod -Uri "https://your-api.azurewebsites.net/api/ClientUpdate/version"

# Restart API if needed (IIS)
iisreset

# Or restart App Service
az webapp restart --name YourAppService --resource-group YourResourceGroup
```

## Best Practices

### ? DO

- **Test locally** before publishing
- **Use semantic versioning** consistently
- **Provide checksums** for integrity verification
- **Document release notes** for each version
- **Test in dev/test** environment first
- **Monitor adoption** via dashboard
- **Keep old versions** available for rollback
- **Use HTTPS** for download URLs

### ? DON'T

- **Skip testing** before release
- **Change version** without updating API config
- **Delete old packages** immediately
- **Force updates** without testing
- **Use HTTP** for downloads
- **Forget to update** release notes

## Release Checklist

- [ ] Update version in `.csproj`
- [ ] Update release notes
- [ ] Build and test locally
- [ ] Run `Publish-ClientVersion.ps1`
- [ ] Verify package created
- [ ] Upload to Azure Storage (or copy to share)
- [ ] Update API `appsettings.json`
- [ ] Restart API
- [ ] Test version check API
- [ ] Deploy to test devices
- [ ] Monitor for errors
- [ ] Deploy to production
- [ ] Monitor adoption in dashboard
- [ ] Document known issues

## Quick Commands Reference

```powershell
# Publish new version (local only)
.\scripts\Publish-ClientVersion.ps1 -Version "1.2.0"

# Publish to Azure with auto-config
.\scripts\Publish-ClientVersion.ps1 `
    -Version "1.2.0" `
    -UploadToAzure `
    -AzureStorageAccount "secbootcert" `
    -UpdateApiConfig

# Check current version
(Get-Item .\SecureBootWatcher.Client\bin\Release\net48\SecureBootWatcher.Client.exe).VersionInfo.FileVersion

# Calculate checksum
Get-FileHash .\release\1.2.0\SecureBootWatcher-Client-1.2.0.zip -Algorithm SHA256

# Test version check API
Invoke-RestMethod -Uri "https://your-api.azurewebsites.net/api/ClientUpdate/version"

# View version distribution
Invoke-RestMethod -Uri "https://your-api.azurewebsites.net/api/Devices" | 
    Group-Object ClientVersion | 
    Select-Object Name, Count
```

## Support

For issues or questions:
- Review this guide
- Check [CLIENT_VERSION_TRACKING.md](CLIENT_VERSION_TRACKING.md)
- Check [INTUNE_WIN32_DEPLOYMENT.md](INTUNE_WIN32_DEPLOYMENT.md)
- Review API logs
- Check dashboard `/ClientVersions` page

---

**Version:** 1.0  
**Last Updated:** 2025-01-09  
**Related Docs:**
- [CLIENT_VERSION_TRACKING.md](CLIENT_VERSION_TRACKING.md)
- [CLIENT_VERSIONS_DASHBOARD.md](CLIENT_VERSIONS_DASHBOARD.md)
- [INTUNE_WIN32_DEPLOYMENT.md](INTUNE_WIN32_DEPLOYMENT.md)
