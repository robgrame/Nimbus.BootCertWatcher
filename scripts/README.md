# Scripts Directory - SecureBootWatcher

This directory contains PowerShell scripts for deploying, managing, and publishing the SecureBootWatcher client.

## ?? Scripts Overview

| Script | Purpose | Usage |
|--------|---------|-------|
| `Deploy-Client.ps1` | Build and deploy client locally | Development, manual deployment |
| `Publish-ClientVersion.ps1` | Build, package, and publish new version | **Release management** |
| `Install-Client-Intune.ps1` | Install client via Intune | Intune Win32 app deployment |
| `Uninstall-Client-Intune.ps1` | Uninstall client via Intune | Intune Win32 app removal |
| `Detect-Client-Intune.ps1` | Detect if client is installed | Intune detection rule |
| `Detect-Client.ps1` | Detection script for remediation | Intune remediation |
| `Remediate-Client.ps1` | Fix client issues | Intune remediation |

---

## ?? Quick Start

### Publishing a New Version

```powershell
# Full automated release
.\Publish-ClientVersion.ps1 `
    -Version "1.2.0" `
    -UploadToAzure `
    -AzureStorageAccount "secbootcert" `
    -UpdateApiConfig
```

### Local Deployment

```powershell
# Deploy to local machine with scheduled task
.\Deploy-Client.ps1 -CreateScheduledTask
```

### Intune Package Preparation

```powershell
# After publishing, prepare Intune package
# (Script to be created - see INTUNE_WIN32_DEPLOYMENT.md for manual steps)
```

---

## ?? Detailed Script Documentation

### Publish-ClientVersion.ps1

**Purpose:** Build, package, and publish a new client version

**Parameters:**
- `-Version` (Required): Version number (e.g., "1.2.0")
- `-OutputPath`: Output directory (default: ".\release")
- `-Configuration`: Build configuration (default: "Release")
- `-AzureStorageAccount`: Azure Storage account name
- `-AzureContainer`: Azure container name (default: "client-packages")
- `-UploadToAzure`: Upload package to Azure Blob Storage
- `-UpdateApiConfig`: Automatically update API appsettings.json

**Examples:**

```powershell
# Local build only
.\Publish-ClientVersion.ps1 -Version "1.2.0"

# Build and upload to Azure
.\Publish-ClientVersion.ps1 -Version "1.2.0" -UploadToAzure -AzureStorageAccount "secbootcert"

# Full release with API config update
.\Publish-ClientVersion.ps1 `
    -Version "1.2.0" `
    -UploadToAzure `
    -AzureStorageAccount "secbootcert" `
    -UpdateApiConfig
```

**Output:**
- `release\{Version}\SecureBootWatcher-Client-{Version}.zip`
- `release\{Version}\SecureBootWatcher-Client-{Version}.sha256`

---

### Deploy-Client.ps1

**Purpose:** Build and deploy client to local machine

**Parameters:**
- `-OutputPath`: Package output path
- `-ApiBaseUrl`: API base URL
- `-FleetId`: Fleet identifier
- `-Configuration`: Build configuration
- `-CreateScheduledTask`: Create Windows scheduled task
- `-InstallPath`: Installation directory
- `-TaskTime`: Scheduled task start time
- `-ScheduleType`: Task frequency (Once, Daily, Hourly, Custom)
- `-RepeatEveryHours`: Repeat interval for Custom schedule
- `-SkipBuild`: Use existing binaries
- `-PackageZipPath`: Use precompiled package

**Examples:**

```powershell
# Build and install with daily scheduled task
.\Deploy-Client.ps1 -CreateScheduledTask

# Install from precompiled package
.\Deploy-Client.ps1 `
    -PackageZipPath ".\release\1.2.0\SecureBootWatcher-Client-1.2.0.zip" `
    -CreateScheduledTask

# Custom schedule (every 4 hours)
.\Deploy-Client.ps1 `
    -CreateScheduledTask `
    -ScheduleType Custom `
    -RepeatEveryHours 4
```

---

### Install-Client-Intune.ps1

**Purpose:** Install client as Intune Win32 app

**Parameters:**
- `-ApiBaseUrl`: API base URL (optional)
- `-FleetId`: Fleet ID (optional)
- `-CertificatePassword`: PFX certificate password (optional)

**Usage in Intune:**

```powershell
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Install-Client-Intune.ps1"
```

**With parameters:**

```powershell
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Install-Client-Intune.ps1" -ApiBaseUrl "https://api.contoso.com" -FleetId "production" -CertificatePassword "YourPassword"
```

**Installation:**
- Installs to: `C:\Program Files\SecureBootWatcher`
- Imports certificate to: `Cert:\LocalMachine\My`
- Creates scheduled task: `SecureBootWatcher`
- Logs to: `C:\ProgramData\SecureBootWatcher\install.log`

---

### Uninstall-Client-Intune.ps1

**Purpose:** Uninstall client from Intune deployment

**Usage in Intune:**

```powershell
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Uninstall-Client-Intune.ps1"
```

**Actions:**
- Stops scheduled task
- Removes scheduled task
- Stops running processes
- Deletes installation directory
- Logs to: `C:\ProgramData\SecureBootWatcher\uninstall.log`

**Note:** Does NOT remove imported certificate (intentional for security)

---

### Detect-Client-Intune.ps1

**Purpose:** Detection script for Intune Win32 app

**Usage in Intune:**
- Detection method: PowerShell script
- Script: `Detect-Client-Intune.ps1`

**Detection logic:**
1. Checks installation directory exists
2. Checks executable exists
3. Checks appsettings.json exists
4. Checks scheduled task exists

**Output:**
- Success: "Installed" (exit code 0)
- Failure: No output (exit code 1)

---

### Detect-Client.ps1 & Remediate-Client.ps1

**Purpose:** Intune Proactive Remediation scripts

**Detect-Client.ps1:**
- Checks if client is installed correctly
- Checks if scheduled task is configured
- Checks if last execution was successful

**Remediate-Client.ps1:**
- Restarts scheduled task if stopped
- Recreates task if missing
- Fixes common configuration issues

**Usage in Intune:**
- Create remediation package
- Upload both scripts
- Schedule to run daily/weekly

---

## ?? Workflow Examples

### New Version Release

```powershell
# 1. Publish new version
.\Publish-ClientVersion.ps1 `
    -Version "1.2.0" `
    -UploadToAzure `
    -AzureStorageAccount "secbootcert" `
    -UpdateApiConfig

# 2. Test locally
.\Deploy-Client.ps1 `
    -PackageZipPath ".\release\1.2.0\SecureBootWatcher-Client-1.2.0.zip" `
    -CreateScheduledTask

# 3. Prepare Intune package (manual - see INTUNE_WIN32_DEPLOYMENT.md)
# 4. Upload to Intune
# 5. Deploy to pilot group
# 6. Monitor adoption via dashboard
# 7. Deploy to production
```

### Quick Local Test

```powershell
# Build and run once
.\Deploy-Client.ps1 -SkipBuild

# Check output
Get-Content "C:\ProgramData\SecureBootWatcher\logs\*.log" -Tail 50
```

### Troubleshooting Deployment

```powershell
# Check scheduled task
Get-ScheduledTask -TaskName "SecureBootWatcher" | Format-List *

# Check installation
Test-Path "C:\Program Files\SecureBootWatcher\SecureBootWatcher.Client.exe"

# Check logs
Get-Content "C:\ProgramData\SecureBootWatcher\install.log"

# Run client manually
cd "C:\Program Files\SecureBootWatcher"
.\SecureBootWatcher.Client.exe
```

---

## ?? Prerequisites

### All Scripts
- Windows 10/11 or Server 2016+
- PowerShell 5.1+
- Administrator privileges

### Publish-ClientVersion.ps1
- .NET SDK installed
- (Optional) Azure CLI for uploads

### Deploy-Client.ps1
- .NET SDK installed (unless using `-PackageZipPath`)

### Intune Scripts
- No additional requirements (run on target devices)

---

## ?? Security Notes

### Certificate Handling

- **Never commit** PFX files to Git
- Store certificate password in Azure Key Vault (production)
- Use `-CertificatePassword` parameter securely

### Script Execution

- Scripts use `-ExecutionPolicy Bypass` for Intune compatibility
- Always review scripts before execution
- Test in development environment first

### Azure Storage

- Use private containers for client packages
- Consider using SAS tokens with expiration
- Enable HTTPS-only access

---

## ?? Related Documentation

- [PUBLISHING_CLIENT_VERSIONS.md](../docs/PUBLISHING_CLIENT_VERSIONS.md) - Detailed publishing guide
- [PUBLISHING_EXAMPLE.md](../docs/PUBLISHING_EXAMPLE.md) - Step-by-step example
- [INTUNE_WIN32_DEPLOYMENT.md](../docs/INTUNE_WIN32_DEPLOYMENT.md) - Intune deployment guide
- [CLIENT_VERSION_TRACKING.md](../docs/CLIENT_VERSION_TRACKING.md) - Version tracking feature
- [DETECTION_REMEDIATION_GUIDE.md](../docs/DETECTION_REMEDIATION_GUIDE.md) - Intune remediation

---

## ??? Common Tasks

### Check Current Version

```powershell
# From .csproj
$csproj = [xml](Get-Content ..\SecureBootWatcher.Client\SecureBootWatcher.Client.csproj)
$csproj.Project.PropertyGroup.Version

# From installed client
(Get-Item "C:\Program Files\SecureBootWatcher\SecureBootWatcher.Client.exe").VersionInfo.FileVersion
```

### Build Without Publishing

```powershell
cd ..
dotnet build SecureBootWatcher.Client -c Release
```

### Create Package from Existing Build

```powershell
$publishPath = "..\SecureBootWatcher.Client\bin\Release\net48\win-x86\publish"
Compress-Archive -Path "$publishPath\*" -DestinationPath ".\manual-package.zip"
```

### Upload Package to Azure (Manual)

```powershell
az storage blob upload `
    --account-name secbootcert `
    --container-name client-packages `
    --name "SecureBootWatcher-Client-1.2.0.zip" `
    --file ".\release\1.2.0\SecureBootWatcher-Client-1.2.0.zip" `
    --auth-mode login
```

---

## ? Troubleshooting

### Script Won't Run

```powershell
# Check execution policy
Get-ExecutionPolicy

# Allow scripts (as Administrator)
Set-ExecutionPolicy RemoteSigned -Scope CurrentUser
```

### Build Fails

```powershell
# Clean and rebuild
dotnet clean ..\SecureBootWatcher.Client
dotnet restore ..\SecureBootWatcher.Client
dotnet build ..\SecureBootWatcher.Client -c Release
```

### Azure Upload Fails

```powershell
# Login to Azure
az login

# Test connection
az storage account show --name secbootcert

# Check permissions
az role assignment list --assignee your@email.com
```

---

## ?? Tips

- Use `-Verbose` for detailed output
- Always test in development first
- Keep old versions for rollback
- Document release notes
- Monitor dashboard after deployment

---

**Last Updated:** 2025-01-09  
**Maintained By:** SecureBootWatcher Team
