# ?? Precompiled Package Deployment - Quick Guide

## ?? What's New?

The `Deploy-Client.ps1` script now supports **installation from precompiled ZIP packages**, making deployment easier when you don't have access to source code or build tools.

---

## ? Key Benefits

| Before | After (with `-PackageZipPath`) |
|--------|-------------------------------|
| ? Required .NET SDK 8.0+ | ? No SDK required |
| ? Required Visual Studio / Build Tools | ? No build tools needed |
| ? Build on every machine | ? Build once, deploy many |
| ? 5-10 minutes deployment time | ? 30 seconds deployment time |
| ?? Inconsistent binaries across machines | ? Consistent binaries everywhere |

---

## ?? Quick Start

### Scenario 1: Build Server + Target Machines

**On Build Server (once):**
```powershell
# Build and create package
.\scripts\Deploy-Client.ps1 `
    -ApiBaseUrl "https://api.contoso.com" `
    -FleetId "fleet-prod"

# Output: .\client-package\SecureBootWatcher-Client.zip
```

**On Target Machines (many):**
```powershell
# Install from precompiled package (no build tools required!)
.\scripts\Deploy-Client.ps1 `
    -PackageZipPath "\\fileserver\packages\SecureBootWatcher-Client.zip" `
    -CreateScheduledTask
```

**Done!** ? Client installed and scheduled in ~30 seconds.

---

## ?? Usage Examples

### Example 1: Install from Local ZIP
```powershell
.\scripts\Deploy-Client.ps1 `
    -PackageZipPath "C:\Temp\SecureBootWatcher-Client.zip" `
    -CreateScheduledTask
```

### Example 2: Install from Network Share
```powershell
.\scripts\Deploy-Client.ps1 `
    -PackageZipPath "\\fileserver\software\SecureBootWatcher-Client.zip" `
    -CreateScheduledTask
```

### Example 3: Install with Custom API URL
```powershell
.\scripts\Deploy-Client.ps1 `
    -PackageZipPath ".\client-package\SecureBootWatcher-Client.zip" `
    -ApiBaseUrl "https://api-prod.contoso.com" `
    -FleetId "fleet-prod" `
    -CreateScheduledTask
```

### Example 4: Install to Custom Location
```powershell
.\scripts\Deploy-Client.ps1 `
    -PackageZipPath ".\client-package\SecureBootWatcher-Client.zip" `
    -InstallPath "C:\Apps\SecureBootWatcher" `
    -CreateScheduledTask
```

### Example 5: Configure Only (No Install)
```powershell
# Just update appsettings.json in the package
.\scripts\Deploy-Client.ps1 `
    -PackageZipPath ".\client-package\SecureBootWatcher-Client.zip" `
    -ApiBaseUrl "https://api.contoso.com"

# Package is updated but not installed
# Use -CreateScheduledTask to install
```

---

## ?? Deployment Workflows

### Workflow 1: Group Policy (GPO)

**Step 1: Build on Admin Workstation**
```powershell
.\scripts\Deploy-Client.ps1 `
    -ApiBaseUrl "https://api.contoso.com" `
    -FleetId "fleet-corporate" `
    -OutputPath "\\fileserver\NETLOGON\SecureBootWatcher"
```

**Step 2: Create GPO Startup Script**
```powershell
# install-secureboot.ps1
$pkg = "\\fileserver\NETLOGON\SecureBootWatcher\SecureBootWatcher-Client.zip"
$script = "\\fileserver\NETLOGON\SecureBootWatcher\Deploy-Client.ps1"

Set-ExecutionPolicy Bypass -Scope Process -Force
& $script -PackageZipPath $pkg -CreateScheduledTask
```

**Step 3: Apply GPO**
- Computer Configuration ? Scripts ? Startup ? Add script
- Deploy to target OU

**Result**: All computers in OU install client on next reboot.

---

### Workflow 2: Manual Installation (IT Admin)

```powershell
# 1. Copy package to target machine
Copy-Item "\\fileserver\packages\SecureBootWatcher-Client.zip" -Destination "C:\Temp\"

# 2. Install
cd C:\Temp
.\Deploy-Client.ps1 `
    -PackageZipPath "C:\Temp\SecureBootWatcher-Client.zip" `
    -CreateScheduledTask

# 3. Test
Start-ScheduledTask -TaskName SecureBootWatcher
```

---

### Workflow 3: Intune Deployment

**Step 1: Prepare Package**
```powershell
# Create package with config
.\scripts\Deploy-Client.ps1 `
    -ApiBaseUrl "https://api.contoso.com" `
    -FleetId "intune-devices"
```

**Step 2: Create Intunewin**
```powershell
# Use Microsoft Win32 Content Prep Tool
.\IntuneWinAppUtil.exe `
    -c ".\client-package" `
    -s "SecureBootWatcher-Client.zip" `
    -o ".\intune-package"
```

**Step 3: Create Win32 App**
- **Install**: `powershell.exe -ExecutionPolicy Bypass -File Deploy-Client.ps1 -PackageZipPath "%cd%\SecureBootWatcher-Client.zip" -CreateScheduledTask`
- **Detection**: File exists at `C:\Program Files\SecureBootWatcher\SecureBootWatcher.Client.exe`

---

## ??? How It Works

### Traditional Method (Build from Source)
```
1. dotnet publish (5-10 minutes)
   ?
2. Configure appsettings.json
   ?
3. Create ZIP package
   ?
4. Install (if -CreateScheduledTask)
```

### New Method (Precompiled Package)
```
1. Validate ZIP exists
   ?
2. Extract to temp directory
   ?
3. Configure appsettings.json in temp
   ?
4. Install to target directory
   ?
5. Cleanup temp directory
```

**Time Saved**: ~95% faster deployment (30 sec vs 5-10 min)

---

## ?? Comparison

| Feature | `-SkipBuild` | `-PackageZipPath` |
|---------|-------------|-------------------|
| **Requires source code** | ? Yes | ? No |
| **Requires .NET SDK** | ? Yes | ? No |
| **Requires publish directory** | ? Yes | ? No |
| **Works with ZIP only** | ? No | ? Yes |
| **Configuration support** | ? Yes | ? Yes |
| **Installation support** | ? Yes | ? Yes |
| **Best for** | Re-packaging after build | Production deployment |

---

## ?? Parameter Details

### `-PackageZipPath`

**Type**: `string`  
**Required**: No  
**Default**: Empty (uses build mode)

**Description**:  
Path to a precompiled SecureBootWatcher client ZIP package. When specified, the script skips the build process and installs directly from the package.

**Accepts**:
- Local paths: `C:\Temp\package.zip`
- Relative paths: `.\client-package\SecureBootWatcher-Client.zip`
- UNC paths: `\\fileserver\share\package.zip`

**Validation**:
- File must exist
- File must have `.zip` extension

**Example Package Structure**:
```
SecureBootWatcher-Client.zip
??? SecureBootWatcher.Client.exe
??? appsettings.json
??? *.dll (dependencies)
??? ... (other files)
```

---

## ? Validation Checks

The script performs the following validations:

### 1. Package Exists
```powershell
if (-not (Test-Path $PackageZipPath)) {
    Write-Error "Package not found at: $PackageZipPath"
    exit 1
}
```

### 2. Package is ZIP
```powershell
if (-not ($PackageZipPath -like "*.zip")) {
    Write-Error "Package must be a ZIP file"
    exit 1
}
```

### 3. Extraction Success
```powershell
try {
    Expand-Archive -Path $PackageZipPath -DestinationPath $tempPath -Force
} catch {
    Write-Error "Extraction failed: $_"
    exit 1
}
```

---

## ?? Troubleshooting

### Error: Package not found

**Problem**: `Package not found at: <path>`

**Solutions**:
```powershell
# 1. Check if file exists
Test-Path "C:\Temp\SecureBootWatcher-Client.zip"

# 2. Use absolute path
$pkg = Resolve-Path ".\client-package\SecureBootWatcher-Client.zip"
.\Deploy-Client.ps1 -PackageZipPath $pkg -CreateScheduledTask

# 3. Check network share connectivity
Test-Path "\\fileserver\packages"
```

---

### Error: Package must be a ZIP file

**Problem**: File extension is not `.zip`

**Solutions**:
```powershell
# 1. Check file extension
Get-Item "C:\Temp\package.rar" | Select-Object Extension

# 2. Rename if compressed with wrong extension
Rename-Item "package.rar" -NewName "package.zip"

# 3. Re-create package with correct extension
.\Deploy-Client.ps1  # Creates .zip by default
```

---

### Error: Extraction failed

**Problem**: ZIP file is corrupted or inaccessible

**Solutions**:
```powershell
# 1. Test ZIP integrity
Expand-Archive -Path "package.zip" -DestinationPath "test" -Force

# 2. Re-download or re-create package
.\Deploy-Client.ps1

# 3. Check disk space
Get-PSDrive C | Select-Object Used, Free
```

---

## ?? Best Practices

### 1. Centralized Build
```powershell
# Build once on dedicated build server
.\Deploy-Client.ps1 `
    -ApiBaseUrl "https://api.contoso.com" `
    -OutputPath "\\fileserver\packages"

# Result: Consistent binaries for all devices
```

### 2. Version Control
```powershell
# Include version in package name
$version = "1.0.0"
$outputPath = ".\client-package-v$version"

.\Deploy-Client.ps1 `
    -OutputPath $outputPath `
    -ApiBaseUrl "https://api.contoso.com"

# Rename package
$pkg = "$outputPath\SecureBootWatcher-Client.zip"
$versionedPkg = "$outputPath\SecureBootWatcher-Client-v$version.zip"
Rename-Item $pkg -NewName $versionedPkg
```

### 3. Checksum Verification
```powershell
# Generate checksum after build
$pkg = ".\client-package\SecureBootWatcher-Client.zip"
$hash = Get-FileHash $pkg -Algorithm SHA256
$hash.Hash | Out-File "$pkg.sha256"

# Verify on target machine
$expected = Get-Content "$pkg.sha256"
$actual = (Get-FileHash $pkg -Algorithm SHA256).Hash

if ($expected -eq $actual) {
    Write-Host "? Package integrity verified" -ForegroundColor Green
} else {
    Write-Host "? Package corrupted!" -ForegroundColor Red
    exit 1
}
```

### 4. Configuration Management
```powershell
# Store fleet configurations in separate files
$configs = @{
    "prod" = @{ ApiUrl = "https://api-prod.contoso.com"; Fleet = "fleet-prod" }
    "test" = @{ ApiUrl = "https://api-test.contoso.com"; Fleet = "fleet-test" }
}

# Deploy with correct config
$env = "prod"
$config = $configs[$env]

.\Deploy-Client.ps1 `
    -PackageZipPath ".\package.zip" `
    -ApiBaseUrl $config.ApiUrl `
    -FleetId $config.Fleet `
    -CreateScheduledTask
```

---

## ?? Training Scenario

### IT Admin Training Exercise

**Goal**: Deploy SecureBootWatcher client to a test VM using precompiled package.

**Prerequisites**:
- Windows VM with PowerShell 5.0+
- Package ZIP available at: `\\training\packages\SecureBootWatcher-Client.zip`
- Deploy script at: `\\training\scripts\Deploy-Client.ps1`

**Steps**:

1. **Copy files to VM**:
   ```powershell
   Copy-Item "\\training\packages\*" -Destination "C:\Temp" -Recurse
   ```

2. **Open PowerShell as Administrator**

3. **Navigate to temp directory**:
   ```powershell
   cd C:\Temp
   ```

4. **Deploy client**:
   ```powershell
   .\Deploy-Client.ps1 `
       -PackageZipPath "C:\Temp\SecureBootWatcher-Client.zip" `
       -ApiBaseUrl "https://api-training.contoso.com" `
       -FleetId "training-fleet" `
       -CreateScheduledTask
   ```

5. **Verify installation**:
   ```powershell
   Test-Path "C:\Program Files\SecureBootWatcher\SecureBootWatcher.Client.exe"
   Get-ScheduledTask -TaskName "SecureBootWatcher"
   ```

6. **Run immediately**:
   ```powershell
   Start-ScheduledTask -TaskName "SecureBootWatcher"
   ```

7. **Check logs**:
   ```powershell
   Get-Content "C:\Program Files\SecureBootWatcher\logs\client-*.log" -Tail 20
   ```

**Expected Result**: Client installed, scheduled, and first report sent successfully.

---

## ?? Related Documentation

- **Full Deployment Guide**: `docs\CLIENT_DEPLOYMENT_SCRIPTS.md`
- **Configuration Examples**: `SecureBootWatcher.Client\appsettings.examples.json`
- **Main README**: `README.md`
- **Troubleshooting**: `docs\TROUBLESHOOTING_PORTS.md`

---

## ?? Summary

**Before this feature**:
- ? Complex: Required build tools on every machine
- ? Slow: 5-10 minutes per deployment
- ? Inconsistent: Different binaries if build twice

**After this feature**:
- ? Simple: Just ZIP file + PowerShell script
- ? Fast: 30 seconds per deployment
- ? Consistent: Same binaries everywhere

**Perfect for**:
- ?? Enterprise deployments (GPO, Intune, SCCM)
- ?? Production environments (no build tools)
- ? Quick rollouts (pilot testing, emergency updates)
- ?? Remote sites (limited connectivity)

---

## ?? Tips

- **Tip 1**: Always use absolute paths for network shares to avoid path resolution issues
- **Tip 2**: Test package on a single machine before mass deployment
- **Tip 3**: Keep package versions organized with clear naming conventions
- **Tip 4**: Document your deployment workflow in your internal wiki
- **Tip 5**: Automate builds with CI/CD pipelines (Azure DevOps, GitHub Actions)

---

**Ready to deploy? Start with the examples above!** ??
