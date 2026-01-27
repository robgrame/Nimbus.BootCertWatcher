# SecureBootWatcher Client Comparison Guide

## Overview

SecureBootWatcher provides two client implementations for collecting Secure Boot inventory from Windows devices:

1. **.NET Framework 4.8 Client** - Compiled C# application
2. **PowerShell Client** - Pure PowerShell script

This guide helps you choose the right client for your environment.

## Quick Comparison Table

| Feature | .NET Framework Client | PowerShell Client |
|---------|----------------------|-------------------|
| **Deployment** | Compiled executable | PowerShell script |
| **Package Size** | ~5-10 MB (with dependencies) | ~100 KB (scripts only) |
| **Prerequisites** | .NET Framework 4.8 runtime | PowerShell 5.0+ (built-in) |
| **Compilation Required** | Yes | No |
| **Easy to Modify** | No (requires rebuild) | Yes (edit script) |
| **Intune Deployment** | Win32 App (larger package) | Win32 App (smaller package) |
| **SCCM Deployment** | Application (installer) | Application (script) |
| **Auto-Update** | ✅ Yes (built-in) | ❌ No |
| **Azure Queue Sink** | ✅ Yes | ❌ No |
| **Web API Sink** | ✅ Yes | ✅ Yes |
| **File Share Sink** | ✅ Yes | ✅ Yes |
| **Certificate Enumeration** | ✅ Yes (advanced parsing) | ✅ Yes (basic parsing) |
| **Command Processing** | ✅ Yes | ✅ Yes |
| **Performance** | Faster startup | Slightly slower startup |
| **Memory Footprint** | Lower | Higher |
| **Logging** | Serilog (structured) | Custom (text-based) |
| **Troubleshooting** | Binary (harder) | Script (easier) |
| **Customization** | Requires C# knowledge | PowerShell knowledge |

## When to Use .NET Framework Client

✅ **Choose .NET Client if you:**
- Need auto-update functionality
- Require Azure Queue sink for buffering
- Want the fastest performance
- Have infrastructure for distributing compiled applications
- Need the most advanced certificate parsing
- Prefer strongly-typed compiled code
- Have concerns about PowerShell execution policies

### Best For:
- Large enterprise deployments with established .NET infrastructure
- High-volume environments where performance is critical
- Organizations using Azure Queue Storage for message buffering
- Deployments requiring automatic client updates

## When to Use PowerShell Client

✅ **Choose PowerShell Client if you:**
- Want the simplest deployment (no compilation)
- Need easy customization without rebuilding
- Prefer script-based deployment via Intune/SCCM
- Have restrictive policies on installing .NET applications
- Want smaller package sizes
- Need to troubleshoot or modify behavior easily
- Don't require auto-update functionality
- Use only Web API or File Share sinks

### Best For:
- Intune/MDM deployments where script-based solutions are preferred
- Environments with restrictive application whitelisting
- Quick pilot deployments and testing
- Organizations with strong PowerShell expertise
- Scenarios requiring custom modifications
- Air-gapped environments where updates are managed externally

## Feature Comparison Details

### Inventory Collection
Both clients collect identical inventory data:
- ✅ Device identity (manufacturer, model, firmware, OS)
- ✅ Secure Boot registry snapshot (root, Servicing, State, SBAT sub-keys)
- ✅ Device attributes (15+ attributes including firmware info, OEM details)
- ✅ Telemetry policy
- ✅ Event logs
- ✅ UEFI certificate enumeration

**Note**: As of v1.14, the PowerShell client has been updated to match the .NET client's registry collection capabilities, including:
- Servicing sub-key data (UefiCa2023Status, BucketHash, ConfidenceLevel, reboot flags)
- State sub-key data (UEFISecureBootEnabled, PolicyPublisher, PolicyVersion)
- SBAT sub-key data (SbatLevel, UpdateStatus)
- Complete device attributes from correct registry path

**Difference**: The .NET client has more sophisticated certificate parsing logic, but both provide sufficient detail for dashboard reporting.

### Reporting Sinks

| Sink Type | .NET Client | PowerShell Client |
|-----------|-------------|-------------------|
| Web API (HTTP POST) | ✅ Full support | ✅ Full support |
| File Share (JSON) | ✅ Full support | ✅ Full support |
| Azure Queue Storage | ✅ Full support | ❌ Not implemented |

**Note**: If you need Azure Queue buffering, use the .NET client. For most deployments, Web API direct posting is sufficient.

### Command Processing

Both clients support remote command execution:
- ✅ Fetch pending commands from API
- ✅ Execute configuration changes
- ✅ Report results back to API

**Supported Commands**:
- Configure Microsoft Update Opt-In (CFR)
- Configure Telemetry Level

**Not Supported in PowerShell Client**:
- Certificate update commands (requires UEFI-level access best handled by Windows Update)

### Auto-Update Capability

| Feature | .NET Client | PowerShell Client |
|---------|-------------|-------------------|
| Check for updates | ✅ Yes | ❌ No |
| Auto-download | ✅ Yes | ❌ No |
| Auto-install | ✅ Yes | ❌ No |
| Version tracking | ✅ Yes | ❌ No |

**Why no auto-update in PowerShell?**
- Script-based clients are typically managed through device management platforms (Intune, SCCM)
- Simpler deployment model without update complexity
- Platform-managed updates are more reliable for scripts

### Deployment Scenarios

#### Scenario 1: Intune Deployment
- **PowerShell**: ⭐ Recommended
  - Smaller package size (~100 KB vs ~5-10 MB)
  - Simpler Win32 app packaging
  - No .NET Framework dependency verification needed
  - Faster deployment to devices

- **.NET**: Alternative
  - Requires .NET Framework 4.8 runtime check
  - Larger .intunewin package
  - Auto-update capability beneficial

#### Scenario 2: SCCM/ConfigMgr Deployment
- **Both are equally suitable**
  - Both can be deployed as Applications
  - .NET provides auto-update
  - PowerShell provides easier customization

#### Scenario 3: Manual/GPO Deployment
- **PowerShell**: ⭐ Recommended
  - Single script file to copy
  - No installation required
  - Easy to distribute via GPO file copy
  - Simple scheduled task creation

#### Scenario 4: Air-Gapped Environment
- **PowerShell**: ⭐ Recommended
  - No auto-update dependency
  - Easier to review security (read the script)
  - Smaller package to transfer
  - Manual update process is simpler

#### Scenario 5: High-Volume (1000+ devices)
- **.NET**: ⭐ Recommended
  - Better performance at scale
  - Auto-update reduces maintenance
  - More efficient resource usage

## Migration Between Clients

Both clients are compatible and can be migrated between:

### From .NET to PowerShell
1. Install PowerShell client
2. Verify data appears in dashboard
3. Uninstall .NET client
4. Configuration files are similar (minimal changes needed)

### From PowerShell to .NET
1. Install .NET Framework 4.8 runtime if needed
2. Install .NET client
3. Verify data appears in dashboard
4. Uninstall PowerShell client

### Coexistence
Both clients can run on the same device with different scheduled tasks:
- Use different task names
- Use different log directories
- Both report to same dashboard
- Useful for testing/migration

## Configuration Compatibility

Both clients use `appsettings.json` with nearly identical structure:

```json
{
  "SecureBootWatcher": {
    "FleetId": "fleet-01",
    "RunMode": "Once",
    "Sinks": {
      "EnableWebApi": true,
      "WebApi": {
        "BaseAddress": "https://api.example.com"
      }
    }
  }
}
```

**Differences**:
- .NET has `ClientUpdate` section (not in PowerShell)
- .NET has more Azure Queue authentication options
- PowerShell has simpler logging configuration

## Performance Considerations

### Startup Time
- **.NET**: ~1-2 seconds
- **PowerShell**: ~3-5 seconds (script parsing overhead)

### Memory Usage
- **.NET**: ~50-100 MB
- **PowerShell**: ~100-200 MB (PowerShell host + script)

### Network Efficiency
- Both identical (same JSON payload)
- Both support retry logic
- Both support timeout configuration

**Recommendation**: Performance difference is negligible for typical once-daily execution.

## Security Considerations

### Code Inspection
- **.NET**: Compiled binary (harder to inspect)
- **PowerShell**: Plain text script (easy to review)

### Execution Policy
- **.NET**: Runs as standard executable
- **PowerShell**: May require `ExecutionPolicy Bypass`

### Signed Code
- **.NET**: Authenticode signing recommended
- **PowerShell**: Script signing recommended but not required

### Privilege Requirements
- Both require Administrator/SYSTEM privileges
- Both access same registry keys and UEFI data
- Both use same network endpoints

## Troubleshooting Ease

### .NET Client
- **Pros**: Structured logging with Serilog, CMTrace format
- **Cons**: Binary debugging harder, needs Visual Studio for changes

### PowerShell Client
- **Pros**: Read and modify script directly, easy debugging
- **Cons**: Less structured logging, text-based output

**Recommendation**: PowerShell client is easier to troubleshoot in the field.

## Recommendation Summary

### Choose PowerShell Client for:
- 🎯 Intune/MDM deployments
- 🎯 Quick pilots and testing
- 🎯 Need for easy customization
- 🎯 Smaller package sizes
- 🎯 Environments without .NET Framework 4.8
- 🎯 Air-gapped or restricted environments

### Choose .NET Client for:
- 🎯 Large-scale enterprise (1000+ devices)
- 🎯 Need auto-update capability
- 🎯 Using Azure Queue Storage
- 🎯 Performance-critical deployments
- 🎯 Existing .NET infrastructure
- 🎯 Want most advanced features

### Default Recommendation:
For **most organizations**, start with the **PowerShell client**:
- Easier to deploy via Intune
- Simpler to customize and troubleshoot
- Adequate performance for typical use cases
- Lower maintenance overhead

You can always migrate to the .NET client later if you need auto-update or Azure Queue support.

## Getting Started

### PowerShell Client
1. Read [PowerShell Client Guide](POWERSHELL_CLIENT.md)
2. Download `SecureBootWatcher-Client.ps1` and `appsettings.powershell.json`
3. Configure settings
4. Run `Prepare-PowerShellPackage.ps1` for Intune deployment
5. Deploy via Intune Win32 App

### .NET Client
1. Read [Client Deployment Scripts](CLIENT_DEPLOYMENT_SCRIPTS.md)
2. Download compiled client package
3. Configure `appsettings.json`
4. Run `Prepare-IntunePackage.ps1` for Intune deployment
5. Deploy via Intune Win32 App

## Support

Both clients are fully supported:
- Same dashboard API endpoints
- Same data format
- Same feature set (except noted differences)
- Both maintained in same repository

For questions or issues:
- GitHub Issues: https://github.com/robgrame/Nimbus.BootCertWatcher/issues
- Documentation: See `docs/` directory
