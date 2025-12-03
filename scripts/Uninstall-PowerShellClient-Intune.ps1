<#
.SYNOPSIS
    Uninstall SecureBootWatcher PowerShell Client for Intune Win32 App

.DESCRIPTION
    This script removes the SecureBootWatcher PowerShell client from Windows devices
    including the scheduled task and installation files.

.NOTES
    Exit code 0 = success, non-zero = failure
#>

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

# Define paths
$installPath = "C:\Program Files\SecureBootWatcher\PowerShell"
$taskName = "SecureBootWatcher-PowerShell"
$logPath = Join-Path $env:ProgramData "SecureBootWatcher\uninstall-powershell.log"

# Create log directory
$logDir = Split-Path $logPath -Parent
if (-not (Test-Path $logDir)) {
    New-Item -ItemType Directory -Path $logDir -Force | Out-Null
}

# Log function
function Write-UninstallLog {
    param([string]$Message)
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    "$timestamp - $Message" | Out-File -FilePath $logPath -Append
    Write-Host $Message
}

Write-UninstallLog "Starting SecureBootWatcher PowerShell Client uninstallation"

try {
    # Step 1: Remove scheduled task
    Write-UninstallLog "Removing scheduled task"
    $existingTask = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
    if ($existingTask) {
        Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
        Write-UninstallLog "Scheduled task removed successfully"
    }
    else {
        Write-UninstallLog "Scheduled task not found (already removed or never created)"
    }

    # Step 2: Remove installation directory
    Write-UninstallLog "Removing installation directory"
    if (Test-Path $installPath) {
        Remove-Item -Path $installPath -Recurse -Force
        Write-UninstallLog "Installation directory removed: $installPath"
    }
    else {
        Write-UninstallLog "Installation directory not found (already removed)"
    }

    # Step 3: Clean up parent directory if empty
    $parentPath = Split-Path $installPath -Parent
    if (Test-Path $parentPath) {
        $items = Get-ChildItem -Path $parentPath -ErrorAction SilentlyContinue
        if (-not $items -or $items.Count -eq 0) {
            Remove-Item -Path $parentPath -Force
            Write-UninstallLog "Removed empty parent directory: $parentPath"
        }
    }

    Write-UninstallLog "Uninstallation completed successfully"
    exit 0
}
catch {
    Write-UninstallLog "ERROR: Uninstallation failed - $_"
    Write-UninstallLog "Stack trace: $($_.ScriptStackTrace)"
    exit 1
}
