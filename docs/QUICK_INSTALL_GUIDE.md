# Quick Installation Guide - SecureBootWatcher Client

This guide provides the essential PowerShell commands to install and configure the SecureBootWatcher client.

---

## Prerequisites

- **Windows 10/11** or **Windows Server 2016+**
- **PowerShell 5.0+** (run as Administrator)
- **.NET Framework 4.8** installed
- Network access to your API endpoint or Azure Storage

---

## Installation Methods

### Method 1: Quick Install (Recommended)

**Single command to build, configure, install, and schedule:**

```powershell
# Navigate to repository root
cd "C:\Users\nefario\source\repos\robgrame\Nimbus.BootCertWatcher"

# Install with all defaults (daily at 9 AM)
.\scripts\Deploy-Client.ps1 `
    -ApiBaseUrl "https://SRVCM01.MSINTUNE.LAB:5001" `
    -FleetId "mslabs" `
    -CreateScheduledTask
```

**Parameters explained:**
- `-ApiBaseUrl` ? Your API endpoint URL
- `-FleetId` ? Identifier for your device fleet (optional)
- `-CreateScheduledTask` ? Automatically creates Windows scheduled task

**What this does:**
1. ? Builds the client
2. ? Configures `appsettings.json` with your API URL
3. ? Installs to `C:\Program Files\SecureBootWatcher`
4. ? Creates scheduled task to run daily at 9:00 AM as SYSTEM

---

### Method 2: Install with Custom Schedule

```powershell
# Install with custom time (e.g., 8:30 PM)
.\scripts\Deploy-Client.ps1 `
    -ApiBaseUrl "https://SRVCM01.MSINTUNE.LAB:5001" `
    -FleetId "mslabs" `
    -CreateScheduledTask `
    -TaskTime "08:30PM"
```

---

### Method 3: Build Package Only (for distribution)

```powershell
# Build package without installing locally
.\scripts\Deploy-Client.ps1 `
    -ApiBaseUrl "https://SRVCM01.MSINTUNE.LAB:5001" `
    -FleetId "mslabs" `
    -OutputPath ".\deployment-package"
```

**Output:**
- `deployment-package\SecureBootWatcher-Client.zip` ready for distribution

**Then distribute via:**
- Group Policy (NETLOGON share)
- Intune Win32 app
- SCCM application
- Manual copy to devices

---

### Method 4: Install from Existing Package

```powershell
# If you already built the package, install it without rebuilding
.\scripts\Deploy-Client.ps1 `
    -SkipBuild `
    -CreateScheduledTask `
    -InstallPath "C:\Program Files\SecureBootWatcher"
```

---

## Verify Installation

### Check if installed correctly:

```powershell
# 1. Check files exist
Test-Path "C:\Program Files\SecureBootWatcher\SecureBootWatcher.Client.exe"

# 2. Check scheduled task
Get-ScheduledTask -TaskName "SecureBootWatcher"

# 3. View configuration
Get-Content "C:\Program Files\SecureBootWatcher\appsettings.json" | ConvertFrom-Json | Format-List
```

---

## Run Client Manually (for testing)

```powershell
# Navigate to install directory
cd "C:\Program Files\SecureBootWatcher"

# Run once
.\SecureBootWatcher.Client.exe
```

**Expected output:**
```
[10:00:00 INF] SecureBootWatcher Client Starting
[10:00:00 INF] Version: 1.1.1.48182
[10:00:01 INF] Registry snapshot captured
[10:00:02 INF] Sending report using strategy: StopOnFirstSuccess
[10:00:02 INF] ? Successfully sent report to WebApi
[10:00:02 INF] Report delivery summary: 1 succeeded, 0 failed
```

---

## Run Scheduled Task Immediately

```powershell
# Trigger the scheduled task now (instead of waiting for daily schedule)
Start-ScheduledTask -TaskName "SecureBootWatcher"

# Check task status
Get-ScheduledTaskInfo -TaskName "SecureBootWatcher"

# View last run result
Get-ScheduledTaskInfo -TaskName "SecureBootWatcher" | Select-Object LastRunTime, LastTaskResult
```

**Last Task Result Codes:**
- `0` ? Success
- `1` ? Error
- `267011` ? Task is currently running

---

## View Logs

```powershell
# Navigate to log directory
cd "C:\Program Files\SecureBootWatcher\logs"

# View latest log file
Get-Content "client-*.log" -Tail 50

# Watch logs in real-time
Get-Content "client-*.log" -Tail 50 -Wait
```

---

## Update Configuration After Installation

If you need to change settings after installation:

```powershell
# Edit configuration file
notepad "C:\Program Files\SecureBootWatcher\appsettings.json"

# Or using PowerShell
$config = Get-Content "C:\Program Files\SecureBootWatcher\appsettings.json" -Raw | ConvertFrom-Json
$config.SecureBootWatcher.Sinks.WebApi.BaseAddress = "https://NEW-API-URL:5001"
$config | ConvertTo-Json -Depth 10 | Set-Content "C:\Program Files\SecureBootWatcher\appsettings.json"
```

---

## Uninstall

```powershell
# Run uninstall script
.\scripts\Uninstall-Client.ps1

# Or manual uninstall:
Unregister-ScheduledTask -TaskName "SecureBootWatcher" -Confirm:$false
Remove-Item "C:\Program Files\SecureBootWatcher" -Recurse -Force
```

---

## Common Scenarios

### Scenario 1: Install on Single Test Machine

```powershell
cd "C:\Users\nefario\source\repos\robgrame\Nimbus.BootCertWatcher"

.\scripts\Deploy-Client.ps1 `
    -ApiBaseUrl "https://SRVCM01.MSINTUNE.LAB:5001" `
    -FleetId "test-machines" `
    -CreateScheduledTask `
    -Configuration Debug
```

### Scenario 2: Build Package for 100+ Production Devices

```powershell
# Step 1: Build package on dev machine
cd "C:\Users\nefario\source\repos\robgrame\Nimbus.BootCertWatcher"

.\scripts\Deploy-Client.ps1 `
    -ApiBaseUrl "https://api-production.company.com" `
    -FleetId "production-fleet" `
    -Configuration Release `
    -OutputPath "\\fileserver\deploy\SecureBootWatcher"

# Step 2: On each target device (via GPO/Intune/SCCM)
$packagePath = "\\fileserver\deploy\SecureBootWatcher\SecureBootWatcher-Client.zip"
$installPath = "C:\Program Files\SecureBootWatcher"

Expand-Archive -Path $packagePath -DestinationPath $installPath -Force

# Step 3: Create scheduled task on each device
cd "C:\Users\nefario\source\repos\robgrame\Nimbus.BootCertWatcher"
.\scripts\Deploy-Client.ps1 -SkipBuild -CreateScheduledTask
```

### Scenario 3: Use Azure Queue Instead of Direct API

```powershell
# Build package
.\scripts\Deploy-Client.ps1 `
    -FleetId "azure-queue-clients" `
    -OutputPath ".\azure-clients"

# Then manually edit appsettings.json in the package to enable AzureQueue:
# Set EnableWebApi = false
# Set EnableAzureQueue = true
# Configure QueueServiceUri, QueueName, etc.
```

### Scenario 4: Quick Rebuild and Update Existing Installation

```powershell
# Rebuild without reinstalling
.\scripts\Deploy-Client.ps1 -Configuration Release

# Then manually replace files in C:\Program Files\SecureBootWatcher\
# Or reinstall:
.\scripts\Deploy-Client.ps1 -CreateScheduledTask -Configuration Release
```

---

## Troubleshooting

### Issue: "Build failed with exit code 1"

**Solution:**
```powershell
# Clean and try again
dotnet clean SecureBootWatcher.Client
.\scripts\Deploy-Client.ps1 -ApiBaseUrl "https://YOUR-API-URL:5001" -CreateScheduledTask
```

### Issue: "Client executable not found"

**Solution:**
```powershell
# Check if build actually produced files
Get-ChildItem "SecureBootWatcher.Client\bin\Release\net48\win-x86\publish"

# If empty, build manually:
dotnet publish SecureBootWatcher.Client -c Release -r win-x86 --self-contained false
```

### Issue: "Access denied" when creating scheduled task

**Solution:**
```powershell
# Run PowerShell as Administrator
# Right-click PowerShell ? Run as Administrator
# Then run the deploy script again
```

### Issue: Scheduled task not running

**Solution:**
```powershell
# Check task status
Get-ScheduledTask -TaskName "SecureBootWatcher"

# Check last run time and result
Get-ScheduledTaskInfo -TaskName "SecureBootWatcher"

# Run manually to see errors
cd "C:\Program Files\SecureBootWatcher"
.\SecureBootWatcher.Client.exe
```

### Issue: "Unable to connect to API"

**Solution:**
```powershell
# Test network connectivity
Test-NetConnection -ComputerName "SRVCM01.MSINTUNE.LAB" -Port 5001

# Check API URL in config
Get-Content "C:\Program Files\SecureBootWatcher\appsettings.json" | Select-String "BaseAddress"

# Try accessing API from browser:
# https://SRVCM01.MSINTUNE.LAB:5001/health
```

---

## Complete Example Walkthrough

```powershell
# ===============================================
# Complete Installation from Start to Finish
# ===============================================

# 1. Open PowerShell as Administrator
# Right-click PowerShell ? Run as Administrator

# 2. Navigate to repository
cd "C:\Users\nefario\source\repos\robgrame\Nimbus.BootCertWatcher"

# 3. Run deployment script with your settings
.\scripts\Deploy-Client.ps1 `
    -ApiBaseUrl "https://SRVCM01.MSINTUNE.LAB:5001" `
    -FleetId "mslabs" `
    -CreateScheduledTask `
    -TaskTime "09:00AM"

# 4. Verify installation
Get-ScheduledTask -TaskName "SecureBootWatcher"
Test-Path "C:\Program Files\SecureBootWatcher\SecureBootWatcher.Client.exe"

# 5. Test manually
cd "C:\Program Files\SecureBootWatcher"
.\SecureBootWatcher.Client.exe

# 6. Check logs
Get-Content "C:\Program Files\SecureBootWatcher\logs\client-*.log" -Tail 20

# 7. If all looks good, trigger scheduled task
Start-ScheduledTask -TaskName "SecureBootWatcher"

# 8. Verify report reached API
# Check API logs at: R:\Nimbus.SecureBootCert\logs\api-*.log
# Or query dashboard: https://YOUR-DASHBOARD/Devices
```

---

## Quick Reference Card

| Task | Command |
|------|---------|
| **Quick install** | `.\scripts\Deploy-Client.ps1 -ApiBaseUrl "https://YOUR-API:5001" -CreateScheduledTask` |
| **Build package only** | `.\scripts\Deploy-Client.ps1 -ApiBaseUrl "https://YOUR-API:5001"` |
| **Install from package** | `.\scripts\Deploy-Client.ps1 -SkipBuild -CreateScheduledTask` |
| **Run manually** | `cd "C:\Program Files\SecureBootWatcher" ; .\SecureBootWatcher.Client.exe` |
| **Run scheduled task** | `Start-ScheduledTask -TaskName "SecureBootWatcher"` |
| **View logs** | `Get-Content "C:\Program Files\SecureBootWatcher\logs\client-*.log" -Tail 50` |
| **Check task status** | `Get-ScheduledTaskInfo -TaskName "SecureBootWatcher"` |
| **Uninstall** | `.\scripts\Uninstall-Client.ps1` |
| **Test API connectivity** | `Test-NetConnection -ComputerName "YOUR-API-HOST" -Port 5001` |

---

## Next Steps

After successful installation:

1. ? Wait for scheduled task to run (or trigger manually)
2. ? Check API logs: `R:\Nimbus.SecureBootCert\logs\api-*.log`
3. ? View dashboard: Navigate to your web dashboard to see device reports
4. ? Deploy to remaining devices using Group Policy, Intune, or SCCM

---

## Related Documentation

- **Full Deployment Guide:** `docs\DEPLOYMENT_GUIDE.md`
- **Client Deployment Scripts:** `docs\CLIENT_DEPLOYMENT_SCRIPTS.md`
- **Troubleshooting:** `docs\TROUBLESHOOTING_PORTS.md`
- **Configuration Reference:** `README.md`
- **Retry & Resilience:** `docs\RETRY_RESILIENCE_GUIDE.md`

---

## Support

For issues or questions:
1. Check logs: `C:\Program Files\SecureBootWatcher\logs\`
2. Review documentation in `docs\` folder
3. Open GitHub issue with log output
