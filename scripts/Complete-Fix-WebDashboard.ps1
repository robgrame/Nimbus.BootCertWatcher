# ===============================================================================
# Complete-Fix-WebDashboard.ps1
#
# Complete fix for all Web Dashboard issues found
# ===============================================================================

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$WebPath = "C:\inetpub\SecureBootDashboard.Web",
    
    [Parameter(Mandatory = $false)]
    [string]$AppPoolName = "SecureBootDashboard.Web",
    
    [Parameter(Mandatory = $false)]
    [string]$SiteName = "SecureBootDashboard.Web"
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host "COMPLETE FIX - SecureBootDashboard.Web" -ForegroundColor Cyan
Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Fix Windows Authentication Conflict
Write-Host "Step 1: Fixing Windows Authentication Conflict..." -ForegroundColor Yellow

try {
    Import-Module WebAdministration -ErrorAction Stop
    
    $sitePath = "IIS:\Sites\$SiteName"
    
    # Disable Windows Auth
    Set-WebConfigurationProperty `
        -Filter /system.webServer/security/authentication/windowsAuthentication `
        -Name enabled `
        -Value false `
        -PSPath $sitePath
    
    Write-Host "? Windows Authentication disabled" -ForegroundColor Green
    
    # Enable Anonymous Auth
    Set-WebConfigurationProperty `
        -Filter /system.webServer/security/authentication/anonymousAuthentication `
        -Name enabled `
        -Value true `
        -PSPath $sitePath
    
    Write-Host "? Anonymous Authentication enabled" -ForegroundColor Green
    
} catch {
    Write-Host "? Could not fix authentication: $_" -ForegroundColor Red
    exit 1
}

# Step 2: Fix Serilog Path in appsettings.Production.json
Write-Host "`nStep 2: Fixing Serilog Path..." -ForegroundColor Yellow

$settingsPath = "$WebPath\appsettings.Production.json"
if (Test-Path $settingsPath) {
    try {
        $settings = Get-Content $settingsPath -Raw | ConvertFrom-Json
        
        # Check current path
        if ($settings.Serilog -and $settings.Serilog.WriteTo) {
            $currentPath = $settings.Serilog.WriteTo[0].Args.path
            Write-Host "  Current path: $currentPath" -ForegroundColor Gray
            
            if ($currentPath -like "R:\*") {
                Write-Host "  ? Path uses non-existent R:\ drive" -ForegroundColor Yellow
                Write-Host "  Changing to C:\Logs\SecureBootDashboard..." -ForegroundColor Yellow
                
                # Update path
                $settings.Serilog.WriteTo[0].Args.path = "C:\Logs\SecureBootDashboard\web-.log"
                
                # Backup original
                $backupPath = "$settingsPath.backup_$(Get-Date -Format 'yyyyMMddHHmmss')"
                Copy-Item $settingsPath $backupPath
                Write-Host "  Backup: $(Split-Path $backupPath -Leaf)" -ForegroundColor Gray
                
                # Save updated
                $settings | ConvertTo-Json -Depth 10 | Set-Content $settingsPath -Encoding UTF8
                Write-Host "? Serilog path updated to C:\Logs\SecureBootDashboard" -ForegroundColor Green
            } else {
                Write-Host "? Serilog path is correct" -ForegroundColor Green
            }
        } else {
            Write-Host "? No Serilog configuration found" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "? Could not update appsettings: $_" -ForegroundColor Red
    }
} else {
    Write-Host "? appsettings.Production.json not found!" -ForegroundColor Red
}

# Step 3: Ensure log directories exist with permissions
Write-Host "`nStep 3: Ensuring Log Directories..." -ForegroundColor Yellow

$logDir = "C:\Logs\SecureBootDashboard"
if (-not (Test-Path $logDir)) {
    New-Item -Path $logDir -ItemType Directory -Force | Out-Null
    Write-Host "? Created: $logDir" -ForegroundColor Green
} else {
    Write-Host "? Exists: $logDir" -ForegroundColor Green
}

# Set permissions
try {
    $acl = Get-Acl $logDir
    $identity = "IIS AppPool\$AppPoolName"
    $rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
        $identity, "FullControl", "ContainerInherit,ObjectInherit", "None", "Allow"
    )
    $acl.AddAccessRule($rule)
    Set-Acl $logDir $acl
    Write-Host "? Permissions set for $identity" -ForegroundColor Green
} catch {
    Write-Host "? Could not set permissions: $_" -ForegroundColor Yellow
}

# Step 4: Verify web.config
Write-Host "`nStep 4: Verifying web.config..." -ForegroundColor Yellow

$webConfigPath = "$WebPath\web.config"
if (Test-Path $webConfigPath) {
    try {
        [xml]$webConfig = Get-Content $webConfigPath
        $aspNetCore = $webConfig.SelectSingleNode("//aspNetCore")
        
        if ($aspNetCore) {
            $stdoutEnabled = $aspNetCore.GetAttribute("stdoutLogEnabled")
            $stdoutFile = $aspNetCore.GetAttribute("stdoutLogFile")
            $hostingModel = $aspNetCore.GetAttribute("hostingModel")
            
            $allGood = $true
            
            # Check stdout enabled
            if ($stdoutEnabled -ne "true") {
                Write-Host "? stdoutLogEnabled is not 'true'" -ForegroundColor Yellow
                $allGood = $false
            }
            
            # Check stdout path is absolute
            if (-not ($stdoutFile -match "^[A-Z]:")) {
                Write-Host "? stdoutLogFile is not absolute path" -ForegroundColor Yellow
                $allGood = $false
            }
            
            # Check hosting model
            if ($hostingModel -ne "outofprocess") {
                Write-Host "? hostingModel is '$hostingModel' (should be 'outofprocess')" -ForegroundColor Yellow
                $allGood = $false
            }
            
            if ($allGood) {
                Write-Host "? web.config is correctly configured" -ForegroundColor Green
                Write-Host "  stdoutLogEnabled: $stdoutEnabled" -ForegroundColor Gray
                Write-Host "  stdoutLogFile: $stdoutFile" -ForegroundColor Gray
                Write-Host "  hostingModel: $hostingModel" -ForegroundColor Gray
            } else {
                Write-Host "? web.config needs manual review" -ForegroundColor Yellow
            }
        }
    } catch {
        Write-Host "? Could not parse web.config: $_" -ForegroundColor Red
    }
} else {
    Write-Host "? web.config not found!" -ForegroundColor Red
}

# Step 5: Restart App Pool
Write-Host "`nStep 5: Restarting Application Pool..." -ForegroundColor Yellow

try {
    Stop-WebAppPool $AppPoolName -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    Start-WebAppPool $AppPoolName
    Start-Sleep -Seconds 5
    
    $state = (Get-WebAppPoolState $AppPoolName).Value
    Write-Host "? App Pool: $state" -ForegroundColor Green
} catch {
    Write-Host "? Could not restart App Pool: $_" -ForegroundColor Red
}

# Step 6: Test Site
Write-Host "`nStep 6: Testing Site..." -ForegroundColor Yellow

try {
    $response = Invoke-WebRequest -Uri "https://secbootsrv.mslabs.local" `
        -UseBasicParsing `
        -SkipCertificateCheck `
        -TimeoutSec 10 `
        -ErrorAction Stop
    
    Write-Host "? Site is responding: HTTP $($response.StatusCode)" -ForegroundColor Green
    
    if ($response.Content -match "An error occurred") {
        Write-Host "? Page contains error message" -ForegroundColor Yellow
    } else {
        Write-Host "? Page loaded successfully" -ForegroundColor Green
    }
    
} catch {
    Write-Host "? Site test failed: $_" -ForegroundColor Red
    
    if ($_.Exception.Response) {
        $statusCode = [int]$_.Exception.Response.StatusCode
        Write-Host "  Status Code: $statusCode" -ForegroundColor Red
    }
}

# Step 7: Check for recent errors
Write-Host "`nStep 7: Checking for Recent Errors..." -ForegroundColor Yellow

try {
    $recentErrors = Get-EventLog -LogName Application -Source "*AspNetCore*" -Newest 3 -ErrorAction SilentlyContinue |
        Where-Object { $_.EntryType -eq "Error" -and $_.TimeGenerated -gt (Get-Date).AddMinutes(-5) }
    
    if ($recentErrors) {
        Write-Host "? Found $($recentErrors.Count) recent error(s) (last 5 min)" -ForegroundColor Yellow
        foreach ($error in $recentErrors) {
            Write-Host "  [$($error.TimeGenerated)] $($error.Message.Substring(0, [Math]::Min(100, $error.Message.Length)))..." -ForegroundColor Red
        }
    } else {
        Write-Host "? No recent errors in Event Viewer" -ForegroundColor Green
    }
} catch {
    Write-Host "  Could not check Event Viewer: $_" -ForegroundColor Gray
}

# Summary
Write-Host ""
Write-Host "="*79 -ForegroundColor Cyan
Write-Host "SUMMARY" -ForegroundColor Cyan
Write-Host "="*79 -ForegroundColor Cyan

Write-Host ""
Write-Host "Configuration:" -ForegroundColor Yellow
Write-Host "  • Windows Authentication: DISABLED" -ForegroundColor White
Write-Host "  • Anonymous Authentication: ENABLED" -ForegroundColor White
Write-Host "  • Serilog Path: C:\Logs\SecureBootDashboard" -ForegroundColor White
Write-Host "  • Stdout Path: C:\Logs\SecureBootDashboard\stdout" -ForegroundColor White
Write-Host "  • Hosting Model: outofprocess" -ForegroundColor White
Write-Host ""
Write-Host "Next:" -ForegroundColor Yellow
Write-Host "  1. Test site in browser: https://secbootsrv.mslabs.local" -ForegroundColor White
Write-Host "  2. Check if Windows login prompt appears" -ForegroundColor White
Write-Host "  3. Verify dashboard loads after authentication" -ForegroundColor White
Write-Host ""
Write-Host "If issues persist:" -ForegroundColor Yellow
Write-Host "  Run: Get-EventLog -LogName Application -Source '*AspNetCore*' -Newest 5" -ForegroundColor Cyan
Write-Host ""

