# Client Deployment Scripts Guide

This guide explains how to use the PowerShell scripts for deploying and managing the SecureBootWatcher client on Windows devices.

---

## Overview

The client deployment toolkit includes:

| Script | Purpose |
|--------|---------|
| `Deploy-Client.ps1` | Build, package, and deploy the client |
| `Uninstall-Client.ps1` | Remove the client and scheduled task |
| `Fix-CertificatePermissions.ps1` | Fix Azure certificate permissions |
| `Diagnose-QueueCertificate.ps1` | Diagnose Azure Queue authentication |
| `Assign-StorageQueueRole.ps1` | Assign RBAC roles for Azure Queue |

---

## Deploy-Client.ps1

### Purpose
Builds the SecureBootWatcher client, creates a deployment package, and optionally installs it locally with a scheduled task. **NEW**: Supports installation from precompiled ZIP packages.

### Parameters

| Parameter | Description | Default | Required |
|-----------|-------------|---------|----------|
| `-OutputPath` | Directory for deployment package | `.\client-package` | No |
| `-ApiBaseUrl` | Dashboard API base URL | Empty | No |
| `-FleetId` | Fleet/group identifier | Empty | No |
| `-Configuration` | Build configuration (Release/Debug) | `Release` | No |
| `-CreateScheduledTask` | Install locally and create scheduled task | False | No |
| `-InstallPath` | Installation directory | `C:\Program Files\SecureBootWatcher` | No |
| `-TaskTime` | Scheduled task run time | `09:00AM` | No |
| `-ScheduleType` | **NEW**: Schedule frequency (Once/Daily/Hourly/Custom) | `Daily` | No |
| `-RepeatEveryHours` | **NEW**: Hours between runs (for Custom schedule) | `4` | No |
| `-SkipBuild` | Skip build step (use existing binaries) | False | No |
| `-PackageZipPath` | **NEW**: Path to precompiled ZIP package | Empty | No |

### Usage Examples

#### 1. Build and Package (for distribution)
```powershell
# Basic package creation
.\scripts\Deploy-Client.ps1

# Package with pre-configured API URL
.\scripts\Deploy-Client.ps1 -ApiBaseUrl "https://app-secureboot-api-prod.azurewebsites.net"

# Package with API URL and Fleet ID
.\scripts\Deploy-Client.ps1 `
    -ApiBaseUrl "https://app-secureboot-api-prod.azurewebsites.net" `
    -FleetId "fleet-production"
```

Output: Creates `.\client-package\SecureBootWatcher-Client.zip`

#### 2. Install from Precompiled Package (NEW)

**This is the main new feature!** Install directly from a ZIP file without needing the source code or build tools.

```powershell
# Install from default package location
.\scripts\Deploy-Client.ps1 `
    -PackageZipPath ".\client-package\SecureBootWatcher-Client.zip" `
    -CreateScheduledTask

# Install from custom location with API configuration
.\scripts\Deploy-Client.ps1 `
    -PackageZipPath "C:\Temp\SecureBootWatcher-Client.zip" `
    -ApiBaseUrl "https://api.contoso.com" `
    -CreateScheduledTask

# Install from network share
.\scripts\Deploy-Client.ps1 `
    -PackageZipPath "\\fileserver\packages\SecureBootWatcher-Client.zip" `
    -ApiBaseUrl "https://app-secureboot-api-prod.azurewebsites.net" `
    -FleetId "fleet-prod" `
    -CreateScheduledTask

# Install to custom location
.\scripts\Deploy-Client.ps1 `
    -PackageZipPath ".\client-package\SecureBootWatcher-Client.zip" `
    -InstallPath "C:\Apps\SecureBootWatcher" `
    -CreateScheduledTask
```

**Benefits:**
- ? No build tools required on target machine
- ? Faster deployment (no compilation)
- ? Consistent binaries across all devices
- ? Works on machines without .NET SDK or Visual Studio
- ? Perfect for production deployments

#### 3. Local Installation (for testing)
```powershell
# Install on current machine with scheduled task (daily)
.\scripts\Deploy-Client.ps1 `
    -ApiBaseUrl "https://app-secureboot-api-prod.azurewebsites.net" `
    -CreateScheduledTask

# Install with hourly schedule
.\scripts\Deploy-Client.ps1 `
    -ApiBaseUrl "https://localhost:5001" `
    -CreateScheduledTask `
    -ScheduleType Hourly

# Install with custom schedule (every 6 hours)
.\scripts\Deploy-Client.ps1 `
    -ApiBaseUrl "https://app-secureboot-api-prod.azurewebsites.net" `
    -CreateScheduledTask `
    -ScheduleType Custom `
    -RepeatEveryHours 6

# Install to custom location with custom schedule
.\scripts\Deploy-Client.ps1 `
    -ApiBaseUrl "https://localhost:5001" `
    -CreateScheduledTask `
    -InstallPath "C:\Temp\SecureBootWatcher" `
    -ScheduleType Custom `
    -RepeatEveryHours 4 `
    -TaskTime "08:00AM"

# Install with one-time execution
.\scripts\Deploy-Client.ps1 `
    -ApiBaseUrl "https://app-secureboot-api-prod.azurewebsites.net" `
    -CreateScheduledTask `
    -ScheduleType Once `
    -TaskTime "3:00PM"
```

#### 4. Re-package Without Rebuilding
```powershell
# Use existing build artifacts
.\scripts\Deploy-Client.ps1 -SkipBuild

# Note: -SkipBuild requires existing publish directory
# If you only have the ZIP, use -PackageZipPath instead
```

### What It Does

#### Mode 1: Build from Source (Default)

1. **Builds Client** (unless `-SkipBuild`):
   - Publishes to `win-x86` (32-bit for compatibility)
   - Creates .NET Framework 4.8 deployment
   - Does not use single-file publishing (for easier configuration)

2. **Configures appsettings.json**:
   - Updates `ApiBaseUrl` if provided
   - Updates `FleetId` if provided
   - Enables WebApi sink if `ApiBaseUrl` is set

3. **Creates ZIP Package**:
   - Compresses publish output to `SecureBootWatcher-Client.zip`
   - Ready for distribution via GPO, Intune, SCCM, etc.

4. **Installs Locally** (if `-CreateScheduledTask`):
   - Extracts package to `InstallPath`
   - Creates Windows Scheduled Task

#### Mode 2: Install from Precompiled Package (NEW)

When `-PackageZipPath` is specified:

1. **Validates Package**:
   - Checks if file exists
   - Verifies it's a ZIP file

2. **Extracts to Temporary Directory**:
   - Unpacks ZIP to temp folder
   - Applies configuration changes (ApiBaseUrl, FleetId)

3. **Installs to Target** (if `-CreateScheduledTask`):
   - Copies configured files to `InstallPath`
   - Creates Windows Scheduled Task:
     - **Name**: `SecureBootWatcher`
     - **Runs as**: `SYSTEM`
     - **Schedule**: Daily at specified time
     - **Elevation**: Highest privileges

4. **Cleanup**:
   - Removes temporary extraction directory

### Post-Deployment

**Test Manual Execution:**
```powershell
cd "C:\Program Files\SecureBootWatcher"
.\SecureBootWatcher.Client.exe
```

**Run Scheduled Task Immediately:**
```powershell
Start-ScheduledTask -TaskName SecureBootWatcher
```

**View Task History:**
```powershell
Get-ScheduledTaskInfo -TaskName SecureBootWatcher
```

**Check Logs:**
```powershell
Get-Content "C:\Program Files\SecureBootWatcher\logs\client-*.log" -Tail 50
```

---

## Typical Deployment Workflows

### Workflow 1: Centralized Build + Distributed Installation

**Step 1: Build on Build Server**
```powershell
# On build/deployment server (with .NET SDK)
.\scripts\Deploy-Client.ps1 `
    -ApiBaseUrl "https://app-secureboot-api-prod.azurewebsites.net" `
    -FleetId "fleet-corporate" `
    -OutputPath "\\fileserver\Software\SecureBootWatcher"
```

**Step 2: Distribute Package**
- Package is now at: `\\fileserver\Software\SecureBootWatcher\SecureBootWatcher-Client.zip`
- Available to all domain computers

**Step 3: Install on Target Devices**
```powershell
# On each target device (via GPO startup script or manual)
# No build tools required!
.\Deploy-Client.ps1 `
    -PackageZipPath "\\fileserver\Software\SecureBootWatcher\SecureBootWatcher-Client.zip" `
    -CreateScheduledTask
```

**Benefits:**
- ? Build once, deploy many
- ? Consistent binaries
- ? No SDK required on endpoints

### Workflow 2: Group Policy Deployment with Precompiled Package

**Step 1: Build Package**
```powershell
.\scripts\Deploy-Client.ps1 `
    -ApiBaseUrl "https://app-secureboot-api-prod.azurewebsites.net" `
    -FleetId "fleet-corporate" `
    -OutputPath "\\fileserver\NETLOGON\SecureBootWatcher"
```

**Step 2: Create GPO Startup Script** (`install-secureboot.ps1`):
```powershell
# GPO Startup Script
$packagePath = "\\fileserver\NETLOGON\SecureBootWatcher\SecureBootWatcher-Client.zip"
$scriptPath = "\\fileserver\NETLOGON\SecureBootWatcher\Deploy-Client.ps1"

# Download script and run
Set-ExecutionPolicy Bypass -Scope Process -Force
& $scriptPath -PackageZipPath $packagePath -CreateScheduledTask
```

**Step 3: Attach to GPO:**
- Computer Configuration ? Policies ? Windows Settings ? Scripts ? Startup
- Add `install-secureboot.ps1`

### Workflow 3: Microsoft Endpoint Manager (Intune)

**Step 1: Build Client Package**
```powershell
.\scripts\Deploy-Client.ps1 `
    -ApiBaseUrl "https://app-secureboot-api-prod.azurewebsites.net" `
    -FleetId "fleet-remote-workers"
```

**Step 2: Create Intunewin Package**
```powershell
# Use Microsoft Win32 Content Prep Tool
.\IntuneWinAppUtil.exe `
    -c ".\client-package" `
    -s "SecureBootWatcher-Client.zip" `
    -o ".\intune-package"
```

**Step 3: Create Win32 App in Intune**
- **Install command**:
  ```powershell
  powershell.exe -ExecutionPolicy Bypass -File Deploy-Client.ps1 -PackageZipPath "%cd%\SecureBootWatcher-Client.zip" -CreateScheduledTask
  ```
- **Detection rule**: File exists at `C:\Program Files\SecureBootWatcher\SecureBootWatcher.Client.exe`

### Workflow 4: SCCM/ConfigMgr

**Step 1: Build Package**
```powershell
.\scripts\Deploy-Client.ps1 -ApiBaseUrl "https://api.contoso.com"
```

**Step 2: Create SCCM Application**
- **Source**: `.\client-package\`
- **Install command**:
  ```powershell
  powershell.exe -ExecutionPolicy Bypass -File Deploy-Client.ps1 -PackageZipPath "SecureBootWatcher-Client.zip" -CreateScheduledTask
  ```
- **Uninstall command**:
  ```powershell
  powershell.exe -ExecutionPolicy Bypass -File Uninstall-Client.ps1 -Force
  ```
- **Detection method**: Registry key or file existence

### Workflow 5: Manual Deployment (IT Admin)

**Scenario**: Deploy to a single machine for testing

```powershell
# Step 1: Copy package to target machine
Copy-Item "\\fileserver\packages\SecureBootWatcher-Client.zip" -Destination "C:\Temp\"

# Step 2: Run script as Administrator
cd C:\Temp
.\Deploy-Client.ps1 `
    -PackageZipPath "C:\Temp\SecureBootWatcher-Client.zip" `
    -ApiBaseUrl "https://app-secureboot-api-prod.azurewebsites.net" `
    -FleetId "test-fleet" `
    -CreateScheduledTask

# Step 3: Test immediately
Start-ScheduledTask -TaskName SecureBootWatcher
```

---

## Uninstall-Client.ps1

### Purpose
Removes the SecureBootWatcher client, scheduled task, and running processes from the local system.

### Parameters

| Parameter | Description | Default | Required |
|-----------|-------------|---------|----------|
| `-InstallPath` | Client installation directory | `C:\Program Files\SecureBootWatcher` | No |
| `-Force` | Skip confirmation prompts | False | No |
| `-KeepLogs` | Backup logs before removal | False | No |

### Usage Examples

#### 1. Basic Uninstall
```powershell
# Remove client with prompts
.\scripts\Uninstall-Client.ps1

# Silent removal (no prompts)
.\scripts\Uninstall-Client.ps1 -Force
```

#### 2. Uninstall with Log Backup
```powershell
# Keep logs in temporary directory
.\scripts\Uninstall-Client.ps1 -KeepLogs

# Logs backed up to: C:\Users\<user>\AppData\Local\Temp\SecureBootWatcher-Logs-<timestamp>
```

#### 3. Remove from Custom Location
```powershell
.\scripts\Uninstall-Client.ps1 -InstallPath "C:\Temp\SecureBootWatcher"
```

### What It Does

1. **Removes Scheduled Task**:
   - Stops running task instances
   - Unregisters task from Task Scheduler

2. **Stops Running Processes**:
   - Finds all `SecureBootWatcher.Client.exe` processes
   - Terminates gracefully with `-Force`

3. **Removes Installation Directory**:
   - Deletes all files and subdirectories
   - Optionally backs up logs before deletion

4. **Verifies Removal**:
   - Checks for remaining artifacts
   - Reports any items that could not be removed

### Notes

- **Does not remove device records** from the dashboard database
- **Does not remove reports** already sent to the API
- Device records can be managed via the dashboard web interface
- Run as Administrator for best results

---

## Configuration Options

After deployment, you can customize `appsettings.json` on each device:

### WebApi Sink (Recommended)
```json
{
  "SecureBootWatcher": {
    "FleetId": "fleet-production",
    "Sinks": {
      "EnableWebApi": true,
      "WebApi": {
        "BaseAddress": "https://app-secureboot-api-prod.azurewebsites.net",
        "IngestionRoute": "/api/SecureBootReports",
        "HttpTimeout": "00:00:30"
      }
    }
  }
}
```

### Azure Queue Sink (for high-volume fleets)
```json
{
  "Sinks": {
    "EnableAzureQueue": true,
    "AzureQueue": {
      "QueueServiceUri": "https://secbootcert.queue.core.windows.net",
      "QueueName": "secureboot-reports",
      "AuthenticationMethod": "ManagedIdentity"
    }
  }
}
```

### File Share Sink (for air-gapped environments)
```json
{
  "Sinks": {
    "EnableFileShare": true,
    "FileShare": {
      "RootPath": "\\\\fileserver\\SecureBootReports",
      "FileExtension": ".json"
    }
  }
}
```

---

## Troubleshooting

### Client Not Sending Reports

**Check scheduled task status:**
```powershell
Get-ScheduledTaskInfo -TaskName SecureBootWatcher
```

**View last run result:**
- `0` = Success
- `267009` = Currently running
- Other codes = Error (see Windows Event Log)

**Check logs:**
```powershell
Get-Content "C:\Program Files\SecureBootWatcher\logs\client-*.log" -Tail 100
```

**Run manually for debugging:**
```powershell
cd "C:\Program Files\SecureBootWatcher"
.\SecureBootWatcher.Client.exe
```

### Package Not Found Error

**Error**: `Package not found at: <path>`

**Solutions:**
1. Verify the ZIP file exists at the specified path
2. Check network connectivity to UNC paths
3. Ensure correct file name and extension (.zip)
4. Use absolute paths to avoid confusion

```powershell
# Test if package is accessible
Test-Path "\\fileserver\packages\SecureBootWatcher-Client.zip"

# If False, check:
# - Network connectivity
# - File permissions
# - Correct path syntax
```

### Build Failures (when not using precompiled package)

**Ensure prerequisites installed:**
- .NET SDK 8.0+
- .NET Framework 4.8 Developer Pack
- Visual Studio 2022 or VS Build Tools

**Check .NET installations:**
```powershell
dotnet --list-sdks
dotnet --list-runtimes
```

**Verify solution builds:**
```powershell
dotnet build SecureBootWatcher.sln
```

### Permission Errors

**Run PowerShell as Administrator:**
```powershell
# Right-click PowerShell ? Run as Administrator
```

**Check execution policy:**
```powershell
Get-ExecutionPolicy
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
```

### Temp Directory Cleanup Errors

If deployment fails partway through, temporary files may remain:

```powershell
# List temp SecureBootWatcher directories
Get-ChildItem $env:TEMP | Where-Object { $_.Name -like "SecureBootWatcher-Deploy-*" }

# Remove manually if needed
Remove-Item "$env:TEMP\SecureBootWatcher-Deploy-*" -Recurse -Force
```

---

## Security Considerations

- **Run as SYSTEM**: Scheduled task runs with highest privileges to access registry and event logs
- **Network Access**: Client requires HTTPS outbound to dashboard API (port 443)
- **Credentials**: Use Managed Identity or certificate-based auth for Azure Queue (avoid connection strings in config)
- **File Share**: Use UNC paths with appropriate NTFS permissions (client needs Write access)
- **Logs**: May contain sensitive system information; protect log directory
- **Package Integrity**: Verify ZIP package checksums in production environments

---

## Best Practices

1. **Build Once, Deploy Many**: Create package on build server, distribute to all devices
2. **Test First**: Deploy to pilot group before organization-wide rollout
3. **Configure Centrally**: Pre-configure API URL and Fleet ID in package
4. **Monitor Logs**: Set up log collection (e.g., Splunk, Azure Monitor)
5. **Schedule Wisely**: Avoid peak hours; stagger task times across fleets
6. **Version Control**: Track client versions deployed to each fleet
7. **Document Changes**: Record configuration changes and deployment dates
8. **Use Precompiled Packages**: Simplifies deployment and ensures consistency

---

## Quick Reference

### Common Commands

```powershell
# Build and create package
.\Deploy-Client.ps1

# Install from precompiled package
.\Deploy-Client.ps1 -PackageZipPath ".\client-package\SecureBootWatcher-Client.zip" -CreateScheduledTask

# Install with API configuration
.\Deploy-Client.ps1 `
    -PackageZipPath "\\server\share\SecureBootWatcher-Client.zip" `
    -ApiBaseUrl "https://api.contoso.com" `
    -FleetId "prod" `
    -CreateScheduledTask

# Uninstall
.\Uninstall-Client.ps1 -Force

# Test client
Start-ScheduledTask -TaskName SecureBootWatcher
Get-ScheduledTaskInfo -TaskName SecureBootWatcher
```

---

## Support

For issues or questions:
- Review logs in `C:\Program Files\SecureBootWatcher\logs\`
- Check dashboard API health: `https://<api-url>/health`
- Consult deployment guide: `docs\DEPLOYMENT_GUIDE.md`
- Open GitHub issue: [Repository Issues](https://github.com/robgrame/Nimbus.BootCertWatcher/issues)

---

## Additional Resources

- **Main README**: `README.md`
- **Deployment Guide**: `docs\DEPLOYMENT_GUIDE.md`
- **API Documentation**: Swagger UI at `https://<api-url>/swagger`
- **Configuration Reference**: `docs\CONFIGURATION_GUIDE.md`
