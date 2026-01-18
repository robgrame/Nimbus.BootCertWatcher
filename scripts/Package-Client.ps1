# Secure Boot Watcher Client Package Script
# This script builds and packages the SecureBootWatcher client with custom configuration

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$OutputPath = ".\client-package",
    
    [Parameter(Mandatory = $false)]
    [string]$ApiBaseUrl = "https://srvcm00.msintune.lab:5001",
    
    [Parameter(Mandatory = $false)]
    [string]$FleetId = "MSLABS",
    
    [Parameter(Mandatory = $false)]
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",
    
    [Parameter(Mandatory = $false)]
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  SecureBootWatcher Client Packager" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Get script directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent $scriptDir

# Normalize output path relative to repo root for consistency
if (-not [System.IO.Path]::IsPathRooted($OutputPath)) {
    $OutputPath = Join-Path $rootDir $OutputPath
}

# Step 1: Build Client
if (-not $SkipBuild) {
    Write-Host "[1/3] Building SecureBootWatcher Client..." -ForegroundColor Yellow
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
        Write-Host "  Build successful" -ForegroundColor Green
        Write-Host "     Output: $publishPath" -ForegroundColor Gray
    }
    catch {
        Write-Host "  Build failed: $_" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "[1/3] Skipping build (using existing binaries)..." -ForegroundColor Yellow
    $publishPath = Join-Path $rootDir "SecureBootWatcher.Client\bin\$Configuration\net48\win-x86\publish"
    
    # Validate publish directory exists
    if (-not (Test-Path $publishPath)) {
        Write-Host "Error: Publish directory not found at: $publishPath" -ForegroundColor Red
        Write-Host ""
        Write-Host "Run the script without -SkipBuild to build the client first." -ForegroundColor Yellow
        exit 1
    }
    
    Write-Host "  Using existing binaries" -ForegroundColor Green
    Write-Host "     Path: $publishPath" -ForegroundColor Gray
}
Write-Host ""

# Step 2: Configure appsettings.json
Write-Host "[2/3] Configuring appsettings.json..." -ForegroundColor Yellow

$appsettingsPath = Join-Path $publishPath "appsettings.json"
$configUpdated = $false

if (Test-Path $appsettingsPath) {
    try {
        $appsettings = Get-Content $appsettingsPath -Raw | ConvertFrom-Json
        
        # Update API Base URL if provided
        if (-not [string]::IsNullOrEmpty($ApiBaseUrl)) {
            if ($null -eq $appsettings.SecureBootWatcher) {
                $appsettings | Add-Member -NotePropertyName "SecureBootWatcher" -NotePropertyValue @{} -Force
            }
            if ($null -eq $appsettings.SecureBootWatcher.Sinks) {
                $appsettings.SecureBootWatcher | Add-Member -NotePropertyName "Sinks" -NotePropertyValue @{} -Force
            }
            if ($null -eq $appsettings.SecureBootWatcher.Sinks.WebApi) {
                $appsettings.SecureBootWatcher.Sinks | Add-Member -NotePropertyName "WebApi" -NotePropertyValue @{} -Force
            }
            
            $appsettings.SecureBootWatcher.Sinks.WebApi.BaseAddress = $ApiBaseUrl
            $appsettings.SecureBootWatcher.Sinks.EnableWebApi = $true
            
            Write-Host "  API Base URL: $ApiBaseUrl" -ForegroundColor Green
            $configUpdated = $true
        }
        
        # Update Fleet ID if provided
        if (-not [string]::IsNullOrEmpty($FleetId)) {
            if ($null -eq $appsettings.SecureBootWatcher) {
                $appsettings | Add-Member -NotePropertyName "SecureBootWatcher" -NotePropertyValue @{} -Force
            }
            
            $appsettings.SecureBootWatcher.FleetId = $FleetId
            Write-Host "  Fleet ID: $FleetId" -ForegroundColor Green
            $configUpdated = $true
        }
        
        if ($configUpdated) {
            # Save updated configuration
            $appsettings | ConvertTo-Json -Depth 10 | Set-Content $appsettingsPath -Encoding UTF8
            Write-Host "  Configuration updated" -ForegroundColor Green
        } else {
            Write-Host "  No configuration changes requested (use -ApiBaseUrl or -FleetId)" -ForegroundColor Gray
        }
    }
    catch {
        Write-Host "  Warning: Could not update appsettings.json: $_" -ForegroundColor Yellow
    }
} else {
    Write-Host "  Warning: appsettings.json not found in publish directory" -ForegroundColor Yellow
}
Write-Host ""

# Step 3: Create Package
Write-Host "[3/3] Creating deployment package..." -ForegroundColor Yellow

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
    Write-Host "  Package created" -ForegroundColor Green
    Write-Host "     Path: $packagePath" -ForegroundColor Gray
    Write-Host "     Size: $([math]::Round($packageSize, 2)) MB" -ForegroundColor Gray
}
catch {
    Write-Host "  Package creation failed: $_" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Summary
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Packaging Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "✓ Client package created successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "Package Location:" -ForegroundColor White
Write-Host "  $packagePath" -ForegroundColor Cyan
Write-Host ""

if ($configUpdated) {
    Write-Host "Configuration Applied:" -ForegroundColor White
    if (-not [string]::IsNullOrEmpty($ApiBaseUrl)) {
        Write-Host "  API Base URL: $ApiBaseUrl" -ForegroundColor Cyan
    }
    if (-not [string]::IsNullOrEmpty($FleetId)) {
        Write-Host "  Fleet ID: $FleetId" -ForegroundColor Cyan
    }
    Write-Host ""
}

Write-Host "Next Steps:" -ForegroundColor White
Write-Host ""
Write-Host "1. Distribute the package to target devices" -ForegroundColor Yellow
Write-Host ""
Write-Host "2. On each target device, extract to:" -ForegroundColor Yellow
Write-Host "   C:\Program Files\SecureBootWatcher\" -ForegroundColor Cyan
Write-Host ""
Write-Host "3. Review and customize appsettings.json if needed" -ForegroundColor Yellow
Write-Host ""
Write-Host "4. Install using the deployment script:" -ForegroundColor Yellow
Write-Host "   .\Deploy-Client.ps1 -PackageZipPath `"$packagePath`" -CreateScheduledTask" -ForegroundColor Cyan
Write-Host ""

Write-Host "Usage Examples:" -ForegroundColor White
Write-Host ""
Write-Host "  # Build and package with default settings:" -ForegroundColor Gray
Write-Host "  .\Package-Client.ps1" -ForegroundColor Cyan
Write-Host ""
Write-Host "  # Package with custom API URL:" -ForegroundColor Gray
Write-Host "  .\Package-Client.ps1 -ApiBaseUrl `"https://api.contoso.com`"" -ForegroundColor Cyan
Write-Host ""
Write-Host "  # Package with Fleet ID:" -ForegroundColor Gray
Write-Host "  .\Package-Client.ps1 -FleetId `"fleet-production`"" -ForegroundColor Cyan
Write-Host ""
Write-Host "  # Package with both API URL and Fleet ID:" -ForegroundColor Gray
Write-Host "  .\Package-Client.ps1 ``" -ForegroundColor Cyan
Write-Host "      -ApiBaseUrl `"https://api.contoso.com`" ``" -ForegroundColor Cyan
Write-Host "      -FleetId `"fleet-production`"" -ForegroundColor Cyan
Write-Host ""
Write-Host "  # Use existing build (skip rebuild):" -ForegroundColor Gray
Write-Host "  .\Package-Client.ps1 -SkipBuild" -ForegroundColor Cyan
Write-Host ""
Write-Host "  # Debug build:" -ForegroundColor Gray
Write-Host "  .\Package-Client.ps1 -Configuration Debug" -ForegroundColor Cyan
Write-Host ""

Write-Host "Parameters:" -ForegroundColor White
Write-Host "  -OutputPath <path>        : Output directory for package (default: .\client-package)" -ForegroundColor Gray
Write-Host "  -ApiBaseUrl <url>         : API base URL to configure in appsettings.json" -ForegroundColor Gray
Write-Host "  -FleetId <id>             : Fleet ID to configure in appsettings.json" -ForegroundColor Gray
Write-Host "  -Configuration <config>   : Build configuration (Release|Debug, default: Release)" -ForegroundColor Gray
Write-Host "  -SkipBuild                : Skip build step, use existing binaries" -ForegroundColor Gray
Write-Host ""

Write-Host "Documentation:" -ForegroundColor White
Write-Host "  - Deployment Guide: docs\DEPLOYMENT_GUIDE.md" -ForegroundColor Gray
Write-Host "  - README: README.md" -ForegroundColor Gray
Write-Host ""
