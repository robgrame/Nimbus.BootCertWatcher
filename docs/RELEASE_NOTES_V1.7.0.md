# Release Notes - Version 1.7.0

**Release Date:** 2025-01-14  
**Type:** Major Feature Release  
**Status:** ? Ready for Production

---

## ?? Overview

Version 1.7.0 introduces **Remote Command Processing**, enabling IT administrators to remotely configure device settings for certificate updates and CFR eligibility directly from the central dashboard. This transforms the solution from a monitoring-only tool to a full device configuration management platform.

---

## ?? New Features

### 1. Remote Command Processing Framework

**Client-Side Command Processor:**
- `ICommandProcessor` interface for extensible command handling
- `CommandProcessor` implementation with full command lifecycle management
- Automatic command fetching from API on each execution cycle
- Local registry write operations with verification
- Result reporting back to central API

**Command Flow:**
```
1. Client starts execution
   ?
2. Fetch pending commands from API (GET /api/ClientCommands/pending)
   ?
3. Execute each command locally (write to registry)
   ?
4. Verify command result (read registry to confirm)
   ?
5. Report result to API (POST /api/ClientCommands/result)
   ?
6. Continue with normal inventory collection
```

---

### 2. Supported Command Types

#### **Certificate Update Command**
```csharp
CertificateUpdateCommand
{
    UpdateType: 0 | 1 | 2,  // 0=None, 1=DB, 2=Boot Manager
    ForceUpdate: bool
}
```

**Registry Key Modified:**
- `HKLM\SYSTEM\CurrentControlSet\Control\SecureBoot\UpdateType` (DWORD)

**Use Case:**
- Trigger certificate update deployment on specific devices
- Force update on devices marked as not capable

---

#### **Microsoft Update Opt-In Command**
```csharp
MicrosoftUpdateOptInCommand
{
    OptIn: bool  // true=Enable CFR, false=Disable CFR
}
```

**Registry Key Modified:**
- `HKLM\SYSTEM\CurrentControlSet\Control\SecureBoot\MicrosoftUpdateManagedOptIn` (DWORD)

**Use Case:**
- Enable/disable Controlled Feature Rollout eligibility
- Manage which devices participate in Microsoft-managed updates

---

#### **Telemetry Configuration Command**
```csharp
TelemetryConfigurationCommand
{
    RequiredTelemetryLevel: 0 | 1 | 2 | 3,
    ValidateOnly: bool
}
```

**Registry Key Modified:**
- `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection\AllowTelemetry` (DWORD)

**Telemetry Levels:**
- `0` = Security (Enterprise/Education/Server only) - ? Not CFR eligible
- `1` = Basic - ? CFR eligible
- `2` = Enhanced - ? CFR eligible
- `3` = Full - ? CFR eligible

**Use Case:**
- Ensure devices meet CFR telemetry requirements
- Validate compliance without making changes (ValidateOnly mode)
- Automatically configure telemetry level for CFR eligibility

---

### 3. Configuration Options

**Client Configuration (`appsettings.json`):**
```json
{
  "SecureBootWatcher": {
    "Commands": {
      "EnableCommandProcessing": false,      // ? Enable/disable feature
      "ProcessBeforeInventory": true,         // Process commands before or after inventory
      "MaxCommandsPerCycle": 10,              // Limit commands per execution
      "CommandExecutionDelay": "00:00:02",    // Delay between commands (registry propagation)
      "ContinueOnCommandFailure": true        // Continue inventory even if commands fail
    }
  }
}
```

**Key Configuration Options:**

| Setting | Default | Description |
|---------|---------|-------------|
| `EnableCommandProcessing` | `false` | **Must be explicitly enabled** - Opt-in security model |
| `ProcessBeforeInventory` | `true` | Apply configuration changes before capturing state |
| `MaxCommandsPerCycle` | `10` | Prevents runaway processing if many commands queued |
| `CommandExecutionDelay` | `2 seconds` | Allows registry changes to propagate |
| `ContinueOnCommandFailure` | `true` | Resilient mode - always send inventory data |

---

### 4. Security & Requirements

**Administrator Privileges Required:**
- Client **must** run as Administrator to write to `HKLM` registry keys
- Commands will fail with `UnauthorizedAccessException` if not elevated

**Opt-In Security Model:**
- Command processing is **disabled by default**
- Must be explicitly enabled in configuration
- Provides defense-in-depth against unauthorized configuration changes

**Verification & Audit:**
- Every command execution is verified by reading registry after write
- All results (success/failure) are reported back to API
- Full audit trail of command execution in client logs

---

## ?? Technical Changes

### Client (SecureBootWatcher.Client)

**New Files:**
1. **`Services/ICommandProcessor.cs`** (136 lines)
   - Interface for command processing operations
   - Methods: FetchPendingCommandsAsync, ExecuteCommandAsync, VerifyCommandResultAsync, ReportResultAsync

2. **`Services/CommandProcessor.cs`** (387 lines)
   - Full implementation of command processing
   - Registry write operations with verification
   - HTTP communication with API endpoints
   - Comprehensive error handling and logging

**Modified Files:**
1. **`Services/SecureBootWatcherService.cs`**
   - Integrated `ProcessCommandsAsync()` method
   - Three-phase execution: Commands ? Inventory ? Commands (configurable)
   - Detailed logging for command processing phase

2. **`Program.cs`**
   - Registered `ICommandProcessor` in DI container
   - Added command configuration logging

3. **`appsettings.json`**
   - Added `Commands` configuration section

### Shared Models (SecureBootWatcher.Shared)

**Modified Files:**
1. **`Configuration/SecureBootWatcherOptions.cs`**
   - Added `CommandProcessingOptions` class
   - Full configuration model for command processing

---

## ?? Command Execution Flow

### Client Execution Phases

```
???????????????????????????????????????????
?  Phase 1: Command Processing (Optional) ?
???????????????????????????????????????????
?  1. Fetch pending commands from API     ?
?  2. Execute each command locally        ?
?  3. Verify registry changes             ?
?  4. Report results to API               ?
???????????????????????????????????????????
                  ?
???????????????????????????????????????????
?  Phase 2: Inventory Collection          ?
???????????????????????????????????????????
?  1. Capture registry state              ?
?  2. Enumerate certificates              ?
?  3. Read event logs                     ?
?  4. Build and send report               ?
???????????????????????????????????????????
```

**Configuration Flexibility:**
- `ProcessBeforeInventory = true` ? Commands execute before inventory (recommended)
- `ProcessBeforeInventory = false` ? Commands execute after inventory

**Rationale for "Before":**
- Registry changes are applied first
- Inventory captures the **new** state immediately
- Reduces delay between configuration and reporting

---

## ?? Example Scenarios

### Scenario 1: Enable CFR on Pilot Devices

**Goal:** Enable Microsoft Update Managed Opt-In on 10 pilot devices

**Steps:**
1. Admin sends `MicrosoftUpdateOptInCommand` with `OptIn = true`
2. Clients fetch command on next execution cycle
3. Clients write `MicrosoftUpdateManagedOptIn = 1` to registry
4. Clients verify registry value is set correctly
5. Clients report success to API
6. Inventory report shows `MicrosoftUpdateManagedOptIn = true`

**Timeline:**
- Command issued: 10:00 AM
- Client executes (scheduled task): 10:30 AM
- Command completes: 10:30:05 AM (5 seconds)
- Inventory sent: 10:30:10 AM
- Dashboard updated: 10:30:15 AM

---

### Scenario 2: Fix Telemetry Compliance

**Goal:** Ensure all production devices have telemetry level ? Basic (1)

**Step 1 - Validation:**
```csharp
TelemetryConfigurationCommand {
    RequiredTelemetryLevel = 1,
    ValidateOnly = true
}
```
Result: Identifies non-compliant devices

**Step 2 - Remediation:**
```csharp
TelemetryConfigurationCommand {
    RequiredTelemetryLevel = 1,
    ValidateOnly = false
}
```
Result: Sets telemetry level to Basic on all devices

---

### Scenario 3: Trigger Certificate Update

**Goal:** Force certificate update on devices stuck in pending state

**Command:**
```csharp
CertificateUpdateCommand {
    UpdateType = 1,  // DB update
    ForceUpdate = true
}
```

**Registry Effect:**
- `HKLM\SYSTEM\CurrentControlSet\Control\SecureBoot\UpdateType = 1`
- Windows Update detects change and triggers certificate update process

---

## ?? Deployment

### Upgrade Steps

**Client Upgrade:**
1. **Stop Scheduled Task** (if running):
   ```powershell
   Stop-ScheduledTask -TaskName "SecureBootWatcher"
   ```

2. **Deploy New Client** (v1.7.0):
   ```powershell
   .\Deploy-Client.ps1 -PackageZipPath ".\SecureBootWatcher-Client-v1.7.0.zip"
   ```

3. **Enable Command Processing** (optional):
   ```json
   {
     "Commands": {
       "EnableCommandProcessing": true
     }
   }
   ```

4. **Restart Scheduled Task**:
   ```powershell
   Start-ScheduledTask -TaskName "SecureBootWatcher"
   ```

**Backward Compatibility:**
- ? Clients v1.7 work with API v1.6 (commands disabled)
- ? Clients v1.6 work with API v1.7 (ignore new endpoints)
- ? No breaking changes to existing functionality

---

## ?? Future Enhancements (v1.8+)

### Planned for v1.8:

1. **API Server-Side Implementation:**
   - `ClientCommandsController` with GET/POST endpoints
   - `PendingCommandsEntity` database table
   - EF Core migration for command storage

2. **Web Dashboard UI:**
   - Send commands to devices from dashboard
   - Command history and audit trail
   - Batch operations for fleets
   - Command scheduling and retry management

3. **Advanced Command Features:**
   - Command templates
   - Scheduled commands (execute at specific time)
   - Conditional commands (execute if criteria met)
   - Command rollback support

---

## ?? Breaking Changes

**None** - Fully backward compatible with v1.6.x

---

## ?? Bug Fixes

None - Pure feature addition release

---

## ?? Documentation

**New Documents:**
- `docs/RELEASE_NOTES_V1.7.0.md` - This document
- `docs/COMMAND_PROCESSING_GUIDE.md` - Complete usage guide (planned)

**Updated Documents:**
- `README.md` - Added v1.7 feature summary
- `version.json` - Version bump to 1.7

---

## ?? Testing

### Validation Steps

**1. Verify Command Processing is Disabled by Default:**
```powershell
# Check appsettings.json
$config = Get-Content ".\appsettings.json" | ConvertFrom-Json
$config.SecureBootWatcher.Commands.EnableCommandProcessing
# Expected: False
```

**2. Enable and Test Command Execution:**
```powershell
# 1. Enable commands in config
# 2. Run client as Administrator
.\SecureBootWatcher.Client.exe

# 3. Check logs for command processing phase
Get-Content ".\logs\client-*.log" | Select-String "Command Processing"
```

**3. Verify Registry Write:**
```powershell
# After executing MicrosoftUpdateOptInCommand with OptIn=true
Get-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Control\SecureBoot" -Name MicrosoftUpdateManagedOptIn
# Expected: 1
```

---

## ?? Support

For questions or issues:
- **Documentation**: [docs/](docs/) directory
- **GitHub Issues**: [Report bugs](https://github.com/robgrame/Nimbus.BootCertWatcher/issues)
- **GitHub Discussions**: [Ask questions](https://github.com/robgrame/Nimbus.BootCertWatcher/discussions)

---

## ?? Contributors

- **Development Team** - Remote command processing implementation
- **IT Operations Teams** - Feature requirements and use case validation

---

**Version:** 1.7.0  
**Build Date:** 2025-01-14  
**Git Tag:** `v1.7.0`

