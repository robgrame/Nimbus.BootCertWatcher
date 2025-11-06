# Secure Boot Watcher Client Deployment Script
# This script builds, packages, and optionally deploys the SecureBootWatcher client to Windows devices

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$OutputPath = ".\client-package",
    
    [Parameter(Mandatory = $false)]
    [string]$ApiBaseUrl = "",
    
    [Parameter(Mandatory = $false)]
    [string]$FleetId = "",
    
    [Parameter(Mandatory = $false)]
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",
    
    [Parameter(Mandatory = $false)]
    [switch]$CreateScheduledTask,
    
    [Parameter(Mandatory = $false)]
    [string]$InstallPath = "C:\Program Files\SecureBootWatcher",
    
    [Parameter(Mandatory = $false)]
    [string]$TaskTime = "09:00AM",
    
    [Parameter(Mandatory = $false)]
    [switch]$SkipBuild,
    
    [Parameter(Mandatory = $false)]
    [string]$PackageZipPath = ""
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  SecureBootWatcher Client Deployment" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Get script directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent $scriptDir

# Determine deployment mode
$usePrecompiledPackage = -not [string]::IsNullOrEmpty($PackageZipPath)

if ($usePrecompiledPackage) {
    Write-Host "Mode: Using precompiled package" -ForegroundColor Cyan
    Write-Host "Package: $PackageZipPath" -ForegroundColor Gray
    Write-Host ""
    
    # Validate package exists
    if (-not (Test-Path $PackageZipPath)) {
        Write-Host "? Error: Package not found at: $PackageZipPath" -ForegroundColor Red
        Write-Host ""
        Write-Host "Please specify a valid path to the client package ZIP file." -ForegroundColor Yellow
        Write-Host "Example: .\Deploy-Client.ps1 -PackageZipPath `".\client-package\SecureBootWatcher-Client.zip`" -CreateScheduledTask" -ForegroundColor Cyan
        exit 1
    }
    
    # Validate it's a ZIP file
    if (-not ($PackageZipPath -like "*.zip")) {
        Write-Host "? Error: Package must be a ZIP file" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "? Package validated" -ForegroundColor Green
    Write-Host ""
    
    # Extract package to temporary directory for configuration
    $tempExtractPath = Join-Path $env:TEMP "SecureBootWatcher-Deploy-$(Get-Date -Format 'yyyyMMddHHmmss')"
    
    Write-Host "[1/4] Extracting package to temporary directory..." -ForegroundColor Yellow
    Write-Host "  Temp Path: $tempExtractPath" -ForegroundColor Gray
    
    try {
        New-Item -ItemType Directory -Path $tempExtractPath -Force | Out-Null
        Expand-Archive -Path $PackageZipPath -DestinationPath $tempExtractPath -Force
        Write-Host "  ? Package extracted" -ForegroundColor Green
    }
    catch {
        Write-Host "  ? Extraction failed: $_" -ForegroundColor Red
        exit 1
    }
    Write-Host ""
    
    # Use temp path for configuration
    $publishPath = $tempExtractPath
    
} else {
    # Original build logic
    
    # Step 1: Build Client
    if (-not $SkipBuild) {
        Write-Host "[1/4] Building SecureBootWatcher Client..." -ForegroundColor Yellow
        Write-Host "  Configuration: $Configuration" -ForegroundColor Gray
        Write-Host "  Target: win-x86" -ForegroundColor Gray
        
        try {
            $publishPath = Join-Path $rootDir "SecureBootWatcher.Client\bin\$Configuration\net48\win-x86\publish"
            
            Push-Location $rootDir
            dotnet publish SecureBootWatcher.Client `
                -c $Configuration `
                -r win-x86 `
                --self-contained false `
                -o $publishPath `
                /p:PublishSingleFile=false `
                /p:PublishTrimmed=false
        
            if ($LASTEXITCODE -ne 0) {
                throw "Build failed with exit code $LASTEXITCODE"
            }
        
            Pop-Location
            Write-Host "  ? Build successful" -ForegroundColor Green
            Write-Host "     Output: $publishPath" -ForegroundColor Gray
        }
        catch {
            Write-Host "  ? Build failed: $_" -ForegroundColor Red
            exit 1
        }
    } else {
        Write-Host "[1/4] Skipping build (using existing binaries)..." -ForegroundColor Yellow
        $publishPath = Join-Path $rootDir "SecureBootWatcher.Client\bin\$Configuration\net48\win-x86\publish"
        
        # Validate publish directory exists
        if (-not (Test-Path $publishPath)) {
            Write-Host "? Error: Publish directory not found at: $publishPath" -ForegroundColor Red
            Write-Host ""
            Write-Host "Options:" -ForegroundColor Yellow
            Write-Host "1. Build first: .\Deploy-Client.ps1 (without -SkipBuild)" -ForegroundColor Cyan
            Write-Host "2. Use precompiled package: .\Deploy-Client.ps1 -PackageZipPath `"path\to\package.zip`"" -ForegroundColor Cyan
            exit 1
        }
    }
    Write-Host ""
}

# Step 2: Configure appsettings.json
Write-Host "[2/4] Configuring appsettings.json..." -ForegroundColor Yellow

$appsettingsPath = Join-Path $publishPath "appsettings.json"

if (Test-Path $appsettingsPath) {
    try {
        $appsettings = Get-Content $appsettingsPath -Raw | ConvertFrom-Json
        
        # Update API Base URL if provided
        if (-not [string]::IsNullOrEmpty($ApiBaseUrl)) {
            if ($appsettings.SecureBootWatcher.Sinks.WebApi) {
                $appsettings.SecureBootWatcher.Sinks.WebApi.BaseAddress = $ApiBaseUrl
                $appsettings.SecureBootWatcher.Sinks.EnableWebApi = $true
                Write-Host "  ? API Base URL: $ApiBaseUrl" -ForegroundColor Green
            }
        }
        
        # Update Fleet ID if provided
        if (-not [string]::IsNullOrEmpty($FleetId)) {
            $appsettings.SecureBootWatcher.FleetId = $FleetId
            Write-Host "  ? Fleet ID: $FleetId" -ForegroundColor Green
        }
        
        # Save updated configuration
        $appsettings | ConvertTo-Json -Depth 10 | Set-Content $appsettingsPath -Encoding UTF8
        Write-Host "  ? Configuration updated" -ForegroundColor Green
    }
    catch {
        Write-Host "  ??  Warning: Could not update appsettings.json: $_" -ForegroundColor Yellow
    }
} else {
    Write-Host "  ??  Warning: appsettings.json not found in publish directory" -ForegroundColor Yellow
}
Write-Host ""

# Step 3: Create/Update Package (only if not using precompiled package)
if (-not $usePrecompiledPackage) {
    Write-Host "[3/4] Creating deployment package..." -ForegroundColor Yellow

    $packagePath = Join-Path $OutputPath "SecureBootWatcher-Client.zip"
    $packageDir = Split-Path -Parent $packagePath

    if (-not (Test-Path $packageDir)) {
        New-Item -ItemType Directory -Path $packageDir -Force | Out-Null
    }

    try {
        if (Test-Path $packagePath) {
            Remove-Item $packagePath -Force
        }
        
        Compress-Archive -Path "$publishPath\*" -DestinationPath $packagePath -Force
        
        $packageSize = (Get-Item $packagePath).Length / 1MB
        Write-Host "  ? Package created" -ForegroundColor Green
        Write-Host "     Path: $packagePath" -ForegroundColor Gray
        Write-Host "     Size: $([math]::Round($packageSize, 2)) MB" -ForegroundColor Gray
    }
    catch {
        Write-Host "  ? Package creation failed: $_" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "[3/4] Using precompiled package (configuration applied to temp copy)..." -ForegroundColor Yellow
    # We'll install from the temp path with updated config
}
Write-Host ""

# Step 4: Install Client (optional)
if ($CreateScheduledTask) {
    Write-Host "[4/4] Installing client to: $InstallPath" -ForegroundColor Yellow
    
    try {
        # Create install directory
        if (-not (Test-Path $InstallPath)) {
            New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
        }
        
        # Copy files from publish/temp path to install directory
        Copy-Item -Path "$publishPath\*" -Destination $InstallPath -Recurse -Force
        
        Write-Host "  ? Client installed" -ForegroundColor Green
        Write-Host "     Location: $InstallPath" -ForegroundColor Gray
        
        # Create scheduled task
        Write-Host ""
        Write-Host "  Creating scheduled task..." -ForegroundColor Yellow
        
        $exePath = Join-Path $InstallPath "SecureBootWatcher.Client.exe"
        
        if (-not (Test-Path $exePath)) {
            throw "Client executable not found at: $exePath"
        }
        
        # Check if task already exists
        $existingTask = Get-ScheduledTask -TaskName "SecureBootWatcher" -ErrorAction SilentlyContinue
        
        if ($existingTask) {
            Write-Host "  ??  Scheduled task already exists, removing..." -ForegroundColor Yellow
            Unregister-ScheduledTask -TaskName "SecureBootWatcher" -Confirm:$false
        }
        
        # Create new task
        $action = New-ScheduledTaskAction -Execute $exePath -WorkingDirectory $InstallPath
        $trigger = New-ScheduledTaskTrigger -Daily -At $TaskTime
        $principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest
        $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable
        
        Register-ScheduledTask `
            -TaskName "SecureBootWatcher" `
            -Action $action `
            -Trigger $trigger `
            -Principal $principal `
            -Settings $settings `
            -Description "Monitors Secure Boot certificate status and reports to dashboard" | Out-Null
        
        Write-Host "  ? Scheduled task created" -ForegroundColor Green
        Write-Host "     Task Name: SecureBootWatcher" -ForegroundColor Gray
        Write-Host "     Run As: SYSTEM" -ForegroundColor Gray
        Write-Host "     Schedule: Daily at $TaskTime" -ForegroundColor Gray
        Write-Host "     Executable: $exePath" -ForegroundColor Gray
    }
    catch {
        Write-Host "  ? Installation/Scheduled task failed: $_" -ForegroundColor Red
        exit 1
    }
    finally {
        # Cleanup temp directory if using precompiled package
        if ($usePrecompiledPackage -and (Test-Path $tempExtractPath)) {
            Write-Host ""
            Write-Host "  Cleaning up temporary files..." -ForegroundColor Gray
            Remove-Item -Path $tempExtractPath -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
} else {
    Write-Host "[4/4] Skipping installation (use -CreateScheduledTask to install)" -ForegroundColor Yellow
    
    # Cleanup temp directory if using precompiled package
    if ($usePrecompiledPackage -and (Test-Path $tempExtractPath)) {
        Write-Host ""
        Write-Host "  Cleaning up temporary files..." -ForegroundColor Gray
        Remove-Item -Path $tempExtractPath -Recurse -Force -ErrorAction SilentlyContinue
    }
}
Write-Host ""

# Summary
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Deployment Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if ($usePrecompiledPackage) {
    Write-Host "? Deployment from precompiled package completed!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Source Package:" -ForegroundColor White
    Write-Host "  $PackageZipPath" -ForegroundColor Cyan
} else {
    Write-Host "? Client package created successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Package Location:" -ForegroundColor White
    Write-Host "  $packagePath" -ForegroundColor Cyan
}
Write-Host ""

if ($CreateScheduledTask) {
    Write-Host "? Client installed and scheduled!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Installation:" -ForegroundColor White
    Write-Host "  Location: $InstallPath" -ForegroundColor Cyan
    Write-Host "  Task Name: SecureBootWatcher" -ForegroundColor Cyan
    Write-Host "  Schedule: Daily at $TaskTime" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "To test the client manually:" -ForegroundColor White
    Write-Host "  cd `"$InstallPath`"" -ForegroundColor Cyan
    Write-Host "  .\SecureBootWatcher.Client.exe" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "To run the scheduled task immediately:" -ForegroundColor White
    Write-Host "  Start-ScheduledTask -TaskName SecureBootWatcher" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "To view task history:" -ForegroundColor White
    Write-Host "  Get-ScheduledTaskInfo -TaskName SecureBootWatcher" -ForegroundColor Cyan
} else {
    Write-Host "Next Steps:" -ForegroundColor White
    Write-Host ""
    
    if ($usePrecompiledPackage) {
        Write-Host "1. To install locally with this precompiled package:" -ForegroundColor Yellow
        Write-Host "   .\Deploy-Client.ps1 -PackageZipPath `"$PackageZipPath`" -CreateScheduledTask" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "2. To install with custom API URL:" -ForegroundColor Yellow
        Write-Host "   .\Deploy-Client.ps1 -PackageZipPath `"$PackageZipPath`" -ApiBaseUrl `"https://your-api.com`" -CreateScheduledTask" -ForegroundColor Cyan
    } else {
        Write-Host "1. Distribute the package:" -ForegroundColor Yellow
        Write-Host "   - Via Group Policy (NETLOGON share)" -ForegroundColor Gray
        Write-Host "   - Via Microsoft Endpoint Manager (Intune)" -ForegroundColor Gray
        Write-Host "   - Via SCCM/ConfigMgr" -ForegroundColor Gray
        Write-Host "   - Manual installation" -ForegroundColor Gray
        Write-Host ""
        Write-Host "2. On target devices, extract to:" -ForegroundColor Yellow
        Write-Host "   C:\Program Files\SecureBootWatcher\" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "3. Configure appsettings.json on each device" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "4. Create scheduled task:" -ForegroundColor Yellow
        Write-Host "   .\Deploy-Client.ps1 -PackageZipPath `"$packagePath`" -CreateScheduledTask" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "Or use this script with -CreateScheduledTask to install locally:" -ForegroundColor Yellow
        Write-Host "   .\Deploy-Client.ps1 -ApiBaseUrl `"https://your-api.com`" -CreateScheduledTask" -ForegroundColor Cyan
    }
    Write-Host ""
}

Write-Host ""
Write-Host "Configuration:" -ForegroundColor White
Write-Host "  Edit appsettings.json to configure:" -ForegroundColor Gray
Write-Host "  - API Base URL" -ForegroundColor Gray
Write-Host "  - Fleet ID" -ForegroundColor Gray
Write-Host "  - Sink options (FileShare, AzureQueue, WebApi)" -ForegroundColor Gray
Write-Host "  - Polling intervals" -ForegroundColor Gray
Write-Host ""

Write-Host "Usage Examples:" -ForegroundColor White
Write-Host ""
Write-Host "  # Build and create package:" -ForegroundColor Gray
Write-Host "  .\Deploy-Client.ps1" -ForegroundColor Cyan
Write-Host ""
Write-Host "  # Install from precompiled package:" -ForegroundColor Gray
Write-Host "  .\Deploy-Client.ps1 -PackageZipPath `".\client-package\SecureBootWatcher-Client.zip`" -CreateScheduledTask" -ForegroundColor Cyan
Write-Host ""
Write-Host "  # Install with custom API and Fleet:" -ForegroundColor Gray
Write-Host "  .\Deploy-Client.ps1 -PackageZipPath `".\client-package\SecureBootWatcher-Client.zip`" ``" -ForegroundColor Cyan
Write-Host "                      -ApiBaseUrl `"https://api.contoso.com`" ``" -ForegroundColor Cyan
Write-Host "                      -FleetId `"fleet-prod`" ``" -ForegroundColor Cyan
Write-Host "                      -CreateScheduledTask" -ForegroundColor Cyan
Write-Host ""

Write-Host "Documentation:" -ForegroundColor White
Write-Host "  - Deployment Guide: docs\DEPLOYMENT_GUIDE.md" -ForegroundColor Gray
Write-Host "  - Client Deployment Scripts: docs\CLIENT_DEPLOYMENT_SCRIPTS.md" -ForegroundColor Gray
Write-Host "  - README: README.md" -ForegroundColor Gray
Write-Host ""
