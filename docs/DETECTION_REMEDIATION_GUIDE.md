# Detection and Remediation Scripts Guide

## Overview

These scripts enable automated detection and remediation of SecureBootWatcher client installations, particularly useful for:
- **Microsoft Endpoint Manager (Intune)** - Proactive Remediations
- **SCCM/ConfigMgr** - Compliance Settings
- **Custom monitoring solutions**
- **Manual troubleshooting**

---

## Scripts

### Detect-Client.ps1

**Purpose**: Detects if the SecureBootWatcher client is properly installed and configured.

**Exit Codes**:
- `0` = Client is properly installed (detection passed)
- `1` = Client is not installed or misconfigured (detection failed)

**Checks Performed**:
1. ? Installation directory exists
2. ? Main executable exists and version check
3. ? Required dependencies (DLLs) are present
4. ? Configuration file exists and is valid JSON
5. ? Scheduled task exists and is properly configured
6. ? Task runs as SYSTEM account
7. ? Logs directory exists (optional)

**Parameters**:

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `-InstallPath` | String | `C:\Program Files\SecureBootWatcher` | Installation directory |
| `-TaskName` | String | `SecureBootWatcher` | Scheduled task name |
| `-MinimumVersion` | String | `1.0.0.0` | Minimum required version |
| `-Verbose` | Switch | False | Show detailed output |
| `-CheckTaskEnabled` | Switch | False | Fail if task is disabled |

**Usage Examples**:

```powershell
# Basic detection (silent)
.\Detect-Client.ps1

# Verbose detection with output
.\Detect-Client.ps1 -Verbose

# Check specific version
.\Detect-Client.ps1 -MinimumVersion "1.3.0.0" -Verbose

# Require task to be enabled
.\Detect-Client.ps1 -CheckTaskEnabled -Verbose

# Custom installation path
.\Detect-Client.ps1 -InstallPath "C:\Apps\SecureBootWatcher" -Verbose
```

**Output Example (Verbose)**:

```
[INFO] Checking installation directory: C:\Program Files\SecureBootWatcher
[OK] Installation directory exists
[INFO] Checking executable: C:\Program Files\SecureBootWatcher\SecureBootWatcher.Client.exe
[OK] Executable found
[INFO] Current version: 1.3.21.39942
[INFO] Checking required dependencies...
[OK] All required dependencies found
[INFO] Checking configuration: C:\Program Files\SecureBootWatcher\appsettings.json
[OK] Configuration file exists
[OK] Configuration has at least one sink enabled
[INFO] Checking scheduled task: SecureBootWatcher
[OK] Scheduled task exists
[INFO] Task state: Ready
[OK] Scheduled task is enabled
[OK] Task executable path is correct
[OK] Task runs as SYSTEM
[OK] Logs directory exists
[OK] Found 3 recent log file(s)
----------------------------------------
[OK] DETECTION PASSED - Client is properly installed

Installation Details:
  Location: C:\Program Files\SecureBootWatcher
  Executable: C:\Program Files\SecureBootWatcher\SecureBootWatcher.Client.exe
  Scheduled Task: SecureBootWatcher
  Task State: Ready
  Next Run: 11/9/2025 9:00:00 AM
```

---

### Remediate-Client.ps1

**Purpose**: Automatically fixes common issues with the SecureBootWatcher client installation.

**Exit Codes**:
- `0` = Remediation successful or not needed
- `1` = Remediation attempted but some issues could not be fixed

**Remediation Actions**:
1. ? Creates logs directory if missing
2. ? Recreates scheduled task if missing
3. ? Enables scheduled task if disabled
4. ? Fixes file permissions for SYSTEM account
5. ? Clears temporary data/lock files

**Parameters**:

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `-InstallPath` | String | `C:\Program Files\SecureBootWatcher` | Installation directory |
| `-TaskName` | String | `SecureBootWatcher` | Scheduled task name |
| `-TaskTime` | String | `09:00AM` | Start time for recreated task |
| `-ScheduleType` | String | `Daily` | Schedule type (Once/Daily/Hourly/Custom) |
| `-RepeatEveryHours` | Int | `4` | Repeat interval for Custom schedule |
| `-Verbose` | Switch | False | Show detailed output |

**Usage Examples**:

```powershell
# Basic remediation (silent)
.\Remediate-Client.ps1

# Verbose remediation with output
.\Remediate-Client.ps1 -Verbose

# Recreate task with custom schedule
.\Remediate-Client.ps1 -ScheduleType Custom -RepeatEveryHours 6 -Verbose

# Recreate task with different start time
.\Remediate-Client.ps1 -TaskTime "08:00AM" -Verbose

# Custom installation path
.\Remediate-Client.ps1 -InstallPath "C:\Apps\SecureBootWatcher" -Verbose
```

**Output Example (Verbose)**:

```
[INFO] Starting SecureBootWatcher client remediation
[INFO] Creating logs directory: C:\Program Files\SecureBootWatcher\logs
[OK] Logs directory created
[INFO] Scheduled task not found, attempting to recreate
[OK] Scheduled task recreated successfully
----------------------------------------
[OK] REMEDIATION COMPLETED - Client issues have been fixed
```

---

## Microsoft Endpoint Manager (Intune) Configuration

### Proactive Remediations

**Step 1**: Create Detection Script
1. Navigate to: **Devices** > **Scripts and remediations** > **Proactive remediations**
2. Click **+ Create**
3. Name: `SecureBootWatcher Client Health Check`
4. Upload Detection script: `Detect-Client.ps1`

**Step 2**: Create Remediation Script
1. Upload Remediation script: `Remediate-Client.ps1`
2. Configure settings:
   - **Run this script using the logged-on credentials**: No (run as SYSTEM)
   - **Enforce script signature check**: No (unless you sign the scripts)
   - **Run script in 64-bit PowerShell**: Yes

**Step 3**: Configure Schedule
- **Run schedule**: Daily
- **Run time**: After business hours (e.g., 10 PM)

**Step 4**: Assign to Groups
- Select device groups to monitor
- Click **Review + Create**

**Expected Behavior**:
1. Detection script runs daily on assigned devices
2. If detection fails (exit 1), remediation script runs automatically
3. Reports available in Intune portal under **Proactive remediations**

---

## SCCM/ConfigMgr Configuration

### Compliance Settings

**Step 1**: Create Configuration Item
1. **Assets and Compliance** > **Compliance Settings** > **Configuration Items**
2. Right-click > **Create Configuration Item**
3. Name: `SecureBootWatcher Client Installation`
4. Type: **Windows Desktops and Servers**

**Step 2**: Add Detection Rule
1. **Settings** tab > **New** > **Script**
2. Setting type: **Script**
3. Data type: **Integer**
4. Discovery script:
   ```powershell
   # Wrapper for SCCM
   & "C:\Path\To\Detect-Client.ps1"
   if ($LASTEXITCODE -eq 0) {
       Write-Output 1  # Compliant
   } else {
       Write-Output 0  # Non-compliant
   }
   ```

**Step 3**: Add Compliance Rule
1. Rule type: **Value**
2. Value equals: `1`
3. Non-compliance severity: **Warning** or **Critical**

**Step 4**: Create Remediation (Optional)
1. **Remediation** tab
2. Remediation script: `Remediate-Client.ps1`

**Step 5**: Create Baseline
1. **Configuration Baselines** > **Create Configuration Baseline**
2. Add the configuration item
3. Deploy to collections

---

## Manual Troubleshooting

### Quick Health Check

```powershell
# Run verbose detection
.\scripts\Detect-Client.ps1 -Verbose

# If detection fails, run remediation
.\scripts\Remediate-Client.ps1 -Verbose

# Re-run detection to verify
.\scripts\Detect-Client.ps1 -Verbose
```

### Common Issues and Fixes

#### Issue: Scheduled Task Missing

**Detection Output**:
```
[ERROR] Scheduled task not found
```

**Fix**:
```powershell
.\scripts\Remediate-Client.ps1 -Verbose
```

Or manually:
```powershell
.\scripts\Deploy-Client.ps1 `
    -PackageZipPath ".\client-package\SecureBootWatcher-Client.zip" `
    -CreateScheduledTask
```

#### Issue: Executable Not Found

**Detection Output**:
```
[ERROR] Executable not found
```

**Fix**: Reinstall the client
```powershell
.\scripts\Deploy-Client.ps1 `
    -PackageZipPath ".\client-package\SecureBootWatcher-Client.zip" `
    -CreateScheduledTask
```

#### Issue: Version Outdated

**Detection Output**:
```
[ERROR] Version 1.2.0.0 is older than minimum required 1.3.0.0
```

**Fix**: Deploy updated package
```powershell
# First, build new package
.\scripts\Deploy-Client.ps1 -ApiBaseUrl "https://your-api.com"

# Then deploy
.\scripts\Deploy-Client.ps1 `
    -PackageZipPath ".\client-package\SecureBootWatcher-Client.zip" `
    -CreateScheduledTask
```

#### Issue: Task Disabled

**Detection Output**:
```
[ERROR] Scheduled task is disabled
```

**Fix**:
```powershell
.\scripts\Remediate-Client.ps1 -Verbose
```

Or manually:
```powershell
Enable-ScheduledTask -TaskName "SecureBootWatcher"
```

#### Issue: Configuration Invalid

**Detection Output**:
```
[ERROR] Configuration file is not valid JSON
```

**Fix**: Restore from package or manually edit
```powershell
# Restore from package
Expand-Archive -Path ".\client-package\SecureBootWatcher-Client.zip" `
    -DestinationPath "C:\Temp\SecureBootRestore"

Copy-Item "C:\Temp\SecureBootRestore\appsettings.json" `
    -Destination "C:\Program Files\SecureBootWatcher\" `
    -Force
```

---

## Automated Monitoring with Task Scheduler

### Daily Health Check

Create a scheduled task that runs detection and sends email alerts:

```powershell
$action = New-ScheduledTaskAction -Execute "powershell.exe" -Argument `
    "-NoProfile -ExecutionPolicy Bypass -File `"C:\Scripts\Detect-Client.ps1`" -Verbose"

$trigger = New-ScheduledTaskTrigger -Daily -At "6:00AM"

$principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount

Register-ScheduledTask `
    -TaskName "SecureBootWatcher-HealthCheck" `
    -Action $action `
    -Trigger $trigger `
    -Principal $principal `
    -Description "Daily health check for SecureBootWatcher client"
```

---

## Integration with Monitoring Tools

### Splunk

```powershell
# Run detection and forward to Splunk
$result = & ".\Detect-Client.ps1" -Verbose 2>&1
$exitCode = $LASTEXITCODE

$event = @{
    timestamp = Get-Date -Format "yyyy-MM-ddTHH:mm:ss"
    hostname = $env:COMPUTERNAME
    check = "SecureBootWatcher-Detection"
    result = if ($exitCode -eq 0) { "PASS" } else { "FAIL" }
    exit_code = $exitCode
    details = $result -join "`n"
}

# Send to Splunk HEC
Invoke-RestMethod -Uri "https://splunk.contoso.com:8088/services/collector" `
    -Method Post `
    -Headers @{ Authorization = "Splunk YOUR-HEC-TOKEN" } `
    -Body ($event | ConvertTo-Json)
```

### Azure Monitor

```powershell
# Run detection and send to Azure Monitor
$result = & ".\Detect-Client.ps1" -Verbose 2>&1
$exitCode = $LASTEXITCODE

$logEntry = @{
    Computer = $env:COMPUTERNAME
    CheckName = "SecureBootWatcher-Detection"
    Status = if ($exitCode -eq 0) { "Healthy" } else { "Unhealthy" }
    ExitCode = $exitCode
    Details = $result -join "`n"
}

# Send to Log Analytics workspace
# (Requires Azure Monitor Agent or custom data collection)
```

---

## Event Log Integration

Both scripts write events to Windows Event Log:

**Event Source**: `SecureBootWatcher`  
**Event Log**: `Application`  
**Event ID**: `1000`

**View Events**:
```powershell
Get-EventLog -LogName Application -Source "SecureBootWatcher" -Newest 10
```

**Filter Events**:
```powershell
# Show only errors
Get-EventLog -LogName Application -Source "SecureBootWatcher" `
    -EntryType Error -Newest 10

# Show events from last 24 hours
Get-EventLog -LogName Application -Source "SecureBootWatcher" `
    -After (Get-Date).AddDays(-1)
```

---

## Testing

### Test Detection Script

```powershell
# Test 1: Client installed (should return 0)
.\scripts\Detect-Client.ps1 -Verbose
Write-Host "Exit Code: $LASTEXITCODE"

# Test 2: Minimum version check
.\scripts\Detect-Client.ps1 -MinimumVersion "2.0.0.0" -Verbose
Write-Host "Exit Code: $LASTEXITCODE"

# Test 3: Require enabled task
.\scripts\Detect-Client.ps1 -CheckTaskEnabled -Verbose
Write-Host "Exit Code: $LASTEXITCODE"
```

### Test Remediation Script

```powershell
# Test: Disable task, then remediate
Disable-ScheduledTask -TaskName "SecureBootWatcher"

.\scripts\Remediate-Client.ps1 -Verbose
Write-Host "Exit Code: $LASTEXITCODE"

# Verify fix
Get-ScheduledTask -TaskName "SecureBootWatcher" | Select-Object TaskName, State
```

---

## Best Practices

### Detection
- ? Run detection frequently (daily or hourly)
- ? Use `-CheckTaskEnabled` in production
- ? Set appropriate minimum version
- ? Monitor exit codes in your management platform

### Remediation
- ?? Test remediation in pilot group first
- ?? Review remediation logs regularly
- ?? Set up alerts for repeated remediation attempts
- ?? Escalate if remediation fails multiple times

### Monitoring
- ?? Track detection success rate
- ?? Monitor remediation frequency
- ?? Alert on version drift
- ?? Dashboard for fleet health

---

## Troubleshooting

### Scripts Not Running

**Issue**: PowerShell execution policy

**Fix**:
```powershell
Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy Bypass -Force
```

### Permission Denied

**Issue**: Scripts require elevation

**Fix**: Run PowerShell as Administrator

### Task Recreation Fails

**Issue**: Executable not found

**Fix**: Ensure client is deployed before running remediation

---

## Additional Resources

- **Deployment Guide**: `docs/DEPLOYMENT_GUIDE.md`
- **Client Scripts**: `docs/CLIENT_DEPLOYMENT_SCRIPTS.md`
- **Main README**: `README.md`

---

**Created**: 2025-11-08  
**Version**: 1.0  
**Scripts**: Detect-Client.ps1, Remediate-Client.ps1
