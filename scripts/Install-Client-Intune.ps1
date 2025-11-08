# SecureBootWatcher Client Install Script for Intune Win32 App
# This script installs the SecureBootWatcher client from the package
# Exit code 0 = success, non-zero = failure

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$ApiBaseUrl = "",
    
    [Parameter(Mandatory = $false)]
    [string]$FleetId = ""
)

$ErrorActionPreference = "Stop"

# Define paths
$installPath = "C:\Program Files\SecureBootWatcher"
$taskName = "SecureBootWatcher"
$logPath = Join-Path $env:ProgramData "SecureBootWatcher\install.log"

# Get script directory (where the package content is extracted by Intune)
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Create log directory
$logDir = Split-Path $logPath -Parent
if (-not (Test-Path $logDir)) {
    New-Item -ItemType Directory -Path $logDir -Force | Out-Null
}

# Log function
function Write-InstallLog {
    param([string]$Message)
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    "$timestamp - $Message" | Out-File -FilePath $logPath -Append
    Write-Host $Message
}

Write-InstallLog "Starting SecureBootWatcher installation"
Write-InstallLog "Script directory: $scriptDir"
Write-InstallLog "Target directory: $installPath"

try {
    # Step 1: Create installation directory
    Write-InstallLog "Creating installation directory"
    if (-not (Test-Path $installPath)) {
        New-Item -ItemType Directory -Path $installPath -Force | Out-Null
    }

    # Step 2: Copy files from script directory to installation directory
    Write-InstallLog "Copying client files"
    
    # Files are already extracted by Intune to $scriptDir
    # Copy everything except this install script
    $filesToCopy = Get-ChildItem -Path $scriptDir -File | 
        Where-Object { $_.Name -notlike "Install-*.ps1" -and $_.Name -notlike "Uninstall-*.ps1" -and $_.Name -notlike "Detect-*.ps1" }
    
    foreach ($file in $filesToCopy) {
        Copy-Item -Path $file.FullName -Destination $installPath -Force
        Write-InstallLog "Copied: $($file.Name)"
    }

    # Copy directories (e.g., logs folder structure if present)
    $dirsToCheck = @("logs")
    foreach ($dir in $dirsToCheck) {
        $sourceDir = Join-Path $scriptDir $dir
        if (Test-Path $sourceDir) {
            $destDir = Join-Path $installPath $dir
            if (-not (Test-Path $destDir)) {
                New-Item -ItemType Directory -Path $destDir -Force | Out-Null
            }
            Write-InstallLog "Created directory: $dir"
        }
    }

    # Step 3: Configure appsettings.json if parameters provided
    if (-not [string]::IsNullOrEmpty($ApiBaseUrl) -or -not [string]::IsNullOrEmpty($FleetId)) {
        $appsettingsPath = Join-Path $installPath "appsettings.json"
        
        if (Test-Path $appsettingsPath) {
            Write-InstallLog "Configuring appsettings.json"
            $config = Get-Content $appsettingsPath -Raw | ConvertFrom-Json
            
            if (-not [string]::IsNullOrEmpty($ApiBaseUrl)) {
                $config.SecureBootWatcher.Sinks.WebApi.BaseAddress = $ApiBaseUrl
                $config.SecureBootWatcher.Sinks.EnableWebApi = $true
                Write-InstallLog "Set API Base URL: $ApiBaseUrl"
            }
            
            if (-not [string]::IsNullOrEmpty($FleetId)) {
                $config.SecureBootWatcher.FleetId = $FleetId
                Write-InstallLog "Set Fleet ID: $FleetId"
            }
            
            $config | ConvertTo-Json -Depth 10 | Set-Content $appsettingsPath -Encoding UTF8
        }
    }

    # Step 4: Create scheduled task
    Write-InstallLog "Creating scheduled task"
    
    $exePath = Join-Path $installPath "SecureBootWatcher.Client.exe"
    
    # Remove existing task if present
    $existingTask = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
    if ($existingTask) {
        Write-InstallLog "Removing existing scheduled task"
        Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
    }

    # Create task action
    $action = New-ScheduledTaskAction -Execute $exePath -WorkingDirectory $installPath

    # Create task trigger (daily at 9 AM with random delay)
    $taskTime = [DateTime]::Parse("09:00AM")
    $randomDelay = New-TimeSpan -Minutes (Get-Random -Minimum 0 -Maximum 60)
    $trigger = New-ScheduledTaskTrigger -Daily -At $taskTime -RandomDelay $randomDelay

    # Create task principal (run as SYSTEM)
    $principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest

    # Create task settings
    $settings = New-ScheduledTaskSettingsSet `
        -AllowStartIfOnBatteries `
        -DontStopIfGoingOnBatteries `
        -StartWhenAvailable `
        -MultipleInstances IgnoreNew

    # Register task
    Register-ScheduledTask `
        -TaskName $taskName `
        -Action $action `
        -Trigger $trigger `
        -Principal $principal `
        -Settings $settings `
        -Description "Monitors Secure Boot certificate status and reports to dashboard" | Out-Null

    Write-InstallLog "Scheduled task created successfully"

    Write-InstallLog "Installation completed successfully"
    exit 0
}
catch {
    Write-InstallLog "ERROR: Installation failed - $_"
    exit 1
}
