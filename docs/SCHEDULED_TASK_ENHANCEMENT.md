# Scheduled Task Configuration - Feature Summary

## Overview

The `Install-Client-Intune.ps1` script has been enhanced to support flexible scheduled task configuration, matching the capabilities of `Deploy-Client.ps1`.

## What Changed

### Before

**Fixed Schedule:**
- Always created daily task at 9:00 AM
- Random delay: 0-60 minutes (hardcoded)
- No customization options

```powershell
# Only option was basic install
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Install-Client-Intune.ps1"
```

### After

**Flexible Configuration:**
- Multiple schedule types: Once, Daily, Hourly, Custom
- Configurable start time
- Configurable random delay (0-1440 minutes)
- Custom interval support (1-24 hours)

```powershell
# Now supports full customization
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Install-Client-Intune.ps1" `
    -ScheduleType "Custom" `
    -RepeatEveryHours 4 `
    -TaskTime "08:00AM" `
    -RandomDelayMinutes 15
```

## New Parameters

| Parameter | Type | Range | Default | Description |
|-----------|------|-------|---------|-------------|
| `ScheduleType` | string | Once/Daily/Hourly/Custom | Daily | Execution frequency |
| `TaskTime` | string | HH:MM AM/PM | 09:00AM | Start time |
| `RepeatEveryHours` | int | 1-24 | 4 | Interval for Custom |
| `RandomDelayMinutes` | int | 0-1440 | 60 | Random delay range |

## Usage Examples

### Daily (Default)

```powershell
# Simple daily execution at 9 AM
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Install-Client-Intune.ps1"

# Daily at custom time
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Install-Client-Intune.ps1" `
    -ScheduleType "Daily" `
    -TaskTime "02:00AM" `
    -RandomDelayMinutes 120
```

### Hourly

```powershell
# Every hour with 30-minute random delay
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Install-Client-Intune.ps1" `
    -ScheduleType "Hourly" `
    -TaskTime "08:00AM" `
    -RandomDelayMinutes 30
```

### Custom Interval

```powershell
# Every 4 hours
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Install-Client-Intune.ps1" `
    -ScheduleType "Custom" `
    -RepeatEveryHours 4

# Every 6 hours starting at midnight
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Install-Client-Intune.ps1" `
    -ScheduleType "Custom" `
    -RepeatEveryHours 6 `
    -TaskTime "00:00AM" `
    -RandomDelayMinutes 30
```

### Once (Testing)

```powershell
# Single execution at specified time
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Install-Client-Intune.ps1" `
    -ScheduleType "Once" `
    -TaskTime "10:00AM" `
    -RandomDelayMinutes 0
```

## Recommended Configurations

### Standard Corporate (150 devices)

```powershell
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Install-Client-Intune.ps1" `
    -ScheduleType "Daily" `
    -TaskTime "09:00AM" `
    -RandomDelayMinutes 60
```

**Result:** Runs once daily between 9:00 AM and 10:00 AM

### High-Security (50 devices)

```powershell
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Install-Client-Intune.ps1" `
    -ScheduleType "Hourly" `
    -TaskTime "00:00AM" `
    -RandomDelayMinutes 15
```

**Result:** Runs every hour with 0-15 minute random delay

### Balanced (300 devices)

```powershell
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Install-Client-Intune.ps1" `
    -ScheduleType "Custom" `
    -RepeatEveryHours 6 `
    -TaskTime "00:00AM" `
    -RandomDelayMinutes 90
```

**Result:** Runs 4 times daily (12 AM, 6 AM, 12 PM, 6 PM) with ±90 min delay

### Off-Hours (500 devices)

```powershell
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Install-Client-Intune.ps1" `
    -ScheduleType "Daily" `
    -TaskTime "02:00AM" `
    -RandomDelayMinutes 180
```

**Result:** Runs between 2:00 AM and 5:00 AM

## Random Delay Purpose

Prevents API overload by spreading executions over time:

```
Without Random Delay:
500 devices hit API at exactly 09:00 AM
? API overload, slow responses, queue backlog

With 60-Minute Random Delay:
Devices execute anywhere from 09:00 AM to 10:00 AM
? ~8-9 requests per minute
? Smooth API load, fast responses
```

### Recommended Delays

| Fleet Size | Random Delay |
|------------|--------------|
| 1-10 | 0-15 min |
| 10-50 | 15-30 min |
| 50-200 | 30-60 min |
| 200-500 | 60-120 min |
| 500+ | 120-240 min |

## Installation Log

The installation log now includes schedule details:

```
2025-01-15 10:30:00 - Starting SecureBootWatcher installation
2025-01-15 10:30:00 - Schedule configuration: Type=Custom, Time=00:00AM, RandomDelay=30 min
...
2025-01-15 10:30:15 - Scheduled task created successfully
2025-01-15 10:30:15 -   Schedule: Every 6 hours starting at 00:00AM (±30 min)
2025-01-15 10:30:15 -   Random delay: 0-30 minutes
2025-01-15 10:30:15 -   Task name: SecureBootWatcher
2025-01-15 10:30:15 -   Run as: SYSTEM
```

## Verification

### Check Task Configuration

```powershell
# View task details
Get-ScheduledTask -TaskName "SecureBootWatcher" | Format-List *

# View triggers
Get-ScheduledTask -TaskName "SecureBootWatcher" | 
    Select-Object -ExpandProperty Triggers

# Check last run
Get-ScheduledTaskInfo -TaskName "SecureBootWatcher"
```

### Check Installation Log

```powershell
Get-Content "C:\ProgramData\SecureBootWatcher\install.log" -Tail 20
```

## Comparison with Deploy-Client.ps1

Both scripts now have feature parity for scheduled task configuration:

| Feature | Deploy-Client.ps1 | Install-Client-Intune.ps1 |
|---------|-------------------|---------------------------|
| Daily schedule | ? | ? |
| Hourly schedule | ? | ? |
| Custom interval | ? | ? |
| Once schedule | ? | ? |
| Custom start time | ? | ? |
| Random delay | ? | ? |
| Configurable delay | ? | ? |

## Migration

### From Old Version

If you're updating from the previous version:

**Old command:**
```powershell
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Install-Client-Intune.ps1"
```

**New command (same behavior):**
```powershell
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Install-Client-Intune.ps1" `
    -ScheduleType "Daily" `
    -TaskTime "09:00AM" `
    -RandomDelayMinutes 60
```

Or simply use defaults (no change needed):
```powershell
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Install-Client-Intune.ps1"
```

### Updating Existing Deployments

To change schedule on already-deployed devices:

1. **Update Intune install command** with new parameters
2. **Redeploy** to device group
3. Script will **remove and recreate** scheduled task

Or manually via PowerShell on device:
```powershell
# Remove old task
Unregister-ScheduledTask -TaskName "SecureBootWatcher" -Confirm:$false

# Run install script with new parameters
cd "C:\Program Files\SecureBootWatcher"
.\Install-Client-Intune.ps1 -ScheduleType "Hourly" -RandomDelayMinutes 30
```

## Documentation

New and updated documentation:

- ? **[SCHEDULED_TASK_CONFIGURATION.md](SCHEDULED_TASK_CONFIGURATION.md)** - Complete guide
- ? **[INTUNE_WIN32_DEPLOYMENT.md](INTUNE_WIN32_DEPLOYMENT.md)** - Updated with new parameters
- ? **[Install-Client-Intune.ps1](../scripts/Install-Client-Intune.ps1)** - Script updated

## Benefits

### Before
- ? Fixed 9 AM schedule only
- ? No customization
- ? Limited to daily execution
- ? Fixed random delay

### After
- ? Flexible schedule types
- ? Customizable start time
- ? Hourly, custom intervals
- ? Configurable random delay
- ? Feature parity with Deploy-Client.ps1
- ? Production-ready for all scenarios

## Summary

The enhanced `Install-Client-Intune.ps1` script now provides:

? **Full flexibility** for scheduled task configuration  
? **Feature parity** with Deploy-Client.ps1  
? **Production-ready** for all deployment scenarios  
? **Backward compatible** (defaults unchanged)  
? **Well documented** with examples and best practices  

**Most common production command:**

```powershell
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Install-Client-Intune.ps1" `
    -ApiBaseUrl "https://your-api.com" `
    -FleetId "production" `
    -CertificatePassword "%CERT_PASSWORD%" `
    -ScheduleType "Custom" `
    -RepeatEveryHours 6 `
    -RandomDelayMinutes 60
```

This provides balanced monitoring (4x daily) with appropriate randomization for most environments.

---

**Version:** 1.0  
**Last Updated:** 2025-01-09  
**Related:** [SCHEDULED_TASK_CONFIGURATION.md](SCHEDULED_TASK_CONFIGURATION.md), [INTUNE_WIN32_DEPLOYMENT.md](INTUNE_WIN32_DEPLOYMENT.md)
