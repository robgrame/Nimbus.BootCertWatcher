# Secure Boot Watcher Client Uninstall Script
# This script removes the SecureBootWatcher client and scheduled task from Windows devices

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$InstallPath = "C:\Program Files\SecureBootWatcher",
    
    [Parameter(Mandatory = $false)]
    [switch]$Force,
    
    [Parameter(Mandatory = $false)]
    [switch]$KeepLogs
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  SecureBootWatcher Client Uninstall" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if running as Administrator
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Host "??  Warning: This script should be run as Administrator" -ForegroundColor Yellow
    if (-not $Force) {
        $continue = Read-Host "Continue anyway? (y/n)"
        if ($continue -ne 'y') {
            exit 0
        }
    }
}
Write-Host ""

# Step 1: Remove Scheduled Task
Write-Host "[1/3] Removing scheduled task..." -ForegroundColor Yellow

try {
    $task = Get-ScheduledTask -TaskName "SecureBootWatcher" -ErrorAction SilentlyContinue
    
    if ($task) {
        # Stop task if running
        $taskInfo = Get-ScheduledTaskInfo -TaskName "SecureBootWatcher"
        if ($taskInfo.LastTaskResult -eq 267009) { # Task is running
            Write-Host "  ??  Stopping running task..." -ForegroundColor Gray
            Stop-ScheduledTask -TaskName "SecureBootWatcher" -ErrorAction SilentlyContinue
            Start-Sleep -Seconds 2
        }
        
        # Remove task
        Unregister-ScheduledTask -TaskName "SecureBootWatcher" -Confirm:$false
        Write-Host "  ? Scheduled task removed" -ForegroundColor Green
    } else {
        Write-Host "  ??  Scheduled task not found (already removed or never created)" -ForegroundColor Gray
    }
}
catch {
    Write-Host "  ??  Could not remove scheduled task: $_" -ForegroundColor Yellow
}
Write-Host ""

# Step 2: Stop any running processes
Write-Host "[2/3] Stopping client processes..." -ForegroundColor Yellow

try {
    $processes = Get-Process -Name "SecureBootWatcher.Client" -ErrorAction SilentlyContinue
    
    if ($processes) {
        Write-Host "  Found $($processes.Count) running process(es)" -ForegroundColor Gray
        
        foreach ($proc in $processes) {
            try {
                Stop-Process -Id $proc.Id -Force -ErrorAction Stop
                Write-Host "  ? Stopped process (PID: $($proc.Id))" -ForegroundColor Green
            }
            catch {
                Write-Host "  ??  Could not stop process (PID: $($proc.Id)): $_" -ForegroundColor Yellow
            }
        }
        
        # Wait for processes to terminate
        Start-Sleep -Seconds 2
    } else {
        Write-Host "  ??  No running client processes found" -ForegroundColor Gray
    }
}
catch {
    Write-Host "  ??  Error checking for running processes: $_" -ForegroundColor Yellow
}
Write-Host ""

# Step 3: Remove Installation Directory
Write-Host "[3/3] Removing installation directory..." -ForegroundColor Yellow

if (Test-Path $InstallPath) {
    try {
        # Check for logs
        $logsPath = Join-Path $InstallPath "logs"
        $hasLogs = Test-Path $logsPath
        
        if ($hasLogs -and $KeepLogs) {
            # Backup logs before removal
            $backupPath = Join-Path $env:TEMP "SecureBootWatcher-Logs-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
            Write-Host "  ?? Backing up logs to: $backupPath" -ForegroundColor Cyan
            
            New-Item -ItemType Directory -Path $backupPath -Force | Out-Null
            Copy-Item -Path "$logsPath\*" -Destination $backupPath -Recurse -Force
            
            Write-Host "  ? Logs backed up" -ForegroundColor Green
        }
        
        # Remove directory
        $itemCount = (Get-ChildItem -Path $InstallPath -Recurse -File).Count
        Write-Host "  ???  Removing $itemCount file(s)..." -ForegroundColor Gray
        
        Remove-Item -Path $InstallPath -Recurse -Force
        
        Write-Host "  ? Installation directory removed" -ForegroundColor Green
        Write-Host "     Path: $InstallPath" -ForegroundColor Gray
        
        if ($hasLogs -and $KeepLogs) {
            Write-Host "     Logs saved to: $backupPath" -ForegroundColor Gray
        }
    }
    catch {
        Write-Host "  ? Could not remove installation directory: $_" -ForegroundColor Red
        Write-Host ""
        Write-Host "  Try manually:" -ForegroundColor Yellow
        Write-Host "    Remove-Item -Path '$InstallPath' -Recurse -Force" -ForegroundColor Cyan
        exit 1
    }
} else {
    Write-Host "  ??  Installation directory not found (already removed)" -ForegroundColor Gray
    Write-Host "     Path: $InstallPath" -ForegroundColor Gray
}
Write-Host ""

# Verify removal
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Uninstall Complete" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "? SecureBootWatcher client has been removed" -ForegroundColor Green
Write-Host ""

# Check for any remaining artifacts
$artifacts = @()

$taskCheck = Get-ScheduledTask -TaskName "SecureBootWatcher" -ErrorAction SilentlyContinue
if ($taskCheck) {
    $artifacts += "Scheduled task still exists"
}

$procCheck = Get-Process -Name "SecureBootWatcher.Client" -ErrorAction SilentlyContinue
if ($procCheck) {
    $artifacts += "Client process still running"
}

$dirCheck = Test-Path $InstallPath
if ($dirCheck) {
    $artifacts += "Installation directory still exists"
}

if ($artifacts.Count -gt 0) {
    Write-Host "??  Warning: Some artifacts may still remain:" -ForegroundColor Yellow
    foreach ($artifact in $artifacts) {
        Write-Host "  - $artifact" -ForegroundColor Gray
    }
    Write-Host ""
    Write-Host "You may need to manually remove these items or reboot the system." -ForegroundColor Yellow
} else {
    Write-Host "? All components successfully removed" -ForegroundColor Green
}

Write-Host ""
Write-Host "Note: Client configuration and reports sent to the API remain intact." -ForegroundColor Gray
Write-Host "Device records can be managed from the dashboard web interface." -ForegroundColor Gray
Write-Host ""
