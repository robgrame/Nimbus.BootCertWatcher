# SecureBootWatcher Client Detection Script for Intune Win32 App
# This script must exit with code 0 if the app is installed, non-zero otherwise
# No parameters allowed - Intune Win32 detection scripts cannot accept parameters

$ErrorActionPreference = "SilentlyContinue"

# Define installation path (hardcoded - no parameters in Intune detection)
$installPath = "C:\Program Files\SecureBootWatcher"
$exePath = Join-Path $installPath "SecureBootWatcher.Client.exe"
$configPath = Join-Path $installPath "appsettings.json"
$taskName = "SecureBootWatcher"

# Detection logic - all checks must pass
$detectionPassed = $true

# Check 1: Installation directory exists
if (-not (Test-Path $installPath)) {
    $detectionPassed = $false
}

# Check 2: Main executable exists
if (-not (Test-Path $exePath)) {
    $detectionPassed = $false
}

# Check 3: Configuration file exists
if (-not (Test-Path $configPath)) {
    $detectionPassed = $false
}

# Check 4: Scheduled task exists
$scheduledTask = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
if (-not $scheduledTask) {
    $detectionPassed = $false
}

# Return result
if ($detectionPassed) {
    # Intune expects "Installed" output for successful detection
    Write-Output "Installed"
    exit 0
} else {
    # No output or any output other than success indicates not installed
    exit 1
}
