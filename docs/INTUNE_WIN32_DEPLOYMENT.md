# Intune Win32 App Deployment Guide

## Overview

This guide explains how to deploy SecureBootWatcher client as a Win32 app through Microsoft Endpoint Manager (Intune).

---

## Prerequisites

1. **Microsoft Win32 Content Prep Tool**
   - Download: https://github.com/Microsoft/Microsoft-Win32-Content-Prep-Tool
   - Used to convert package to `.intunewin` format

2. **SecureBootWatcher Client Package**
   - Built using: `.\scripts\Deploy-Client.ps1`
   - Location: `.\client-package\SecureBootWatcher-Client.zip`

3. **Intune Administrator Access**
   - Permission to create and deploy Win32 apps

---

## Scripts for Intune

### Install Script: `Install-Client-Intune.ps1`
- Copies files to `C:\Program Files\SecureBootWatcher`
- Optionally configures API URL and Fleet ID
- Creates scheduled task
- Logs to `%ProgramData%\SecureBootWatcher\install.log`

### Uninstall Script: `Uninstall-Client-Intune.ps1`
- Removes scheduled task
- Stops running processes
- Deletes installation directory
- Logs to `%ProgramData%\SecureBootWatcher\uninstall.log`

### Detection Script: `Detect-Client-Intune.ps1`
- No parameters (Intune requirement)
- Checks installation directory, executable, config, and task
- Returns "Installed" on success (exit 0)
- Returns nothing on failure (exit 1)

---

## Step-by-Step Deployment

### Step 1: Prepare the Package

Extract the client package to a working directory:

```powershell
# Create working directory
New-Item -ItemType Directory -Path "C:\Temp\SecureBootWatcher-Intune" -Force

# Extract package
Expand-Archive -Path ".\client-package\SecureBootWatcher-Client.zip" `
    -DestinationPath "C:\Temp\SecureBootWatcher-Intune" -Force

# Copy Intune scripts to the package directory
Copy-Item ".\scripts\Install-Client-Intune.ps1" `
    -Destination "C:\Temp\SecureBootWatcher-Intune\" -Force

Copy-Item ".\scripts\Uninstall-Client-Intune.ps1" `
    -Destination "C:\Temp\SecureBootWatcher-Intune\" -Force

Copy-Item ".\scripts\Detect-Client-Intune.ps1" `
    -Destination "C:\Temp\SecureBootWatcher-Intune\" -Force
```

**Package Contents Should Include**:
```
C:\Temp\SecureBootWatcher-Intune\
??? Install-Client-Intune.ps1          (Install script)
??? Uninstall-Client-Intune.ps1        (Uninstall script)
??? Detect-Client-Intune.ps1           (Detection script)
??? SecureBootWatcher.Client.exe       (Main executable)
??? SecureBootWatcher.Shared.dll       (Shared library)
??? appsettings.json                   (Configuration)
??? [Other DLLs and dependencies]
```

### Step 2: Create IntuneWin Package

Use the Microsoft Win32 Content Prep Tool:

```powershell
# Download IntuneWinAppUtil.exe first, then run:
.\IntuneWinAppUtil.exe `
    -c "C:\Temp\SecureBootWatcher-Intune" `
    -s "Install-Client-Intune.ps1" `
    -o "C:\Temp\IntunePackage" `
    -q
```

**Parameters Explained**:
- `-c` = Source folder containing all files
- `-s` = Setup file (install script)
- `-o` = Output folder for `.intunewin` file
- `-q` = Quiet mode (no prompts)

**Output**: `SecureBootWatcher-Client.intunewin` in `C:\Temp\IntunePackage\`

### Step 3: Upload to Intune

1. **Navigate to Intune Portal**:
   - https://endpoint.microsoft.com
   - **Apps** ? **Windows** ? **+ Add**

2. **Select App Type**:
   - App type: **Windows app (Win32)**
   - Click **Select**

3. **App Information**:
   - **Select app package file**: Upload `SecureBootWatcher-Client.intunewin`
   - Click **OK**

4. **Fill App Details**:
   ```
   Name: SecureBootWatcher Client
   Description: Monitors and reports Secure Boot certificate status to the dashboard
   Publisher: Your Organization
   Category: Security
   Show this as a featured app in the Company Portal: No (optional)
   Information URL: https://github.com/robgrame/Nimbus.BootCertWatcher
   Privacy URL: (leave blank or add your privacy policy)
   ```

5. **Program Configuration**:

   **Install command**:
   ```powershell
   powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Install-Client-Intune.ps1"
   ```

   **Uninstall command**:
   ```powershell
   powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Uninstall-Client-Intune.ps1"
   ```

   **Install behavior**: System
   
   **Device restart behavior**: No specific action

6. **Requirements**:
   ```
   Operating system architecture: 64-bit
   Minimum operating system: Windows 10 1607
   Disk space required (MB): 50
   Physical memory required (MB): 256
   Minimum number of logical processors required: 1
   Minimum CPU speed required (MHz): 1000
   ```

7. **Detection Rules**:
   
   **Rule format**: Use custom detection script
   
   **Script file**: Upload `Detect-Client-Intune.ps1`
   
   **Run script as 32-bit process on 64-bit clients**: No
   
   **Enforce script signature check**: No (unless you sign your scripts)

8. **Dependencies**: None

9. **Supersedence**: None (unless replacing older version)

10. **Assignments**:
    
    **Required**:
    - Select device groups that must have the client installed
    
    **Available for enrolled devices**:
    - Select user groups who can optionally install from Company Portal
    
    **Uninstall**:
    - Select device groups to uninstall from (if needed)

11. **Review + Create**:
    - Review all settings
    - Click **Create**

---

## Step 4: Monitor Deployment

### In Intune Portal

1. **Navigate to the App**:
   - **Apps** ? **Windows** ? **SecureBootWatcher Client**

2. **View Device Install Status**:
   - Click **Device install status**
   - Monitor deployment progress

3. **View User Install Status**:
   - Click **User install status**
   - See per-user deployment status

### Status Codes

| Status | Meaning |
|--------|---------|
| **Installed** | Detection script returned success |
| **Not installed** | Detection script failed or app not deployed |
| **Installation in progress** | Currently installing |
| **Failed** | Installation error (check logs) |
| **Not applicable** | Device doesn't meet requirements |

### Troubleshooting Failed Installations

**On the Device**:

1. Check installation log:
   ```powershell
   Get-Content "C:\ProgramData\SecureBootWatcher\install.log" -Tail 50
   ```

2. Check Intune Management Extension logs:
   ```powershell
   Get-Content "C:\ProgramData\Microsoft\IntuneManagementExtension\Logs\IntuneManagementExtension.log" -Tail 100
   ```

3. Check Event Viewer:
   - Event Viewer ? Applications and Services Logs ? Microsoft ? Windows ? DeviceManagement-Enterprise-Diagnostics-Provider

**Common Issues**:

| Issue | Solution |
|-------|----------|
| Detection script fails | Verify installation path is correct |
| Install fails with access denied | Ensure "Install behavior" is set to "System" |
| Task not created | Check install log for scheduled task errors |
| Files not copied | Verify .intunewin package includes all files |

---

## Advanced Configuration

### Customize API URL and Fleet ID

You can pre-configure the API URL and Fleet ID during installation by modifying the install command:

**Install command with parameters**:
```powershell
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Install-Client-Intune.ps1" -ApiBaseUrl "https://your-api.contoso.com" -FleetId "production-fleet"
```

**Example for Different Fleets**:

```powershell
# Production fleet
-ApiBaseUrl "https://api-prod.contoso.com" -FleetId "prod"

# Development fleet
-ApiBaseUrl "https://api-dev.contoso.com" -FleetId "dev"

# Regional fleet
-ApiBaseUrl "https://api-emea.contoso.com" -FleetId "emea"
```

### Multiple Deployments for Different Fleets

Create separate Win32 apps for each fleet:

1. **Production App**:
   - Name: `SecureBootWatcher Client - Production`
   - Install command: `...-File "Install-Client-Intune.ps1" -ApiBaseUrl "https://api-prod.contoso.com" -FleetId "prod"`
   - Assign to production device groups

2. **Development App**:
   - Name: `SecureBootWatcher Client - Development`
   - Install command: `...-File "Install-Client-Intune.ps1" -ApiBaseUrl "https://api-dev.contoso.com" -FleetId "dev"`
   - Assign to development device groups

---

## Post-Deployment Verification

### On a Single Device

```powershell
# Check if client is installed
Test-Path "C:\Program Files\SecureBootWatcher\SecureBootWatcher.Client.exe"

# Check scheduled task
Get-ScheduledTask -TaskName "SecureBootWatcher"

# Run detection script manually
& "C:\Program Files\SecureBootWatcher\..\Install-Client-Intune.ps1"  # Should output "Installed"

# View recent logs
Get-Content "C:\Program Files\SecureBootWatcher\logs\client-*.log" -Tail 50

# Run client manually to test
Start-Process "C:\Program Files\SecureBootWatcher\SecureBootWatcher.Client.exe" -Wait

# Check if report was sent to dashboard
# (verify in dashboard web UI or API logs)
```

### Fleet-Wide Verification

Use the detection script or Proactive Remediations:

**Detection Script** (from earlier):
```powershell
.\scripts\Detect-Client.ps1 -Verbose
```

**Intune Device Compliance**:
- Create compliance policy that checks for scheduled task
- Or use Proactive Remediations for ongoing monitoring

---

## Update Strategy

### Deploy New Version

1. **Build new package** with updated version
2. **Create new IntuneWin** package
3. **Upload to Intune** as a new app version
4. **Configure Supersedence**:
   - New app supersedes old app
   - Select "Uninstall" for previous version

### In-Place Update

Alternatively, update the existing app:

1. Upload new `.intunewin` package to existing app
2. Intune will automatically reinstall on devices

---

## Best Practices

### Pilot Deployment

1. Create pilot device group (10-50 devices)
2. Deploy as "Required" to pilot group
3. Monitor for 1-2 days
4. Address any issues
5. Expand to production groups

### Staged Rollout

Use Intune's deployment rings:

1. **Ring 1** (Pilot): 5% of devices
2. **Ring 2** (Early Adopters): 20% of devices
3. **Ring 3** (Broad Deployment): 100% of devices

Configure delays between rings (e.g., 2 days).

### Monitoring

- **Daily**: Check device install status
- **Weekly**: Review failure logs
- **Monthly**: Verify version compliance

### Maintenance

- **Update client** when new versions are released
- **Monitor scheduled task** execution (dashboard should show reports)
- **Review logs** for errors or warnings

---

## Cleanup and Uninstall

### Uninstall from Intune

1. Navigate to app in Intune portal
2. Change assignment to "Uninstall" for target groups
3. Intune will run `Uninstall-Client-Intune.ps1` on devices

### Manual Uninstall

On a single device:

```powershell
& "C:\Program Files\SecureBootWatcher\..\Uninstall-Client-Intune.ps1"
```

Or use the full uninstall script:

```powershell
.\scripts\Uninstall-Client.ps1 -Force
```

---

## Security Considerations

### Script Signing

For enhanced security, sign your PowerShell scripts:

```powershell
# Get code signing certificate
$cert = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert

# Sign scripts
Set-AuthenticodeSignature -FilePath "Install-Client-Intune.ps1" -Certificate $cert
Set-AuthenticodeSignature -FilePath "Uninstall-Client-Intune.ps1" -Certificate $cert
Set-AuthenticodeSignature -FilePath "Detect-Client-Intune.ps1" -Certificate $cert
```

Then enable "Enforce script signature check" in Intune app configuration.

### API Authentication

- Use **certificate-based auth** for Azure Queue sink (recommended)
- Or use **Managed Identity** if deploying to Azure-joined devices
- Avoid storing connection strings in `appsettings.json`

---

## Troubleshooting

### Detection Always Fails

**Symptom**: Intune shows "Not installed" even after successful installation

**Fix**: 
1. Verify detection script path in Intune matches actual installation
2. Run detection script manually on device: `.\Detect-Client-Intune.ps1`
3. Check detection script output: Should see "Installed" and exit code 0

### Installation Succeeds but Task Doesn't Run

**Symptom**: Files installed, but no reports in dashboard

**Fix**:
1. Check task manually: `Get-ScheduledTask -TaskName "SecureBootWatcher"`
2. Run task immediately: `Start-ScheduledTask -TaskName "SecureBootWatcher"`
3. Check logs: `Get-Content "C:\Program Files\SecureBootWatcher\logs\client-*.log"`

### Scheduled Task Runs but Reports Not Received

**Symptom**: Task runs successfully, but dashboard shows no data

**Fix**:
1. Verify API URL in `appsettings.json`
2. Check network connectivity to API
3. Review API logs for incoming requests
4. Test API health: `https://your-api.com/health`

---

## Additional Resources

- **Main Deployment Guide**: `docs/DEPLOYMENT_GUIDE.md`
- **Detection/Remediation**: `docs/DETECTION_REMEDIATION_GUIDE.md`
- **PowerShell Scripts**: `docs/CLIENT_DEPLOYMENT_SCRIPTS.md`
- **README**: `README.md`

---

## Quick Reference

### Intune Win32 App Configuration

```
App Name: SecureBootWatcher Client
Install Command: powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Install-Client-Intune.ps1"
Uninstall Command: powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Uninstall-Client-Intune.ps1"
Detection: Custom script (Detect-Client-Intune.ps1)
Install Behavior: System
Requirements: Windows 10 1607+, 64-bit
```

### File Locations

```
Installation: C:\Program Files\SecureBootWatcher\
Logs: C:\Program Files\SecureBootWatcher\logs\
Install Log: C:\ProgramData\SecureBootWatcher\install.log
Uninstall Log: C:\ProgramData\SecureBootWatcher\uninstall.log
Scheduled Task: Task Scheduler > SecureBootWatcher
```

---

**Created**: 2025-11-08  
**Version**: 1.0  
**Scripts**: Install-Client-Intune.ps1, Uninstall-Client-Intune.ps1, Detect-Client-Intune.ps1
