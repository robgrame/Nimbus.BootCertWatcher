# SecureBootWatcher Client Detection Script
# Used for Microsoft Endpoint Manager (Intune) or SCCM detection
# Returns exit code 0 if client is properly installed, 1 if not

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$InstallPath = "C:\Program Files\SecureBootWatcher",
    
    [Parameter(Mandatory = $false)]
    [string]$TaskName = "SecureBootWatcher",
    
    [Parameter(Mandatory = $false)]
    [string]$MinimumVersion = "1.0.0.0",
    
    [Parameter(Mandatory = $false)]
    [switch]$Verbose,
    
    [Parameter(Mandatory = $false)]
    [switch]$CheckTaskEnabled
)

$ErrorActionPreference = "SilentlyContinue"

# Detection result
$detectionPassed = $true
$detectionMessages = @()

function Write-DetectionLog {
    param([string]$Message, [string]$Level = "Info")
    
    if ($Verbose) {
        switch ($Level) {
            "Error" { Write-Host "[ERROR] $Message" -ForegroundColor Red }
            "Warning" { Write-Host "[WARN] $Message" -ForegroundColor Yellow }
            "Success" { Write-Host "[OK] $Message" -ForegroundColor Green }
            default { Write-Host "[INFO] $Message" -ForegroundColor Cyan }
        }
    }
    
    $detectionMessages += $Message
}

# Check 1: Installation directory exists
Write-DetectionLog "Checking installation directory: $InstallPath"
if (-not (Test-Path $InstallPath)) {
    Write-DetectionLog "Installation directory not found" "Error"
    $detectionPassed = $false
} else {
    Write-DetectionLog "Installation directory exists" "Success"
}

# Check 2: Main executable exists
$exePath = Join-Path $InstallPath "SecureBootWatcher.Client.exe"
Write-DetectionLog "Checking executable: $exePath"
if (-not (Test-Path $exePath)) {
    Write-DetectionLog "Executable not found" "Error"
    $detectionPassed = $false
} else {
    Write-DetectionLog "Executable found" "Success"
    
    # Check executable version
    try {
        $fileVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($exePath)
        $currentVersion = $fileVersion.FileVersion
        Write-DetectionLog "Current version: $currentVersion"
        
        if ($MinimumVersion -ne "1.0.0.0") {
            $minVer = [Version]$MinimumVersion
            $curVer = [Version]$currentVersion
            
            if ($curVer -lt $minVer) {
                Write-DetectionLog "Version $currentVersion is older than minimum required $MinimumVersion" "Error"
                $detectionPassed = $false
            } else {
                Write-DetectionLog "Version check passed" "Success"
            }
        }
    }
    catch {
        Write-DetectionLog "Could not read executable version: $_" "Warning"
    }
}

# Check 3: Required DLLs exist
$requiredFiles = @(
    "SecureBootWatcher.Shared.dll",
    "Microsoft.Extensions.Configuration.dll",
    "Microsoft.Extensions.DependencyInjection.dll",
    "Microsoft.Extensions.Logging.dll",
    "Serilog.dll"
)

Write-DetectionLog "Checking required dependencies..."
$missingFiles = @()
foreach ($file in $requiredFiles) {
    $filePath = Join-Path $InstallPath $file
    if (-not (Test-Path $filePath)) {
        $missingFiles += $file
    }
}

if ($missingFiles.Count -gt 0) {
    Write-DetectionLog "Missing dependencies: $($missingFiles -join ', ')" "Error"
    $detectionPassed = $false
} else {
    Write-DetectionLog "All required dependencies found" "Success"
}

# Check 4: Configuration file exists
$configPath = Join-Path $InstallPath "appsettings.json"
Write-DetectionLog "Checking configuration: $configPath"
if (-not (Test-Path $configPath)) {
    Write-DetectionLog "Configuration file not found" "Error"
    $detectionPassed = $false
} else {
    Write-DetectionLog "Configuration file exists" "Success"
    
    # Validate configuration is valid JSON
    try {
        $config = Get-Content $configPath -Raw | ConvertFrom-Json
        
        # Check critical configuration sections
        if (-not $config.SecureBootWatcher) {
            Write-DetectionLog "Configuration missing SecureBootWatcher section" "Warning"
        }
        
        if (-not $config.SecureBootWatcher.Sinks) {
            Write-DetectionLog "Configuration missing Sinks section" "Warning"
        } else {
            # Check if at least one sink is enabled
            $webApiEnabled = $config.SecureBootWatcher.Sinks.EnableWebApi -eq $true
            $azureQueueEnabled = $config.SecureBootWatcher.Sinks.EnableAzureQueue -eq $true
            $fileShareEnabled = $config.SecureBootWatcher.Sinks.EnableFileShare -eq $true
            
            if (-not ($webApiEnabled -or $azureQueueEnabled -or $fileShareEnabled)) {
                Write-DetectionLog "No sinks are enabled in configuration" "Warning"
            } else {
                Write-DetectionLog "Configuration has at least one sink enabled" "Success"
            }
        }
    }
    catch {
        Write-DetectionLog "Configuration file is not valid JSON: $_" "Error"
        $detectionPassed = $false
    }
}

# Check 5: Scheduled task exists
Write-DetectionLog "Checking scheduled task: $TaskName"
$scheduledTask = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue

if (-not $scheduledTask) {
    Write-DetectionLog "Scheduled task not found" "Error"
    $detectionPassed = $false
} else {
    Write-DetectionLog "Scheduled task exists" "Success"
    
    # Check task state
    $taskState = $scheduledTask.State
    Write-DetectionLog "Task state: $taskState"
    
    if ($CheckTaskEnabled) {
        if ($taskState -eq "Disabled") {
            Write-DetectionLog "Scheduled task is disabled" "Error"
            $detectionPassed = $false
        } else {
            Write-DetectionLog "Scheduled task is enabled" "Success"
        }
    }
    
    # Verify task action points to correct executable
    $taskAction = $scheduledTask.Actions | Select-Object -First 1
    if ($taskAction) {
        $taskExePath = $taskAction.Execute
        if ($taskExePath -ne $exePath) {
            Write-DetectionLog "Task executable path mismatch: Expected '$exePath', Found '$taskExePath'" "Warning"
        } else {
            Write-DetectionLog "Task executable path is correct" "Success"
        }
    }
    
    # Check task principal (should run as SYSTEM)
    $taskPrincipal = $scheduledTask.Principal
    if ($taskPrincipal.UserId -ne "SYSTEM" -and $taskPrincipal.UserId -ne "NT AUTHORITY\SYSTEM") {
        Write-DetectionLog "Task does not run as SYSTEM (runs as: $($taskPrincipal.UserId))" "Warning"
    } else {
        Write-DetectionLog "Task runs as SYSTEM" "Success"
    }
}

# Check 6: Logs directory exists (optional, not critical)
$logsPath = Join-Path $InstallPath "logs"
if (Test-Path $logsPath) {
    Write-DetectionLog "Logs directory exists" "Success"
    
    # Check for recent log files (last 7 days)
    $recentLogs = Get-ChildItem -Path $logsPath -Filter "*.log" -File |
        Where-Object { $_.LastWriteTime -gt (Get-Date).AddDays(-7) }
    
    if ($recentLogs) {
        Write-DetectionLog "Found $($recentLogs.Count) recent log file(s)" "Success"
    } else {
        Write-DetectionLog "No recent log files found (last 7 days)" "Warning"
    }
} else {
    Write-DetectionLog "Logs directory not found (will be created on first run)" "Warning"
}

# Summary
Write-DetectionLog "----------------------------------------"
if ($detectionPassed) {
    Write-DetectionLog "DETECTION PASSED - Client is properly installed" "Success"
    
    if ($Verbose) {
        Write-Host ""
        Write-Host "Installation Details:" -ForegroundColor Cyan
        Write-Host "  Location: $InstallPath" -ForegroundColor Gray
        Write-Host "  Executable: $exePath" -ForegroundColor Gray
        Write-Host "  Scheduled Task: $TaskName" -ForegroundColor Gray
        if ($scheduledTask) {
            Write-Host "  Task State: $($scheduledTask.State)" -ForegroundColor Gray
            Write-Host "  Next Run: $((Get-ScheduledTaskInfo -TaskName $TaskName).NextRunTime)" -ForegroundColor Gray
        }
    }
    
    # Exit 0 = Detection successful (installed)
    exit 0
} else {
    Write-DetectionLog "DETECTION FAILED - Client installation incomplete or misconfigured" "Error"
    
    if ($Verbose) {
        Write-Host ""
        Write-Host "Issues Found:" -ForegroundColor Red
        foreach ($msg in $detectionMessages | Where-Object { $_ -like "*not found*" -or $_ -like "*missing*" -or $_ -like "*failed*" }) {
            Write-Host "  - $msg" -ForegroundColor Yellow
        }
    }
    
    # Exit 1 = Detection failed (not installed or broken)
    exit 1
}
