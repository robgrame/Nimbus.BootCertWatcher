# Intune Win32 App Deployment Guide

## Overview

This guide explains how to deploy SecureBootWatcher client as a Win32 app through Microsoft Endpoint Manager (Intune).

---

## Prerequisites

1. **Microsoft Win32 Content Prep Tool**
   - Download: https://github.com/Microsoft/Microsoft-Win32-Content-Prep-Tool
   - Used to convert package to `.intunewin` format

2. **SecureBootWatcher Client Package**
   - **Must be built first** using: `.\scripts\Deploy-Client.ps1`
   - This creates: `.\client-package\SecureBootWatcher-Client.zip`
   - Verify the ZIP exists before proceeding:
     ```powershell
     Test-Path ".\client-package\SecureBootWatcher-Client.zip"
     ```

3. **Intune Administrator Access**
   - Permission to create and deploy Win32 apps
   - Access to Microsoft Endpoint Manager admin center

4. **Optional: PFX Certificate**
   - Required only if using Azure Queue sink for authentication
   - Must be exported with private key
   - See [Certificate Management Guide](CERTIFICATE_MANAGEMENT_INTUNE.md) for details

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

Prepare the Intune package directory with the required files.

#### Option A: Using the Automated Script (Recommended)

```powershell
# Prepare the package automatically
.\scripts\Prepare-IntunePackage.ps1

# Or with custom output path
.\scripts\Prepare-IntunePackage.ps1 -OutputPath "C:\Custom\Path"

# Or including a certificate
.\scripts\Prepare-IntunePackage.ps1 -CertificatePath "C:\Path\To\SecureBootWatcher.pfx"

# Or overwrite existing directory
.\scripts\Prepare-IntunePackage.ps1 -Force
```

This script will:
- ? Verify the client package ZIP exists in `.\client-package\`
- ? Create the staging directory
- ? Copy all required files
- ? Verify package structure
- ? Provide next steps for conversion

#### Option B: Manual Preparation

If you prefer to prepare the package manually:

```powershell
# Create working directory
New-Item -ItemType Directory -Path "C:\Temp\SecureBootWatcher-Intune" -Force

# Copy the pre-built client ZIP package from client-package folder (do NOT extract it!)
# The ZIP file is located in .\client-package\ directory of the repository
Copy-Item ".\client-package\SecureBootWatcher-Client.zip" `
    -Destination "C:\Temp\SecureBootWatcher-Intune\" -Force

# Verify the ZIP file was copied
if (-not (Test-Path "C:\Temp\SecureBootWatcher-Intune\SecureBootWatcher-Client.zip")) {
    Write-Error "Failed to copy SecureBootWatcher-Client.zip"
    exit 1
}

# Copy Intune scripts to the package directory
Copy-Item ".\scripts\Install-Client-Intune.ps1" `
    -Destination "C:\Temp\SecureBootWatcher-Intune\" -Force

Copy-Item ".\scripts\Uninstall-Client-Intune.ps1" `
    -Destination "C:\Temp\SecureBootWatcher-Intune\" -Force

Copy-Item ".\scripts\Detect-Client-Intune.ps1" `
    -Destination "C:\Temp\SecureBootWatcher-Intune\" -Force

# **OPTIONAL**: Copy the PFX certificate to the package directory
# This certificate is used for Azure Queue authentication
# Copy-Item "C:\Path\To\Your\SecureBootWatcher.pfx" `
#     -Destination "C:\Temp\SecureBootWatcher-Intune\" -Force

# Verify package structure
Write-Host "`nPackage contents:" -ForegroundColor Cyan
Get-ChildItem "C:\Temp\SecureBootWatcher-Intune" | Format-Table Name, Length, LastWriteTime
```

**Expected package structure:**
```
C:\Temp\SecureBootWatcher-Intune\
??? Install-Client-Intune.ps1          (Install script)
??? Uninstall-Client-Intune.ps1        (Uninstall script)
??? Detect-Client-Intune.ps1           (Detection script)
??? SecureBootWatcher.pfx              (Certificate - OPTIONAL)
??? SecureBootWatcher-Client.zip       (Client binaries - REQUIRED)
```

**Note**: The `SecureBootWatcher-Client.zip` file must be created first by running:
```powershell
.\scripts\Deploy-Client.ps1
```
This creates the ZIP file in `.\client-package\SecureBootWatcher-Client.zip`.

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
| `RandomDelayMinutes` | int | 60 | **Maximum** random delay in minutes (0-1440). Task Scheduler will apply a random delay between 0 and this value. **Note**: Only supported for `Once` and `Daily` schedules. Not supported for `Hourly` and `Custom` schedules due to Task Scheduler limitations. |

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

**Note**: For `Hourly` and `Custom` schedules, the task will repeat for a maximum of 31 days, which is the Windows Task Scheduler limit. The task will automatically renew after this period.

**Important Limitation**: The `RandomDelayMinutes` parameter only works with `Once` and `Daily` schedules. For `Hourly` and `Custom` schedules, random delay is not supported by Windows Task Scheduler when using repetition intervals. If you need to distribute the load across devices for hourly or custom schedules, consider using different `TaskTime` values for different device groups instead of relying on random delay.

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

**Recommendation**: Sign PowerShell scripts for production deployment.

```powershell
# Create or use an existing code signing certificate
$cert = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert | Select-Object -First 1

# Sign the scripts
Set-AuthenticodeSignature -FilePath "Install-Client-Intune.ps1" -Certificate $cert
Set-AuthenticodeSignature -FilePath "Uninstall-Client-Intune.ps1" -Certificate $cert
Set-AuthenticodeSignature -FilePath "Detect-Client-Intune.ps1" -Certificate $cert
```

---

## Troubleshooting

### Installation Fails with "Package directory is empty"

**Symptom**: Installation log shows:
```
ERROR: Client package not found: C:\...\SecureBootWatcher-Client.zip
ERROR: Client package ZIP file not found in package directory
```

**Cause**: The `.intunewin` package was not prepared correctly, or the ZIP file is missing.

**Solution**:

1. **Build the client package first** (if not already done):
   ```powershell
   # From the repository root
   .\scripts\Deploy-Client.ps1
   
   # Verify the ZIP was created
   Test-Path ".\client-package\SecureBootWatcher-Client.zip"
   # Should return: True
   
   # Check the ZIP file size
   (Get-Item ".\client-package\SecureBootWatcher-Client.zip").Length / 1MB
   # Should be several MB
   ```

2. **Verify package contents before converting to `.intunewin`**:
   ```powershell
   # List files in the staging directory
   Get-ChildItem -Path "C:\Temp\SecureBootWatcher-Intune"
   
   # Should show:
   # - Install-Client-Intune.ps1
   # - Uninstall-Client-Intune.ps1
   # - Detect-Client-Intune.ps1
   # - SecureBootWatcher.pfx (optional)
   # - SecureBootWatcher-Client.zip (REQUIRED - from .\client-package\ folder)
   ```

3. **Verify the ZIP file exists and contains binaries**:
   ```powershell
   # Check ZIP file in staging directory
   Test-Path "C:\Temp\SecureBootWatcher-Intune\SecureBootWatcher-Client.zip"
   
   # List contents
   Expand-Archive -Path "C:\Temp\SecureBootWatcher-Intune\SecureBootWatcher-Client.zip" `
       -DestinationPath "C:\Temp\VerifyZip" -Force
   
   Get-ChildItem "C:\Temp\VerifyZip"
   # Should show: SecureBootWatcher.Client.exe, *.dll, appsettings.json, etc.
   
   # Clean up
   Remove-Item "C:\Temp\VerifyZip" -Recurse -Force
   ```

4. **Re-prepare the package**:
   ```powershell
   # Clean and recreate
   Remove-Item "C:\Temp\SecureBootWatcher-Intune" -Recurse -Force -ErrorAction SilentlyContinue
   New-Item -ItemType Directory -Path "C:\Temp\SecureBootWatcher-Intune" -Force
   
   # Copy the pre-built client ZIP from client-package folder (IMPORTANT - don't extract it!)
   Copy-Item ".\client-package\SecureBootWatcher-Client.zip" `
       -Destination "C:\Temp\SecureBootWatcher-Intune\" -Force
   
   # Copy Intune scripts
   Copy-Item ".\scripts\Install-Client-Intune.ps1" -Destination "C:\Temp\SecureBootWatcher-Intune\" -Force
   Copy-Item ".\scripts\Uninstall-Client-Intune.ps1" -Destination "C:\Temp\SecureBootWatcher-Intune\" -Force
   Copy-Item ".\scripts\Detect-Client-Intune.ps1" -Destination "C:\Temp\SecureBootWatcher-Intune\" -Force
   
   # Optional: Copy certificate
   # Copy-Item "C:\Path\To\SecureBootWatcher.pfx" -Destination "C:\Temp\SecureBootWatcher-Intune\" -Force
   ```

### Manual Installation Testing

Before deploying via Intune, test the installation manually:

```powershell
# 1. Prepare test directory
New-Item -ItemType Directory -Path "C:\Temp\TestInstall" -Force

# 2. Copy the client ZIP package (IMPORTANT - don't extract it!)
Copy-Item ".\client-package\SecureBootWatcher-Client.zip" -Destination "C:\Temp\TestInstall\" -Force

# 3. Copy scripts
Copy-Item ".\scripts\Install-Client-Intune.ps1" -Destination "C:\Temp\TestInstall\" -Force
Copy-Item ".\scripts\Uninstall-Client-Intune.ps1" -Destination "C:\Temp\TestInstall\" -Force
Copy-Item ".\scripts\Detect-Client-Intune.ps1" -Destination "C:\Temp\TestInstall\" -Force

# 4. Optional: Copy certificate
Copy-Item "C:\Path\To\SecureBootWatcher.pfx" -Destination "C:\Temp\TestInstall\" -Force

# 5. Run installation (simulates Intune behavior)
cd C:\Temp\TestInstall
.\Install-Client-Intune.ps1 `
    -ApiBaseUrl "https://test-api.contoso.com" `
    -FleetId "test-fleet" `
    -CertificatePassword "YourPassword" `
    -ScheduleType Daily `
    -TaskTime "09:00AM" `
    -RandomDelayMinutes 60

# 6. Verify installation
Get-ChildItem "C:\Program Files\SecureBootWatcher"
Get-ScheduledTask -TaskName "SecureBootWatcher"
Get-Content "$env:ProgramData\SecureBootWatcher\install.log"

# 7. Test detection
.\Detect-Client-Intune.ps1
echo $LASTEXITCODE  # Should be 0 if installed

# 8. Test the client runs
cd "C:\Program Files\SecureBootWatcher"
.\SecureBootWatcher.Client.exe

# 9. Clean up
cd C:\Temp\TestInstall
.\Uninstall-Client-Intune.ps1
