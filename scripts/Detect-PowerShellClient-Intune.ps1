<#
.SYNOPSIS
    Detection script for SecureBootWatcher PowerShell Client (Intune Win32 App)

.DESCRIPTION
    This script detects if the SecureBootWatcher PowerShell client is installed correctly.
    Returns exit code 0 if installed, non-zero otherwise.
    
    No parameters allowed - Intune Win32 detection scripts cannot accept parameters.

.NOTES
    Exit code 0 = installed (detection successful)
    Exit code 1 = not installed (detection failed)
#>

$ErrorActionPreference = "SilentlyContinue"

# Define installation paths (hardcoded - no parameters in Intune detection)
$installPath = "C:\Program Files\SecureBootWatcher\PowerShell"
$scriptPath = Join-Path $installPath "SecureBootWatcher-Client.ps1"
$configPath = Join-Path $installPath "appsettings.json"
$taskName = "SecureBootWatcher-PowerShell"

# Detection logic - all checks must pass
$detectionPassed = $true

# Check 1: Installation directory exists
if (-not (Test-Path $installPath)) {
    $detectionPassed = $false
}

# Check 2: Main PowerShell script exists
if (-not (Test-Path $scriptPath)) {
    $detectionPassed = $false
}

# Check 3: Configuration file exists
if (-not (Test-Path $configPath)) {
    $detectionPassed = $false
}

# Check 4: Scheduled task exists and is enabled
$scheduledTask = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
if (-not $scheduledTask) {
    $detectionPassed = $false
}
elseif ($scheduledTask.State -eq 'Disabled') {
    $detectionPassed = $false
}

# Return result
if ($detectionPassed) {
    # Intune expects "Installed" output for successful detection
    Write-Output "Installed"
    exit 0
}
else {
    # No output or any output other than success indicates not installed
    exit 1
}
