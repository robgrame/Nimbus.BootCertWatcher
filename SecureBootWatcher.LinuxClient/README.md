# SecureBootWatcher Linux Client

Cross-platform Secure Boot certificate monitoring client for Linux systems running UEFI firmware.

## Overview

The **SecureBootWatcher.LinuxClient** is a .NET 8 application that monitors Secure Boot certificate status on Linux systems. It enumerates UEFI firmware certificates, tracks system events, and reports status to a centralized dashboard.

## Features

- ✅ **EFI Variable Access**: Reads Secure Boot certificates directly from `/sys/firmware/efi/efivars`
- ✅ **Certificate Enumeration**: Extracts X.509 certificates from UEFI databases (db, dbx, KEK, PK)
- ✅ **Event Logging**: Queries systemd journal (journald) for Secure Boot related events
- ✅ **Hardware Detection**: Reads system information from DMI/SMBIOS (`/sys/class/dmi/id/`)
- ✅ **Multiple Sinks**: Supports File Share, Azure Queue Storage, and Web API reporting
- ✅ **Cross-Architecture**: Targets `linux-x64` and `linux-arm64`

## System Requirements

### Operating System
- Linux distribution with UEFI firmware support:
  - Ubuntu 20.04 LTS or later
  - Red Hat Enterprise Linux 8 or later
  - Debian 11 or later
  - Fedora 33 or later
  - SUSE Linux Enterprise 15 or later

### Software Requirements
- **.NET 8 Runtime**: Install via package manager
  ```bash
  # Ubuntu/Debian
  sudo apt-get update
  sudo apt-get install -y dotnet-runtime-8.0
  
  # RHEL/Fedora
  sudo dnf install dotnet-runtime-8.0
  ```
- **systemd**: For journal event logging
- **UEFI Firmware**: System must boot in UEFI mode (not legacy BIOS)

### Permissions
- **Root/sudo access**: Required to read EFI variables from `/sys/firmware/efi/efivars`

## Installation

### Option 1: Build from Source
```bash
cd SecureBootWatcher.LinuxClient
dotnet build -c Release
```

### Option 2: Publish Self-Contained
```bash
# For x64 architecture
dotnet publish -c Release -r linux-x64 --self-contained -o ./publish/linux-x64

# For ARM64 architecture
dotnet publish -c Release -r linux-arm64 --self-contained -o ./publish/linux-arm64
```

### Option 3: Framework-Dependent
```bash
dotnet publish -c Release -r linux-x64 --no-self-contained -o ./publish/linux-x64
```

## Configuration

Edit `appsettings.json` to configure the client:

```json
{
  "SecureBootWatcher": {
    "FleetId": "linux-fleet-01",
    "RegistryPollInterval": "00:30:00",
    "EventQueryInterval": "00:30:00",
    "EventLookbackPeriod": "1.00:00:00",
    "EventChannels": [
      "journald"
    ],
    "Sinks": {
      "ExecutionStrategy": "FirstSuccess",
      "EnableWebApi": true,
      "WebApi": {
        "BaseAddress": "https://your-dashboard-api.azurewebsites.net",
        "IngestionRoute": "/api/SecureBootReports",
        "HttpTimeout": "00:01:00"
      }
    }
  }
}
```

### Configuration Options

#### FleetId
Optional identifier to group devices by deployment fleet or environment.

#### RegistryPollInterval / EventQueryInterval
How often to check for Secure Boot status and events. Format: `HH:MM:SS`

#### Sinks
Configure one or more reporting destinations:

- **File Share**: Write JSON reports to a network mount
  ```json
  "EnableFileShare": true,
  "FileShare": {
    "RootPath": "/mnt/secureboot-reports",
    "FileExtension": ".json"
  }
  ```

- **Azure Queue Storage**: Send reports to Azure Queue
  ```json
  "EnableAzureQueue": true,
  "AzureQueue": {
    "QueueServiceUri": "https://yourstorageaccount.queue.core.windows.net",
    "QueueName": "secureboot-reports",
    "AuthenticationMethod": "ManagedIdentity"
  }
  ```

- **Web API**: HTTP POST to dashboard API
  ```json
  "EnableWebApi": true,
  "WebApi": {
    "BaseAddress": "https://your-dashboard-api.azurewebsites.net",
    "IngestionRoute": "/api/SecureBootReports"
  }
  ```

## Running the Client

### Development
```bash
cd SecureBootWatcher.LinuxClient
sudo dotnet run
```

### Production (Published)
```bash
cd /path/to/publish/linux-x64
sudo ./SecureBootWatcher.LinuxClient
```

### As a systemd Service

Create `/etc/systemd/system/secureboot-watcher.service`:

```ini
[Unit]
Description=SecureBootWatcher Linux Client
After=network.target

[Service]
Type=simple
User=root
WorkingDirectory=/opt/secureboot-watcher
ExecStart=/opt/secureboot-watcher/SecureBootWatcher.LinuxClient
Restart=on-failure
RestartSec=30
StandardOutput=journal
StandardError=journal

[Install]
WantedBy=multi-user.target
```

Enable and start the service:
```bash
sudo systemctl daemon-reload
sudo systemctl enable secureboot-watcher
sudo systemctl start secureboot-watcher
sudo systemctl status secureboot-watcher
```

View logs:
```bash
sudo journalctl -u secureboot-watcher -f
```

## How It Works

### Certificate Enumeration
The Linux client reads UEFI Secure Boot certificates by:

1. **Checking Secure Boot Status**
   - Reading `/sys/firmware/efi/efivars/SecureBoot-*`
   - Optionally using `mokutil --sb-state` if available

2. **Reading EFI Variables**
   - Accessing certificate databases from `/sys/firmware/efi/efivars/`
   - Variables: `db-*`, `dbx-*`, `KEK-*`, `PK-*`

3. **Parsing EFI Signature Lists**
   - Extracting X.509 certificates from EFI_SIGNATURE_LIST format
   - Parsing certificate properties (subject, issuer, expiration, etc.)

### Event Logging
The client queries systemd journal for Secure Boot related events:

```bash
journalctl --since="YYYY-MM-DD HH:MM:SS" -o json -n 1000 -p info
```

Events containing "secure", "uefi", or "boot" keywords are collected and reported.

### Hardware Detection
System information is read from DMI/SMBIOS:
- Manufacturer: `/sys/class/dmi/id/sys_vendor`
- Model: `/sys/class/dmi/id/product_name`
- BIOS Version: `/sys/class/dmi/id/bios_version`

## Troubleshooting

### Permission Denied Errors
```
Failed to access EFI variables due to insufficient permissions
```
**Solution**: Run the client with `sudo` or as root.

### EFI Variables Path Not Found
```
EFI variables path /sys/firmware/efi/efivars not found
```
**Solution**: Ensure system is booted in UEFI mode (not legacy BIOS). Check with:
```bash
[ -d /sys/firmware/efi ] && echo "UEFI" || echo "Legacy BIOS"
```

### Secure Boot Not Enabled
```
Secure Boot is not enabled on this device
```
**Solution**: Enable Secure Boot in UEFI firmware settings during system boot.

### mokutil Command Not Found
This is optional. The client can function without `mokutil` by reading EFI variables directly.

To install mokutil:
```bash
# Ubuntu/Debian
sudo apt-get install mokutil

# RHEL/Fedora
sudo dnf install mokutil
```

### No Certificates Found
If no certificates are enumerated:
1. Verify Secure Boot is enabled: `mokutil --sb-state`
2. Check EFI variable permissions: `ls -l /sys/firmware/efi/efivars/db-*`
3. Ensure running with root privileges

## Differences from Windows Client

| Feature | Windows Client | Linux Client |
|---------|----------------|--------------|
| Target Framework | .NET Framework 4.8 | .NET 8 |
| Registry Access | Windows Registry | Not applicable (uses EFI variables) |
| Event Logs | Windows Event Log | systemd journald |
| Certificate Access | PowerShell Get-SecureBootUEFI | Direct EFI variable read |
| Hardware Info | WMI (System.Management) | DMI/SMBIOS files |
| Deployment State | Tracks UEFI CA 2023 update | Not tracked (Windows-specific) |
| Privilege Requirement | Administrator/SYSTEM | Root/sudo |

## Architecture Support

- **linux-x64**: Intel/AMD 64-bit (x86_64)
- **linux-arm64**: ARM 64-bit (aarch64)

To target a specific architecture:
```bash
dotnet publish -r linux-x64
dotnet publish -r linux-arm64
```

## Logs

Logs are written to:
- **Console**: Real-time log output
- **File**: `logs/client-YYYY-MM-DD.log` (rotating daily, 30-day retention)

Log levels:
- Information: Normal operations
- Warning: Non-critical issues
- Error: Failures requiring attention
- Debug: Detailed diagnostic information

To enable debug logging, set in `appsettings.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug"
    }
  }
}
```

## Security Considerations

- **Root Access**: The client requires root privileges to access EFI variables. Consider running as a systemd service with limited privileges where possible.
- **EFI Variable Integrity**: The client only reads EFI variables; it does not write or modify them.
- **Network Security**: Use HTTPS for Web API sink and secure Azure connections with Managed Identity.
- **Log Sensitivity**: Logs may contain certificate details. Secure log files appropriately.

## Support

For issues or questions:
- **GitHub Issues**: [Report bugs](https://github.com/robgrame/Nimbus.BootCertWatcher/issues)
- **Documentation**: [Main README](../README.md)
- **Discussions**: [GitHub Discussions](https://github.com/robgrame/Nimbus.BootCertWatcher/discussions)

## License

MIT License - see [LICENSE](../LICENSE) for details.
