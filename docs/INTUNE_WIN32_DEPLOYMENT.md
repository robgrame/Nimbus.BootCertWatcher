# Intune Win32 App Deployment Guide

## Overview

This guide explains how to deploy SecureBootWatcher client as a Win32 app through Microsoft Endpoint Manager (Intune).

---

## Prerequisites

1. **Microsoft Win32 Content Prep Tool**
   - Download: https://github.com/Microsoft/Microsoft-Win32-Content-Prep-Tool
   - Used to convert package to `.intunewin` format

2. **SecureBootWatcher Client Package**
   - Built using: `.\scripts\Deploy-Client.ps1`
   - Location: `.\client-package\SecureBootWatcher-Client.zip`

3. **Intune Administrator Access**
   - Permission to create and deploy Win32 apps

---

## Scripts for Intune

### Install Script: `Install-Client-Intune.ps1`
- Copies files to `C:\Program Files\SecureBootWatcher`
- Optionally configures API URL and Fleet ID
- Creates scheduled task
- Logs to `%ProgramData%\SecureBootWatcher\install.log`

### Uninstall Script: `Uninstall-Client-Intune.ps1`
- Removes scheduled task
- Stops running processes
- Deletes installation directory
- Logs to `%ProgramData%\SecureBootWatcher\uninstall.log`

### Detection Script: `Detect-Client-Intune.ps1`
- No parameters (Intune requirement)
- Checks installation directory, executable, config, and task
- Returns "Installed" on success (exit 0)
- Returns nothing on failure (exit 1)

---

## Step-by-Step Deployment

### Step 1: Prepare the Package

Extract the client package to a working directory and add the certificate:

```powershell
# Create working directory
New-Item -ItemType Directory -Path "C:\Temp\SecureBootWatcher-Intune" -Force

# Extract package
Expand-Archive -Path ".\client-package\SecureBootWatcher-Client.zip" `
    -DestinationPath "C:\Temp\SecureBootWatcher-Intune" -Force

# Copy Intune scripts to the package directory
Copy-Item ".\scripts\Install-Client-Intune.ps1" `
    -Destination "C:\Temp\SecureBootWatcher-Intune\" -Force

Copy-Item ".\scripts\Uninstall-Client-Intune.ps1" `
    -Destination "C:\Temp\SecureBootWatcher-Intune\" -Force

Copy-Item ".\scripts\Detect-Client-Intune.ps1" `
    -Destination "C:\Temp\SecureBootWatcher-Intune\" -Force

# **IMPORTANT**: Copy the PFX certificate to the package directory
# This certificate is used for Azure Queue authentication
Copy-Item "C:\Path\To\Your\SecureBootWatcher.pfx" `
    -Destination "C:\Temp\SecureBootWatcher-Intune\" -Force
```

**Package Contents Should Include**:
```
C:\Temp\SecureBootWatcher-Intune\
??? Install-Client-Intune.ps1          (Install script)
??? Uninstall-Client-Intune.ps1        (Uninstall script)
??? Detect-Client-Intune.ps1           (Detection script)
??? SecureBootWatcher.pfx              (Certificate for Azure Queue auth) ? NEW
??? SecureBootWatcher.Client.exe       (Main executable)
??? SecureBootWatcher.Shared.dll       (Shared library)
??? appsettings.json                   (Configuration)
??? [Other DLLs and dependencies]
```

**Certificate Requirements**:
- File name must be: `SecureBootWatcher.pfx`
- Must include private key
- Should match the thumbprint in `appsettings.json`
- Certificate password (if any) will be provided via install command parameters

**Security Note**: The certificate will be:
1. Imported to `LocalMachine\My` certificate store during installation
2. Private key permissions granted to SYSTEM account
3. PFX file will NOT be copied to the installation directory (only to cert store)

---

## Program Configuration

### Install Command Options

The `Install-Client-Intune.ps1` script supports the following parameters:

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `ApiBaseUrl` | string | (empty) | API base URL for WebApi sink |
| `FleetId` | string | (empty) | Fleet identifier for grouping devices |
| `CertificatePassword` | string | (empty) | Password for PFX certificate |
| `ScheduleType` | string | "Daily" | Task frequency: Once, Daily, Hourly, Custom |
| `TaskTime` | string | "09:00AM" | Start time for scheduled task |
| `RepeatEveryHours` | int | 4 | Repeat interval for Custom schedule (1-24) |
| `RandomDelayMinutes` | int | 60 | Random delay range (0-1440 minutes) |

### Basic Install Commands

**Minimal install** (default daily schedule at 9 AM):
```powershell
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Install-Client-Intune.ps1"
```

**With certificate password**:
```powershell
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Install-Client-Intune.ps1" -CertificatePassword "YourCertPassword"
```

**With API configuration**:
```powershell
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Install-Client-Intune.ps1" -ApiBaseUrl "https://your-api.contoso.com" -FleetId "production-fleet" -CertificatePassword "YourCertPassword"
```

### Schedule Configuration Examples

**Daily at 9 AM with 60-minute random delay** (default):
```powershell
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Install-Client-Intune.ps1" -ScheduleType "Daily" -TaskTime "09:00AM" -RandomDelayMinutes 60
```

**Hourly execution**:
```powershell
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Install-Client-Intune.ps1" -ScheduleType "Hourly" -TaskTime "08:00AM" -RandomDelayMinutes 30
```

**Every 4 hours** (Custom):
```powershell
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Install-Client-Intune.ps1" -ScheduleType "Custom" -RepeatEveryHours 4 -TaskTime "08:00AM" -RandomDelayMinutes 15
```

**Every 6 hours with minimal random delay**:
```powershell
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Install-Client-Intune.ps1" -ScheduleType "Custom" -RepeatEveryHours 6 -TaskTime "00:00AM" -RandomDelayMinutes 5
```

**Daily at 2 AM (off-hours)**:
```powershell
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Install-Client-Intune.ps1" -ScheduleType "Daily" -TaskTime "02:00AM" -RandomDelayMinutes 120
```

**Once (for testing)**:
```powershell
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Install-Client-Intune.ps1" -ScheduleType "Once" -TaskTime "10:00AM" -RandomDelayMinutes 0
```

### Complete Example (Production)

```powershell
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Install-Client-Intune.ps1" `
    -ApiBaseUrl "https://secureboot-api.contoso.com" `
    -FleetId "corporate-fleet" `
    -CertificatePassword "StrongP@ssw0rd!" `
    -ScheduleType "Custom" `
    -RepeatEveryHours 6 `
    -TaskTime "00:00AM" `
    -RandomDelayMinutes 30
```

This will:
- Configure API to `https://secureboot-api.contoso.com`
- Tag device with Fleet ID `corporate-fleet`
- Import certificate with specified password
- Create scheduled task running every 6 hours starting at midnight
- Add random delay of 0-30 minutes to prevent API flooding

### Recommended Configurations

| Scenario | ScheduleType | Interval | TaskTime | RandomDelay |
|----------|--------------|----------|----------|-------------|
| **Production (default)** | Daily | - | 09:00AM | 60 min |
| **High-frequency monitoring** | Hourly | - | 08:00AM | 30 min |
| **Balanced** | Custom | 4 hours | 00:00AM | 15 min |
| **Low-frequency** | Custom | 12 hours | 00:00AM | 60 min |
| **Off-hours only** | Daily | - | 02:00AM | 120 min |
| **Testing** | Once | - | 10:00AM | 0 min |

**Security Recommendation**: Store certificate password in Azure Key Vault or as a script variable in Intune, not hardcoded in the install command.

**Uninstall command**:
```powershell
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Uninstall-Client-Intune.ps1"
```

**Install behavior**: System

**Device restart behavior**: No specific action

---

## Security Considerations

### Certificate Management

**PFX Certificate Security**:
- **Never commit** the PFX file to source control
- Store PFX in a secure location (Azure Key Vault, secured file share)
- Use a strong password for the PFX file
- Rotate certificate before expiration

**Certificate Password Security**:
Three options for handling certificate password:

**Option 1: No Password (Less Secure)**
```powershell
# Export certificate without password protection
Export-PfxCertificate -Cert $cert -FilePath "SecureBootWatcher.pfx" -Password (ConvertTo-SecureString -String "" -AsPlainText -Force)

# Install command (no password parameter needed)
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Install-Client-Intune.ps1"
```

**Option 2: Intune Script Variable (Recommended)**
```powershell
# In Intune, create a script variable for the password
# Then reference it in the install command
$certPassword = $env:CERT_PASSWORD  # Set by Intune script variable
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Install-Client-Intune.ps1" -CertificatePassword $certPassword
```

**Option 3: Azure Key Vault (Most Secure)**
```powershell
# Modify install script to retrieve password from Azure Key Vault
# Requires Azure PowerShell module on target devices
$certPassword = (Get-AzKeyVaultSecret -VaultName "YourVault" -Name "CertPassword").SecretValueText
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Install-Client-Intune.ps1" -CertificatePassword $certPassword
```

**After Installation**:
- Certificate is stored in `Cert:\LocalMachine\My`
- Private key is accessible only to SYSTEM account
- PFX file is NOT copied to installation directory
- Password is not stored anywhere on the device

### Script Signing
