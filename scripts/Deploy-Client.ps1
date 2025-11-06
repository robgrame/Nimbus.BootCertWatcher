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
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  SecureBootWatcher Client Deployment" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Get script directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent $scriptDir

# Step 1: Build Client
if (-not $SkipBuild) {
    Write-Host "[1/5] Building SecureBootWatcher Client..." -ForegroundColor Yellow
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
    Write-Host "[1/5] Skipping build (using existing binaries)..." -ForegroundColor Yellow
    $publishPath = Join-Path $rootDir "SecureBootWatcher.Client\bin\$Configuration\net48\win-x86\publish"
}
Write-Host ""

# Step 2: Configure appsettings.json
Write-Host "[2/5] Configuring appsettings.json..." -ForegroundColor Yellow

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

# Step 3: Create Package
Write-Host "[3/5] Creating deployment package..." -ForegroundColor Yellow

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
Write-Host ""

# Step 4: Install Client (optional)
if ($CreateScheduledTask) {
    Write-Host "[4/5] Installing client to: $InstallPath" -ForegroundColor Yellow
    
    try {
        # Create install directory
        if (-not (Test-Path $InstallPath)) {
            New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
        }
        
        # Extract package
        Expand-Archive -Path $packagePath -DestinationPath $InstallPath -Force
        
        Write-Host "  ? Client installed" -ForegroundColor Green
        Write-Host "     Location: $InstallPath" -ForegroundColor Gray
    }
    catch {
        Write-Host "  ? Installation failed: $_" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "[4/5] Skipping installation (use -CreateScheduledTask to install)" -ForegroundColor Yellow
}
Write-Host ""

# Step 5: Create Scheduled Task (optional)
if ($CreateScheduledTask) {
    Write-Host "[5/5] Creating scheduled task..." -ForegroundColor Yellow
    
    try {
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
        Write-Host "  ? Scheduled task creation failed: $_" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "[5/5] Skipping scheduled task creation (use -CreateScheduledTask to create)" -ForegroundColor Yellow
}
Write-Host ""

# Summary
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Deployment Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "? Client package created successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "Package Location:" -ForegroundColor White
Write-Host "  $packagePath" -ForegroundColor Cyan
Write-Host ""

if ($CreateScheduledTask) {
    Write-Host "? Client installed and scheduled!" -ForegroundColor Green
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
    Write-Host "   .\Deploy-Client.ps1 -CreateScheduledTask -SkipBuild" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Or use this script with -CreateScheduledTask to install locally:" -ForegroundColor Yellow
    Write-Host "   .\Deploy-Client.ps1 -ApiBaseUrl `"https://your-api.com`" -CreateScheduledTask" -ForegroundColor Cyan
}

Write-Host ""
Write-Host "Configuration:" -ForegroundColor White
Write-Host "  Edit appsettings.json to configure:" -ForegroundColor Gray
Write-Host "  - API Base URL" -ForegroundColor Gray
Write-Host "  - Fleet ID" -ForegroundColor Gray
Write-Host "  - Sink options (FileShare, AzureQueue, WebApi)" -ForegroundColor Gray
Write-Host "  - Polling intervals" -ForegroundColor Gray
Write-Host ""

Write-Host "Documentation:" -ForegroundColor White
Write-Host "  - Deployment Guide: docs\DEPLOYMENT_GUIDE.md" -ForegroundColor Gray
Write-Host "  - README: README.md" -ForegroundColor Gray
Write-Host ""
