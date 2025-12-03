<#
.SYNOPSIS
    Install SecureBootWatcher PowerShell Client for Intune Win32 App deployment

.DESCRIPTION
    This script installs the SecureBootWatcher PowerShell client on Windows devices
    and creates a scheduled task to run the inventory collection.

.PARAMETER ApiBaseUrl
    Base URL of the SecureBootWatcher Dashboard API

.PARAMETER FleetId
    Fleet identifier for grouping devices

.PARAMETER ScheduleType
    Type of schedule: Once, Daily, Hourly, or Custom

.PARAMETER TaskTime
    Time of day to run the task (for Daily schedule)

.PARAMETER RepeatEveryHours
    Hours between executions (for Custom schedule)

.PARAMETER RandomDelayMinutes
    Maximum random delay in minutes to spread load

.EXAMPLE
    .\Install-PowerShellClient-Intune.ps1 -ApiBaseUrl "https://api.contoso.com" -FleetId "PROD"

.NOTES
    Exit code 0 = success, non-zero = failure
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$ApiBaseUrl,
    
    [Parameter(Mandatory = $false)]
    [string]$FleetId = "Default",
    
    [Parameter(Mandatory = $false)]
    [ValidateSet("Once", "Daily", "Hourly", "Custom")]
    [string]$ScheduleType = "Daily",
    
    [Parameter(Mandatory = $false)]
    [string]$TaskTime = "09:00AM",
    
    [Parameter(Mandatory = $false)]
    [ValidateRange(1, 24)]
    [int]$RepeatEveryHours = 4,
    
    [Parameter(Mandatory = $false)]
    [ValidateRange(0, 1440)]
    [int]$RandomDelayMinutes = 60
)

$ErrorActionPreference = "Stop"

# Define paths
$installPath = "C:\Program Files\SecureBootWatcher\PowerShell"
$taskName = "SecureBootWatcher-PowerShell"
$logPath = Join-Path $env:ProgramData "SecureBootWatcher\install-powershell.log"

# Get script directory (where Intune extracts the package)
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

Write-InstallLog "Starting SecureBootWatcher PowerShell Client installation"
Write-InstallLog "Script directory: $scriptDir"
Write-InstallLog "Target directory: $installPath"
Write-InstallLog "Schedule: Type=$ScheduleType, Time=$TaskTime, RandomDelay=$RandomDelayMinutes min"

try {
    # Step 1: Create installation directory
    Write-InstallLog "Creating installation directory"
    if (-not (Test-Path $installPath)) {
        New-Item -ItemType Directory -Path $installPath -Force | Out-Null
    }

    # Step 2: Copy PowerShell script and config files
    Write-InstallLog "Copying PowerShell client files"
    
    $mainScript = Join-Path $scriptDir "SecureBootWatcher-Client.ps1"
    $configFile = Join-Path $scriptDir "appsettings.powershell.json"
    
    if (-not (Test-Path $mainScript)) {
        throw "Main script not found: $mainScript"
    }
    
    if (-not (Test-Path $configFile)) {
        throw "Configuration file not found: $configFile"
    }
    
    # Copy files
    Copy-Item -Path $mainScript -Destination $installPath -Force
    Copy-Item -Path $configFile -Destination (Join-Path $installPath "appsettings.json") -Force
    
    Write-InstallLog "Files copied successfully"
    Write-InstallLog "  - SecureBootWatcher-Client.ps1"
    Write-InstallLog "  - appsettings.json"

    # Step 3: Configure appsettings.json if parameters provided
    if (-not [string]::IsNullOrEmpty($ApiBaseUrl) -or -not [string]::IsNullOrEmpty($FleetId)) {
        $appsettingsPath = Join-Path $installPath "appsettings.json"
        
        if (Test-Path $appsettingsPath) {
            Write-InstallLog "Configuring appsettings.json"
            $config = Get-Content $appsettingsPath -Raw | ConvertFrom-Json
            
            if (-not [string]::IsNullOrEmpty($ApiBaseUrl)) {
                Write-InstallLog "Configuring WebApi: $ApiBaseUrl"
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
    
    $scriptPath = Join-Path $installPath "SecureBootWatcher-Client.ps1"
    $configPath = Join-Path $installPath "appsettings.json"
    
    # Remove existing task if present
    $existingTask = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
    if ($existingTask) {
        Write-InstallLog "Removing existing scheduled task"
        Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
    }

    # Create task action - use PowerShell to run the script
    $actionArgs = "-NoProfile -ExecutionPolicy Bypass -File `"$scriptPath`" -ConfigPath `"$configPath`""
    $action = New-ScheduledTaskAction -Execute "PowerShell.exe" -Argument $actionArgs -WorkingDirectory $installPath

    # Parse task time
    try {
        $taskDateTime = [DateTime]::Parse($TaskTime)
    }
    catch {
        Write-InstallLog "WARNING: Invalid TaskTime '$TaskTime', using 09:00AM"
        $taskDateTime = [DateTime]::Parse("09:00AM")
    }

    # Create random delay TimeSpan
    $randomDelayTimeSpan = New-TimeSpan -Minutes $RandomDelayMinutes
    
    # Create task trigger based on schedule type
    $trigger = $null
    $scheduleDescription = ""
    $maxRepetitionDuration = New-TimeSpan -Days 31
    
    switch ($ScheduleType) {
        "Once" {
            $trigger = New-ScheduledTaskTrigger -Once -At $taskDateTime -RandomDelay $randomDelayTimeSpan
            $scheduleDescription = "Once at $TaskTime (±$RandomDelayMinutes min)"
        }
        "Daily" {
            $trigger = New-ScheduledTaskTrigger -Daily -At $taskDateTime -RandomDelay $randomDelayTimeSpan
            $scheduleDescription = "Daily at $TaskTime (±$RandomDelayMinutes min)"
        }
        "Hourly" {
            $trigger = New-ScheduledTaskTrigger -Once -At $taskDateTime -RepetitionInterval (New-TimeSpan -Hours 1) -RepetitionDuration $maxRepetitionDuration
            $scheduleDescription = "Every hour starting at $TaskTime"
            Write-InstallLog "  Note: RandomDelay not supported for Hourly schedule"
        }
        "Custom" {
            $trigger = New-ScheduledTaskTrigger -Once -At $taskDateTime -RepetitionInterval (New-TimeSpan -Hours $RepeatEveryHours) -RepetitionDuration $maxRepetitionDuration
            $scheduleDescription = "Every $RepeatEveryHours hours starting at $TaskTime"
            Write-InstallLog "  Note: RandomDelay not supported for Custom schedule with repetition"
        }
    }

    # Create task principal (run as SYSTEM)
    $principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest

    # Create task settings
    $settings = New-ScheduledTaskSettingsSet `
        -AllowStartIfOnBatteries `
        -DontStopIfGoingOnBatteries `
        -StartWhenAvailable `
        -ExecutionTimeLimit (New-TimeSpan -Hours 2) `
        -MultipleInstances IgnoreNew

    # Register task
    Register-ScheduledTask `
        -TaskName $taskName `
        -Action $action `
        -Trigger $trigger `
        -Principal $principal `
        -Settings $settings `
        -Description "SecureBootWatcher PowerShell Client - Monitors Secure Boot certificate status" | Out-Null

    Write-InstallLog "Scheduled task created successfully"
    Write-InstallLog "  Schedule: $scheduleDescription"
    Write-InstallLog "  Task name: $taskName"
    Write-InstallLog "  Run as: SYSTEM"
    Write-InstallLog "  Script: $scriptPath"

    Write-InstallLog "Installation completed successfully"
    exit 0
}
catch {
    Write-InstallLog "ERROR: Installation failed - $_"
    Write-InstallLog "Stack trace: $($_.ScriptStackTrace)"
    exit 1
}
