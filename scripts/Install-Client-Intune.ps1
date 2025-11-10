# SecureBootWatcher Client Install Script for Intune Win32 App
# This script installs the SecureBootWatcher client from the package
# Exit code 0 = success, non-zero = failure

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$ApiBaseUrl = "",
    
    [Parameter(Mandatory = $false)]
    [string]$FleetId = "",
    
    [Parameter(Mandatory = $false)]
    [string]$CertificatePassword = "",
    
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
$installPath = "C:\Program Files\SecureBootWatcher"
$taskName = "SecureBootWatcher"
$logPath = Join-Path $env:ProgramData "SecureBootWatcher\install.log"
$certificateFileName = "SecureBootWatcher.pfx"  # Expected certificate file name in package

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
Write-InstallLog "Schedule configuration: Type=$ScheduleType, Time=$TaskTime, RandomDelay=$RandomDelayMinutes min"

try {
    # Step 1: Import certificate if present
    $certificatePath = Join-Path $scriptDir $certificateFileName
    if (Test-Path $certificatePath) {
        Write-InstallLog "Certificate found, importing to LocalMachine\My store"
        
        try {
            # Convert password to SecureString
            $securePassword = if (-not [string]::IsNullOrEmpty($CertificatePassword)) {
                ConvertTo-SecureString -String $CertificatePassword -AsPlainText -Force
            } else {
                ConvertTo-SecureString -String "" -AsPlainText -Force  # Empty password
            }
            
            # Import certificate to LocalMachine\My store
            $cert = Import-PfxCertificate `
                -FilePath $certificatePath `
                -CertStoreLocation Cert:\LocalMachine\My `
                -Password $securePassword `
                -Exportable
            
            Write-InstallLog "Certificate imported successfully"
            Write-InstallLog "  Thumbprint: $($cert.Thumbprint)"
            Write-InstallLog "  Subject: $($cert.Subject)"
            Write-InstallLog "  Expiration: $($cert.NotAfter)"
            
            # Grant SYSTEM account read permissions on private key
            Write-InstallLog "Granting SYSTEM account permissions on private key"
            
            try {
                $rsaCert = [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($cert)
                $keyName = $rsaCert.Key.UniqueName
                $keyPath = "C:\ProgramData\Microsoft\Crypto\Keys\$keyName"
                
                if (-not (Test-Path $keyPath)) {
                    # Try RSA folder
                    $keyPath = "C:\ProgramData\Microsoft\Crypto\RSA\MachineKeys\$keyName"
                }
                
                if (Test-Path $keyPath) {
                    # Grant Read permission to SYSTEM account
                    $result = icacls $keyPath /grant "SYSTEM:(R)"
                    
                    if ($LASTEXITCODE -eq 0) {
                        Write-InstallLog "  Permissions granted to SYSTEM account"
                    } else {
                        Write-InstallLog "  WARNING: Failed to grant permissions (exit code: $LASTEXITCODE)"
                    }
                } else {
                    Write-InstallLog "  WARNING: Private key file not found at expected location"
                }
            }
            catch {
                Write-InstallLog "  WARNING: Could not set private key permissions: $_"
            }
        }
        catch {
            Write-InstallLog "WARNING: Certificate import failed: $_"
            Write-InstallLog "Continuing installation (certificate may already be installed)"
        }
    } else {
        Write-InstallLog "No certificate file found in package (optional)"
    }

    # Step 2: Create installation directory
    Write-InstallLog "Creating installation directory"
    if (-not (Test-Path $installPath)) {
        New-Item -ItemType Directory -Path $installPath -Force | Out-Null
    }

    # Step 3: Extract client package and copy files
    Write-InstallLog "Extracting and copying client files"
    
    # Look for the client package ZIP file
    $packageZipName = "SecureBootWatcher-Client.zip"
    $packageZipPath = Join-Path $scriptDir $packageZipName
    
    if (-not (Test-Path $packageZipPath)) {
        Write-InstallLog "ERROR: Client package not found: $packageZipPath"
        Write-InstallLog "Expected file: $packageZipName"
        Write-InstallLog "Files in package directory:"
        Get-ChildItem -Path $scriptDir -File | ForEach-Object {
            Write-InstallLog "  - $($_.Name)"
        }
        throw "Client package ZIP file not found in package directory"
    }
    
    Write-InstallLog "Found client package: $packageZipName ($([math]::Round((Get-Item $packageZipPath).Length / 1MB, 2)) MB)"
    
    # Extract ZIP to a temporary directory
    $tempExtractPath = Join-Path $env:TEMP "SecureBootWatcher-Install-$(Get-Date -Format 'yyyyMMddHHmmss')"
    
    try {
        Write-InstallLog "Extracting package to temporary directory: $tempExtractPath"
        New-Item -ItemType Directory -Path $tempExtractPath -Force | Out-Null
        Expand-Archive -Path $packageZipPath -DestinationPath $tempExtractPath -Force
        
        # List extracted files for diagnostics
        $extractedFiles = Get-ChildItem -Path $tempExtractPath -File
        Write-InstallLog "Extracted $($extractedFiles.Count) files from package"
        
        if ($extractedFiles.Count -eq 0) {
            throw "ZIP extraction resulted in no files"
        }
        
        # Copy all files from temp directory to installation directory
        $copiedCount = 0
        foreach ($file in $extractedFiles) {
            try {
                Copy-Item -Path $file.FullName -Destination $installPath -Force
                Write-InstallLog "  Copied: $($file.Name) ($([math]::Round($file.Length / 1KB, 2)) KB)"
                $copiedCount++
            }
            catch {
                Write-InstallLog "  ERROR copying $($file.Name): $_"
                throw
            }
        }
        
        Write-InstallLog "Successfully copied $copiedCount files"
        
        # Verify critical files exist in install directory
        $criticalFiles = @(
            "SecureBootWatcher.Client.exe",
            "appsettings.json"
        )
        
        $missingFiles = @()
        foreach ($file in $criticalFiles) {
            $filePath = Join-Path $installPath $file
            if (-not (Test-Path $filePath)) {
                $missingFiles += $file
                Write-InstallLog "  ERROR: Critical file missing: $file"
            } else {
                Write-InstallLog "  Verified: $file"
            }
        }
        
        if ($missingFiles.Count -gt 0) {
            Write-InstallLog "ERROR: Installation incomplete - missing critical files:"
            $missingFiles | ForEach-Object { Write-InstallLog "  - $_" }
            throw "Critical files missing from installation directory"
        }
    }
    finally {
        # Clean up temporary extraction directory
        if (Test-Path $tempExtractPath) {
            Write-InstallLog "Cleaning up temporary extraction directory"
            Remove-Item -Path $tempExtractPath -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    # Step 4: Configure appsettings.json if parameters provided
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

    # Step 5: Create scheduled task
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

    # Parse task time
    try {
        $taskDateTime = [DateTime]::Parse($TaskTime)
    }
    catch {
        Write-InstallLog "WARNING: Invalid TaskTime '$TaskTime', using 09:00AM"
        $taskDateTime = [DateTime]::Parse("09:00AM")
    }

    # Create random delay TimeSpan (this is the MAXIMUM delay, not a specific random value)
    # Task Scheduler will apply a random delay between 0 and this value automatically
    $randomDelayTimeSpan = New-TimeSpan -Minutes $RandomDelayMinutes
    
    # Create task trigger based on schedule type
    $trigger = $null
    $scheduleDescription = ""
    
    # Task Scheduler maximum duration is 31 days (P31D)
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
            # Create a trigger that repeats every hour
            # NOTE: RandomDelay is not supported with RepetitionInterval, so we omit it
            $trigger = New-ScheduledTaskTrigger -Once -At $taskDateTime -RepetitionInterval (New-TimeSpan -Hours 1) -RepetitionDuration $maxRepetitionDuration
            $scheduleDescription = "Every hour starting at $TaskTime"
            Write-InstallLog "  Note: RandomDelay not supported for Hourly schedule"
        }
        "Custom" {
            # Create a trigger that repeats every N hours
            # NOTE: RandomDelay is not supported with RepetitionInterval, so we omit it
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
    Write-InstallLog "  Schedule: $scheduleDescription"
    Write-InstallLog "  Random delay: 0-$RandomDelayMinutes minutes"
    Write-InstallLog "  Task name: $taskName"
    Write-InstallLog "  Run as: SYSTEM"

    Write-InstallLog "Installation completed successfully"
    exit 0
}
catch {
    Write-InstallLog "ERROR: Installation failed - $_"
    exit 1
}
