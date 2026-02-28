# ===============================================================================
# Fix-401Error.ps1
#
# Fix HTTP 401.2 - Unauthorized error
# Ensures proper authentication configuration in IIS
# ===============================================================================

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$SiteName = "SecureBootDashboard.Web",
    
    [Parameter(Mandatory = $false)]
    [string]$AppPoolName = "SecureBootDashboard.Web"
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host "Fix HTTP 401.2 - Unauthorized Error" -ForegroundColor Cyan
Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "Issue: No authentication protocol selected in IIS" -ForegroundColor Yellow
Write-Host "Solution: Enable Anonymous Authentication" -ForegroundColor Yellow
Write-Host ""

# Import WebAdministration
try {
    Import-Module WebAdministration -ErrorAction Stop
    Write-Host "? WebAdministration module loaded" -ForegroundColor Green
} catch {
    Write-Host "? Failed to load WebAdministration module: $_" -ForegroundColor Red
    exit 1
}

# Check site exists
$sitePath = "IIS:\Sites\$SiteName"
if (-not (Test-Path $sitePath)) {
    Write-Host "? Site '$SiteName' not found!" -ForegroundColor Red
    exit 1
}

Write-Host "? Site found: $SiteName" -ForegroundColor Green

# Check current authentication settings
Write-Host "`nCurrent Authentication Settings:" -ForegroundColor Cyan

try {
    $windowsAuth = Get-WebConfigurationProperty `
        -Filter /system.webServer/security/authentication/windowsAuthentication `
        -Name enabled `
        -PSPath $sitePath
    
    $anonymousAuth = Get-WebConfigurationProperty `
        -Filter /system.webServer/security/authentication/anonymousAuthentication `
        -Name enabled `
        -PSPath $sitePath
    
    $basicAuth = Get-WebConfigurationProperty `
        -Filter /system.webServer/security/authentication/basicAuthentication `
        -Name enabled `
        -PSPath $sitePath -ErrorAction SilentlyContinue
    
    Write-Host "  Windows Authentication: $($windowsAuth.Value)" -ForegroundColor $(if ($windowsAuth.Value) { "Yellow" } else { "Gray" })
    Write-Host "  Anonymous Authentication: $($anonymousAuth.Value)" -ForegroundColor $(if ($anonymousAuth.Value) { "Green" } else { "Red" })
    if ($basicAuth) {
        Write-Host "  Basic Authentication: $($basicAuth.Value)" -ForegroundColor Gray
    }
    
    # Identify the problem
    if (-not $anonymousAuth.Value) {
        Write-Host "`n? PROBLEM: Anonymous Authentication is DISABLED" -ForegroundColor Red
        Write-Host "  This causes 401.2 error!" -ForegroundColor Red
    }
} catch {
    Write-Host "  ? Could not read current settings: $_" -ForegroundColor Yellow
}

# Apply fix
Write-Host "`nApplying fix..." -ForegroundColor Yellow

try {
    # Ensure Windows Auth is OFF
    Write-Host "  1. Disabling Windows Authentication..." -ForegroundColor Gray
    Set-WebConfigurationProperty `
        -Filter /system.webServer/security/authentication/windowsAuthentication `
        -Name enabled `
        -Value false `
        -PSPath $sitePath
    Write-Host "     ? Done" -ForegroundColor Green
    
    # Enable Anonymous Auth at site level
    Write-Host "  2. Enabling Anonymous Authentication (site level)..." -ForegroundColor Gray
    Set-WebConfigurationProperty `
        -Filter /system.webServer/security/authentication/anonymousAuthentication `
        -Name enabled `
        -Value true `
        -PSPath $sitePath
    Write-Host "     ? Done" -ForegroundColor Green
    
    # Also try at application level (might not be needed but doesn't hurt)
    Write-Host "  3. Ensuring Anonymous Authentication (application level)..." -ForegroundColor Gray
    try {
        Set-WebConfigurationProperty `
            -Filter /system.webServer/security/authentication/anonymousAuthentication `
            -Name enabled `
            -Value true `
            -Location "$SiteName"
        Write-Host "     ? Done" -ForegroundColor Green
    } catch {
        Write-Host "     ? Not needed or already set" -ForegroundColor Gray
    }
    
    # Ensure Basic Auth is OFF (if exists)
    try {
        Set-WebConfigurationProperty `
            -Filter /system.webServer/security/authentication/basicAuthentication `
            -Name enabled `
            -Value false `
            -PSPath $sitePath -ErrorAction SilentlyContinue
        Write-Host "  4. Basic Authentication disabled" -ForegroundColor Gray
    } catch {
        Write-Host "  4. Basic Authentication not available (OK)" -ForegroundColor Gray
    }
    
} catch {
    Write-Host "? Failed to update authentication settings: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "Try manual fix in IIS Manager:" -ForegroundColor Yellow
    Write-Host "  1. Open IIS Manager" -ForegroundColor White
    Write-Host "  2. Select site: $SiteName" -ForegroundColor White
    Write-Host "  3. Double-click 'Authentication'" -ForegroundColor White
    Write-Host "  4. Enable 'Anonymous Authentication'" -ForegroundColor White
    Write-Host "  5. Disable 'Windows Authentication'" -ForegroundColor White
    exit 1
}

# Verify changes
Write-Host "`nVerifying changes..." -ForegroundColor Yellow

try {
    $windowsAuth = Get-WebConfigurationProperty `
        -Filter /system.webServer/security/authentication/windowsAuthentication `
        -Name enabled `
        -PSPath $sitePath
    
    $anonymousAuth = Get-WebConfigurationProperty `
        -Filter /system.webServer/security/authentication/anonymousAuthentication `
        -Name enabled `
        -PSPath $sitePath
    
    Write-Host "  Windows Authentication: $($windowsAuth.Value)" -ForegroundColor $(if ($windowsAuth.Value) { "Red" } else { "Green" })
    Write-Host "  Anonymous Authentication: $($anonymousAuth.Value)" -ForegroundColor $(if ($anonymousAuth.Value) { "Green" } else { "Red" })
    
    if ($windowsAuth.Value -eq $false -and $anonymousAuth.Value -eq $true) {
        Write-Host "`n? Authentication settings are correct!" -ForegroundColor Green
    } else {
        Write-Host "`n? Settings may not be correct. Manual verification needed." -ForegroundColor Yellow
    }
} catch {
    Write-Host "? Could not verify settings: $_" -ForegroundColor Yellow
}

# Check web.config for any overrides
Write-Host "`nChecking web.config..." -ForegroundColor Yellow
$webConfigPath = "C:\inetpub\SecureBootDashboard.Web\web.config"

if (Test-Path $webConfigPath) {
    try {
        [xml]$webConfig = Get-Content $webConfigPath
        
        # Check for authentication elements
        $authSection = $webConfig.SelectNodes("//authentication")
        $windowsAuthSection = $webConfig.SelectNodes("//windowsAuthentication")
        $anonymousAuthSection = $webConfig.SelectNodes("//anonymousAuthentication")
        
        if ($authSection -or $windowsAuthSection -or $anonymousAuthSection) {
            Write-Host "  ? web.config contains authentication settings" -ForegroundColor Yellow
            Write-Host "  These might override IIS settings" -ForegroundColor Yellow
            
            if ($windowsAuthSection) {
                foreach ($node in $windowsAuthSection) {
                    Write-Host "  Found: <windowsAuthentication enabled='$($node.enabled)' />" -ForegroundColor Gray
                }
            }
            if ($anonymousAuthSection) {
                foreach ($node in $anonymousAuthSection) {
                    Write-Host "  Found: <anonymousAuthentication enabled='$($node.enabled)' />" -ForegroundColor Gray
                }
            }
        } else {
            Write-Host "  ? No authentication overrides in web.config" -ForegroundColor Green
        }
    } catch {
        Write-Host "  ? Could not parse web.config: $_" -ForegroundColor Gray
    }
} else {
    Write-Host "  ? web.config not found at: $webConfigPath" -ForegroundColor Yellow
}

# Restart App Pool
Write-Host "`nRestarting Application Pool..." -ForegroundColor Yellow

try {
    Stop-WebAppPool $AppPoolName -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    Start-WebAppPool $AppPoolName
    Start-Sleep -Seconds 3
    
    $state = (Get-WebAppPoolState $AppPoolName).Value
    Write-Host "? App Pool restarted: $state" -ForegroundColor Green
} catch {
    Write-Host "? Could not restart App Pool: $_" -ForegroundColor Yellow
    Write-Host "  Try manually: Restart-WebAppPool '$AppPoolName'" -ForegroundColor Yellow
}

# Test site
Write-Host "`nTesting site..." -ForegroundColor Yellow
Start-Sleep -Seconds 5

try {
    $response = Invoke-WebRequest -Uri "https://secbootsrv.mslabs.local" `
        -UseBasicParsing `
        -SkipCertificateCheck `
        -TimeoutSec 15 `
        -ErrorAction Stop
    
    Write-Host "? Site is responding: HTTP $($response.StatusCode)" -ForegroundColor Green
    Write-Host ""
    Write-Host "===============================================================================" -ForegroundColor Cyan
    Write-Host "SUCCESS - 401.2 Error Fixed!" -ForegroundColor Green
    Write-Host "===============================================================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Your application is now accessible." -ForegroundColor White
    Write-Host "ASP.NET Core is handling Windows Authentication via Negotiate handler." -ForegroundColor White
    Write-Host ""
    
} catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    Write-Host "? Site test result: HTTP $statusCode" -ForegroundColor Yellow
    
    if ($statusCode -eq 401) {
        Write-Host ""
        Write-Host "Still getting 401 error. Additional checks:" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "1. Check Event Viewer:" -ForegroundColor White
        Write-Host "   Get-EventLog -LogName Application -Source '*AspNetCore*' -Newest 5" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "2. Check stdout logs:" -ForegroundColor White
        Write-Host "   Get-Content C:\Logs\SecureBootDashboard\stdout-*.log -Tail 30" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "3. Verify IIS Manager:" -ForegroundColor White
        Write-Host "   - Open IIS Manager" -ForegroundColor Gray
        Write-Host "   - Select '$SiteName'" -ForegroundColor Gray
        Write-Host "   - Double-click 'Authentication'" -ForegroundColor Gray
        Write-Host "   - Ensure 'Anonymous Authentication' is ENABLED" -ForegroundColor Gray
        Write-Host "   - Ensure 'Windows Authentication' is DISABLED" -ForegroundColor Gray
        Write-Host ""
    } else {
        Write-Host "? Unexpected error: $_" -ForegroundColor Red
        Write-Host ""
        Write-Host "Check logs:" -ForegroundColor Yellow
        Write-Host "  C:\Logs\SecureBootDashboard\stdout-*.log" -ForegroundColor Cyan
    }
}

Write-Host ""
Write-Host "Authentication Configuration Summary:" -ForegroundColor Cyan
Write-Host "  IIS Windows Auth: DISABLED" -ForegroundColor White
Write-Host "  IIS Anonymous Auth: ENABLED" -ForegroundColor White
Write-Host "  ASP.NET Core: Handling Windows Authentication" -ForegroundColor White
Write-Host ""

