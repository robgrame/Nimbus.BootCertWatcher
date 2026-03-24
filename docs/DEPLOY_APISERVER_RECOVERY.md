# How to Recover from Deploy-ApiServer.ps1 Failures

## If You Encountered These Errors

### Error 1: "Cannot find drive. A drive with the name 'IIS' does not exist."
```
? Application Pool created: SecureBootDashboard.Api
? Deployment failed: Cannot find drive. A drive with the name 'IIS' does not exist.
Stack trace: at New-ApplicationPool, C:\Temp\Deploy-ApiServer.ps1: line 275
```

**What Happened**: IISAdministration module was loaded instead of WebAdministration, so the `IIS:\` PowerShell drive wasn't available. The script tried to use `Set-ItemProperty "IIS:\AppPools\$Name"...` which crashed.

**This is FIXED** in the updated script.

---

### Error 2: "Method invocation failed because [...] does not contain a method named 'AddSslCertificate'."
```
? Website created with HTTPS binding
? Deployment failed: Method invocation failed because [Deserialized.Microsoft.IIs.PowerShell.Framework.ConfigurationElement#bindings#binding] 
does not contain a method named 'AddSslCertificate'.
Stack trace: at New-IisWebsite, C:\Temp\Deploy-ApiServer.ps1: line 406
```

**What Happened**: The code tried to call a method that doesn't exist on the binding object: `$binding.AddSslCertificate($CertThumbprint, "my")`

**This is FIXED** in the updated script by using `Set-ItemProperty` instead.

---

### Error 3: "The term 'Set-ApplicationConfiguration' is not recognized"
```
? Deployment failed: The term 'Set-ApplicationConfiguration' is not recognized as the name of a cmdlet, function, script file, or operable program.
```

**What Happened**: The script called a function that was never defined in the script.

**This is FIXED** in the updated script by removing the call to the undefined function.

---

## Recovery Steps

### Step 1: Clean Up Partial Deployment
```powershell
# Run as Administrator

# Stop services if running
Stop-Website -Name "SecureBootDashboard.Api" -ErrorAction SilentlyContinue
Stop-WebAppPool -Name "SecureBootDashboard.Api" -ErrorAction SilentlyContinue

# Wait for services to stop
Start-Sleep -Seconds 2

# Remove incomplete files (careful - creates backup first)
if (Test-Path "C:\inetpub\SecureBootDashboard.Api") {
    $backupPath = "C:\inetpub\SecureBootDashboard.Api.backup_cleanup_$(Get-Date -Format 'yyyyMMddHHmmss')"
    Copy-Item "C:\inetpub\SecureBootDashboard.Api" -Destination $backupPath -Recurse -Force
    Remove-Item "C:\inetpub\SecureBootDashboard.Api" -Recurse -Force -ErrorAction SilentlyContinue
}
```

### Step 2: Get the Fixed Script
```powershell
# The fixed version is available in the repository
# Update from git or download the latest version
cd C:\Users\<user>\source\repos\robgrame\Nimbus.BootCertWatcher
git pull origin main
```

### Step 3: Verify Prerequisites
```powershell
# Check IIS is installed
Get-WindowsFeature Web-Server | Select-Object Name, Installed

# Check for .NET 10 Hosting Bundle
Test-Path "$env:SystemRoot\System32\inetsrv\aspnetcorev2.dll"

# Check for SSL certificate
Get-ChildItem "Cert:\LocalMachine\My" | Where-Object { $_.Subject -like "*yourdomain*" }

# Check published binaries exist
Test-Path ".\SecureBootDashboard.Api\bin\Release\net10.0\publish"
```

### Step 4: Re-run Deployment with Fixed Script
```powershell
# Test mode first (no changes)
.\scripts\Deploy-ApiServer.ps1 `
    -SiteName "SecureBootDashboard.Api" `
    -AppPoolName "SecureBootDashboard.Api" `
    -PhysicalPath "C:\inetpub\SecureBootDashboard.Api" `
    -HostHeader "api.yourdomain.com" `
    -SslCertificateThumbprint "YOUR_THUMBPRINT_HERE" `
    -WhatIf

# Actual deployment
.\scripts\Deploy-ApiServer.ps1 `
    -SiteName "SecureBootDashboard.Api" `
    -AppPoolName "SecureBootDashboard.Api" `
    -PhysicalPath "C:\inetpub\SecureBootDashboard.Api" `
    -HostHeader "api.yourdomain.com" `
    -SslCertificateThumbprint "YOUR_THUMBPRINT_HERE"
```

### Step 5: Verify Deployment
```powershell
# Check application pool exists and is running
Get-WebAppPoolState "SecureBootDashboard.Api"

# Check website exists and is running
Get-WebSiteState "SecureBootDashboard.Api"

# Check SSL certificate is bound
Get-WebBinding -Name "SecureBootDashboard.Api" -Protocol "https" | 
    Select-Object Protocol, BindingInformation, CertificateHash

# Test health endpoint
$url = "https://api.yourdomain.com/health"
Invoke-WebRequest -Uri $url -SkipCertificateCheck | Select-Object StatusCode
```

---

## Partial Cleanup (Without Full Removal)

If you want to keep the website but just rerun the deployment:

```powershell
# Stop services
Stop-Website "SecureBootDashboard.Api" -ErrorAction SilentlyContinue
Stop-WebAppPool "SecureBootDashboard.Api" -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

# Remove only the binaries (keeps configuration)
Remove-Item "C:\inetpub\SecureBootDashboard.Api\*.dll" -Force -ErrorAction SilentlyContinue
Remove-Item "C:\inetpub\SecureBootDashboard.Api\*.pdb" -Force -ErrorAction SilentlyContinue

# Re-run deployment to copy new files
.\scripts\Deploy-ApiServer.ps1 -PhysicalPath "C:\inetpub\SecureBootDashboard.Api" -HostHeader "api.yourdomain.com"
```

---

## Manual IIS Configuration (If Script Fails)

If the fixed script still doesn't work, you can configure manually:

### 1. Create Application Pool
```powershell
# Create app pool with no managed code (.NET Core)
New-WebAppPool -Name "SecureBootDashboard.Api" -Force | Out-Null

# Configure settings
Set-ItemProperty "IIS:\AppPools\SecureBootDashboard.Api" `
    -Name "managedRuntimeVersion" -Value ""
Set-ItemProperty "IIS:\AppPools\SecureBootDashboard.Api" `
    -Name "processModel.identityType" -Value "ApplicationPoolIdentity"
Set-ItemProperty "IIS:\AppPools\SecureBootDashboard.Api" `
    -Name "autoStart" -Value $true
Set-ItemProperty "IIS:\AppPools\SecureBootDashboard.Api" `
    -Name "startMode" -Value "AlwaysRunning"
```

### 2. Create Website
```powershell
# Create with HTTPS
New-WebSite -Name "SecureBootDashboard.Api" `
    -PhysicalPath "C:\inetpub\SecureBootDashboard.Api" `
    -ApplicationPool "SecureBootDashboard.Api" `
    -HostHeader "api.yourdomain.com" `
    -Port 443 `
    -Ssl
```

### 3. Bind SSL Certificate
```powershell
# Get your certificate thumbprint
$cert = Get-ChildItem "Cert:\LocalMachine\My" | 
    Where-Object { $_.Subject -like "*yourdomain*" }
$thumbprint = $cert.Thumbprint

# Bind certificate
$bindingPath = "IIS:\Sites\SecureBootDashboard.Api\Bindings\*:443:api.yourdomain.com"
Set-ItemProperty -Path $bindingPath -Name "certificateHash" -Value $thumbprint
Set-ItemProperty -Path $bindingPath -Name "certificateStoreName" -Value "My"
```

### 4. Configure Settings
```powershell
# Max request size
Set-WebConfigurationProperty -PSPath "IIS:\Sites\SecureBootDashboard.Api" `
    -Filter "system.webServer/security/requestFiltering/requestLimits" `
    -Name "maxAllowedContentLength" -Value 104857600

# Enable compression
Set-WebConfigurationProperty -PSPath "IIS:\Sites\SecureBootDashboard.Api" `
    -Filter "system.webServer/httpCompression" `
    -Name "doDynamicCompression" -Value $true
Set-WebConfigurationProperty -PSPath "IIS:\Sites\SecureBootDashboard.Api" `
    -Filter "system.webServer/httpCompression" `
    -Name "doStaticCompression" -Value $true
```

### 5. Start Services
```powershell
Start-WebAppPool "SecureBootDashboard.Api"
Start-Website "SecureBootDashboard.Api"
```

---

## Troubleshooting Specific Errors

### "Destination element already exists" error
```powershell
# This means the app pool already exists
# Solution 1: Remove and recreate
Remove-WebAppPool "SecureBootDashboard.Api"
New-WebAppPool "SecureBootDashboard.Api"

# Solution 2: Just continue (script will update it)
# Re-run the script, it will detect and update existing app pool
```

### "Cannot bind to port 443" error
```powershell
# Check what's using port 443
netstat -ano | findstr :443

# If it's HTTPS (port 443), check existing bindings
Get-NetTCPConnection -LocalPort 443 -ErrorAction SilentlyContinue

# Use different port for testing
.\scripts\Deploy-ApiServer.ps1 -HttpsPort 8443
```

### "Certificate not found" error
```powershell
# List all certificates
Get-ChildItem "Cert:\LocalMachine\My" | Format-Table Subject, Thumbprint, NotAfter

# Find the right one
Get-ChildItem "Cert:\LocalMachine\My" | 
    Where-Object { $_.Subject -like "*yourdomain*" } | 
    Select-Object Subject, Thumbprint

# Use the correct thumbprint (copy exactly, no spaces)
```

### "Website not responding" error
```powershell
# Check app pool is running
Get-WebAppPoolState "SecureBootDashboard.Api"

# Check website is running
Get-WebSiteState "SecureBootDashboard.Api"

# Check files were copied
Get-ChildItem "C:\inetpub\SecureBootDashboard.Api" | Measure-Object

# Check event viewer for errors
Get-WinEvent -LogName "Application" -MaxEvents 20 | 
    Where-Object { $_.Source -like "*AspNet*" -or $_.Source -like "*IIS*" }

# Check app logs
Get-ChildItem "C:\Logs\SecureBootDashboard\" | Sort-Object LastWriteTime -Descending | Select-Object -First 5
```

---

## Getting Help

If recovery steps don't work:

1. **Collect diagnostic information**:
   ```powershell
   # Save this to a file for support
   $output = @"
   PowerShell Version: $(($PSVersionTable.PSVersion))
   OS: $(Get-CimInstance Win32_OperatingSystem | Select-Object -ExpandProperty Caption)
   
   Installed Modules:
   $(Get-Module -ListAvailable | Where-Object { $_.Name -like '*Admin*' })
   
   .NET Hosting:
   $(Test-Path "$env:SystemRoot\System32\inetsrv\aspnetcorev2.dll")
   
   Certificates:
   $(Get-ChildItem "Cert:\LocalMachine\My" | Select-Object Subject, Thumbprint)
   
   App Pool:
   $(Get-WebAppPool "SecureBootDashboard.Api" -ErrorAction SilentlyContinue)
   
   Website:
   $(Get-Website "SecureBootDashboard.Api" -ErrorAction SilentlyContinue)
   "@
   $output | Out-File "deployment-diagnostics.txt"
   ```

2. **Review documentation**:
   - `docs/SERVER_INFRASTRUCTURE_DEPLOYMENT.md` - Main deployment guide
   - `docs/TROUBLESHOOTING_PORTS.md` - Port and networking issues
   - `docs/SSL_CERTIFICATE_BYPASS.md` - Certificate issues

3. **Check logs**:
   - Application logs: `C:\Logs\SecureBootDashboard\`
   - IIS logs: `C:\inetpub\logs\LogFiles\`
   - Event Viewer: Application ? Warning/Error filters

---

## Prevention

To prevent this issue in the future:

1. ? Always use the latest version of the deployment script
2. ? Test with `-WhatIf` before actual deployment
3. ? Keep WebAdministration module installed (more features)
4. ? Document your certificate thumbprints
5. ? Take backups before updating
6. ? Monitor deployment logs for errors

