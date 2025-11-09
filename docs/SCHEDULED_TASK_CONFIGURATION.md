# Scheduled Task Configuration Guide

## Overview

This guide explains how to configure the SecureBootWatcher client scheduled task execution frequency and timing for both Intune and manual deployments.

## Intune Win32 App Deployment

### Parameters

The `Install-Client-Intune.ps1` script supports flexible scheduled task configuration via parameters.

| Parameter | Type | Range | Default | Description |
|-----------|------|-------|---------|-------------|
| `ScheduleType` | string | Once, Daily, Hourly, Custom | "Daily" | Execution frequency |
| `TaskTime` | string | HH:MM AM/PM | "09:00AM" | Start time |
| `RepeatEveryHours` | int | 1-24 | 4 | Interval for Custom schedule |
| `RandomDelayMinutes` | int | 0-1440 | 60 | Random delay range (prevents API flooding) |

### Schedule Types

#### Daily
Runs once per day at specified time.

```powershell
-ScheduleType "Daily" -TaskTime "09:00AM" -RandomDelayMinutes 60
```

**Use case**: Standard monitoring, low-frequency updates

**Example execution times** (with 60-min random delay):
- 09:00 AM ± 0-60 minutes
- Could run anywhere between 09:00 AM and 10:00 AM

#### Hourly
Runs every hour, starting at specified time.

```powershell
-ScheduleType "Hourly" -TaskTime "08:00AM" -RandomDelayMinutes 30
```

**Use case**: High-frequency monitoring, critical environments

**Example execution times**:
- First: 08:00 AM ± 0-30 minutes
- Then: Every hour (09:00 AM, 10:00 AM, 11:00 AM, ...)

#### Custom
Runs every N hours (1-24), starting at specified time.

```powershell
-ScheduleType "Custom" -RepeatEveryHours 4 -TaskTime "00:00AM" -RandomDelayMinutes 15
```

**Use case**: Balanced monitoring, customizable frequency

**Example execution times** (every 4 hours):
- 00:00 AM ± 0-15 minutes
- 04:00 AM ± 0-15 minutes
- 08:00 AM ± 0-15 minutes
- 12:00 PM ± 0-15 minutes
- 04:00 PM ± 0-15 minutes
- 08:00 PM ± 0-15 minutes

#### Once
Runs one time at specified time (for testing).

```powershell
-ScheduleType "Once" -TaskTime "10:00AM" -RandomDelayMinutes 0
```

**Use case**: Testing, one-time execution

**Example**: Runs at 10:00 AM exactly (no random delay)

## Random Delay

### Purpose
Random delay prevents all devices from hitting the API simultaneously, which could cause:
- API overload
- Queue backlog
- Network congestion
- Slower response times

### How It Works

```
Scheduled Time: 09:00 AM
Random Delay: 60 minutes
Actual Execution: Anywhere between 09:00 AM and 10:00 AM
```

Each device randomly picks a time within the delay window.

### Recommended Values

| Fleet Size | Random Delay |
|------------|--------------|
| 1-10 devices | 0-15 minutes |
| 10-50 devices | 15-30 minutes |
| 50-200 devices | 30-60 minutes |
| 200-500 devices | 60-120 minutes |
| 500+ devices | 120-240 minutes |

### Disable Random Delay

For testing or single-device deployments:

```powershell
-RandomDelayMinutes 0
```

**Warning**: Don't use for production with multiple devices!

## Configuration Examples

### Scenario: Standard Corporate Environment

**Requirements:**
- 150 devices
- Business hours: 8 AM - 6 PM
- Daily reports acceptable

**Configuration:**
```powershell
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Install-Client-Intune.ps1" `
    -ScheduleType "Daily" `
    -TaskTime "09:00AM" `
    -RandomDelayMinutes 60
```

**Result**: Runs once daily between 9:00 AM and 10:00 AM

---

### Scenario: High-Security Environment

**Requirements:**
- 50 devices
- Frequent monitoring needed
- Must detect changes within 1 hour

**Configuration:**
```powershell
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Install-Client-Intune.ps1" `
    -ScheduleType "Hourly" `
    -TaskTime "00:00AM" `
    -RandomDelayMinutes 15
```

**Result**: Runs every hour with 0-15 minute random delay

---

### Scenario: Balanced Monitoring

**Requirements:**
- 300 devices
- Check 4 times per day
- Off-hours execution preferred

**Configuration:**
```powershell
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Install-Client-Intune.ps1" `
    -ScheduleType "Custom" `
    -RepeatEveryHours 6 `
    -TaskTime "00:00AM" `
    -RandomDelayMinutes 90
```

**Result**: Runs at 12 AM, 6 AM, 12 PM, 6 PM (±90 minutes)

---

### Scenario: Off-Hours Only

**Requirements:**
- 500 devices
- Minimize business hours impact
- Once daily acceptable

**Configuration:**
```powershell
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Install-Client-Intune.ps1" `
    -ScheduleType "Daily" `
    -TaskTime "02:00AM" `
    -RandomDelayMinutes 180
```

**Result**: Runs between 2:00 AM and 5:00 AM

---

### Scenario: Development/Testing

**Requirements:**
- 5 test devices
- Immediate execution for testing

**Configuration:**
```powershell
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Install-Client-Intune.ps1" `
    -ScheduleType "Hourly" `
    -TaskTime "08:00AM" `
    -RandomDelayMinutes 0
```

**Result**: Runs every hour on the hour (no delay)

## Intune Command Examples

### Basic Commands

**Default (Daily at 9 AM)**:
```powershell
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Install-Client-Intune.ps1"
```

**Hourly**:
```powershell
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Install-Client-Intune.ps1" -ScheduleType "Hourly"
```

**Every 4 hours**:
```powershell
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Install-Client-Intune.ps1" -ScheduleType "Custom" -RepeatEveryHours 4
```

### Advanced Commands

**Every 6 hours starting at midnight with 30-minute delay**:
```powershell
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Install-Client-Intune.ps1" -ScheduleType "Custom" -RepeatEveryHours 6 -TaskTime "00:00AM" -RandomDelayMinutes 30
```

**Daily at 2 AM with 2-hour random window**:
```powershell
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Install-Client-Intune.ps1" -ScheduleType "Daily" -TaskTime "02:00AM" -RandomDelayMinutes 120
```

**Hourly with minimal delay**:
```powershell
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Install-Client-Intune.ps1" -ScheduleType "Hourly" -TaskTime "08:00AM" -RandomDelayMinutes 5
```

### Complete Production Example

```powershell
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Install-Client-Intune.ps1" `
    -ApiBaseUrl "https://secureboot-api.contoso.com" `
    -FleetId "production-fleet" `
    -CertificatePassword "%CERT_PASSWORD%" `
    -ScheduleType "Custom" `
    -RepeatEveryHours 6 `
    -TaskTime "00:00AM" `
    -RandomDelayMinutes 60
```

## Manual Deployment (Deploy-Client.ps1)

For manual deployments using `Deploy-Client.ps1`, use the same parameters:

```powershell
.\Deploy-Client.ps1 `
    -PackageZipPath ".\release\1.0.0\SecureBootWatcher-Client-1.0.0.zip" `
    -CreateScheduledTask `
    -ScheduleType "Custom" `
    -RepeatEveryHours 4 `
    -TaskTime "08:00AM"
```

## Verifying Configuration

### Check Scheduled Task

```powershell
# View task details
Get-ScheduledTask -TaskName "SecureBootWatcher" | Format-List *

# View triggers
Get-ScheduledTask -TaskName "SecureBootWatcher" | 
    Select-Object -ExpandProperty Triggers

# View last run time
Get-ScheduledTaskInfo -TaskName "SecureBootWatcher"
```

### Check Installation Log

```powershell
Get-Content "C:\ProgramData\SecureBootWatcher\install.log" -Tail 20
```

Look for lines like:
```
2025-01-15 10:30:00 - Scheduled task created successfully
2025-01-15 10:30:00 -   Schedule: Every 4 hours starting at 08:00AM (±15 min)
2025-01-15 10:30:00 -   Random delay: 0-15 minutes
```

## Modifying Existing Task

### Option 1: Reinstall via Intune

1. Update install command in Intune
2. Redeploy to device group
3. Task will be recreated with new schedule

### Option 2: Manual PowerShell

```powershell
# Remove existing task
Unregister-ScheduledTask -TaskName "SecureBootWatcher" -Confirm:$false

# Recreate with new schedule
$action = New-ScheduledTaskAction -Execute "C:\Program Files\SecureBootWatcher\SecureBootWatcher.Client.exe" -WorkingDirectory "C:\Program Files\SecureBootWatcher"

$trigger = New-ScheduledTaskTrigger -Once -At "08:00AM" -RepetitionInterval (New-TimeSpan -Hours 4) -RepetitionDuration ([TimeSpan]::MaxValue)
$trigger.RandomDelay = New-TimeSpan -Minutes 15

$principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest

$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable -MultipleInstances IgnoreNew

Register-ScheduledTask -TaskName "SecureBootWatcher" -Action $action -Trigger $trigger -Principal $principal -Settings $settings
```

### Option 3: Task Scheduler GUI

1. Open Task Scheduler (`taskschd.msc`)
2. Navigate to Task Scheduler Library
3. Find "SecureBootWatcher" task
4. Right-click ? Properties
5. Modify triggers and settings
6. Click OK

## Best Practices

### ? DO

- **Use random delay** for fleets >10 devices
- **Test schedule** on pilot group first
- **Monitor API load** after deployment
- **Document configuration** in change log
- **Consider time zones** for global deployments
- **Align with maintenance windows** if applicable

### ? DON'T

- **Disable random delay** in production (>10 devices)
- **Schedule during peak hours** for large fleets
- **Use "Once" schedule** in production
- **Set delay >4 hours** (defeats purpose of monitoring)
- **Forget to test** before mass deployment

## Troubleshooting

### Task Not Running

```powershell
# Check if task exists
Get-ScheduledTask -TaskName "SecureBootWatcher"

# Check last run time
Get-ScheduledTaskInfo -TaskName "SecureBootWatcher"

# Check for errors
Get-WinEvent -LogName "Microsoft-Windows-TaskScheduler/Operational" -MaxEvents 50 | 
    Where-Object {$_.Message -like "*SecureBootWatcher*"}
```

### Task Running Too Frequently

Check trigger configuration:
```powershell
Get-ScheduledTask -TaskName "SecureBootWatcher" | 
    Select-Object -ExpandProperty Triggers |
    Format-List *
```

### Task Not Running at Expected Time

Verify random delay:
```powershell
$task = Get-ScheduledTask -TaskName "SecureBootWatcher"
$trigger = $task.Triggers[0]
Write-Host "Random Delay: $($trigger.RandomDelay)"
```

## Summary

| Configuration | Command |
|---------------|---------|
| **Default** | No parameters needed |
| **Hourly** | `-ScheduleType "Hourly"` |
| **Every N hours** | `-ScheduleType "Custom" -RepeatEveryHours N` |
| **Daily at time** | `-ScheduleType "Daily" -TaskTime "HH:MM AM/PM"` |
| **With delay** | Add `-RandomDelayMinutes N` |

**Most Common**: `-ScheduleType "Custom" -RepeatEveryHours 6 -RandomDelayMinutes 30`

---

**Related Documentation:**
- [INTUNE_WIN32_DEPLOYMENT.md](INTUNE_WIN32_DEPLOYMENT.md)
- [DEPLOYMENT_GUIDE.md](DEPLOYMENT_GUIDE.md)
- [Install-Client-Intune.ps1](../scripts/Install-Client-Intune.ps1)
- [Deploy-Client.ps1](../scripts/Deploy-Client.ps1)

**Last Updated:** 2025-01-09  
**Version:** 1.0
