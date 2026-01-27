<#
.SYNOPSIS
    SecureBootWatcher PowerShell Client - Monitors Secure Boot certificate status and reports to dashboard

.DESCRIPTION
    This PowerShell script collects Secure Boot certificate status, registry snapshots, and event logs
    from Windows devices and transmits reports to a centralized dashboard. It provides the same
    inventory features as the .NET Framework client but is easier to deploy via device management
    solutions like Intune or SCCM.

.PARAMETER ConfigPath
    Path to the configuration JSON file. Default: appsettings.json in script directory

.PARAMETER RunMode
    Execution mode: 'Once' for single-shot or 'Continuous' for long-running service mode
    Default: Once

.EXAMPLE
    .\SecureBootWatcher-Client.ps1
    Runs once with default configuration

.EXAMPLE
    .\SecureBootWatcher-Client.ps1 -RunMode Continuous
    Runs in continuous mode, polling at configured intervals

.NOTES
    Version: 1.0.0
    Author: Nimbus SecureBootWatcher Team
    Requires: PowerShell 5.0+, Windows 10/11 with UEFI Secure Boot
    
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$ConfigPath,
    
    [Parameter(Mandatory = $false)]
    [ValidateSet('Once', 'Continuous')]
    [string]$RunMode = 'Once'
)

#Requires -Version 5.0
#Requires -RunAsAdministrator

# Set strict mode and error handling
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Script version
$script:ClientVersion = '1.14.0'

# Get script directory
$script:ScriptRoot = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }

#region Logging Functions

function Write-Log {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Message,
        
        [Parameter(Mandatory = $false)]
        [ValidateSet('Information', 'Warning', 'Error', 'Debug', 'Verbose')]
        [string]$Level = 'Information',
        
        [Parameter(Mandatory = $false)]
        [object]$Exception
    )
    
    $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss.fff zzz'
    $logMessage = "[$timestamp] [$Level] $Message"
    
    if ($Exception) {
        # Handle both Exception and ErrorRecord types
        if ($Exception -is [System.Management.Automation.ErrorRecord]) {
            $exceptionMessage = $Exception.Exception.Message
            $exceptionStack = $Exception.ScriptStackTrace
        }
        elseif ($Exception -is [System.Exception]) {
            $exceptionMessage = $Exception.Message
            $exceptionStack = $Exception.StackTrace
        }
        else {
            $exceptionMessage = $Exception.ToString()
            $exceptionStack = ""
        }
        
        $logMessage += "`n  Exception: $exceptionMessage"
        if ($exceptionStack) {
            $logMessage += "`n  StackTrace: $exceptionStack"
        }
    }
    
    # Write to console
    switch ($Level) {
        'Error' { Write-Host $logMessage -ForegroundColor Red }
        'Warning' { Write-Host $logMessage -ForegroundColor Yellow }
        'Debug' { if ($DebugPreference -ne 'SilentlyContinue') { Write-Host $logMessage -ForegroundColor Gray } }
        'Verbose' { if ($VerbosePreference -ne 'SilentlyContinue') { Write-Host $logMessage -ForegroundColor Cyan } }
        default { Write-Host $logMessage }
    }
    
    # Write to log file if configured (only if $script:Config has been initialized)
    if ((Test-Path variable:script:Config) -and $script:Config -and $script:Config.Logging -and $script:Config.Logging.File -and $script:Config.Logging.File.Enabled) {
        try {
            $logPath = $script:Config.Logging.File.Path
            if (-not [System.IO.Path]::IsPathRooted($logPath)) {
                $logPath = Join-Path $script:ScriptRoot $logPath
            }
            
            $logDir = Split-Path -Parent $logPath
            if (-not (Test-Path $logDir)) {
                New-Item -ItemType Directory -Path $logDir -Force | Out-Null
            }
            
            $logMessage | Out-File -FilePath $logPath -Append -Encoding UTF8
        }
        catch {
            # Silently ignore file logging errors to avoid recursion
            Write-Host "[Write-Log] Failed to write to log file: $_" -ForegroundColor DarkYellow
        }
    }
}

function Write-LogSection {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Title
    )
    
    Write-Log -Message "========================================"
    Write-Log -Message $Title
    Write-Log -Message "========================================"
}

#endregion

#region Configuration Functions

function Get-Configuration {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false)]
        [string]$Path
    )
    
    # Determine config file path
    if (-not $Path) {
        $Path = Join-Path $script:ScriptRoot 'appsettings.json'
    }
    
    if (-not (Test-Path $Path)) {
        throw "Configuration file not found: $Path"
    }
    
    Write-Log -Message "Loading configuration from: $Path"
    
    try {
        $configJson = Get-Content -Path $Path -Raw | ConvertFrom-Json
        
        # Apply defaults for missing values
        $config = @{
            SecureBootWatcher = @{
                FleetId = $configJson.SecureBootWatcher.FleetId ?? ''
                RunMode = $RunMode ?? $configJson.SecureBootWatcher.RunMode ?? 'Once'
                RegistryPollInterval = $configJson.SecureBootWatcher.RegistryPollInterval ?? '00:30:00'
                EventQueryInterval = $configJson.SecureBootWatcher.EventQueryInterval ?? '00:30:00'
                EventLookbackPeriod = $configJson.SecureBootWatcher.EventLookbackPeriod ?? '1.00:00:00'
                EventChannels = $configJson.SecureBootWatcher.EventChannels ?? @(
                    'Microsoft-Windows-SecureBoot-Servicing/Operational',
                    'Microsoft-Windows-SecureBoot-State/Operational',
                    'System'
                )
                Sinks = @{
                    ExecutionStrategy = $configJson.SecureBootWatcher.Sinks.ExecutionStrategy ?? 'FirstSuccess'
                    SinkPriority = $configJson.SecureBootWatcher.Sinks.SinkPriority ?? 'WebApi,AzureQueue,FileShare'
                    EnableFileShare = $configJson.SecureBootWatcher.Sinks.EnableFileShare ?? $false
                    EnableAzureQueue = $configJson.SecureBootWatcher.Sinks.EnableAzureQueue ?? $false
                    EnableWebApi = $configJson.SecureBootWatcher.Sinks.EnableWebApi ?? $true
                    FileShare = @{
                        RootPath = $configJson.SecureBootWatcher.Sinks.FileShare.RootPath ?? ''
                        FileExtension = $configJson.SecureBootWatcher.Sinks.FileShare.FileExtension ?? '.json'
                    }
                    AzureQueue = @{
                        QueueServiceUri = $configJson.SecureBootWatcher.Sinks.AzureQueue.QueueServiceUri ?? ''
                        QueueName = $configJson.SecureBootWatcher.Sinks.AzureQueue.QueueName ?? 'secureboot-reports'
                        AuthenticationMethod = $configJson.SecureBootWatcher.Sinks.AzureQueue.AuthenticationMethod ?? 'ManagedIdentity'
                    }
                    WebApi = @{
                        BaseAddress = $configJson.SecureBootWatcher.Sinks.WebApi.BaseAddress ?? ''
                        IngestionRoute = $configJson.SecureBootWatcher.Sinks.WebApi.IngestionRoute ?? '/api/SecureBootReports'
                        HttpTimeout = $configJson.SecureBootWatcher.Sinks.WebApi.HttpTimeout ?? '00:02:00'
                    }
                }
                Commands = @{
                    EnableCommandProcessing = $configJson.SecureBootWatcher.Commands.EnableCommandProcessing ?? $false
                    ProcessBeforeInventory = $configJson.SecureBootWatcher.Commands.ProcessBeforeInventory ?? $true
                    MaxCommandsPerCycle = $configJson.SecureBootWatcher.Commands.MaxCommandsPerCycle ?? 10
                    CommandExecutionDelay = $configJson.SecureBootWatcher.Commands.CommandExecutionDelay ?? '00:00:05'
                    ContinueOnCommandFailure = $configJson.SecureBootWatcher.Commands.ContinueOnCommandFailure ?? $true
                }
            }
            Logging = @{
                LogLevel = @{
                    Default = $configJson.Logging.LogLevel.Default ?? 'Information'
                }
                Console = @{
                    Enabled = $configJson.Logging.Console.Enabled ?? $true
                }
                File = @{
                    Enabled = $configJson.Logging.File.Enabled ?? $true
                    Path = $configJson.Logging.File.Path ?? 'logs/secureboot-watcher.log'
                    RollingInterval = $configJson.Logging.File.RollingInterval ?? 'Day'
                    RetainedFileCountLimit = $configJson.Logging.File.RetainedFileCountLimit ?? 30
                }
            }
        }
        
        return [PSCustomObject]$config
    }
    catch {
        throw "Failed to parse configuration file: $_"
    }
}

#endregion

#region Device Identity Functions

function Get-DeviceIdentity {
    [CmdletBinding()]
    param()
    
    Write-Log -Message "Collecting device identity information"
    
    $identity = @{
        MachineName = $env:COMPUTERNAME
        DomainName = $env:USERDOMAIN
        UserPrincipalName = $env:USERNAME
        ClientVersion = $script:ClientVersion
        Tags = @{
        }
    }
    
    # Add FleetId if configured
    if ($script:Config.SecureBootWatcher.FleetId) {
        $identity.Tags['FleetId'] = $script:Config.SecureBootWatcher.FleetId
    }
    
    # Get hardware information
    try {
        $computerSystem = Get-CimInstance -ClassName Win32_ComputerSystem -ErrorAction SilentlyContinue
        if ($computerSystem) {
            $identity.Manufacturer = $computerSystem.Manufacturer
            $identity.Model = $computerSystem.Model
        }
    }
    catch {
        Write-Log -Message "Failed to query Win32_ComputerSystem: $_" -Level Warning
    }
    
    # Get BIOS/firmware information
    try {
        $bios = Get-CimInstance -ClassName Win32_BIOS -ErrorAction SilentlyContinue
        if ($bios) {
            $identity.FirmwareVersion = $bios.SMBIOSBIOSVersion
            if ($bios.ReleaseDate) {
                try {
                    $identity.FirmwareReleaseDate = [datetime]$bios.ReleaseDate
                }
                catch {
                    $identity.FirmwareReleaseDate = $bios.ReleaseDate.ToString('o')
                }
            }
        }
    }
    catch {
        Write-Log -Message "Failed to query Win32_BIOS: $_" -Level Warning
    }
    
    # Get OS information
    try {
        $os = Get-CimInstance -ClassName Win32_OperatingSystem -ErrorAction SilentlyContinue
        if ($os) {
            $identity.OperatingSystem = $os.Caption
            $identity.OSVersion = $os.Version
            $identity.OSBuildNumber = $os.BuildNumber
            
            # Map ProductType to OSProductType enum (numeric values)
            # 1 = Workstation, 2 = DomainController, 3 = Server
            # API expects int: 0=Unknown, 1=Workstation, 2=DomainController, 3=Server
            $identity.OSProductType = switch ($os.ProductType) {
                1 { 1 }  # Workstation
                2 { 2 }  # DomainController
                3 { 3 }  # Server
                default { 0 }  # Unknown
            }
        }
    }
    catch {
        Write-Log -Message "Failed to query Win32_OperatingSystem: $_" -Level Warning
    }
    
    # Get chassis information
    try {
        $chassis = Get-CimInstance -ClassName Win32_SystemEnclosure -ErrorAction SilentlyContinue
        if ($chassis -and $chassis.ChassisTypes) {
            $identity.ChassisTypes = @($chassis.ChassisTypes)
        }
    }
    catch {
        Write-Log -Message "Failed to query Win32_SystemEnclosure: $_" -Level Warning
    }
    
    # Detect virtual machine
    try {
        $identity.IsVirtualMachine = $false
        $identity.VirtualizationPlatform = $null
        
        if ($computerSystem) {
            $model = $computerSystem.Model.ToLower()
            $manufacturer = $computerSystem.Manufacturer.ToLower()
            
            if ($model -match 'virtual|vmware|kvm|xen' -or $manufacturer -match 'microsoft corporation|vmware|xen|qemu') {
                $identity.IsVirtualMachine = $true
                
                if ($manufacturer -match 'microsoft corporation' -and $model -match 'virtual') {
                    $identity.VirtualizationPlatform = 'Hyper-V'
                }
                elseif ($manufacturer -match 'vmware') {
                    $identity.VirtualizationPlatform = 'VMware'
                }
                elseif ($manufacturer -match 'xen') {
                    $identity.VirtualizationPlatform = 'Xen'
                }
                elseif ($manufacturer -match 'qemu') {
                    $identity.VirtualizationPlatform = 'QEMU/KVM'
                }
            }
        }
    }
    catch {
        Write-Log -Message "Failed to detect virtualization: $_" -Level Warning
    }
    
    Write-Log -Message "Device identity collected: $($identity.Manufacturer) $($identity.Model)" -Level Verbose
    
    return [PSCustomObject]$identity
}

#endregion

#region Registry Snapshot Functions

function Get-RegistryValue {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        
        [Parameter(Mandatory = $true)]
        [string]$Name
    )
    
    try {
        $value = Get-ItemProperty -Path $Path -Name $Name -ErrorAction SilentlyContinue
        if ($value) {
            return $value.$Name
        }
    }
    catch {
        # Silently return null if key doesn't exist
    }
    
    return $null
}

function Get-SecureBootRegistrySnapshot {
    [CmdletBinding()]
    param()
    
    Write-Log -Message "Capturing Secure Boot registry snapshot"
    
    $basePath = 'HKLM:\SYSTEM\CurrentControlSet\Control\SecureBoot'
    $servicingPath = "$basePath\Servicing"
    $statePath = "$basePath\State"
    $sbatPath = "$basePath\SBAT"
    
    # Initialize snapshot structure matching .NET client model
    $snapshot = @{
        # Root level keys
        AvailableUpdates = $null
        HighConfidenceOptOut = $null
        MicrosoftUpdateManagedOptIn = $null
        CollectedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
        
        # Servicing sub-key
        Servicing = @{
            UefiCa2023Status = 0  # Unknown/NotStarted
            UefiCa2023Error = $null
            WindowsUEFICA2023Capable = $null
            BucketHash = $null
            ConfidenceLevel = $null
            RebootRequestedDB = $null
            RebootRequestedDBX = $null
            RebootRequestedKEK = $null
            CollectedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
        }
        
        # State sub-key
        State = @{
            UEFISecureBootEnabled = $null
            PolicyPublisher = $null
            PolicyVersion = $null
            CollectedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
        }
        
        # SBAT sub-key
        Sbat = @{
            SbatLevel = $null
            UpdateStatus = $null
            CollectedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
        }
    }
    
    # Read root level keys
    if (Test-Path $basePath) {
        $snapshot.AvailableUpdates = Get-RegistryValue -Path $basePath -Name 'AvailableUpdates'
        $snapshot.HighConfidenceOptOut = Get-RegistryValue -Path $basePath -Name 'HighConfidenceOptOut'
        $snapshot.MicrosoftUpdateManagedOptIn = Get-RegistryValue -Path $basePath -Name 'MicrosoftUpdateManagedOptIn'
    }
    
    # Read Servicing sub-key
    if (Test-Path $servicingPath) {
        # Read UEFICA2023Status as string and map to enum
        $statusString = Get-RegistryValue -Path $servicingPath -Name 'UEFICA2023Status'
        if ($statusString) {
            $snapshot.Servicing.UefiCa2023Status = switch ($statusString) {
                'NotStarted' { 1 }
                'InProgress' { 2 }
                'Updated' { 3 }
                'Error' { 4 }
                default { 0 }  # Unknown
            }
        }
        
        $snapshot.Servicing.UefiCa2023Error = Get-RegistryValue -Path $servicingPath -Name 'UefiCa2023Error'
        $snapshot.Servicing.WindowsUEFICA2023Capable = Get-RegistryValue -Path $servicingPath -Name 'WindowsUEFICA2023Capable'
        $snapshot.Servicing.BucketHash = Get-RegistryValue -Path $servicingPath -Name 'BucketHash'
        $snapshot.Servicing.ConfidenceLevel = Get-RegistryValue -Path $servicingPath -Name 'ConfidenceLevel'
        $snapshot.Servicing.RebootRequestedDB = Get-RegistryValue -Path $servicingPath -Name 'RebootRequestedDB'
        $snapshot.Servicing.RebootRequestedDBX = Get-RegistryValue -Path $servicingPath -Name 'RebootRequestedDBX'
        $snapshot.Servicing.RebootRequestedKEK = Get-RegistryValue -Path $servicingPath -Name 'RebootRequestedKEK'
    }
    
    # Read State sub-key
    if (Test-Path $statePath) {
        $snapshot.State.UEFISecureBootEnabled = Get-RegistryValue -Path $statePath -Name 'UEFISecureBootEnabled'
        $snapshot.State.PolicyPublisher = Get-RegistryValue -Path $statePath -Name 'PolicyPublisher'
        $snapshot.State.PolicyVersion = Get-RegistryValue -Path $statePath -Name 'PolicyVersion'
    }
    
    # Read SBAT sub-key
    if (Test-Path $sbatPath) {
        $snapshot.Sbat.SbatLevel = Get-RegistryValue -Path $sbatPath -Name 'SbatLevel'
        $snapshot.Sbat.UpdateStatus = Get-RegistryValue -Path $sbatPath -Name 'UpdateStatus'
    }
    
    Write-Log -Message "Registry snapshot captured: UefiCa2023Status=$($snapshot.Servicing.UefiCa2023Status), UEFISecureBootEnabled=$($snapshot.State.UEFISecureBootEnabled)" -Level Verbose
    
    return [PSCustomObject]$snapshot
}

function Get-DeviceAttributesSnapshot {
    [CmdletBinding()]
    param()
    
    Write-Log -Message "Capturing device attributes snapshot"
    
    # CORRECTED PATH: Aligned with .NET client RegistrySnapshotProvider.cs
    # Was: HKLM:\SYSTEM\CurrentControlSet\Control\DeviceAttributes (incorrect)
    # Now: HKLM:\SYSTEM\CurrentControlSet\Control\SecureBoot\Servicing\DeviceAttributes (correct)
    $deviceAttributesPath = 'HKLM:\SYSTEM\CurrentControlSet\Control\SecureBoot\Servicing\DeviceAttributes'
    
    $snapshot = @{
        CanAttemptUpdateAfter = $null
        OEMManufacturerName = $null
        OEMModelSystemVersion = $null
        BaseBoardManufacturer = $null
        FirmwareManufacturer = $null
        OEMModelBaseBoard = $null
        FirmwareVersion = $null
        OEMModelNumber = $null
        OEMModelSystemFamily = $null
        OEMName = $null
        OSArchitecture = $null
        OEMModelSKU = $null
        FirmwareReleaseDate = $null
        OEMModelBaseBoardVersion = $null
        StateAttributes = $null
        CollectedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    }
    
    if (Test-Path $deviceAttributesPath) {
        # Read CanAttemptUpdateAfter as binary FILETIME and convert to DateTime
        $canAttemptUpdateAfterBytes = Get-RegistryValue -Path $deviceAttributesPath -Name 'CanAttemptUpdateAfter'
        if ($canAttemptUpdateAfterBytes -and $canAttemptUpdateAfterBytes.Length -eq 8) {
            try {
                $fileTime = [BitConverter]::ToInt64($canAttemptUpdateAfterBytes, 0)
                $snapshot.CanAttemptUpdateAfter = [DateTime]::FromFileTimeUtc($fileTime).ToString('o')
            }
            catch {
                Write-Log -Message "Failed to parse CanAttemptUpdateAfter: $_" -Level Warning
            }
        }
        
        # Read string values
        $snapshot.OEMManufacturerName = Get-RegistryValue -Path $deviceAttributesPath -Name 'OEMManufacturerName'
        $snapshot.OEMModelSystemVersion = Get-RegistryValue -Path $deviceAttributesPath -Name 'OEMModelSystemVersion'
        $snapshot.BaseBoardManufacturer = Get-RegistryValue -Path $deviceAttributesPath -Name 'BaseBoardManufacturer'
        $snapshot.FirmwareManufacturer = Get-RegistryValue -Path $deviceAttributesPath -Name 'FirmwareManufacturer'
        $snapshot.OEMModelBaseBoard = Get-RegistryValue -Path $deviceAttributesPath -Name 'OEMModelBaseBoard'
        $snapshot.FirmwareVersion = Get-RegistryValue -Path $deviceAttributesPath -Name 'FirmwareVersion'
        $snapshot.OEMModelNumber = Get-RegistryValue -Path $deviceAttributesPath -Name 'OEMModelNumber'
        $snapshot.OEMModelSystemFamily = Get-RegistryValue -Path $deviceAttributesPath -Name 'OEMModelSystemFamily'
        $snapshot.OEMName = Get-RegistryValue -Path $deviceAttributesPath -Name 'OEMName'
        $snapshot.OSArchitecture = Get-RegistryValue -Path $deviceAttributesPath -Name 'OSArchitecture'
        $snapshot.OEMModelSKU = Get-RegistryValue -Path $deviceAttributesPath -Name 'OEMModelSKU'
        
        # Read FirmwareReleaseDate as string (format: MM/DD/YYYY)
        $firmwareReleaseDateStr = Get-RegistryValue -Path $deviceAttributesPath -Name 'FirmwareReleaseDate'
        if ($firmwareReleaseDateStr) {
            try {
                $firmwareReleaseDate = [DateTime]::ParseExact($firmwareReleaseDateStr, 'MM/dd/yyyy', $null)
                $snapshot.FirmwareReleaseDate = $firmwareReleaseDate.ToString('o')
            }
            catch {
                # If parsing fails, store as-is
                $snapshot.FirmwareReleaseDate = $firmwareReleaseDateStr
            }
        }
        
        $snapshot.OEMModelBaseBoardVersion = Get-RegistryValue -Path $deviceAttributesPath -Name 'OEMModelBaseBoardVersion'
        $snapshot.StateAttributes = Get-RegistryValue -Path $deviceAttributesPath -Name 'StateAttributes'
        
        Write-Log -Message "Device attributes captured: Manufacturer=$($snapshot.OEMManufacturerName), FirmwareVersion=$($snapshot.FirmwareVersion)" -Level Verbose
    }
    else {
        Write-Log -Message "Device attributes registry path not found at $deviceAttributesPath. This is normal for devices without Secure Boot servicing configured." -Level Debug
    }
    
    return [PSCustomObject]$snapshot
}

function Get-TelemetryPolicySnapshot {
    [CmdletBinding()]
    param()
    
    Write-Log -Message "Capturing telemetry policy snapshot"
    
    $telemetryPath = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection'
    $fallbackPath = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection'
    
    $snapshot = @{
        AllowTelemetry = $null
        TelemetryLevelDescription = 'Unknown'
        MeetsCfrTelemetryRequirement = $false
    }
    
    # Try policy path first, then fallback
    $allowTelemetry = Get-RegistryValue -Path $telemetryPath -Name 'AllowTelemetry'
    if ($null -eq $allowTelemetry) {
        $allowTelemetry = Get-RegistryValue -Path $fallbackPath -Name 'AllowTelemetry'
    }
    
    if ($null -ne $allowTelemetry) {
        $snapshot.AllowTelemetry = $allowTelemetry
        
        $snapshot.TelemetryLevelDescription = switch ($allowTelemetry) {
            0 { 'Security' }
            1 { 'Basic' }
            2 { 'Enhanced' }
            3 { 'Full' }
            default { 'Unknown' }
        }
        
        # CFR requires Basic (1) or higher
        $snapshot.MeetsCfrTelemetryRequirement = ($allowTelemetry -ge 1)
    }
    
    return [PSCustomObject]$snapshot
}

#endregion

#region Event Log Functions

function Get-SecureBootEvents {
    [CmdletBinding()]
    param()
    
    Write-Log -Message "Reading Secure Boot event logs"
    
    $lookbackPeriod = [TimeSpan]::Parse($script:Config.SecureBootWatcher.EventLookbackPeriod)
    $startTime = (Get-Date).Subtract($lookbackPeriod)
    
    $events = @()
    
    foreach ($channel in $script:Config.SecureBootWatcher.EventChannels) {
        try {
            Write-Log -Message "Querying event channel: $channel" -Level Verbose
            
            $channelEvents = Get-WinEvent -FilterHashtable @{
                LogName = $channel
                StartTime = $startTime
            } -ErrorAction SilentlyContinue
            
            if ($channelEvents) {
                foreach ($event in $channelEvents) {
                    # Filter for Secure Boot related events
                    if ($event.Message -match 'Secure Boot|UEFI|dbx|db|KEK|PK|CA 2023') {
                        $events += @{
                            EventId = $event.Id
                            Level = $event.LevelDisplayName
                            Source = $event.ProviderName
                            Message = $event.Message
                            TimeCreated = $event.TimeCreated.ToString('o')
                            Channel = $channel
                        }
                    }
                }
            }
        }
        catch {
            Write-Log -Message "Failed to query event channel '$channel': $_" -Level Warning
        }
    }
    
    Write-Log -Message "Collected $($events.Count) Secure Boot events" -Level Verbose
    
    return $events | ForEach-Object { [PSCustomObject]$_ }
}

#endregion

#region Certificate Enumeration Functions

function Get-SecureBootCertificates {
    [CmdletBinding()]
    param()
    
    Write-Log -Message "Enumerating Secure Boot certificates"
    
    $collection = @{
        SecureBootEnabled = $false
        Certificates = @{
            db = @()
            dbx = @()
            KEK = @()
            PK = @()
        }
        TotalCertificateCount = 0
        ExpiredCertificateCount = 0
        ExpiringCertificateCount = 0
        ErrorMessage = $null
    }
    
    try {
        # Check if Secure Boot is enabled
        try {
            $secureBootEnabled = Confirm-SecureBootUEFI -ErrorAction Stop
            $collection.SecureBootEnabled = $secureBootEnabled
            
            if (-not $secureBootEnabled) {
                $collection.ErrorMessage = 'Secure Boot is not enabled on this device'
                Write-Log -Message "Secure Boot is not enabled" -Level Warning
                return [PSCustomObject]$collection
            }
        }
        catch {
            $collection.ErrorMessage = "Unable to determine Secure Boot status: $_"
            Write-Log -Message $collection.ErrorMessage -Level Warning
            return [PSCustomObject]$collection
        }
        
        # Get certificates from each database
        $databases = @('db', 'dbx', 'KEK', 'PK')
        
        foreach ($db in $databases) {
            try {
                Write-Log -Message "Enumerating $db database" -Level Verbose
                
                $certs = Get-SecureBootUEFI -Name $db -ErrorAction SilentlyContinue
                
                if ($certs -and $certs.Bytes) {
                    # Parse the EFI signature list
                    $parsedCerts = Parse-EfiSignatureList -Bytes $certs.Bytes -Database $db
                    $collection.Certificates.$db = $parsedCerts
                    $collection.TotalCertificateCount += $parsedCerts.Count
                    
                    # Count expired and expiring certificates
                    $now = Get-Date
                    $expiringThreshold = $now.AddDays(90)
                    
                    foreach ($cert in $parsedCerts) {
                        if ($cert.NotAfter -and $cert.NotAfter -lt $now) {
                            $collection.ExpiredCertificateCount++
                        }
                        elseif ($cert.NotAfter -and $cert.NotAfter -lt $expiringThreshold -and $cert.NotAfter -gt $now) {
                            $collection.ExpiringCertificateCount++
                        }
                    }
                }
            }
            catch {
                Write-Log -Message "Failed to enumerate $db database: $_" -Level Warning
            }
        }
        
        Write-Log -Message "Enumerated $($collection.TotalCertificateCount) certificates total" -Level Verbose
    }
    catch {
        $collection.ErrorMessage = "Certificate enumeration failed: $_"
        Write-Log -Message $collection.ErrorMessage -Level Error -Exception $_
    }
    
    return [PSCustomObject]$collection
}

function Parse-EfiSignatureList {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [byte[]]$Bytes,
        
        [Parameter(Mandatory = $true)]
        [string]$Database
    )
    
    $certificates = @()
    
    try {
        # Try to parse as X.509 certificates
        # This is a simplified parser - the .NET client uses more sophisticated parsing
        $offset = 0
        
        while ($offset -lt $Bytes.Length - 16) {
            try {
                # Look for X.509 certificate signature (30 82)
                if ($Bytes[$offset] -eq 0x30 -and $Bytes[$offset + 1] -eq 0x82) {
                    # Try to extract certificate
                    $certLength = ([int]$Bytes[$offset + 2] -shl 8) + [int]$Bytes[$offset + 3] + 4
                    
                    if ($offset + $certLength -le $Bytes.Length) {
                        $certBytes = $Bytes[$offset..($offset + $certLength - 1)]
                        
                        try {
                            $x509Cert = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($certBytes)
                            
                            $isMicrosoft = $x509Cert.Subject -match 'Microsoft|Windows' -or $x509Cert.Issuer -match 'Microsoft|Windows'
                            
                            $certificates += @{
                                Subject = $x509Cert.Subject
                                Issuer = $x509Cert.Issuer
                                NotBefore = $x509Cert.NotBefore.ToString('o')
                                NotAfter = $x509Cert.NotAfter.ToString('o')
                                Thumbprint = $x509Cert.Thumbprint
                                Version = $x509Cert.Version
                                SignatureAlgorithm = $x509Cert.SignatureAlgorithm.FriendlyName
                                IsMicrosoftCertificate = $isMicrosoft
                                Database = $Database
                            }
                            
                            $x509Cert.Dispose()
                        }
                        catch {
                            # Not a valid X.509 certificate, skip
                        }
                        
                        $offset += $certLength
                    }
                    else {
                        $offset++
                    }
                }
                else {
                    $offset++
                }
            }
            catch {
                $offset++
            }
        }
    }
    catch {
        Write-Log -Message "Failed to parse EFI signature list for $Database`: $_" -Level Warning
    }
    
    return $certificates | ForEach-Object { [PSCustomObject]$_ }
}

#endregion

#region Report Building Functions

function Build-SecureBootReport {
    [CmdletBinding()]
    param()
    
    Write-LogSection -Title "Building Secure Boot Status Report"
    
    # Collect all components
    $device = Get-DeviceIdentity
    $registry = Get-SecureBootRegistrySnapshot
    $deviceAttributes = Get-DeviceAttributesSnapshot
    $telemetryPolicy = Get-TelemetryPolicySnapshot
    $events = Get-SecureBootEvents
    $certificates = Get-SecureBootCertificates
    
    # Build report object
    $report = @{
        Device = $device
        Registry = $registry
        DeviceAttributes = $deviceAttributes
        TelemetryPolicy = $telemetryPolicy
        Events = $events
        Certificates = $certificates
        Alerts = @()
        CreatedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
        ClientVersion = $script:ClientVersion
        CorrelationId = [Guid]::NewGuid().ToString('N')
    }
    
    # Populate alerts
    $alerts = @()
    
    # Access nested Servicing.UefiCa2023Status for status checks
    $uefiCa2023Status = if ($registry.Servicing) { $registry.Servicing.UefiCa2023Status } else { 0 }
    $uefiCa2023Error = if ($registry.Servicing) { $registry.Servicing.UefiCa2023Error } else { $null }
    
    # Map enum values for readability
    # 0=Unknown, 1=NotStarted, 2=InProgress, 3=Updated, 4=Error
    $statusName = switch ($uefiCa2023Status) {
        0 { 'Unknown' }
        1 { 'NotStarted' }
        2 { 'InProgress' }
        3 { 'Updated' }
        4 { 'Error' }
        default { 'Unknown' }
    }
    
    if ($uefiCa2023Status -eq 4) {  # Error
        $alerts += "Secure Boot update reported error code $($uefiCa2023Error ?? 0)."
    }
    
    if ($uefiCa2023Status -eq 1) {  # NotStarted
        $alerts += "Secure Boot certificate update has not started on this device."
    }
    
    if ($registry.HighConfidenceOptOut -eq $true) {
        $alerts += "Device is opted out of high-confidence automatic deployments."
    }
    
    if ($registry.MicrosoftUpdateManagedOptIn -eq $true) {
        $alerts += "Device is opted in to Microsoft managed deployment (CFR)."
    }
    
    if ($telemetryPolicy -and -not $telemetryPolicy.MeetsCfrTelemetryRequirement) {
        $alerts += "⚠ Telemetry level ($($telemetryPolicy.TelemetryLevelDescription)) does not meet CFR requirements. Basic (1) or higher required for Microsoft managed rollout."
    }
    elseif ($telemetryPolicy -and $registry.MicrosoftUpdateManagedOptIn -eq $true) {
        $alerts += "✓ Telemetry level ($($telemetryPolicy.TelemetryLevelDescription)) meets CFR requirements."
    }
    
    if ($events.Count -eq 0 -and $uefiCa2023Status -ne 3) {  # Not Updated
        $alerts += "No Secure Boot events detected within the lookback window."
    }
    
    if ($certificates.SecureBootEnabled -eq $false) {
        $alerts += "Secure Boot is not enabled on this device."
    }
    
    if ($certificates.ExpiredCertificateCount -gt 0) {
        $alerts += "$($certificates.ExpiredCertificateCount) expired certificate(s) detected in Secure Boot databases."
    }
    
    if ($certificates.ExpiringCertificateCount -gt 0) {
        $alerts += "$($certificates.ExpiringCertificateCount) certificate(s) expiring within 90 days."
    }
    
    if ($certificates.ErrorMessage) {
        $alerts += "Certificate enumeration error: $($certificates.ErrorMessage)"
    }
    
    $report.Alerts = $alerts
    
    Write-Log -Message "Report built successfully with $($alerts.Count) alerts (Status: $statusName)"
    
    return [PSCustomObject]$report
}

#endregion

#region Sink Functions

function Send-ReportToWebApi {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [PSCustomObject]$Report
    )
    
    $config = $script:Config.SecureBootWatcher.Sinks.WebApi
    
    if (-not $config.BaseAddress) {
        throw "WebApi BaseAddress is not configured"
    }
    
    $url = $config.BaseAddress.TrimEnd('/') + '/' + $config.IngestionRoute.TrimStart('/')
    
    Write-Log -Message "Sending report to Web API: $url"
    
    try {
        $json = $Report | ConvertTo-Json -Depth 10 -Compress
        $timeout = [TimeSpan]::Parse($config.HttpTimeout)
        
        $headers = @{
            'Content-Type' = 'application/json'
            'User-Agent' = "SecureBootWatcher-PowerShell/$script:ClientVersion"
        }
        
        $response = Invoke-RestMethod -Uri $url -Method Post -Body $json -Headers $headers -TimeoutSec $timeout.TotalSeconds
        
        Write-Log -Message "Report sent successfully to Web API" -Level Information
        return $true
    }
    catch {
        Write-Log -Message "Failed to send report to Web API: $_" -Level Error -Exception $_
        return $false
    }
}

function Send-ReportToFileShare {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [PSCustomObject]$Report
    )
    
    $config = $script:Config.SecureBootWatcher.Sinks.FileShare
    
    if (-not $config.RootPath) {
        throw "FileShare RootPath is not configured"
    }
    
    $fileName = "$($Report.Device.DomainName)_$($env:COMPUTERNAME)_$(Get-Date -Format 'yyyyMMddHHmmss')$($config.FileExtension)"
    $filePath = Join-Path $config.RootPath $fileName
    
    Write-Log -Message "Writing report to file share: $filePath"
    
    try {
        $json = $Report | ConvertTo-Json -Depth 10
        $json | Out-File -FilePath $filePath -Encoding UTF8 -Force
        
        Write-Log -Message "Report written successfully to file share"
        return $true
    }
    catch {
        Write-Log -Message "Failed to write report to file share: $_" -Level Error -Exception $_
        return $false
    }
}

function Send-ReportToAzureQueue {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [PSCustomObject]$Report
    )
    
    Write-Log -Message "Azure Queue sink is not implemented in PowerShell client" -Level Warning
    Write-Log -Message "Please use WebApi or FileShare sink instead" -Level Warning
    
    return $false
}

function Send-Report {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [PSCustomObject]$Report
    )
    
    Write-LogSection -Title "Sending Report"
    
    $sinks = $script:Config.SecureBootWatcher.Sinks
    $strategy = $sinks.ExecutionStrategy
    $priority = $sinks.SinkPriority -split ','
    
    $results = @{}
    $success = $false
    
    foreach ($sinkName in $priority) {
        $sinkName = $sinkName.Trim()
        
        $enabled = switch ($sinkName) {
            'WebApi' { $sinks.EnableWebApi }
            'FileShare' { $sinks.EnableFileShare }
            'AzureQueue' { $sinks.EnableAzureQueue }
            default { $false }
        }
        
        if (-not $enabled) {
            Write-Log -Message "Sink '$sinkName' is disabled, skipping" -Level Verbose
            continue
        }
        
        try {
            $result = switch ($sinkName) {
                'WebApi' { Send-ReportToWebApi -Report $Report }
                'FileShare' { Send-ReportToFileShare -Report $Report }
                'AzureQueue' { Send-ReportToAzureQueue -Report $Report }
                default { $false }
            }
            
            $results[$sinkName] = $result
            
            if ($result) {
                $success = $true
                Write-Log -Message "Sink '$sinkName' succeeded"
                
                if ($strategy -eq 'FirstSuccess') {
                    Write-Log -Message "ExecutionStrategy is FirstSuccess, stopping after first success"
                    break
                }
            }
            else {
                Write-Log -Message "Sink '$sinkName' failed" -Level Warning
            }
        }
        catch {
            Write-Log -Message "Sink '$sinkName' threw exception: $_" -Level Error -Exception $_
            $results[$sinkName] = $false
        }
    }
    
    if (-not $success) {
        Write-Log -Message "All enabled sinks failed!" -Level Error
        throw "Failed to send report to any configured sink"
    }
    
    Write-Log -Message "Report sent successfully"
}

#endregion

#region Command Processing Functions

function Get-PendingCommands {
    [CmdletBinding()]
    param()
    
    $config = $script:Config.SecureBootWatcher.Sinks.WebApi
    
    if (-not $config.BaseAddress) {
        Write-Log -Message "WebApi BaseAddress is not configured, cannot fetch commands" -Level Warning
        return @()
    }
    
    # Build URL for fetching commands
    $deviceId = "$($env:USERDOMAIN)\$($env:COMPUTERNAME)"
    $url = $config.BaseAddress.TrimEnd('/') + "/api/Commands/pending/$deviceId"
    
    Write-Log -Message "Fetching pending commands from: $url"
    
    try {
        $timeout = [TimeSpan]::Parse($config.HttpTimeout)
        
        $headers = @{
            'User-Agent' = "SecureBootWatcher-PowerShell/$script:ClientVersion"
        }
        
        $commands = Invoke-RestMethod -Uri $url -Method Get -Headers $headers -TimeoutSec $timeout.TotalSeconds
        
        Write-Log -Message "Fetched $($commands.Count) pending command(s)"
        
        return $commands
    }
    catch {
        Write-Log -Message "Failed to fetch pending commands: $_" -Level Warning
        return @()
    }
}

function Invoke-DeviceCommand {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [PSCustomObject]$Command
    )
    
    Write-Log -Message "Executing command $($Command.CommandId) of type $($Command.ConfigurationType)"
    
    $result = @{
        CommandId = $Command.CommandId
        Success = $false
        Message = ''
        ExecutedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
        CurrentState = $null
    }
    
    try {
        $basePath = 'HKLM:\SYSTEM\CurrentControlSet\Control\SecureBoot'
        $policyPath = "$basePath\Policy"
        
        # Ensure policy key exists
        if (-not (Test-Path $policyPath)) {
            New-Item -Path $policyPath -Force | Out-Null
        }
        
        switch ($Command.ConfigurationType) {
            'UpdateCertificates' {
                # This would trigger certificate update - not implemented in PowerShell client
                $result.Message = 'Certificate update command received but not implemented in PowerShell client'
                $result.Success = $false
            }
            
            'ConfigureMicrosoftUpdateOptIn' {
                $optIn = $Command.Parameters.OptIn
                Set-ItemProperty -Path $policyPath -Name 'MicrosoftUpdateManagedOptIn' -Value ([int]$optIn) -Type DWord
                $result.Message = "Set MicrosoftUpdateManagedOptIn to $optIn"
                $result.Success = $true
            }
            
            'ConfigureTelemetryLevel' {
                $level = $Command.Parameters.TelemetryLevel
                $telemetryPath = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection'
                
                if (-not (Test-Path $telemetryPath)) {
                    New-Item -Path $telemetryPath -Force | Out-Null
                }
                
                Set-ItemProperty -Path $telemetryPath -Name 'AllowTelemetry' -Value $level -Type DWord
                $result.Message = "Set AllowTelemetry to $level"
                $result.Success = $true
            }
            
            default {
                $result.Message = "Unknown command type: $($Command.ConfigurationType)"
                $result.Success = $false
            }
        }
        
        # Verify current state
        $registry = Get-SecureBootRegistrySnapshot
        $telemetry = Get-TelemetryPolicySnapshot
        
        $result.CurrentState = @{
            MicrosoftUpdateManagedOptIn = $registry.MicrosoftUpdateManagedOptIn
            AllowTelemetry = $telemetry.AllowTelemetry
            WindowsUEFICA2023Capable = $registry.WindowsUEFICA2023Capable
        }
    }
    catch {
        $result.Success = $false
        $result.Message = "Command execution failed: $_"
        Write-Log -Message "Command execution failed: $_" -Level Error -Exception $_
    }
    
    return [PSCustomObject]$result
}

function Send-CommandResult {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [PSCustomObject]$Result
    )
    
    $config = $script:Config.SecureBootWatcher.Sinks.WebApi
    
    if (-not $config.BaseAddress) {
        Write-Log -Message "WebApi BaseAddress is not configured, cannot report command result" -Level Warning
        return $false
    }
    
    $url = $config.BaseAddress.TrimEnd('/') + "/api/Commands/result"
    
    Write-Log -Message "Reporting command result to: $url"
    
    try {
        $json = $Result | ConvertTo-Json -Depth 10 -Compress
        $timeout = [TimeSpan]::Parse($config.HttpTimeout)
        
        $headers = @{
            'Content-Type' = 'application/json'
            'User-Agent' = "SecureBootWatcher-PowerShell/$script:ClientVersion"
        }
        
        $response = Invoke-RestMethod -Uri $url -Method Post -Body $json -Headers $headers -TimeoutSec $timeout.TotalSeconds
        
        Write-Log -Message "Command result reported successfully"
        return $true
    }
    catch {
        Write-Log -Message "Failed to report command result: $_" -Level Warning
        return $false
    }
}

function Invoke-CommandProcessing {
    [CmdletBinding()]
    param()
    
    Write-LogSection -Title "PHASE: Command Processing"
    
    $config = $script:Config.SecureBootWatcher.Commands
    
    if (-not $config.EnableCommandProcessing) {
        Write-Log -Message "Command processing is disabled"
        return
    }
    
    try {
        # Fetch pending commands
        $commands = Get-PendingCommands
        
        if ($commands.Count -eq 0) {
            Write-Log -Message "No pending commands to process"
            return
        }
        
        Write-Log -Message "Fetched $($commands.Count) pending command(s)"
        
        # Limit to max commands per cycle
        $commandsToProcess = $commands
        if ($commands.Count -gt $config.MaxCommandsPerCycle) {
            $commandsToProcess = $commands[0..($config.MaxCommandsPerCycle - 1)]
            Write-Log -Message "Limiting to $($config.MaxCommandsPerCycle) commands (total pending: $($commands.Count))" -Level Warning
        }
        
        # Execute each command
        foreach ($command in $commandsToProcess) {
            try {
                Write-Log -Message "Processing command $($command.CommandId) of type $($command.ConfigurationType)"
                
                $result = Invoke-DeviceCommand -Command $command
                
                if ($result.Success) {
                    Write-Log -Message "Command $($command.CommandId) executed successfully: $($result.Message)"
                }
                else {
                    Write-Log -Message "Command $($command.CommandId) execution failed: $($result.Message)" -Level Warning
                }
                
                # Report result back to API
                $reported = Send-CommandResult -Result $result
                
                if ($reported) {
                    Write-Log -Message "Command $($command.CommandId) result reported to API"
                }
                else {
                    Write-Log -Message "Failed to report command $($command.CommandId) result to API" -Level Warning
                }
                
                # Delay between commands
                $delay = [TimeSpan]::Parse($config.CommandExecutionDelay)
                if ($delay.TotalSeconds -gt 0) {
                    Start-Sleep -Seconds $delay.TotalSeconds
                }
            }
            catch {
                Write-Log -Message "Failed to process command $($command.CommandId): $_" -Level Error -Exception $_
                
                if (-not $config.ContinueOnCommandFailure) {
                    throw
                }
            }
        }
        
        Write-LogSection -Title "Command processing phase complete"
    }
    catch {
        Write-Log -Message "Command processing phase failed: $_" -Level Error -Exception $_
        
        if (-not $config.ContinueOnCommandFailure) {
            throw
        }
    }
}

#endregion

#region Main Execution

function Start-SecureBootWatcher {
    [CmdletBinding()]
    param()
    
    Write-LogSection -Title "SecureBootWatcher PowerShell Client Starting"
    Write-Log -Message "Version: $script:ClientVersion"
    Write-Log -Message "Machine: $env:COMPUTERNAME"
    Write-Log -Message "Domain: $env:USERDOMAIN"
    Write-Log -Message "User: $env:USERNAME"
    Write-Log -Message "PowerShell Version: $($PSVersionTable.PSVersion)"
    Write-Log -Message "OS: $([System.Environment]::OSVersion.VersionString)"
    
    # Load configuration
    $script:Config = Get-Configuration -Path $ConfigPath
    
    $runMode = $script:Config.SecureBootWatcher.RunMode
    $runOnce = $runMode -eq 'Once'
    
    if ($runOnce) {
        Write-Log -Message "Run Mode: Single-shot (will exit after one cycle)"
    }
    else {
        Write-Log -Message "Run Mode: Continuous"
    }
    
    do {
        try {
            # Phase 1: Process commands (if enabled and configured to run before inventory)
            if ($script:Config.SecureBootWatcher.Commands.EnableCommandProcessing -and 
                $script:Config.SecureBootWatcher.Commands.ProcessBeforeInventory) {
                Invoke-CommandProcessing
            }
            
            # Phase 2: Build and send inventory report
            $report = Build-SecureBootReport
            Send-Report -Report $report
            
            # Phase 3: Process commands (if enabled and configured to run after inventory)
            if ($script:Config.SecureBootWatcher.Commands.EnableCommandProcessing -and 
                -not $script:Config.SecureBootWatcher.Commands.ProcessBeforeInventory) {
                Invoke-CommandProcessing
            }
        }
        catch {
            Write-Log -Message "Unexpected error during execution cycle: $_" -Level Error -Exception $_
        }
        
        # Exit if running once
        if ($runOnce) {
            break
        }
        
        # Calculate sleep interval
        $interval = [TimeSpan]::Parse($script:Config.SecureBootWatcher.RegistryPollInterval)
        $eventInterval = [TimeSpan]::Parse($script:Config.SecureBootWatcher.EventQueryInterval)
        
        if ($eventInterval -lt $interval) {
            $interval = $eventInterval
        }
        
        if ($interval.TotalSeconds -le 0) {
            $interval = [TimeSpan]::FromMinutes(30)
        }
        
        Write-Log -Message "Sleeping for $($interval.ToString())"
        Start-Sleep -Seconds $interval.TotalSeconds
        
    } while ($true)
    
    Write-LogSection -Title "SecureBootWatcher PowerShell Client Stopped Successfully"
}

#endregion

# Entry point
try {
    Start-SecureBootWatcher
    exit 0
}
catch {
    Write-Log -Message "SecureBootWatcher terminated unexpectedly: $_" -Level Error -Exception $_
    exit 1
}
