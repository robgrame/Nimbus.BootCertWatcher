# Certificate Management Guide for Intune Deployment

## Overview

This guide explains how to export, prepare, and deploy the PFX certificate used by SecureBootWatcher client for Azure Queue authentication.

---

## Prerequisites

- Access to Azure Portal (Entra ID)
- Application Registration with certificate configured
- Permissions to export certificates
- PowerShell 5.1+ with Azure modules (optional)

---

## Step 1: Export Certificate from Azure App Registration

### Option A: Export from Azure Portal (Recommended)

1. **Navigate to App Registration**:
   - Azure Portal ? Entra ID ? App registrations
   - Select your app: `SecureBootWatcher` (or your app name)
   - Go to: **Certificates & secrets**

2. **Download Public Certificate**:
   - Find your certificate in the list
   - Click **Download** (downloads `.cer` file - public key only)
   
   **Note**: This is NOT what you need. You need the PFX with private key.

### Option B: Export from Certificate Store (If you have the cert locally)

If the certificate was created on your machine and uploaded to Azure:

```powershell
# 1. Find the certificate
$thumbprint = "61FC110D5BABD61419B106862B304C2FFF57A262"  # Replace with your thumbprint
$cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object {$_.Thumbprint -eq $thumbprint}

# Verify it has private key
if (-not $cert.HasPrivateKey) {
    Write-Host "ERROR: Certificate does not have private key!" -ForegroundColor Red
    exit 1
}

# 2. Export to PFX with password
$pfxPassword = Read-Host -AsSecureString -Prompt "Enter PFX password"
$pfxPath = ".\SecureBootWatcher.pfx"

Export-PfxCertificate `
    -Cert $cert `
    -FilePath $pfxPath `
    -Password $pfxPassword `
    -ChainOption BuildChain

Write-Host "Certificate exported to: $pfxPath" -ForegroundColor Green
```

### Option C: Create New Self-Signed Certificate (For Testing)

**Warning**: Self-signed certificates are for development/testing only!

```powershell
# 1. Create self-signed certificate
$certSubject = "CN=SecureBootWatcher-Test"
$cert = New-SelfSignedCertificate `
    -Subject $certSubject `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -KeyExportPolicy Exportable `
    -KeySpec Signature `
    -KeyLength 2048 `
    -KeyAlgorithm RSA `
    -HashAlgorithm SHA256 `
    -NotAfter (Get-Date).AddYears(2)

Write-Host "Certificate created with thumbprint: $($cert.Thumbprint)" -ForegroundColor Green

# 2. Export to PFX
$pfxPassword = ConvertTo-SecureString -String "YourStrongPassword123!" -AsPlainText -Force
Export-PfxCertificate `
    -Cert $cert `
    -FilePath ".\SecureBootWatcher.pfx" `
    -Password $pfxPassword

# 3. Upload public key to Azure App Registration
$cerPath = ".\SecureBootWatcher.cer"
Export-Certificate -Cert $cert -FilePath $cerPath

Write-Host "Upload $cerPath to Azure App Registration > Certificates & secrets" -ForegroundColor Yellow
```

---

## Step 2: Prepare Certificate for Intune Deployment

### Verify Certificate File

```powershell
# Test if PFX can be imported
$pfxPath = ".\SecureBootWatcher.pfx"
$testPassword = Read-Host -AsSecureString -Prompt "Enter PFX password to test"

try {
    $testCert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2
    $testCert.Import($pfxPath, $testPassword, [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::DefaultKeySet)
    
    Write-Host "Certificate is valid!" -ForegroundColor Green
    Write-Host "  Thumbprint: $($testCert.Thumbprint)"
    Write-Host "  Subject: $($testCert.Subject)"
    Write-Host "  Issuer: $($testCert.Issuer)"
    Write-Host "  Expires: $($testCert.NotAfter)"
    Write-Host "  Has Private Key: $($testCert.HasPrivateKey)"
}
catch {
    Write-Host "ERROR: Certificate test failed: $_" -ForegroundColor Red
    exit 1
}
```

### Rename Certificate File

The install script expects the certificate to be named exactly `SecureBootWatcher.pfx`:

```powershell
# Rename if needed
Rename-Item -Path "YourOriginalName.pfx" -NewName "SecureBootWatcher.pfx"
```

---

## Step 3: Update appsettings.json

Update the client configuration to match your certificate:

```json
{
  "SecureBootWatcher": {
    "Sinks": {
      "EnableAzureQueue": true,
      "AzureQueue": {
        "QueueServiceUri": "https://yourstorageaccount.queue.core.windows.net",
        "QueueName": "secureboot-reports",
        "AuthenticationMethod": "Certificate",
        "TenantId": "your-tenant-id",
        "ClientId": "your-client-id",
        "CertificateThumbprint": "61FC110D5BABD61419B106862B304C2FFF57A262",
        "CertificateStoreLocation": "LocalMachine",
        "CertificateStoreName": "My"
      }
    }
  }
}
```

**Important**: Update these values:
- `CertificateThumbprint`: Must match your certificate's thumbprint (no spaces, no colons)
- `TenantId`: Your Entra ID tenant ID
- `ClientId`: Your app registration client ID
- `QueueServiceUri`: Your Azure Storage account URL

---

## Step 4: Add Certificate to Intune Package

```powershell
# 1. Extract client package
$workDir = "C:\Temp\SecureBootWatcher-Intune"
New-Item -ItemType Directory -Path $workDir -Force | Out-Null

Expand-Archive -Path ".\client-package\SecureBootWatcher-Client.zip" `
    -DestinationPath $workDir -Force

# 2. Copy scripts
Copy-Item ".\scripts\Install-Client-Intune.ps1" -Destination $workDir
Copy-Item ".\scripts\Uninstall-Client-Intune.ps1" -Destination $workDir
Copy-Item ".\scripts\Detect-Client-Intune.ps1" -Destination $workDir

# 3. Copy certificate
Copy-Item ".\SecureBootWatcher.pfx" -Destination $workDir

# 4. Verify package contents
Write-Host "Package contents:" -ForegroundColor Cyan
Get-ChildItem $workDir | Format-Table Name, Length
```

**Expected files**:
```
Name                              Length
----                              ------
Install-Client-Intune.ps1         ~8 KB
Uninstall-Client-Intune.ps1       ~3 KB
Detect-Client-Intune.ps1          ~2 KB
SecureBootWatcher.pfx             ~2-5 KB
SecureBootWatcher.Client.exe      ~500 KB
SecureBootWatcher.Shared.dll      ~50 KB
appsettings.json                  ~3 KB
[Other DLLs...]                   Various
```

---

## Step 5: Configure Intune Install Command

### Without Password (Not Recommended)

If you exported the PFX without a password:

```powershell
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Install-Client-Intune.ps1"
```

### With Password (Recommended)

**Option 1: Hardcoded (Development Only)**
```powershell
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Install-Client-Intune.ps1" -CertificatePassword "YourPassword123!"
```

**Security Warning**: Password is visible in Intune portal and deployment logs!

**Option 2: Environment Variable (Better)**
```powershell
# In Intune, set script as:
$env:CERT_PASSWORD = "YourPassword123!"  # Set via Intune Script Variable
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Install-Client-Intune.ps1" -CertificatePassword $env:CERT_PASSWORD
```

**Option 3: Azure Key Vault (Production)**

Modify install script to retrieve password from Key Vault:

```powershell
# Add to Install-Client-Intune.ps1 before certificate import

# Retrieve password from Azure Key Vault (requires Az.KeyVault module)
if ([string]::IsNullOrEmpty($CertificatePassword)) {
    try {
        Import-Module Az.KeyVault -ErrorAction Stop
        $secret = Get-AzKeyVaultSecret -VaultName "YourKeyVault" -Name "SecureBootCertPassword" -AsPlainText
        $CertificatePassword = $secret
        Write-InstallLog "Retrieved certificate password from Key Vault"
    }
    catch {
        Write-InstallLog "WARNING: Could not retrieve password from Key Vault: $_"
    }
}
```

Then install command:
```powershell
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "Install-Client-Intune.ps1"
```

---

## Step 6: Test Certificate Import Locally

Before deploying via Intune, test the import process:

```powershell
# Run install script locally with verbose logging
$VerbosePreference = "Continue"

.\Install-Client-Intune.ps1 `
    -CertificatePassword "YourPassword123!" `
    -ApiBaseUrl "https://your-api.azurewebsites.net" `
    -FleetId "test-fleet" `
    -Verbose

# Verify certificate imported
$imported = Get-ChildItem Cert:\LocalMachine\My | 
    Where-Object {$_.Subject -like "*SecureBoot*"}

if ($imported) {
    Write-Host "Certificate imported successfully!" -ForegroundColor Green
    Write-Host "  Thumbprint: $($imported.Thumbprint)"
    Write-Host "  Has Private Key: $($imported.HasPrivateKey)"
} else {
    Write-Host "Certificate NOT found in store!" -ForegroundColor Red
}

# Check install log
Get-Content "C:\ProgramData\SecureBootWatcher\install.log" -Tail 20
```

---

## Step 7: Verify Installation on Target Device

After Intune deployment, verify on a test device:

```powershell
# 1. Check certificate in store
Get-ChildItem Cert:\LocalMachine\My | 
    Where-Object {$_.Thumbprint -eq "61FC110D5BABD61419B106862B304C2FFF57A262"} |
    Format-List Thumbprint, Subject, NotAfter, HasPrivateKey

# 2. Check install log
Get-Content "C:\ProgramData\SecureBootWatcher\install.log"

# 3. Check appsettings.json
Get-Content "C:\Program Files\SecureBootWatcher\appsettings.json" | ConvertFrom-Json | 
    Select-Object -ExpandProperty SecureBootWatcher | 
    Select-Object -ExpandProperty Sinks |
    Select-Object -ExpandProperty AzureQueue

# 4. Test client execution
cd "C:\Program Files\SecureBootWatcher"
.\SecureBootWatcher.Client.exe

# Should see in logs:
# [Information] Using Certificate authentication for Azure Queue
# [Information] Certificate loaded from store: 61FC110D5BABD61419B106862B304C2FFF57A262
```

---

## Troubleshooting

### Issue: "Certificate import failed: The specified network password is not correct"

**Cause**: Wrong password provided

**Fix**:
1. Verify password is correct
2. Try exporting certificate again
3. Test import locally before deploying

```powershell
# Test password locally
$pfxPath = ".\SecureBootWatcher.pfx"
$testPassword = ConvertTo-SecureString -String "YourPassword" -AsPlainText -Force

try {
    Import-PfxCertificate -FilePath $pfxPath -CertStoreLocation Cert:\CurrentUser\My -Password $testPassword
    Write-Host "Password is correct!" -ForegroundColor Green
} catch {
    Write-Host "Password is incorrect or file is corrupted!" -ForegroundColor Red
}
```

### Issue: "Certificate does NOT have private key"

**Cause**: Certificate was exported without private key

**Fix**: Re-export with private key:

```powershell
# Ensure -Exportable flag during certificate creation
$cert = New-SelfSignedCertificate ... -KeyExportPolicy Exportable ...

# Or when exporting
Export-PfxCertificate -Cert $cert ... # PFX format includes private key
```

### Issue: "SYSTEM account cannot access private key"

**Cause**: Permissions not granted correctly

**Fix**: Run permission script manually:

```powershell
# Find certificate
$thumbprint = "61FC110D5BABD61419B106862B304C2FFF57A262"
$cert = Get-ChildItem Cert:\LocalMachine\My | Where-Object {$_.Thumbprint -eq $thumbprint}

# Get private key path
$rsaCert = [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($cert)
$keyName = $rsaCert.Key.UniqueName
$keyPath = "C:\ProgramData\Microsoft\Crypto\RSA\MachineKeys\$keyName"

# Grant permissions
icacls $keyPath /grant "SYSTEM:(R)"
```

### Issue: Certificate expired

**Cause**: Certificate validity period has ended

**Fix**: Create new certificate and update deployment:

```powershell
# Check expiration
$cert = Get-ChildItem Cert:\LocalMachine\My | Where-Object {$_.Thumbprint -eq $thumbprint}
Write-Host "Expires: $($cert.NotAfter)"

# If expired, create new certificate and update:
# 1. Create new certificate (see Step 1)
# 2. Upload to Azure App Registration
# 3. Update appsettings.json with new thumbprint
# 4. Redeploy package
```

---

## Security Best Practices

### Certificate Storage

- ? **DO**: Store PFX in secure location (Azure Key Vault, encrypted share)
- ? **DON'T**: Commit PFX to Git repository
- ? **DON'T**: Email PFX files
- ? **DON'T**: Store on unencrypted network shares

### Password Management

- ? **DO**: Use strong passwords (12+ characters, mixed case, numbers, symbols)
- ? **DO**: Store password in Azure Key Vault for production
- ? **DO**: Use different passwords for dev/test/prod
- ? **DON'T**: Hardcode passwords in scripts
- ? **DON'T**: Store passwords in plain text files

### Certificate Lifecycle

- ? **DO**: Set expiration reminders (90 days before)
- ? **DO**: Document certificate renewal process
- ? **DO**: Test certificate rotation in dev/test first
- ? **DO**: Keep audit trail of certificate changes
- ? **DON'T**: Wait until last minute to renew

### Access Control

- ? **DO**: Limit who can export certificates
- ? **DO**: Use Azure RBAC for Key Vault access
- ? **DO**: Monitor certificate access logs
- ? **DON'T**: Share certificates between environments

---

## Certificate Renewal Process

When certificate is about to expire (90 days or less):

### Step 1: Create New Certificate

```powershell
# Option A: Request new cert from CA
# Option B: Create new self-signed (testing)
$newCert = New-SelfSignedCertificate ... -NotAfter (Get-Date).AddYears(2)
```

### Step 2: Upload to Azure

1. Azure Portal ? Entra ID ? App registrations ? Your App
2. Certificates & secrets ? Upload certificate
3. Note new thumbprint

### Step 3: Update Configuration

Update `appsettings.json`:
```json
{
  "AzureQueue": {
    "CertificateThumbprint": "NEW_THUMBPRINT_HERE"
  }
}
```

### Step 4: Redeploy

1. Build new package with updated config
2. Update Intune deployment
3. Monitor deployment status

### Step 5: Cleanup

After successful deployment:
1. Remove old certificate from Azure App Registration
2. Document change in change log
3. Update documentation with new thumbprint

---

## Quick Reference

### Export Certificate
```powershell
$cert = Get-ChildItem Cert:\CurrentUser\My | Where {$_.Thumbprint -eq "THUMBPRINT"}
$password = ConvertTo-SecureString -String "Password123!" -AsPlainText -Force
Export-PfxCertificate -Cert $cert -FilePath "SecureBootWatcher.pfx" -Password $password
```

### Test Import
```powershell
$password = ConvertTo-SecureString -String "Password123!" -AsPlainText -Force
Import-PfxCertificate -FilePath "SecureBootWatcher.pfx" -CertStoreLocation Cert:\CurrentUser\My -Password $password
```

### Check Certificate
```powershell
Get-ChildItem Cert:\LocalMachine\My | Where {$_.Thumbprint -eq "THUMBPRINT"} | Format-List *
```

### Grant Permissions
```powershell
# Find key file
$cert = Get-ChildItem Cert:\LocalMachine\My | Where {$_.Thumbprint -eq "THUMBPRINT"}
$rsaCert = [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($cert)
$keyPath = "C:\ProgramData\Microsoft\Crypto\RSA\MachineKeys\$($rsaCert.Key.UniqueName)"

# Grant permissions
icacls $keyPath /grant "SYSTEM:(R)"
```

---

## Additional Resources

- **Azure Certificate Docs**: https://docs.microsoft.com/azure/active-directory/develop/howto-create-service-principal-portal
- **PowerShell Certificate Management**: https://docs.microsoft.com/powershell/module/pki/
- **Intune Win32 Apps**: https://docs.microsoft.com/mem/intune/apps/apps-win32-app-management

---

**Last Updated**: 2025-01-08  
**Version**: 1.0  
**Related Docs**: 
- `INTUNE_WIN32_DEPLOYMENT.md`
- `Install-Client-Intune.ps1`
