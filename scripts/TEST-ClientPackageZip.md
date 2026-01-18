# Test Guide - Client Package ZIP Creation

## Overview

This document provides testing steps for the new standalone client ZIP package feature added to `Create-DeploymentPackage.ps1`.

## What Was Changed

### `Create-DeploymentPackage.ps1`

1. **New Function**: `Create-ClientPackageZip`
   - Creates a standalone ZIP file: `SecureBootWatcher-Client-v{Version}.zip`
   - Generates SHA256 checksum file
   - Creates a README with deployment instructions
   - Located in the main output path (not inside the main package)

2. **Execution Flow**:
   - Step 5 (new): Creates standalone client package ZIP
   - All subsequent steps renumbered (6-13)

3. **Output Files**:
   - `SecureBootWatcher-Client-v{Version}.zip` - Standalone client package
   - `SecureBootWatcher-Client-v{Version}.zip.sha256` - Checksum file
   - `SecureBootWatcher-Client-v{Version}-README.txt` - Deployment instructions

## Test Scenarios

### Test 1: Basic Package Creation

```powershell
# Run the script with default settings
cd C:\Users\nefario\source\repos\robgrame\Nimbus.BootCertWatcher\scripts
.\Create-DeploymentPackage.ps1 -Version "1.5.2" -SkipTests

# Expected output files in .\deploy\packages:
# - SecureBootDashboard-Deploy-v1.5.2.zip (main package)
# - SecureBootDashboard-Deploy-v1.5.2 (folder)
# - SecureBootWatcher-Client-v1.5.2.zip (NEW - standalone client)
# - SecureBootWatcher-Client-v1.5.2.zip.sha256 (NEW)
# - SecureBootWatcher-Client-v1.5.2-README.txt (NEW)
```

**Verify**:
- [ ] Client ZIP file exists
- [ ] Client ZIP contains all files from `binaries\client`
- [ ] SHA256 checksum file is created
- [ ] README file is created with correct content
- [ ] Log shows success message for client package creation

### Test 2: Verify Client ZIP Contents

```powershell
# Extract and verify client ZIP contents
$zipPath = ".\deploy\packages\SecureBootWatcher-Client-v1.5.2.zip"
$extractPath = ".\deploy\test-extract"

# Extract
Expand-Archive -Path $zipPath -DestinationPath $extractPath -Force

# Verify contents
Get-ChildItem $extractPath -Recurse | Select-Object Name, Length

# Expected files:
# - SecureBootWatcher.Client.exe
# - appsettings.json
# - All .dll dependencies
# - NO appsettings.local.json (should be removed)
```

**Verify**:
- [ ] Client executable exists
- [ ] appsettings.json exists
- [ ] All dependencies are present
- [ ] No development files (appsettings.local.json)

### Test 3: SHA256 Verification

```powershell
# Verify checksum
$zipPath = ".\deploy\packages\SecureBootWatcher-Client-v1.5.2.zip"
$checksumFile = "$zipPath.sha256"

# Read stored checksum
$storedChecksum = (Get-Content $checksumFile).Split()[0]

# Calculate actual checksum
$actualChecksum = (Get-FileHash -Path $zipPath -Algorithm SHA256).Hash

# Compare
if ($storedChecksum -eq $actualChecksum) {
    Write-Host "? Checksum verified successfully" -ForegroundColor Green
} else {
    Write-Host "? Checksum verification FAILED" -ForegroundColor Red
}
```

**Verify**:
- [ ] Checksums match
- [ ] Checksum file format is correct

### Test 4: Deploy-Client.ps1 Integration

```powershell
# Test deployment using the new ZIP file
$zipPath = ".\deploy\packages\SecureBootWatcher-Client-v1.5.2.zip"

# Test extraction and configuration (without creating scheduled task)
.\Deploy-Client.ps1 `
    -PackageZipPath $zipPath `
    -ApiBaseUrl "https://test-api.contoso.com" `
    -FleetId "test-fleet"

# Check if temp extraction worked
# Check if appsettings.json was updated with API URL and Fleet ID
```

**Verify**:
- [ ] Script accepts the ZIP file path
- [ ] Extraction to temp directory works
- [ ] Configuration is updated correctly
- [ ] Cleanup happens after script completes

### Test 5: Full Deployment Test

```powershell
# WARNING: This will create a scheduled task
# Test full deployment with scheduled task creation
$zipPath = ".\deploy\packages\SecureBootWatcher-Client-v1.5.2.zip"

.\Deploy-Client.ps1 `
    -PackageZipPath $zipPath `
    -ApiBaseUrl "https://test-api.contoso.com" `
    -FleetId "test-fleet" `
    -CreateScheduledTask `
    -ScheduleType Custom `
    -RepeatEveryHours 4 `
    -InstallPath "C:\Program Files\SecureBootWatcher-Test"

# Verify installation
Get-ScheduledTask -TaskName "SecureBootWatcher" -ErrorAction SilentlyContinue
Get-ChildItem "C:\Program Files\SecureBootWatcher-Test"

# Cleanup after test
Unregister-ScheduledTask -TaskName "SecureBootWatcher" -Confirm:$false
Remove-Item "C:\Program Files\SecureBootWatcher-Test" -Recurse -Force
```

**Verify**:
- [ ] Files installed to correct location
- [ ] Scheduled task created successfully
- [ ] Configuration file has correct values
- [ ] Client executable is present

### Test 6: Package with Azure Certificate

```powershell
# Test with Azure certificate generation
# NOTE: The certificate generation step MUST run BEFORE client ZIP creation
.\Create-DeploymentPackage.ps1 `
    -Version "1.5.2-test" `
    -GenerateAzureCertificate `
    -AzureCertificatePassword "TestPassword123!" `
    -SkipTests

# Verify the execution order in the log:
# Step 6: Generate Azure certificate (if requested)  <- Certificate created here
# Step 7: Create standalone client package ZIP       <- Uses certificate from Step 6

# Check that client ZIP includes Azure certificate
$zipPath = ".\deploy\packages\SecureBootWatcher-Client-v1.5.2-test.zip"
$extractPath = ".\deploy\test-cert-extract"

Expand-Archive -Path $zipPath -DestinationPath $extractPath -Force

# Verify certificate files exist
$pfxExists = Test-Path "$extractPath\certificates\AzureAppRegistration.pfx"
$instructionsExist = Test-Path "$extractPath\certificates\INSTALL-CERTIFICATE.txt"

if ($pfxExists -and $instructionsExist) {
    Write-Host "? Certificate correctly included in client ZIP" -ForegroundColor Green
} else {
    Write-Host "? Certificate NOT found in client ZIP" -ForegroundColor Red
    Write-Host "  PFX exists: $pfxExists"
    Write-Host "  Instructions exist: $instructionsExist"
}

# Cleanup
Remove-Item $extractPath -Recurse -Force
```

**Verify**:
- [ ] Client ZIP created successfully
- [ ] Client ZIP INCLUDES Azure certificate (.pfx)
- [ ] Client ZIP includes certificate installation instructions
- [ ] Main package also has Azure certificate in certificates/ folder
- [ ] Both packages work independently
- [ ] Certificate password is documented in INSTALL-CERTIFICATE.txt

### Test 7: Automatic Certificate Installation

```powershell
# Prerequisites: Run Test 6 first to generate package with certificate

$zipPath = ".\deploy\packages\SecureBootWatcher-Client-v1.5.2-test.zip"

# Test automatic certificate installation during deployment
.\Deploy-Client.ps1 `
    -PackageZipPath $zipPath `
    -CreateScheduledTask `
    -InstallPath "C:\Program Files\SecureBootWatcher-CertTest"

# Verify certificate was installed
$certs = Get-ChildItem Cert:\LocalMachine\My | Where-Object { $_.Subject -like "*SecureBootDashboard*" }

if ($certs) {
    Write-Host "? Certificate installed successfully" -ForegroundColor Green
    Write-Host "Thumbprint: $($certs[0].Thumbprint)"
    
    # Verify appsettings.json was updated
    $appsettings = Get-Content "C:\Program Files\SecureBootWatcher-CertTest\appsettings.json" -Raw | ConvertFrom-Json
    
    if ($appsettings.SecureBootWatcher.Sinks.AzureQueue.CertificateThumbprint -eq $certs[0].Thumbprint) {
        Write-Host "? appsettings.json updated with certificate thumbprint" -ForegroundColor Green
    }
} else {
    Write-Host "? Certificate not found" -ForegroundColor Red
}

# Verify certificate files were removed from disk (security)
$certFileExists = Test-Path "C:\Program Files\SecureBootWatcher-CertTest\certificates\AzureAppRegistration.pfx"
if (-not $certFileExists) {
    Write-Host "? Certificate file removed from disk (security best practice)" -ForegroundColor Green
}

# Cleanup
Unregister-ScheduledTask -TaskName "SecureBootWatcher" -Confirm:$false
Remove-Item "C:\Program Files\SecureBootWatcher-CertTest" -Recurse -Force

# Remove installed certificate
$certs | ForEach-Object { Remove-Item "Cert:\LocalMachine\My\$($_.Thumbprint)" -Force }
```

**Verify**:
- [ ] Certificate automatically installed to LocalMachine\My
- [ ] Certificate accessible by SYSTEM account
- [ ] appsettings.json updated with correct thumbprint
- [ ] Certificate .pfx file removed from disk after installation
- [ ] INSTALL-CERTIFICATE.txt removed after installation
- [ ] No security warnings or errors during installation

## Expected Summary Output

After running `Create-DeploymentPackage.ps1`, the summary should show:

```
===============================================================================
Deployment Package Creation Complete!
===============================================================================

Package Details:
  Name: SecureBootDashboard-Deploy-v1.5.2
  Version: 1.5.2
  Configuration: Release
  Location: .\deploy\packages\SecureBootDashboard-Deploy-v1.5.2.zip

Package Contents:
  ? API Binaries (ASP.NET Core 10)
  ? Web Binaries (ASP.NET Core 10)
  ? Client Binaries (.NET Framework 4.8)
  ? Database Scripts (EF Core migrations)
  ? Azure App Registration Certificate
    Password: [certificate password]
  ? Configuration Templates
  ? Deployment Scripts
  ? Documentation
  ? Standalone Client ZIP Package

Client Package:
  Standalone ZIP: SecureBootWatcher-Client-v1.5.2.zip
  Location: .\deploy\packages
  Use with Deploy-Client.ps1 -PackageZipPath parameter
  Includes: Azure certificate for Storage Account authentication
```

## Troubleshooting

### Issue: Client ZIP not created

**Check**:
1. Verify client binaries exist in `binaries\client` folder
2. Check log file for errors during Step 5
3. Ensure output directory has write permissions

### Issue: SHA256 checksum mismatch

**Check**:
1. Re-run `Create-DeploymentPackage.ps1` to regenerate package
2. Verify no manual modifications to ZIP file
3. Check disk integrity

### Issue: Deploy-Client.ps1 can't find ZIP

**Check**:
1. Use full path to ZIP file
2. Verify ZIP file exists at specified location
3. Check ZIP file extension (.zip)

## Success Criteria

All tests pass when:

1. ? Client ZIP file is created in output directory
2. ? Client ZIP contains all necessary files
3. ? SHA256 checksum is valid
4. ? README file has correct instructions
5. ? Deploy-Client.ps1 can use the ZIP file
6. ? Full deployment creates scheduled task correctly
7. ? No leftover temporary files after deployment
8. ? Summary output shows client package information

## Cleanup After Testing

```powershell
# Remove test packages
Remove-Item ".\deploy\packages\*-test*" -Force -ErrorAction SilentlyContinue
Remove-Item ".\deploy\test-extract" -Recurse -Force -ErrorAction SilentlyContinue

# Remove test scheduled task if exists
Unregister-ScheduledTask -TaskName "SecureBootWatcher" -Confirm:$false -ErrorAction SilentlyContinue

# Remove test installation
Remove-Item "C:\Program Files\SecureBootWatcher-Test" -Recurse -Force -ErrorAction SilentlyContinue
```

## Notes

- The standalone client ZIP is independent from the main deployment package
- Both the main package and client ZIP contain the same client binaries
- The client ZIP is designed for easy distribution via Intune/SCCM
- The client ZIP can be used multiple times without extracting the main package
