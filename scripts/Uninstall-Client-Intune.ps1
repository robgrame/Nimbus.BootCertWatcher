# SecureBootWatcher Client Uninstall Script for Intune Win32 App
# This script uninstalls the SecureBootWatcher client
# Exit code 0 = success, non-zero = failure

$ErrorActionPreference = "Stop"

# Define paths (hardcoded - no parameters)
$installPath = "C:\Program Files\SecureBootWatcher"
$taskName = "SecureBootWatcher"
$logPath = Join-Path $env:ProgramData "SecureBootWatcher\uninstall.log"

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
}

Write-UninstallLog "Starting SecureBootWatcher uninstall"

try {
    # Step 1: Remove scheduled task
    Write-UninstallLog "Removing scheduled task: $taskName"
    $task = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
    if ($task) {
        Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
        Write-UninstallLog "Scheduled task removed"
    } else {
        Write-UninstallLog "Scheduled task not found - skipping"
    }

    # Step 2: Stop any running processes
    Write-UninstallLog "Stopping running processes"
    $processes = Get-Process -Name "SecureBootWatcher.Client" -ErrorAction SilentlyContinue
    if ($processes) {
        $processes | Stop-Process -Force
        Write-UninstallLog "Stopped $($processes.Count) running process(es)"
    }

    # Wait a moment for processes to fully terminate
    Start-Sleep -Seconds 2

    # Step 3: Remove installation directory
    Write-UninstallLog "Removing installation directory: $installPath"
    if (Test-Path $installPath) {
        Remove-Item -Path $installPath -Recurse -Force
        Write-UninstallLog "Installation directory removed"
    } else {
        Write-UninstallLog "Installation directory not found - skipping"
    }

    Write-UninstallLog "Uninstall completed successfully"
    exit 0
}
catch {
    Write-UninstallLog "ERROR: Uninstall failed - $_"
    exit 1
}
