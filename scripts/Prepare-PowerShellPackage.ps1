<#
.SYNOPSIS
    Prepare SecureBootWatcher PowerShell Client package for Intune deployment

.DESCRIPTION
    This script creates a deployment package containing all necessary files
    for deploying the PowerShell client via Intune Win32 App or other
    device management solutions.

.PARAMETER OutputPath
    Path where the package directory will be created

.PARAMETER ApiBaseUrl
    Optional: Pre-configure API base URL in the package

.PARAMETER FleetId
    Optional: Pre-configure Fleet ID in the package

.EXAMPLE
    .\Prepare-PowerShellPackage.ps1 -OutputPath "C:\Packages"

.EXAMPLE
    .\Prepare-PowerShellPackage.ps1 -OutputPath "C:\Packages" -ApiBaseUrl "https://api.contoso.com" -FleetId "PROD"

.NOTES
    This script should be run from the repository root directory
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$OutputPath = ".\SecureBootWatcher-PowerShell-Package",
    
    [Parameter(Mandatory = $false)]
    [string]$ApiBaseUrl,
    
    [Parameter(Mandatory = $false)]
    [string]$FleetId
)

$ErrorActionPreference = "Stop"

Write-Host "========================================"
Write-Host "SecureBootWatcher PowerShell Client"
Write-Host "Package Preparation Tool"
Write-Host "========================================"
Write-Host ""

# Get script directory (repository root)
$repoRoot = $PSScriptRoot
Write-Host "Repository root: $repoRoot"

# Define source files
$sourceFiles = @{
    "SecureBootWatcher-Client.ps1" = "SecureBootWatcher-Client.ps1"
    "appsettings.powershell.json" = "appsettings.powershell.json"
    "Install-PowerShellClient-Intune.ps1" = "scripts\Install-PowerShellClient-Intune.ps1"
    "Detect-PowerShellClient-Intune.ps1" = "scripts\Detect-PowerShellClient-Intune.ps1"
    "Uninstall-PowerShellClient-Intune.ps1" = "scripts\Uninstall-PowerShellClient-Intune.ps1"
}

# Create output directory
Write-Host "Creating output directory: $OutputPath"
if (Test-Path $OutputPath) {
    Write-Host "  Output directory already exists, cleaning..."
    Remove-Item -Path $OutputPath -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null

# Copy files
Write-Host ""
Write-Host "Copying files to package:"
foreach ($file in $sourceFiles.GetEnumerator()) {
    $sourcePath = Join-Path $repoRoot $file.Value
    $destPath = Join-Path $OutputPath $file.Key
    
    if (-not (Test-Path $sourcePath)) {
        Write-Host "  ERROR: Source file not found: $sourcePath" -ForegroundColor Red
        throw "Missing required file: $($file.Value)"
    }
    
    Copy-Item -Path $sourcePath -Destination $destPath -Force
    $fileSize = (Get-Item $sourcePath).Length
    Write-Host "  ✓ $($file.Key) ($([math]::Round($fileSize / 1KB, 2)) KB)" -ForegroundColor Green
}

# Rename appsettings file in package
$appsettingsSource = Join-Path $OutputPath "appsettings.powershell.json"
$appsettingsDest = Join-Path $OutputPath "appsettings.json"
if (Test-Path $appsettingsSource) {
    Move-Item -Path $appsettingsSource -Destination $appsettingsDest -Force
    Write-Host "  ✓ Renamed appsettings.powershell.json to appsettings.json" -ForegroundColor Green
}

# Pre-configure settings if provided
if ($ApiBaseUrl -or $FleetId) {
    Write-Host ""
    Write-Host "Pre-configuring settings:"
    
    $configPath = Join-Path $OutputPath "appsettings.json"
    $config = Get-Content $configPath -Raw | ConvertFrom-Json
    
    if ($ApiBaseUrl) {
        $config.SecureBootWatcher.Sinks.WebApi.BaseAddress = $ApiBaseUrl
        $config.SecureBootWatcher.Sinks.EnableWebApi = $true
        Write-Host "  ✓ API Base URL: $ApiBaseUrl" -ForegroundColor Green
    }
    
    if ($FleetId) {
        $config.SecureBootWatcher.FleetId = $FleetId
        Write-Host "  ✓ Fleet ID: $FleetId" -ForegroundColor Green
    }
    
    $config | ConvertTo-Json -Depth 10 | Set-Content $configPath -Encoding UTF8
}

# Create README in package
$readmePath = Join-Path $OutputPath "README.txt"
$readmeContent = @"
SecureBootWatcher PowerShell Client - Intune Deployment Package
================================================================

This package contains all files needed to deploy the SecureBootWatcher
PowerShell client via Microsoft Intune Win32 App or other device
management solutions.

Package Contents:
-----------------
- SecureBootWatcher-Client.ps1     Main PowerShell client script
- appsettings.json                 Configuration file
- Install-PowerShellClient-Intune.ps1   Installation script
- Detect-PowerShellClient-Intune.ps1    Detection script
- Uninstall-PowerShellClient-Intune.ps1 Uninstall script

Deployment Steps:
-----------------

1. Create .intunewin package:
   Download Microsoft Win32 Content Prep Tool from:
   https://github.com/microsoft/Microsoft-Win32-Content-Prep-Tool
   
   Run:
   IntuneWinAppUtil.exe -c "." -s "Install-PowerShellClient-Intune.ps1" -o "..\Output"

2. Upload to Intune:
   - Navigate to Intune Admin Center > Apps > Windows > Add
   - Select Windows app (Win32)
   - Upload the generated .intunewin file

3. Configure installation:
   Install command:
   PowerShell.exe -ExecutionPolicy Bypass -File "Install-PowerShellClient-Intune.ps1" -ApiBaseUrl "https://your-api.azurewebsites.net" -FleetId "Production"
   
   Uninstall command:
   PowerShell.exe -ExecutionPolicy Bypass -File "Uninstall-PowerShellClient-Intune.ps1"

4. Configure detection:
   - Use custom detection script
   - Select: Detect-PowerShellClient-Intune.ps1

5. Assign to device groups and deploy

For detailed documentation, see:
https://github.com/robgrame/Nimbus.BootCertWatcher/blob/main/docs/POWERSHELL_CLIENT.md

Support:
--------
GitHub Issues: https://github.com/robgrame/Nimbus.BootCertWatcher/issues

"@

$readmeContent | Out-File -FilePath $readmePath -Encoding UTF8

Write-Host ""
Write-Host "Package preparation complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Package location: $OutputPath"
Write-Host "Package contents:"
Get-ChildItem -Path $OutputPath | ForEach-Object {
    $size = if ($_.PSIsContainer) { "DIR" } else { "$([math]::Round($_.Length / 1KB, 2)) KB" }
    Write-Host "  - $($_.Name) ($size)"
}

Write-Host ""
Write-Host "Next steps:"
Write-Host "  1. Review the package contents"
Write-Host "  2. Use Microsoft Win32 Content Prep Tool to create .intunewin package"
Write-Host "  3. Upload to Intune and configure deployment"
Write-Host ""
Write-Host "See README.txt in the package for detailed deployment instructions"
Write-Host ""
Write-Host "========================================"
