# Release Notes - PowerShell Client v1.0.0

**Release Date**: 2025-01-03  
**Version**: 1.0.0  
**Component**: SecureBootWatcher PowerShell Client

---

## Overview

We're excited to announce the first release of the **SecureBootWatcher PowerShell Client** - a pure PowerShell implementation of the inventory collection client that makes deployment via Microsoft Intune and other device management platforms significantly easier.

## What's New

### PowerShell Client Implementation

A complete rewrite of the client component in PowerShell, providing:

- ✅ **Pure PowerShell** - No compilation required
- ✅ **Same Features** - All inventory collection capabilities of the .NET client
- ✅ **Smaller Package** - ~100 KB vs ~5-10 MB for .NET client
- ✅ **Easier Deployment** - Ideal for Intune/MDM solutions
- ✅ **Easy Customization** - Edit and modify without rebuilding

### Core Features

#### Complete Inventory Collection
- Device identity with hardware detection
- Secure Boot registry snapshot
- Device attributes and telemetry policy
- Event log collection (Secure Boot events)
- UEFI certificate enumeration (db, dbx, KEK, PK)

#### Multiple Reporting Sinks
- **Web API**: HTTP POST to dashboard
- **File Share**: JSON file output
- Configurable execution strategies and priorities

#### Remote Command Processing
- Fetch and execute commands from dashboard
- Configure Microsoft Update Opt-In (CFR)
- Set Windows Telemetry Level
- Report results with verification

#### Robust Logging
- Console and file logging
- Configurable log levels
- Log rotation and retention
- Structured output

### Deployment Scripts

Complete set of Intune deployment scripts:

1. **Install-PowerShellClient-Intune.ps1**
   - Automated installation
   - Scheduled task creation
   - Configuration management
   - Parameter-driven setup

2. **Detect-PowerShellClient-Intune.ps1**
   - Multi-check detection logic
   - Validates installation integrity
   - Checks scheduled task status

3. **Uninstall-PowerShellClient-Intune.ps1**
   - Clean removal process
   - Removes scheduled tasks
   - Cleans up directories

4. **Prepare-PowerShellPackage.ps1**
   - Package preparation automation
   - Pre-configuration support
   - README generation

5. **Test-PowerShellClient.ps1**
   - Comprehensive validation suite
   - Prerequisites checking
   - Component testing

### Documentation

Two comprehensive documentation guides:

1. **PowerShell Client Guide** (`docs/POWERSHELL_CLIENT.md`)
   - Complete installation instructions
   - Configuration reference
   - Usage examples
   - Troubleshooting guide
   - Migration instructions

2. **Client Comparison Guide** (`docs/CLIENT_COMPARISON.md`)
   - Feature comparison table
   - Deployment scenario recommendations
   - Performance considerations
   - Migration guidance

## Technical Details

### Requirements

**Client Device**:
- Windows 10 version 1809+ / Windows 11
- Windows Server 2016+ with UEFI
- PowerShell 5.0+ (built-in)
- UEFI Secure Boot capable hardware
- Administrator/SYSTEM privileges
- Network connectivity to dashboard API

**Dashboard**:
- SecureBootWatcher Dashboard API v1.0+
- Compatible endpoints unchanged

### Configuration

Uses familiar `appsettings.json` format:

```json
{
  "SecureBootWatcher": {
    "FleetId": "fleet-01",
    "RunMode": "Once",
    "Sinks": {
      "EnableWebApi": true,
      "WebApi": {
        "BaseAddress": "https://your-api.azurewebsites.net"
      }
    }
  }
}
```

### Data Format

Generates identical JSON structure to .NET client - seamless integration with existing dashboards.

## Deployment Options

### Microsoft Intune
1. Prepare package with `Prepare-PowerShellPackage.ps1`
2. Create .intunewin with Microsoft Win32 Content Prep Tool
3. Upload to Intune as Win32 App
4. Configure installation parameters
5. Assign to device groups

### SCCM/ConfigMgr
1. Create Application with Script Installer
2. Use provided installation scripts
3. Deploy to device collections

### Manual/GPO
1. Copy script and configuration to devices
2. Create scheduled task
3. Configure settings

## Differences from .NET Client

### Not Included
- ❌ Client auto-update functionality (by design)
- ❌ Azure Queue Storage sink (use Web API or File Share)

### Advantages
- ✅ Smaller package size (~100 KB)
- ✅ No .NET Framework dependency
- ✅ Easier to customize (edit script directly)
- ✅ Better for Intune deployments
- ✅ Easier troubleshooting (plain text script)

### Performance
- Slightly slower startup (~3-5s vs ~1-2s)
- Negligible for typical once-daily execution
- Same network efficiency

## Migration Guide

### From .NET to PowerShell

1. Deploy PowerShell client to test devices
2. Verify data appears correctly in dashboard
3. Expand deployment to remaining devices
4. (Optional) Uninstall .NET client

### Coexistence

Both clients can run on the same device:
- Use different scheduled task names
- Both report to same dashboard
- Useful for testing and gradual migration

## Use Cases

Perfect for:
- 🎯 Intune/MDM deployments
- 🎯 Quick pilot programs
- 🎯 Organizations preferring script-based solutions
- 🎯 Environments without .NET Framework 4.8
- 🎯 Air-gapped environments
- 🎯 Scenarios requiring easy customization

## Known Limitations

1. **Certificate Parsing**: Uses basic X.509 parsing (vs advanced EFI parsing in .NET)
   - Impact: Minimal - sufficient detail for dashboard reporting
   
2. **No Azure Queue Sink**: Not implemented
   - Workaround: Use Web API or File Share sink
   
3. **No Auto-Update**: By design
   - Workaround: Manage updates via MDM platform

## Breaking Changes

None - this is a new implementation that:
- Uses same API endpoints
- Generates same data format
- Requires no dashboard changes
- Is fully compatible with existing infrastructure

## Upgrade Instructions

This is a new deployment option, not an upgrade:

### New Deployments
- Start with PowerShell client (recommended for Intune)
- See `docs/POWERSHELL_CLIENT.md` for instructions

### Existing .NET Client Deployments
- Can remain on .NET client (both supported)
- Can migrate to PowerShell client (see Migration Guide)
- Can run both clients (during transition)

## Testing & Validation

### Included Test Tools
- Validation test script (`Test-PowerShellClient.ps1`)
- Prerequisites checking
- Component validation
- Configuration verification

### Recommended Testing
1. Run test script on sample device
2. Execute client manually
3. Verify data in dashboard
4. Test scheduled task execution
5. Validate Intune deployment

## Files Included

### Main Components
- `SecureBootWatcher-Client.ps1` - Main client script (1,100+ lines)
- `appsettings.powershell.json` - Configuration template

### Deployment Scripts
- `scripts/Install-PowerShellClient-Intune.ps1`
- `scripts/Detect-PowerShellClient-Intune.ps1`
- `scripts/Uninstall-PowerShellClient-Intune.ps1`
- `scripts/Prepare-PowerShellPackage.ps1`
- `scripts/Test-PowerShellClient.ps1`

### Documentation
- `docs/POWERSHELL_CLIENT.md` - Complete guide
- `docs/CLIENT_COMPARISON.md` - Comparison with .NET client

## Support

### Documentation
- [PowerShell Client Guide](../docs/POWERSHELL_CLIENT.md)
- [Client Comparison](../docs/CLIENT_COMPARISON.md)
- [Main Repository README](../README.md)

### Community Support
- GitHub Issues: https://github.com/robgrame/Nimbus.BootCertWatcher/issues
- GitHub Discussions: https://github.com/robgrame/Nimbus.BootCertWatcher/discussions

## Future Enhancements

Potential future additions:
- Enhanced certificate parsing (if needed)
- Additional command types
- More sink options
- Enhanced logging formats
- Performance optimizations

## Credits

Developed as part of the SecureBootWatcher project to provide easier deployment options for enterprise environments using Microsoft Intune and other device management platforms.

## License

Same as main project - see LICENSE file in repository root.

---

## Quick Start

### 1. Download
```powershell
# Download the PowerShell client files
# Or clone the repository
git clone https://github.com/robgrame/Nimbus.BootCertWatcher.git
```

### 2. Configure
```powershell
# Edit appsettings.powershell.json
# Set your API endpoint and Fleet ID
```

### 3. Test
```powershell
# Run the test script
.\scripts\Test-PowerShellClient.ps1
```

### 4. Deploy
```powershell
# Prepare Intune package
.\scripts\Prepare-PowerShellPackage.ps1 -ApiBaseUrl "https://your-api.azurewebsites.net" -FleetId "Production"

# Create .intunewin and deploy via Intune
```

### 5. Verify
- Check dashboard for device reports
- Review scheduled task execution
- Monitor logs

---

## Feedback

We welcome your feedback! Please:
- Report issues on GitHub
- Share your deployment experiences
- Suggest improvements
- Contribute enhancements

Thank you for using SecureBootWatcher!

---

**Version**: 1.0.0  
**Component**: SecureBootWatcher PowerShell Client  
**Compatibility**: Dashboard API v1.0+  
**Status**: Production Ready ✅
