# SecureBootWatcher Client Remediation Script
# Used for Microsoft Endpoint Manager (Intune) Proactive Remediations
# Attempts to fix common issues with the client installation

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$InstallPath = "C:\Program Files\SecureBootWatcher",
    
    [Parameter(Mandatory = $false)]
    [string]$TaskName = "SecureBootWatcher",
    
    [Parameter(Mandatory = $false)]
    [string]$TaskTime = "09:00AM",
    
    [Parameter(Mandatory = $false)]
    [ValidateSet("Once", "Daily", "Hourly", "Custom")]
    [string]$ScheduleType = "Daily",
    
    [Parameter(Mandatory = $false)]
    [int]$RepeatEveryHours = 4,
    
    [Parameter(Mandatory = $false)]
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"

function Write-RemediationLog {
    param([string]$Message, [string]$Level = "Info")
    
    if ($Verbose) {
        switch ($Level) {
            "Error" { Write-Host "[ERROR] $Message" -ForegroundColor Red }
            "Warning" { Write-Host "[WARN] $Message" -ForegroundColor Yellow }
            "Success" { Write-Host "[OK] $Message" -ForegroundColor Green }
            default { Write-Host "[INFO] $Message" -ForegroundColor Cyan }
        }
    }
    
    # Log to Windows Event Log
    try {
        $source = "SecureBootWatcher"
        if (-not [System.Diagnostics.EventLog]::SourceExists($source)) {
            New-EventLog -LogName Application -Source $source
        }
        
        $eventType = switch ($Level) {
            "Error" { "Error" }
            "Warning" { "Warning" }
            default { "Information" }
        }
        
        Write-EventLog -LogName Application -Source $source -EventId 1000 -EntryType $eventType -Message $Message
    }
    catch {
        # Silently continue if event log write fails
    }
}

$remediationAttempted = $false
$remediationSuccessful = $false

Write-RemediationLog "Starting SecureBootWatcher client remediation"

# Remediation 1: Ensure logs directory exists
$logsPath = Join-Path $InstallPath "logs"
if (-not (Test-Path $logsPath)) {
    Write-RemediationLog "Creating logs directory: $logsPath"
    try {
        New-Item -ItemType Directory -Path $logsPath -Force | Out-Null
        Write-RemediationLog "Logs directory created" "Success"
        $remediationAttempted = $true
    }
    catch {
        Write-RemediationLog "Failed to create logs directory: $_" "Error"
    }
}

# Remediation 2: Fix scheduled task if missing or broken
$scheduledTask = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue

if (-not $scheduledTask) {
    Write-RemediationLog "Scheduled task not found, attempting to recreate"
    $remediationAttempted = $true
    
    try {
        $exePath = Join-Path $InstallPath "SecureBootWatcher.Client.exe"
        
        if (-not (Test-Path $exePath)) {
            Write-RemediationLog "Cannot create scheduled task: executable not found at $exePath" "Error"
        }
        else {
            # Create action
            $action = New-ScheduledTaskAction -Execute $exePath -WorkingDirectory $InstallPath
            
            # Create trigger based on schedule type
            $taskDateTime = [DateTime]::Parse($TaskTime)
            $randomDelay = New-TimeSpan -Minutes (Get-Random -Minimum 0 -Maximum 60)
            
            $trigger = switch ($ScheduleType) {
                "Once" {
                    New-ScheduledTaskTrigger -Once -At $taskDateTime -RandomDelay $randomDelay
                }
                "Daily" {
                    New-ScheduledTaskTrigger -Daily -At $taskDateTime -RandomDelay $randomDelay
                }
                "Hourly" {
                    New-ScheduledTaskTrigger -Once -At $taskDateTime `
                        -RepetitionInterval (New-TimeSpan -Hours 1) `
                        -RepetitionDuration (New-TimeSpan -Days 3650) `
                        -RandomDelay $randomDelay
                }
                "Custom" {
                    New-ScheduledTaskTrigger -Once -At $taskDateTime `
                        -RepetitionInterval (New-TimeSpan -Hours $RepeatEveryHours) `
                        -RepetitionDuration (New-TimeSpan -Days 3650) `
                        -RandomDelay $randomDelay
                }
            }
            
            # Create principal and settings
            $principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest
            $settings = New-ScheduledTaskSettingsSet `
                -AllowStartIfOnBatteries `
                -DontStopIfGoingOnBatteries `
                -StartWhenAvailable `
                -MultipleInstances IgnoreNew
            
            # Register task
            Register-ScheduledTask `
                -TaskName $TaskName `
                -Action $action `
                -Trigger $trigger `
                -Principal $principal `
                -Settings $settings `
                -Description "Monitors Secure Boot certificate status and reports to dashboard" | Out-Null
            
            Write-RemediationLog "Scheduled task recreated successfully" "Success"
            $remediationSuccessful = $true
        }
    }
    catch {
        Write-RemediationLog "Failed to recreate scheduled task: $_" "Error"
    }
}
elseif ($scheduledTask.State -eq "Disabled") {
    Write-RemediationLog "Scheduled task is disabled, attempting to enable"
    $remediationAttempted = $true
    
    try {
        Enable-ScheduledTask -TaskName $TaskName | Out-Null
        Write-RemediationLog "Scheduled task enabled" "Success"
        $remediationSuccessful = $true
    }
    catch {
        Write-RemediationLog "Failed to enable scheduled task: $_" "Error"
    }
}

# Remediation 3: Fix file permissions if needed
try {
    $exePath = Join-Path $InstallPath "SecureBootWatcher.Client.exe"
    
    if (Test-Path $exePath) {
        # Ensure SYSTEM has read/execute permissions
        $acl = Get-Acl $exePath
        $systemAccount = New-Object System.Security.Principal.SecurityIdentifier("S-1-5-18")
        $systemRule = New-Object System.Security.AccessControl.FileSystemAccessRule(
            $systemAccount,
            "ReadAndExecute",
            "Allow"
        )
        
        $needsUpdate = $true
        foreach ($rule in $acl.Access) {
            if ($rule.IdentityReference.Value -eq "NT AUTHORITY\SYSTEM" -and 
                $rule.FileSystemRights -match "ReadAndExecute") {
                $needsUpdate = $false
                break
            }
        }
        
        if ($needsUpdate) {
            Write-RemediationLog "Fixing file permissions for SYSTEM account"
            $remediationAttempted = $true
            
            $acl.AddAccessRule($systemRule)
            Set-Acl -Path $exePath -AclObject $acl
            
            Write-RemediationLog "File permissions updated" "Success"
            $remediationSuccessful = $true
        }
    }
}
catch {
    Write-RemediationLog "Failed to update file permissions: $_" "Error"
}

# Remediation 4: Clear potential lock files or temporary data
$tempDataPath = Join-Path $InstallPath "temp"
if (Test-Path $tempDataPath) {
    Write-RemediationLog "Clearing temporary data"
    $remediationAttempted = $true
    
    try {
        Remove-Item -Path $tempDataPath -Recurse -Force
        Write-RemediationLog "Temporary data cleared" "Success"
        $remediationSuccessful = $true
    }
    catch {
        Write-RemediationLog "Failed to clear temporary data: $_" "Warning"
    }
}

# Summary
Write-RemediationLog "----------------------------------------"
if ($remediationAttempted) {
    if ($remediationSuccessful) {
        Write-RemediationLog "REMEDIATION COMPLETED - Client issues have been fixed" "Success"
        exit 0
    }
    else {
        Write-RemediationLog "REMEDIATION ATTEMPTED - Some issues could not be automatically fixed" "Warning"
        exit 1
    }
}
else {
    Write-RemediationLog "NO REMEDIATION NEEDED - Client is functioning correctly"
    exit 0
}
