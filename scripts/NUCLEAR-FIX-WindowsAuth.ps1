# ===============================================================================
# NUCLEAR-FIX-WindowsAuth.ps1
#
# NUCLEAR OPTION - Force disable Windows Authentication with ALL methods
# ===============================================================================

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$SiteName = "SecureBootDashboard.Web",
    
    [Parameter(Mandatory = $false)]
    [string]$AppPoolName = "SecureBootDashboard.Web"
)

$ErrorActionPreference = "Continue"

Write-Host ""
Write-Host "===============================================================================" -ForegroundColor Red
Write-Host "NUCLEAR OPTION - Force Disable Windows Authentication" -ForegroundColor Red
Write-Host "===============================================================================" -ForegroundColor Red
Write-Host ""

Write-Host "This script will use EVERY available method to disable Windows Auth" -ForegroundColor Yellow
Write-Host ""

# Stop services
Write-Host "Step 1: Stopping services..." -ForegroundColor Yellow
Import-Module WebAdministration -ErrorAction Stop

Stop-WebAppPool $AppPoolName -ErrorAction SilentlyContinue
Stop-Website $SiteName -ErrorAction SilentlyContinue
Start-Sleep -Seconds 3
Write-Host "? Services stopped" -ForegroundColor Green

# Method 1: PowerShell Set-WebConfigurationProperty
Write-Host "`nMethod 1: Set-WebConfigurationProperty..." -ForegroundColor Yellow
try {
    Set-WebConfigurationProperty `
        -Filter "/system.webServer/security/authentication/windowsAuthentication" `
        -Name "enabled" `
        -Value $false `
        -PSPath "IIS:\Sites\$SiteName" `
        -Force
    Write-Host "? Method 1 succeeded" -ForegroundColor Green
} catch {
    Write-Host "? Method 1 failed: $_" -ForegroundColor Red
}

# Method 2: appcmd.exe (Most reliable)
Write-Host "`nMethod 2: appcmd.exe (most reliable)..." -ForegroundColor Yellow
$appcmd = "$env:SystemRoot\System32\inetsrv\appcmd.exe"

if (Test-Path $appcmd) {
    try {
        # Disable Windows Auth
        & $appcmd set config "$SiteName" `
            -section:system.webServer/security/authentication/windowsAuthentication `
            /enabled:false `
            /commit:apphost | Out-Null
        
        Write-Host "? Windows Authentication disabled via appcmd" -ForegroundColor Green
        
        # Enable Anonymous Auth
        & $appcmd set config "$SiteName" `
            -section:system.webServer/security/authentication/anonymousAuthentication `
            /enabled:true `
            /commit:apphost | Out-Null
        
        Write-Host "? Anonymous Authentication enabled via appcmd" -ForegroundColor Green
        
    } catch {
        Write-Host "? appcmd failed: $_" -ForegroundColor Red
    }
} else {
    Write-Host "? appcmd.exe not found at: $appcmd" -ForegroundColor Red
}

# Method 3: Direct applicationHost.config edit (NUCLEAR)
Write-Host "`nMethod 3: Direct applicationHost.config edit (NUCLEAR)..." -ForegroundColor Yellow
$appHostConfig = "$env:SystemRoot\System32\inetsrv\config\applicationHost.config"

if (Test-Path $appHostConfig) {
    try {
        # Backup
        $backup = "$appHostConfig.backup_$(Get-Date -Format 'yyyyMMddHHmmss')"
        Copy-Item $appHostConfig $backup
        Write-Host "  Backup: $backup" -ForegroundColor Gray
        
        # Load XML
        [xml]$config = Get-Content $appHostConfig
        
        # Find site configuration
        $location = $config.configuration.location | Where-Object { $_.path -eq $SiteName }
        
        if ($location) {
            # Find or create security/authentication section
            $security = $location.'system.webServer'.security
            if (-not $security) {
                Write-Host "  Creating security section..." -ForegroundColor Gray
                $security = $config.CreateElement("security")
                $location.'system.webServer'.AppendChild($security) | Out-Null
            }
            
            $authentication = $security.authentication
            if (-not $authentication) {
                Write-Host "  Creating authentication section..." -ForegroundColor Gray
                $authentication = $config.CreateElement("authentication")
                $security.AppendChild($authentication) | Out-Null
            }
            
            # Set Windows Auth
            $windowsAuth = $authentication.windowsAuthentication
            if (-not $windowsAuth) {
                $windowsAuth = $config.CreateElement("windowsAuthentication")
                $windowsAuth.SetAttribute("enabled", "false")
                $authentication.AppendChild($windowsAuth) | Out-Null
                Write-Host "  Created windowsAuthentication element (disabled)" -ForegroundColor Gray
            } else {
                $windowsAuth.SetAttribute("enabled", "false")
                Write-Host "  Updated windowsAuthentication to disabled" -ForegroundColor Gray
            }
            
            # Set Anonymous Auth
            $anonymousAuth = $authentication.anonymousAuthentication
            if (-not $anonymousAuth) {
                $anonymousAuth = $config.CreateElement("anonymousAuthentication")
                $anonymousAuth.SetAttribute("enabled", "true")
                $authentication.AppendChild($anonymousAuth) | Out-Null
                Write-Host "  Created anonymousAuthentication element (enabled)" -ForegroundColor Gray
            } else {
                $anonymousAuth.SetAttribute("enabled", "true")
                Write-Host "  Updated anonymousAuthentication to enabled" -ForegroundColor Gray
            }
            
            # Save
            $config.Save($appHostConfig)
            Write-Host "? applicationHost.config updated directly" -ForegroundColor Green
            
        } else {
            Write-Host "? Site location not found in applicationHost.config" -ForegroundColor Yellow
            Write-Host "  This might be using default settings" -ForegroundColor Gray
        }
        
    } catch {
        Write-Host "? Direct config edit failed: $_" -ForegroundColor Red
    }
} else {
    Write-Host "? applicationHost.config not found" -ForegroundColor Red
}

# Wait for config to propagate
Write-Host "`nWaiting for configuration to propagate..." -ForegroundColor Yellow
Start-Sleep -Seconds 5

# Verification
Write-Host "`nVerification..." -ForegroundColor Yellow

try {
    $winAuth = Get-WebConfigurationProperty `
        -Filter "/system.webServer/security/authentication/windowsAuthentication" `
        -Name "enabled" `
        -PSPath "IIS:\Sites\$SiteName"
    
    $anonAuth = Get-WebConfigurationProperty `
        -Filter "/system.webServer/security/authentication/anonymousAuthentication" `
        -Name "enabled" `
        -PSPath "IIS:\Sites\$SiteName"
    
    Write-Host "  Windows Auth: " -NoNewline
    if ($winAuth.Value) {
        Write-Host "ENABLED (STILL WRONG!)" -ForegroundColor Red
    } else {
        Write-Host "DISABLED (CORRECT!)" -ForegroundColor Green
    }
    
    Write-Host "  Anonymous Auth: " -NoNewline
    if ($anonAuth.Value) {
        Write-Host "ENABLED (CORRECT!)" -ForegroundColor Green
    } else {
        Write-Host "DISABLED (WRONG!)" -ForegroundColor Red
    }
    
} catch {
    Write-Host "  ? Could not verify: $_" -ForegroundColor Yellow
}

# Start services
Write-Host "`nStarting services..." -ForegroundColor Yellow

Start-WebAppPool $AppPoolName
Start-Sleep -Seconds 3
Write-Host "? App Pool started" -ForegroundColor Green

Start-Website $SiteName
Start-Sleep -Seconds 5
Write-Host "? Website started" -ForegroundColor Green

# Final test
Write-Host "`nFinal Test..." -ForegroundColor Yellow

Start-Sleep -Seconds 5

try {
    $response = Invoke-WebRequest -Uri "https://secbootsrv.mslabs.local" `
        -UseBasicParsing `
        -SkipCertificateCheck `
        -TimeoutSec 15 `
        -ErrorAction Stop
    
    Write-Host "? Site responded: HTTP $($response.StatusCode)" -ForegroundColor Green
    
    if ($response.Content -match "An error occurred") {
        Write-Host "? Page shows error - check logs" -ForegroundColor Yellow
    } else {
        Write-Host "? SUCCESS - Page loaded!" -ForegroundColor Green
    }
    
} catch {
    Write-Host "? Site test failed: $_" -ForegroundColor Red
}

# Check logs
Write-Host "`nChecking recent logs..." -ForegroundColor Yellow
$logFile = Get-ChildItem "C:\Logs\SecureBootDashboard" -File -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if ($logFile) {
    $errors = Get-Content $logFile.FullName -Tail 20 | 
        Where-Object { $_ -match "FTL|Negotiate Authentication handler cannot be used" }
    
    if ($errors) {
        Write-Host ""
        Write-Host "??? WINDOWS AUTH STILL CAUSING ISSUES ???" -ForegroundColor Red
        Write-Host ""
        Write-Host "Last resort:" -ForegroundColor Yellow
        Write-Host "  1. Open IIS Manager manually" -ForegroundColor White
        Write-Host "  2. Navigate to Sites ? SecureBootDashboard.Web" -ForegroundColor White
        Write-Host "  3. Double-click 'Authentication'" -ForegroundColor White
        Write-Host "  4. Right-click 'Windows Authentication' ? Disable" -ForegroundColor White
        Write-Host "  5. Right-click 'Anonymous Authentication' ? Enable" -ForegroundColor White
        Write-Host "  6. Restart App Pool" -ForegroundColor White
        Write-Host ""
    } else {
        Write-Host "? No Windows Auth errors in recent logs!" -ForegroundColor Green
    }
} else {
    Write-Host "? No log files found" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host "NUCLEAR FIX COMPLETE" -ForegroundColor Cyan
Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host ""

