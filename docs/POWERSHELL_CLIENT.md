# SecureBootWatcher PowerShell Client

## Overview

The SecureBootWatcher PowerShell Client is a lightweight, PowerShell-based inventory collection tool that monitors Secure Boot certificate status on Windows devices and reports to the SecureBootWatcher Dashboard. It provides the same core inventory features as the .NET Framework client but is designed for easy deployment via device management solutions like Microsoft Intune or Configuration Manager.

## Key Features

- **Pure PowerShell Implementation**: No compiled binaries required, easier to deploy and manage
- **Same Inventory Features**: Collects identical data as the .NET Framework client
  - Device identity (manufacturer, model, firmware, OS details)
  - Registry snapshot (Secure Boot settings and status)
  - Device attributes and telemetry policy
  - Event log reading for Secure Boot events
  - Certificate enumeration from UEFI databases (db, dbx, KEK, PK)
- **Multiple Sinks**: Support for Web API, File Share (Azure Queue not implemented)
- **Command Processing**: Execute configuration commands from the dashboard
- **Flexible Scheduling**: Run once or continuously with configurable intervals
- **Easy Intune Deployment**: Includes installation, detection, and uninstall scripts
- **No Auto-Update**: Simplified deployment without update complexity

## Requirements

### Client Device Requirements
- Windows 10 version 1809 or later / Windows 11
- Windows Server 2016 or later with UEFI firmware
- PowerShell 5.0 or later (5.1 recommended)
- UEFI Secure Boot capable hardware
- Administrator/SYSTEM privileges
- Network connectivity to dashboard API or file share

### Dashboard Requirements
- SecureBootWatcher Dashboard API v1.0+
- Compatible endpoints:
  - `POST /api/SecureBootReports` - Report ingestion
  - `GET /api/Commands/pending/{deviceId}` - Command fetch (optional)
  - `POST /api/Commands/result` - Command result reporting (optional)

## Installation

### Option 1: Manual Installation

1. **Copy files to the target device:**
   ```powershell
   # Create installation directory
   New-Item -ItemType Directory -Path "C:\Program Files\SecureBootWatcher\PowerShell" -Force
   
   # Copy script and configuration
   Copy-Item SecureBootWatcher-Client.ps1 "C:\Program Files\SecureBootWatcher\PowerShell\"
   Copy-Item appsettings.powershell.json "C:\Program Files\SecureBootWatcher\PowerShell\appsettings.json"
   ```

2. **Configure appsettings.json:**
   ```json
   {
     "SecureBootWatcher": {
       "FleetId": "your-fleet-id",
       "Sinks": {
         "EnableWebApi": true,
         "WebApi": {
           "BaseAddress": "https://your-dashboard-api.azurewebsites.net",
           "IngestionRoute": "/api/SecureBootReports"
         }
       }
     }
   }
   ```

3. **Create scheduled task:**
   ```powershell
   $action = New-ScheduledTaskAction `
       -Execute "PowerShell.exe" `
       -Argument "-NoProfile -ExecutionPolicy Bypass -File `"C:\Program Files\SecureBootWatcher\PowerShell\SecureBootWatcher-Client.ps1`""
   
   $trigger = New-ScheduledTaskTrigger -Daily -At "09:00AM" -RandomDelay (New-TimeSpan -Minutes 60)
   
   $principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest
   
   $settings = New-ScheduledTaskSettingsSet `
       -AllowStartIfOnBatteries `
       -DontStopIfGoingOnBatteries `
       -StartWhenAvailable `
       -ExecutionTimeLimit (New-TimeSpan -Hours 2)
   
   Register-ScheduledTask `
       -TaskName "SecureBootWatcher-PowerShell" `
       -Action $action `
       -Trigger $trigger `
       -Principal $principal `
       -Settings $settings
   ```

### Option 2: Intune Win32 App Deployment

1. **Prepare the package:**
   ```powershell
   # Create package directory
   New-Item -ItemType Directory -Path ".\SecureBootWatcher-PowerShell-Package" -Force
   
   # Copy required files
   Copy-Item SecureBootWatcher-Client.ps1 ".\SecureBootWatcher-PowerShell-Package\"
   Copy-Item appsettings.powershell.json ".\SecureBootWatcher-PowerShell-Package\"
   Copy-Item scripts\Install-PowerShellClient-Intune.ps1 ".\SecureBootWatcher-PowerShell-Package\"
   Copy-Item scripts\Detect-PowerShellClient-Intune.ps1 ".\SecureBootWatcher-PowerShell-Package\"
   Copy-Item scripts\Uninstall-PowerShellClient-Intune.ps1 ".\SecureBootWatcher-PowerShell-Package\"
   ```

2. **Create .intunewin package:**
   ```powershell
   # Download Microsoft Win32 Content Prep Tool if needed
   # https://github.com/microsoft/Microsoft-Win32-Content-Prep-Tool
   
   .\IntuneWinAppUtil.exe `
       -c ".\SecureBootWatcher-PowerShell-Package" `
       -s "Install-PowerShellClient-Intune.ps1" `
       -o ".\Output"
   ```

3. **Upload to Intune:**
   - Navigate to **Microsoft Intune Admin Center** > **Apps** > **Windows** > **Add**
   - Select **Windows app (Win32)**
   - Upload the generated `.intunewin` file
   
4. **Configure app settings:**
   - **Name**: SecureBootWatcher PowerShell Client
   - **Description**: Monitors Secure Boot certificate status
   - **Publisher**: Your Organization
   - **Install command**:
     ```powershell
     PowerShell.exe -ExecutionPolicy Bypass -File "Install-PowerShellClient-Intune.ps1" -ApiBaseUrl "https://your-api.azurewebsites.net" -FleetId "Production"
     ```
   - **Uninstall command**:
     ```powershell
     PowerShell.exe -ExecutionPolicy Bypass -File "Uninstall-PowerShellClient-Intune.ps1"
     ```
   - **Install behavior**: System
   - **Device restart behavior**: No specific action
   
5. **Configure detection rules:**
   - **Rule type**: Use a custom detection script
   - **Script file**: `Detect-PowerShellClient-Intune.ps1`
   - **Run script as 32-bit process**: No
   - **Enforce script signature check**: No (or Yes if you sign scripts)

6. **Assign to device groups** and deploy

### Option 3: Configuration Manager (SCCM) Deployment

1. Create an Application with Script Installer type
2. Use `Install-PowerShellClient-Intune.ps1` as installation script
3. Use `Detect-PowerShellClient-Intune.ps1` as detection method
4. Use `Uninstall-PowerShellClient-Intune.ps1` as uninstall script
5. Deploy to device collections

## Configuration

### appsettings.json Structure

```json
{
  "SecureBootWatcher": {
    "FleetId": "fleet-01",
    "RunMode": "Once",
    "RegistryPollInterval": "00:30:00",
    "EventQueryInterval": "00:30:00",
    "EventLookbackPeriod": "1.00:00:00",
    "EventChannels": [
      "Microsoft-Windows-SecureBoot-Servicing/Operational",
      "Microsoft-Windows-SecureBoot-State/Operational",
      "System"
    ],
    "Sinks": {
      "ExecutionStrategy": "FirstSuccess",
      "SinkPriority": "WebApi,FileShare",
      "EnableWebApi": true,
      "EnableFileShare": false,
      "WebApi": {
        "BaseAddress": "https://your-dashboard-api.azurewebsites.net",
        "IngestionRoute": "/api/SecureBootReports",
        "HttpTimeout": "00:02:00"
      },
      "FileShare": {
        "RootPath": "\\\\server\\share\\secureboot-reports",
        "FileExtension": ".json"
      }
    },
    "Commands": {
      "EnableCommandProcessing": false,
      "ProcessBeforeInventory": true,
      "MaxCommandsPerCycle": 10,
      "CommandExecutionDelay": "00:00:05",
      "ContinueOnCommandFailure": true
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    },
    "Console": {
      "Enabled": true
    },
    "File": {
      "Enabled": true,
      "Path": "logs/secureboot-watcher.log",
      "RollingInterval": "Day",
      "RetainedFileCountLimit": 30
    }
  }
}
```

### Key Configuration Options

| Setting | Description | Default |
|---------|-------------|---------|
| `FleetId` | Identifier for grouping devices | `""` |
| `RunMode` | `Once` or `Continuous` | `Once` |
| `RegistryPollInterval` | Interval for registry checks (in continuous mode) | `00:30:00` |
| `EventLookbackPeriod` | How far back to read event logs | `1.00:00:00` (1 day) |
| `ExecutionStrategy` | `FirstSuccess` or `AllSinks` | `FirstSuccess` |
| `EnableWebApi` | Enable Web API sink | `true` |
| `EnableFileShare` | Enable file share sink | `false` |
| `EnableCommandProcessing` | Enable remote command execution | `false` |

## Usage

### Run Manually (Once)
```powershell
# Run with default configuration
.\SecureBootWatcher-Client.ps1

# Run with custom configuration path
.\SecureBootWatcher-Client.ps1 -ConfigPath "C:\Config\custom.json"

# Run in continuous mode
.\SecureBootWatcher-Client.ps1 -RunMode Continuous
```

### Run via Scheduled Task
The scheduled task will run automatically based on the configured schedule. To run manually:
```powershell
Start-ScheduledTask -TaskName "SecureBootWatcher-PowerShell"
```

### View Logs
```powershell
# View console output (if task is running)
Get-ScheduledTaskInfo -TaskName "SecureBootWatcher-PowerShell"

# View log file
Get-Content "C:\Program Files\SecureBootWatcher\PowerShell\logs\secureboot-watcher.log" -Tail 50

# View last task run result
$task = Get-ScheduledTask -TaskName "SecureBootWatcher-PowerShell"
$task.LastRunTime
$task.LastTaskResult  # 0 = success
```

## Collected Data

The PowerShell client collects the same data as the .NET Framework client:

### Device Identity
- Computer name, domain, manufacturer, model
- Firmware version and release date
- Operating system details (version, build, product type)
- Chassis type
- Virtualization platform detection

### Registry Snapshot
- Secure Boot update status (NotStarted, InProgress, Updated, Error)
- Available updates flags
- Policy settings (opt-in/opt-out)
- UEFI CA 2023 capability

### Device Attributes
- Update type preferences

### Telemetry Policy
- Windows diagnostic data level
- CFR (Controlled Feature Rollout) eligibility

### Event Logs
- Secure Boot servicing events
- State change events
- System events related to Secure Boot

### Certificates
- X.509 certificates from UEFI databases (db, dbx, KEK, PK)
- Certificate details (subject, issuer, validity, thumbprint)
- Microsoft certificate identification
- Expiration tracking

## Command Processing

When enabled, the PowerShell client can execute configuration commands from the dashboard:

### Supported Commands
- **ConfigureMicrosoftUpdateOptIn**: Enable/disable Microsoft managed deployment
- **ConfigureTelemetryLevel**: Set Windows diagnostic data level

### Enable Command Processing
```json
{
  "SecureBootWatcher": {
    "Commands": {
      "EnableCommandProcessing": true,
      "ProcessBeforeInventory": true,
      "MaxCommandsPerCycle": 10,
      "CommandExecutionDelay": "00:00:05"
    }
  }
}
```

**Note**: Certificate update commands are not implemented in the PowerShell client as they require complex UEFI interactions best handled by the Windows update mechanism.

## Troubleshooting

### Client not sending reports
1. Check configuration:
   ```powershell
   Get-Content "C:\Program Files\SecureBootWatcher\PowerShell\appsettings.json"
   ```
2. Verify API connectivity:
   ```powershell
   $apiUrl = "https://your-api.azurewebsites.net/health"
   Invoke-RestMethod -Uri $apiUrl
   ```
3. Check scheduled task:
   ```powershell
   Get-ScheduledTask -TaskName "SecureBootWatcher-PowerShell"
   Get-ScheduledTaskInfo -TaskName "SecureBootWatcher-PowerShell"
   ```
4. Review logs:
   ```powershell
   Get-Content "C:\Program Files\SecureBootWatcher\PowerShell\logs\secureboot-watcher.log" -Tail 100
   ```

### Certificate enumeration fails
1. Verify Secure Boot is enabled:
   ```powershell
   Confirm-SecureBootUEFI
   ```
2. Check UEFI mode:
   ```powershell
   $env:firmware_type  # Should be "UEFI"
   ```
3. Ensure running as Administrator/SYSTEM

### Scheduled task not running
1. Check task status:
   ```powershell
   $task = Get-ScheduledTask -TaskName "SecureBootWatcher-PowerShell"
   $task.State  # Should be "Ready" not "Disabled"
   ```
2. Review task history:
   - Open Task Scheduler (taskschd.msc)
   - Find "SecureBootWatcher-PowerShell"
   - View History tab
3. Manually trigger task:
   ```powershell
   Start-ScheduledTask -TaskName "SecureBootWatcher-PowerShell"
   ```

## Differences from .NET Framework Client

### Not Implemented
- **Client Auto-Update**: PowerShell client does not include auto-update functionality
- **Azure Queue Sink**: Not implemented (use Web API or File Share instead)

### Implementation Differences
- **Certificate Parsing**: Uses simplified X.509 parsing (basic certificate details)
- **Logging**: Uses custom logging instead of Serilog
- **Error Handling**: PowerShell-style error handling with try/catch

### Advantages
- **No compilation required**: Easier to modify and deploy
- **Smaller package size**: Just PowerShell scripts, no DLLs
- **Easier troubleshooting**: Read and debug PowerShell code directly
- **Intune-friendly**: No installer required, simple script-based deployment

## Migration from .NET Client

If you're migrating from the .NET Framework client:

1. **Data Compatibility**: The PowerShell client generates the same JSON structure, so existing dashboard installations work without changes
2. **Scheduled Tasks**: Both clients can coexist with different task names, or replace one with the other
3. **Configuration**: Convert your .NET appsettings.json to the PowerShell format (structure is very similar)
4. **Phased Rollout**: Deploy PowerShell client to new devices while keeping .NET client on existing devices

### Migration Steps
1. Test PowerShell client on a few pilot devices
2. Verify data appears correctly in dashboard
3. Create Intune deployment package
4. Assign to test device group
5. Monitor for 1-2 weeks
6. Expand deployment to remaining devices
7. (Optional) Uninstall .NET client from migrated devices

## Support

For issues, questions, or contributions:
- GitHub Issues: https://github.com/robgrame/Nimbus.BootCertWatcher/issues
- Documentation: See `docs/` directory in repository

## Version History

- **v1.0.0** (2025-01-XX) - Initial PowerShell client release
  - Core inventory collection features
  - Web API and File Share sinks
  - Command processing support
  - Intune deployment scripts
  - Compatible with Dashboard API v1.0+

## License

See LICENSE file in the repository root.
