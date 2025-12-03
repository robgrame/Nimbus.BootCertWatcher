# PowerShell Client Quick Start Guide

Get the SecureBootWatcher PowerShell Client up and running in minutes!

## Prerequisites

- ✅ Windows 10/11 or Windows Server 2016+ with UEFI
- ✅ PowerShell 5.0+ (already included in Windows)
- ✅ Administrator privileges
- ✅ Network access to SecureBootWatcher Dashboard API

## 5-Minute Quick Start

### Step 1: Download Files (1 minute)

Download or clone the repository:

```powershell
# Option 1: Clone repository
git clone https://github.com/robgrame/Nimbus.BootCertWatcher.git
cd Nimbus.BootCertWatcher

# Option 2: Download specific files
# - SecureBootWatcher-Client.ps1
# - appsettings.powershell.json
```

### Step 2: Configure Settings (2 minutes)

Edit `appsettings.powershell.json`:

```json
{
  "SecureBootWatcher": {
    "FleetId": "YOUR-FLEET-ID",
    "Sinks": {
      "EnableWebApi": true,
      "WebApi": {
        "BaseAddress": "https://YOUR-API-URL.azurewebsites.net"
      }
    }
  }
}
```

**Replace**:
- `YOUR-FLEET-ID` with your fleet identifier (e.g., "Production", "Pilot", etc.)
- `YOUR-API-URL` with your dashboard API URL

### Step 3: Test Run (1 minute)

```powershell
# Open PowerShell as Administrator
# Navigate to the directory with the script
cd C:\Path\To\PowerShellClient

# Run the client once
.\SecureBootWatcher-Client.ps1 -ConfigPath .\appsettings.powershell.json
```

✅ **Success**: You should see log output and data should appear in your dashboard!

### Step 4: Create Scheduled Task (1 minute)

```powershell
# Create a daily scheduled task
$action = New-ScheduledTaskAction `
    -Execute "PowerShell.exe" `
    -Argument "-NoProfile -ExecutionPolicy Bypass -File `"C:\Path\To\SecureBootWatcher-Client.ps1`""

$trigger = New-ScheduledTaskTrigger -Daily -At "09:00AM"

$principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest

$settings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries `
    -StartWhenAvailable

Register-ScheduledTask `
    -TaskName "SecureBootWatcher-PowerShell" `
    -Action $action `
    -Trigger $trigger `
    -Principal $principal `
    -Settings $settings
```

### Step 5: Verify (Optional)

```powershell
# Run the test script to validate everything
.\scripts\Test-PowerShellClient.ps1
```

**Done!** 🎉 Your PowerShell client is now collecting inventory and reporting to the dashboard.

---

## Intune Deployment Quick Start

### Step 1: Prepare Package (2 minutes)

```powershell
# Navigate to repository root
cd C:\Path\To\Nimbus.BootCertWatcher

# Run package preparation script
.\scripts\Prepare-PowerShellPackage.ps1 `
    -OutputPath "C:\Temp\PowerShellClient-Package" `
    -ApiBaseUrl "https://your-api.azurewebsites.net" `
    -FleetId "Production"
```

### Step 2: Create .intunewin (2 minutes)

1. Download [Microsoft Win32 Content Prep Tool](https://github.com/microsoft/Microsoft-Win32-Content-Prep-Tool)
2. Run:
   ```powershell
   .\IntuneWinAppUtil.exe `
       -c "C:\Temp\PowerShellClient-Package" `
       -s "Install-PowerShellClient-Intune.ps1" `
       -o "C:\Temp\IntunePackage"
   ```

### Step 3: Upload to Intune (3 minutes)

1. Open **Microsoft Intune Admin Center**
2. Navigate to **Apps** > **Windows** > **Add**
3. Select **Windows app (Win32)**
4. Upload the `.intunewin` file
5. Fill in app information:
   - **Name**: SecureBootWatcher PowerShell Client
   - **Description**: Monitors Secure Boot certificate status
   - **Publisher**: Your Organization

### Step 4: Configure Installation (2 minutes)

**Install command**:
```powershell
PowerShell.exe -ExecutionPolicy Bypass -File "Install-PowerShellClient-Intune.ps1" -ApiBaseUrl "https://your-api.azurewebsites.net" -FleetId "Production"
```

**Uninstall command**:
```powershell
PowerShell.exe -ExecutionPolicy Bypass -File "Uninstall-PowerShellClient-Intune.ps1"
```

**Install behavior**: System  
**Device restart behavior**: No specific action

### Step 5: Configure Detection (1 minute)

- **Rule type**: Use a custom detection script
- **Script file**: Upload `Detect-PowerShellClient-Intune.ps1`
- **Run script as 32-bit**: No
- **Enforce signature check**: No (or Yes if you sign scripts)

### Step 6: Assign and Deploy (1 minute)

1. Set **Requirements** (Windows 10 1809+)
2. Assign to **device groups**
3. Click **Create**

**Done!** 🚀 The app will deploy to your devices automatically.

---

## Common Configuration Scenarios

### Scenario 1: Daily Inventory Collection

```json
{
  "SecureBootWatcher": {
    "FleetId": "Production",
    "RunMode": "Once",
    "Sinks": {
      "EnableWebApi": true,
      "WebApi": {
        "BaseAddress": "https://api.contoso.com"
      }
    }
  }
}
```

**Scheduled Task**: Daily at 9:00 AM with random delay

### Scenario 2: Multiple Reports Per Day

```json
{
  "SecureBootWatcher": {
    "FleetId": "HighPriority",
    "RunMode": "Once"
  }
}
```

**Scheduled Task**: Repeat every 4 hours

### Scenario 3: File Share Reporting

```json
{
  "SecureBootWatcher": {
    "FleetId": "OfficeA",
    "Sinks": {
      "EnableFileShare": true,
      "EnableWebApi": false,
      "FileShare": {
        "RootPath": "\\\\server\\share\\secureboot-reports"
      }
    }
  }
}
```

**Use Case**: Air-gapped environments or backup reporting

### Scenario 4: Command Processing Enabled

```json
{
  "SecureBootWatcher": {
    "FleetId": "Managed",
    "Sinks": {
      "EnableWebApi": true,
      "WebApi": {
        "BaseAddress": "https://api.contoso.com"
      }
    },
    "Commands": {
      "EnableCommandProcessing": true,
      "ProcessBeforeInventory": true
    }
  }
}
```

**Use Case**: Enable remote configuration management

---

## Troubleshooting Quick Fixes

### Issue: "Execution policy prevents script from running"

**Solution**:
```powershell
# Run with Bypass policy
PowerShell.exe -ExecutionPolicy Bypass -File .\SecureBootWatcher-Client.ps1
```

### Issue: "Access denied" or "Insufficient permissions"

**Solution**:
```powershell
# Run as Administrator
# Right-click PowerShell > Run as Administrator
```

### Issue: "API endpoint not reachable"

**Check**:
```powershell
# Test connectivity
Invoke-RestMethod -Uri "https://your-api.azurewebsites.net/health"
```

### Issue: "Secure Boot not enabled"

**Expected**: The client will report this status to the dashboard (not an error)

### Issue: "No data appearing in dashboard"

**Verify**:
1. Check configuration: API URL is correct
2. Check logs: `C:\Program Files\SecureBootWatcher\PowerShell\logs\secureboot-watcher.log`
3. Test connectivity to API
4. Verify scheduled task ran successfully

---

## Next Steps

### For Testing
1. ✅ Run `Test-PowerShellClient.ps1` to validate setup
2. ✅ Check dashboard for incoming data
3. ✅ Review logs for any warnings or errors
4. ✅ Test scheduled task execution

### For Production Deployment
1. ✅ Test on pilot group (10-20 devices)
2. ✅ Monitor for 1-2 weeks
3. ✅ Review dashboard data quality
4. ✅ Expand to production groups
5. ✅ Document any environment-specific configurations

### For Advanced Configuration
1. 📖 Read [PowerShell Client Guide](POWERSHELL_CLIENT.md)
2. 📖 Read [Client Comparison Guide](CLIENT_COMPARISON.md)
3. ⚙️ Customize logging levels
4. ⚙️ Configure command processing
5. ⚙️ Set up multiple fleets

---

## Useful Commands

### View Logs
```powershell
# View recent log entries
Get-Content "C:\Program Files\SecureBootWatcher\PowerShell\logs\secureboot-watcher.log" -Tail 50
```

### Check Scheduled Task
```powershell
# Get task information
Get-ScheduledTask -TaskName "SecureBootWatcher-PowerShell"
Get-ScheduledTaskInfo -TaskName "SecureBootWatcher-PowerShell"
```

### Manual Run
```powershell
# Trigger scheduled task manually
Start-ScheduledTask -TaskName "SecureBootWatcher-PowerShell"
```

### Test Configuration
```powershell
# Validate JSON configuration
Get-Content .\appsettings.json | ConvertFrom-Json
```

---

## Getting Help

### Documentation
- 📖 [Complete PowerShell Client Guide](POWERSHELL_CLIENT.md)
- 📖 [Client Comparison](CLIENT_COMPARISON.md)
- 📖 [Main README](../README.md)

### Support
- 💬 GitHub Issues: https://github.com/robgrame/Nimbus.BootCertWatcher/issues
- 💬 GitHub Discussions: https://github.com/robgrame/Nimbus.BootCertWatcher/discussions

### Community
- Share your experiences
- Report bugs
- Suggest improvements
- Contribute enhancements

---

## Checklist for Success

- [ ] Downloaded PowerShell client files
- [ ] Configured `appsettings.json` with API URL and Fleet ID
- [ ] Tested manual execution successfully
- [ ] Verified data appears in dashboard
- [ ] Created scheduled task (manual or Intune)
- [ ] Ran validation test script
- [ ] Reviewed logs for errors
- [ ] Documented any environment-specific settings
- [ ] Deployed to pilot group (if production)
- [ ] Monitored dashboard for incoming data

---

**Estimated Time**: 
- Manual Setup: ~10 minutes
- Intune Deployment: ~15 minutes (plus rollout time)

**Difficulty**: Easy 🟢

**Support**: Full documentation and community support available

Get started now and have Secure Boot inventory collection running in minutes! 🚀
