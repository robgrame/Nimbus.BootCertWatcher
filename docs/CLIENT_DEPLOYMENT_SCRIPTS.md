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
Builds the SecureBootWatcher client, creates a deployment package, and optionally installs it locally with a scheduled task.

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
| `-SkipBuild` | Skip build step (use existing binaries) | False | No |

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

#### 2. Local Installation (for testing)
```powershell
# Install on current machine with scheduled task
.\scripts\Deploy-Client.ps1 `
    -ApiBaseUrl "https://app-secureboot-api-prod.azurewebsites.net" `
    -CreateScheduledTask

# Install to custom location
.\scripts\Deploy-Client.ps1 `
    -ApiBaseUrl "https://localhost:5001" `
    -CreateScheduledTask `
    -InstallPath "C:\Temp\SecureBootWatcher"

# Install with custom schedule (3:00 PM daily)
.\scripts\Deploy-Client.ps1 `
    -ApiBaseUrl "https://app-secureboot-api-prod.azurewebsites.net" `
    -CreateScheduledTask `
    -TaskTime "3:00PM"
```

#### 3. Re-package Without Rebuilding
```powershell
# Use existing build artifacts
.\scripts\Deploy-Client.ps1 -SkipBuild
```

### What It Does

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
   - Creates Windows Scheduled Task:
     - **Name**: `SecureBootWatcher`
     - **Runs as**: `SYSTEM`
     - **Schedule**: Daily at specified time
     - **Elevation**: Highest privileges

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

## Enterprise Deployment Workflows

### Workflow 1: Group Policy Deployment

1. **Build and package client:**
   ```powershell
   .\scripts\Deploy-Client.ps1 `
       -ApiBaseUrl "https://app-secureboot-api-prod.azurewebsites.net" `
       -FleetId "fleet-corporate" `
       -OutputPath "\\fileserver\NETLOGON\SecureBootWatcher"
   ```

2. **Create GPO startup script** (`install-secureboot.ps1`):
   ```powershell
   $installPath = "C:\Program Files\SecureBootWatcher"
   $packagePath = "\\fileserver\NETLOGON\SecureBootWatcher\SecureBootWatcher-Client.zip"
   
   # Extract client
   Expand-Archive -Path $packagePath -DestinationPath $installPath -Force
   
   # Create scheduled task
   $action = New-ScheduledTaskAction -Execute "$installPath\SecureBootWatcher.Client.exe"
   $trigger = New-ScheduledTaskTrigger -Daily -At "9:00AM"
   $principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest
   
   Register-ScheduledTask -TaskName "SecureBootWatcher" -Action $action -Trigger $trigger -Principal $principal -Force
   ```

3. **Attach script to GPO:**
   - Computer Configuration ? Policies ? Windows Settings ? Scripts ? Startup
   - Add `install-secureboot.ps1`

### Workflow 2: Microsoft Endpoint Manager (Intune)

1. **Build client package:**
   ```powershell
   .\scripts\Deploy-Client.ps1 `
       -ApiBaseUrl "https://app-secureboot-api-prod.azurewebsites.net" `
       -FleetId "fleet-remote-workers"
   ```

2. **Create Intune Win32 app:**
   - Use Microsoft Win32 Content Prep Tool to convert ZIP to `.intunewin`
   - Install command: `powershell.exe -ExecutionPolicy Bypass -File install.ps1`
   - Detection rule: File exists at `C:\Program Files\SecureBootWatcher\SecureBootWatcher.Client.exe`

3. **Create installation script** (`install.ps1`):
   ```powershell
   $installPath = "C:\Program Files\SecureBootWatcher"
   Expand-Archive -Path "SecureBootWatcher-Client.zip" -DestinationPath $installPath -Force
   
   # Create scheduled task (same as GPO example)
   ```

### Workflow 3: SCCM/ConfigMgr

1. **Build package:**
   ```powershell
   .\scripts\Deploy-Client.ps1 -ApiBaseUrl "https://api.contoso.com"
   ```

2. **Create SCCM Application:**
   - Source: `.\client-package\`
   - Install command: `powershell.exe -ExecutionPolicy Bypass -File Deploy-Client.ps1 -CreateScheduledTask -SkipBuild`
   - Uninstall command: `powershell.exe -ExecutionPolicy Bypass -File Uninstall-Client.ps1 -Force`
   - Detection method: Registry key or file existence

### Workflow 4: Manual Deployment

1. **Build package:**
   ```powershell
   .\scripts\Deploy-Client.ps1
   ```

2. **Copy ZIP to target device**

3. **On target device (as Administrator):**
   ```powershell
   # Extract
   Expand-Archive -Path "SecureBootWatcher-Client.zip" -DestinationPath "C:\Program Files\SecureBootWatcher"
   
   # Configure
   notepad "C:\Program Files\SecureBootWatcher\appsettings.json"
   # Update ApiBaseUrl and other settings
   
   # Create scheduled task
   cd "C:\Program Files\SecureBootWatcher"
   .\Deploy-Client.ps1 -CreateScheduledTask -SkipBuild
   ```

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

### Build Failures

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

---

## Security Considerations

- **Run as SYSTEM**: Scheduled task runs with highest privileges to access registry and event logs
- **Network Access**: Client requires HTTPS outbound to dashboard API (port 443)
- **Credentials**: Use Managed Identity or certificate-based auth for Azure Queue (avoid connection strings in config)
- **File Share**: Use UNC paths with appropriate NTFS permissions (client needs Write access)
- **Logs**: May contain sensitive system information; protect log directory

---

## Best Practices

1. **Test First**: Deploy to pilot group before organization-wide rollout
2. **Configure Centrally**: Use consistent `FleetId` per environment
3. **Monitor Logs**: Set up log collection (e.g., splunk, Azure Monitor)
4. **Schedule Wisely**: Avoid peak hours; stagger task times across fleets
5. **Version Control**: Track client versions deployed to each fleet
6. **Document Changes**: Record configuration changes and deployment dates

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
