# Troubleshooting HTTP 500.30 Error - SecureBootDashboard.Web

## Quick Diagnostics

### Step 1: Check Stdout Logs (Most Important!)

L'errore 500.30 indica che l'app non riesce ad avviarsi. I log stdout contengono i dettagli.

#### Verifica Log Directory
```powershell
# Check if logs directory exists
Test-Path "C:\Logs\SecureBootDashboard"

# List log files
Get-ChildItem "C:\Logs\SecureBootDashboard\web-*.log" | 
    Sort-Object LastWriteTime -Descending | 
    Select-Object -First 5 Name, LastWriteTime, Length
```

#### Leggi Ultimo Log
```powershell
# Read the most recent log file
$latestLog = Get-ChildItem "C:\Logs\SecureBootDashboard\web-*.log" | 
    Sort-Object LastWriteTime -Descending | 
    Select-Object -First 1

if ($latestLog) {
    Write-Host "Latest log: $($latestLog.FullName)" -ForegroundColor Cyan
    Get-Content $latestLog.FullName -Tail 50
} else {
    Write-Host "No log files found in C:\Logs\SecureBootDashboard" -ForegroundColor Yellow
}
```

#### Abilita Stdout Logging (Se Non Abilitato)

Crea o modifica `web.config` in `C:\inetpub\SecureBootDashboard.Web\web.config`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <location path="." inheritInChildApplications="false">
    <system.webServer>
      <handlers>
        <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
      </handlers>
      <aspNetCore processPath="dotnet" 
                  arguments=".\SecureBootDashboard.Web.dll" 
                  stdoutLogEnabled="true" 
                  stdoutLogFile="C:\Logs\SecureBootDashboard\stdout" 
                  hostingModel="inprocess">
        <environmentVariables>
          <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
        </environmentVariables>
      </aspNetCore>
    </system.webServer>
  </location>
</configuration>
```

**Key Settings**:
- `stdoutLogEnabled="true"` - Abilita logging
- `stdoutLogFile="C:\Logs\SecureBootDashboard\stdout"` - Path log
- `hostingModel="inprocess"` - Hosting mode

---

### Step 2: Check Event Viewer

```powershell
# Check Application Event Log for ASP.NET Core errors
Get-EventLog -LogName Application -Source "IIS AspNetCore Module V2" -Newest 10 | 
    Format-List TimeGenerated, EntryType, Message
```

---

### Step 3: Verify .NET Runtime

```powershell
# Check installed .NET runtimes
dotnet --list-runtimes

# Expected output should include:
# Microsoft.AspNetCore.App 10.0.x [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
# Microsoft.NETCore.App 10.0.x [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
```

**If missing**:
```powershell
# Download and install .NET 10 Hosting Bundle
# https://dotnet.microsoft.com/download/dotnet/10.0
# Direct link: https://aka.ms/dotnet/10.0/dotnet-hosting-win.exe

# After installation, restart IIS
iisreset
```

---

### Step 4: Check Application Files

```powershell
# Verify main DLL exists
Test-Path "C:\inetpub\SecureBootDashboard.Web\SecureBootDashboard.Web.dll"

# Check appsettings files
Get-ChildItem "C:\inetpub\SecureBootDashboard.Web\appsettings*.json" | 
    Select-Object Name, Length, LastWriteTime

# Check for web.config
Test-Path "C:\inetpub\SecureBootDashboard.Web\web.config"
```

---

### Step 5: Check App Pool Identity Permissions

```powershell
# Get App Pool identity
Import-Module WebAdministration
$appPool = Get-Item "IIS:\AppPools\SecureBootDashboard.Web"
Write-Host "App Pool Identity: $($appPool.processModel.identityType)" -ForegroundColor Cyan

# Grant permissions to logs directory
$acl = Get-Acl "C:\Logs\SecureBootDashboard"
$identity = "IIS AppPool\SecureBootDashboard.Web"
$accessRule = New-Object System.Security.AccessControl.FileSystemAccessRule($identity, "FullControl", "ContainerInherit,ObjectInherit", "None", "Allow")
$acl.AddAccessRule($accessRule)
Set-Acl "C:\Logs\SecureBootDashboard" $acl

Write-Host "? Permissions granted to App Pool identity" -ForegroundColor Green
```

---

### Step 6: Check Configuration Errors

```powershell
# Read appsettings.Production.json
$settingsPath = "C:\inetpub\SecureBootDashboard.Web\appsettings.Production.json"
if (Test-Path $settingsPath) {
    Write-Host "`nCurrent appsettings.Production.json:" -ForegroundColor Cyan
    Get-Content $settingsPath | ConvertFrom-Json | ConvertTo-Json -Depth 10
} else {
    Write-Host "? appsettings.Production.json not found!" -ForegroundColor Yellow
}
```

---

### Step 7: Test App Pool

```powershell
# Restart App Pool
Import-Module WebAdministration
Restart-WebAppPool "SecureBootDashboard.Web"
Start-Sleep -Seconds 3

# Check App Pool state
$state = (Get-WebAppPoolState "SecureBootDashboard.Web").Value
Write-Host "App Pool State: $state" -ForegroundColor $(if ($state -eq "Started") { "Green" } else { "Red" })

# Try to access the site
try {
    $response = Invoke-WebRequest -Uri "https://secbootsrv.mslabs.local" -UseBasicParsing -SkipCertificateCheck
    Write-Host "? Site responded: HTTP $($response.StatusCode)" -ForegroundColor Green
} catch {
    Write-Host "? Site error: $_" -ForegroundColor Red
}
```

---

## Common Causes and Solutions

### 1. Missing appsettings.Production.json or Invalid JSON

**Error in logs**: `System.IO.FileNotFoundException` or `JSON parsing error`

**Solution**:
```powershell
# Verify JSON is valid
$settingsPath = "C:\inetpub\SecureBootDashboard.Web\appsettings.Production.json"
try {
    Get-Content $settingsPath | ConvertFrom-Json | Out-Null
    Write-Host "? JSON is valid" -ForegroundColor Green
} catch {
    Write-Host "? JSON is invalid: $_" -ForegroundColor Red
}
```

### 2. Invalid API BaseUrl

**Error in logs**: `Connection refused` or `Unable to connect`

**Solution**:
```json
{
  "ApiSettings": {
    "BaseUrl": "https://secbootsrv.mslabs.local",  // ? Must be reachable
    "UseCertificateAuth": true
  }
}
```

### 3. Missing or Invalid Certificate

**Error in logs**: `Certificate not found` or `X509Certificate2`

**Solution**:
```powershell
# List certificates
Get-ChildItem Cert:\LocalMachine\My | 
    Select-Object Thumbprint, Subject, NotAfter | 
    Format-Table -AutoSize

# Verify certificate in appsettings matches
```

### 4. Port Conflict

**Error in logs**: `Address already in use`

**Solution**:
```powershell
# Check what's using port 443
netstat -ano | findstr :443

# Stop conflicting process if needed
```

### 5. Database Connection (If using SQL Server)

**Error in logs**: `Cannot open database` or `Login failed`

**Solution**: Verify connection string in appsettings

---

## Quick Fix Script

```powershell
# Quick diagnostic and fix script
$webPath = "C:\inetpub\SecureBootDashboard.Web"
$logsPath = "C:\Logs\SecureBootDashboard"

Write-Host "`n=== SecureBootDashboard.Web Diagnostics ===" -ForegroundColor Cyan

# 1. Check files
Write-Host "`n1. Checking files..." -ForegroundColor Yellow
$dllExists = Test-Path "$webPath\SecureBootDashboard.Web.dll"
$settingsExists = Test-Path "$webPath\appsettings.Production.json"
Write-Host "  DLL: $(if ($dllExists) { '?' } else { '?' })" -ForegroundColor $(if ($dllExists) { 'Green' } else { 'Red' })
Write-Host "  Settings: $(if ($settingsExists) { '?' } else { '?' })" -ForegroundColor $(if ($settingsExists) { 'Green' } else { 'Red' })

# 2. Check logs directory
Write-Host "`n2. Checking logs directory..." -ForegroundColor Yellow
if (-not (Test-Path $logsPath)) {
    New-Item -Path $logsPath -ItemType Directory -Force | Out-Null
    Write-Host "  Created logs directory" -ForegroundColor Green
}

# 3. Set permissions
Write-Host "`n3. Setting permissions..." -ForegroundColor Yellow
$acl = Get-Acl $logsPath
$identity = "IIS AppPool\SecureBootDashboard.Web"
$accessRule = New-Object System.Security.AccessControl.FileSystemAccessRule($identity, "FullControl", "ContainerInherit,ObjectInherit", "None", "Allow")
$acl.AddAccessRule($accessRule)
Set-Acl $logsPath $acl
Write-Host "  ? Permissions set" -ForegroundColor Green

# 4. Restart App Pool
Write-Host "`n4. Restarting App Pool..." -ForegroundColor Yellow
Import-Module WebAdministration
Restart-WebAppPool "SecureBootDashboard.Web"
Start-Sleep -Seconds 3
Write-Host "  ? App Pool restarted" -ForegroundColor Green

# 5. Check latest log
Write-Host "`n5. Checking logs..." -ForegroundColor Yellow
$latestLog = Get-ChildItem "$logsPath\*" -File | 
    Sort-Object LastWriteTime -Descending | 
    Select-Object -First 1

if ($latestLog) {
    Write-Host "  Latest log: $($latestLog.Name)" -ForegroundColor Cyan
    Write-Host "`n=== Last 20 lines of log ===" -ForegroundColor Cyan
    Get-Content $latestLog.FullName -Tail 20
} else {
    Write-Host "  ? No logs found" -ForegroundColor Yellow
}

Write-Host "`n=== Diagnostics Complete ===" -ForegroundColor Cyan
```

---

## Next Steps

1. **Run the diagnostics script above**
2. **Check the stdout logs** in `C:\Logs\SecureBootDashboard\stdout-*.log`
3. **Look for the specific error** in the logs
4. **Apply the corresponding solution** from the "Common Causes" section

Share the error from the logs for more specific help!

