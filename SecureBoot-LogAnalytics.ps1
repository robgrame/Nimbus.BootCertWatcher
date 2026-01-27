<#
.SYNOPSIS
    SecureBoot Log Analytics Reporter - Collects Secure Boot details and sends to Azure Log Analytics

.DESCRIPTION
    This PowerShell script collects comprehensive Secure Boot information including:
    - Device details (Manufacturer, Model, BIOS version)
    - Secure Boot registry snapshots
    - PK, KEK, DB, and DBX certificates
    And transmits the data to an Azure Log Analytics workspace.

.PARAMETER WorkspaceId
    The Log Analytics Workspace ID (GUID)

.PARAMETER WorkspaceKey
    The Log Analytics Workspace Primary or Secondary Key

.PARAMETER LogType
    The custom log type name in Log Analytics. Default: SecureBootInventory

.EXAMPLE
    .\SecureBoot-LogAnalytics.ps1 -WorkspaceId "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx" -WorkspaceKey "base64key=="

.NOTES
    Version: 1.0.0
    Author: Nimbus SecureBootWatcher Team
    Requires: PowerShell 5.0+, Windows 10/11 with UEFI Secure Boot
    
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$WorkspaceId = 'b1cb1714-f830-4f55-ba09-43008391b354',
    
    [Parameter(Mandatory = $true)]
    [string]$WorkspaceKey = 'b22e4yB38C0X/y2UpXo54cXpUjBWYvwi/VYR1oM3WqCPVek/mGZ82UV9s1B61lzal2dc36FIERVUMoUx/O9j/Q==',
    
    [Parameter(Mandatory = $false)]
    [string]$LogType = 'SecureBootInventory'
)

#Requires -Version 5.0
#Requires -RunAsAdministrator

# Set strict mode and error handling
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Script version
$script:ClientVersion = '1.7.0'

# Logging configuration
$script:ScriptName = 'SecureBoot-LogAnalytics'
$script:LogsFolder = 'ACIInformatica'
$script:LogsPath = Join-Path $env:TEMP "$script:LogsFolder\Logs"
$script:MaxLogFiles = 5
$script:MaxLogSizeMB = 2

#region Logging Functions

function Initialize-LogFolder {
    # Create logs folder if it doesn't exist
    if (!(Test-Path $script:LogsPath)) {
        New-Item -Path $script:LogsPath -ItemType Directory -Force -Confirm:$false | Out-Null
    }
    
    # Rotate log files - keep only last N files
    $logFiles = @(Get-ChildItem -Path $script:LogsPath -Filter "$($script:ScriptName)*.log" -File -ErrorAction SilentlyContinue | 
                Sort-Object -Property LastWriteTime -Descending)
    
    if ($logFiles -and $logFiles.Count -gt $script:MaxLogFiles) {
        $logFiles | Select-Object -Skip $script:MaxLogFiles | Remove-Item -Force -Confirm:$false -ErrorAction SilentlyContinue
    }
    
    # Check if current log file needs rotation
    $logFile = Join-Path $script:LogsPath "$($script:ScriptName).log"
    if (Test-Path $logFile) {
        $logFileSize = (Get-Item $logFile).Length
        if ($logFileSize -gt ($script:MaxLogSizeMB * 1MB)) {
            $logFileDate = Get-Date -Format "yyyy-MM-dd_HH-mm-ss"
            Rename-Item -Path $logFile -NewName "$($script:ScriptName)-$logFileDate.log" -ErrorAction SilentlyContinue
        }
    }
}

function Write-Log {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Message,
        
        [Parameter(Mandatory = $false)]
        [ValidateSet('Information', 'Warning', 'Error', 'Debug')]
        [string]$Level = 'Information',
        
        [Parameter(Mandatory = $false)]
        [object]$Exception
    )
    
    $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss.fff'
    $logMessage = "[$timestamp] [$Level] $Message"
    
    if ($Exception) {
        if ($Exception -is [System.Management.Automation.ErrorRecord]) {
            $logMessage += "`n  Exception: $($Exception.Exception.Message)"
            $logMessage += "`n  ScriptStackTrace: $($Exception.ScriptStackTrace)"
        }
        elseif ($Exception -is [System.Exception]) {
            $logMessage += "`n  Exception: $($Exception.Message)"
            $logMessage += "`n  StackTrace: $($Exception.StackTrace)"
        }
    }
    
    # Write to console
    switch ($Level) {
        'Error' { Write-Host $logMessage -ForegroundColor Red }
        'Warning' { Write-Host $logMessage -ForegroundColor Yellow }
        'Debug' { if ($DebugPreference -ne 'SilentlyContinue') { Write-Host $logMessage -ForegroundColor Gray } }
        default { Write-Host $logMessage }
    }
    
    # Write to log file
    try {
        # Ensure log folder exists
        if (!(Test-Path $script:LogsPath)) {
            New-Item -Path $script:LogsPath -ItemType Directory -Force -Confirm:$false | Out-Null
        }
        
        $logFile = Join-Path $script:LogsPath "$($script:ScriptName).log"
        Add-Content -Path $logFile -Value $logMessage -Encoding Unicode -ErrorAction SilentlyContinue
    }
    catch {
        # Silently ignore file logging errors to avoid recursion
        Write-Host "[Write-Log] Failed to write to log file: $_" -ForegroundColor DarkYellow
    }
}

#endregion

#region Log Analytics Functions

function Get-LogAnalyticsSignature {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$WorkspaceId,
        
        [Parameter(Mandatory = $true)]
        [string]$WorkspaceKey,
        
        [Parameter(Mandatory = $true)]
        [string]$Date,
        
        [Parameter(Mandatory = $true)]
        [int]$ContentLength,
        
        [Parameter(Mandatory = $true)]
        [string]$Method,
        
        [Parameter(Mandatory = $true)]
        [string]$ContentType,
        
        [Parameter(Mandatory = $true)]
        [string]$Resource
    )
    
    $xHeaders = "x-ms-date:$Date"
    $stringToHash = "$Method`n$ContentLength`n$ContentType`n$xHeaders`n$Resource"
    $bytesToHash = [Text.Encoding]::UTF8.GetBytes($stringToHash)
    $keyBytes = [Convert]::FromBase64String($WorkspaceKey)
    $sha256 = [System.Security.Cryptography.HMACSHA256]::new($keyBytes)
    $calculatedHash = $sha256.ComputeHash($bytesToHash)
    $encodedHash = [Convert]::ToBase64String($calculatedHash)
    $authorization = "SharedKey ${WorkspaceId}:${encodedHash}"
    
    return $authorization
}

function Send-LogAnalyticsData {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$WorkspaceId,
        
        [Parameter(Mandatory = $true)]
        [string]$WorkspaceKey,
        
        [Parameter(Mandatory = $true)]
        [string]$Body,
        
        [Parameter(Mandatory = $true)]
        [string]$LogType
    )
    
    # Validate WorkspaceKey is valid Base64
    try {
        # Clean up the key - remove any whitespace or quotes that might have been added
        $cleanKey = $WorkspaceKey.Trim().Trim('"').Trim("'")
        
        # Test if it's valid Base64
        $testDecode = [Convert]::FromBase64String($cleanKey)
        Write-Log -Message "WorkspaceKey validated (length: $($cleanKey.Length) chars, decoded: $($testDecode.Length) bytes)" -Level Debug
    }
    catch {
        Write-Log -Message "WorkspaceKey validation failed. Key length: $($WorkspaceKey.Length)" -Level Error
        Write-Log -Message "First 10 chars: $($WorkspaceKey.Substring(0, [Math]::Min(10, $WorkspaceKey.Length)))..." -Level Error
        throw "Invalid WorkspaceKey - not a valid Base64 string: $_"
    }
    
    $method = "POST"
    $contentType = "application/json"
    $resource = "/api/logs"
    $rfc1123date = [DateTime]::UtcNow.ToString("r")
    $contentLength = $Body.Length
    
    $signature = Get-LogAnalyticsSignature -WorkspaceId $WorkspaceId `
        -WorkspaceKey $cleanKey `
        -Date $rfc1123date `
        -ContentLength $contentLength `
        -Method $method `
        -ContentType $contentType `
        -Resource $resource
    
    $uri = "https://${WorkspaceId}.ods.opinsights.azure.com${resource}?api-version=2016-04-01"
    
    $headers = @{
        "Authorization" = $signature
        "Log-Type" = $LogType
        "x-ms-date" = $rfc1123date
        "time-generated-field" = "CollectedAtUtc"
    }
    
    try {
        $response = Invoke-RestMethod -Uri $uri -Method $method -ContentType $contentType -Headers $headers -Body $Body
        Write-Log -Message "Data successfully sent to Log Analytics workspace"
        return $true
    }
    catch {
        Write-Log -Message "Failed to send data to Log Analytics: $_" -Level Error -Exception $_
        return $false
    }
}

#endregion

#region Device Information Functions

function Get-DeviceDetails {
    [CmdletBinding()]
    param()
    
    Write-Log -Message "Collecting device information"
    
    $deviceInfo = @{
        MachineName = $env:COMPUTERNAME
        DomainName = $env:USERDOMAIN
        CollectedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    }
    
    # Get hardware information from Win32_ComputerSystem
    try {
        $computerSystem = Get-CimInstance -ClassName Win32_ComputerSystem -ErrorAction SilentlyContinue
        if ($computerSystem) {
            $deviceInfo.Manufacturer = $computerSystem.Manufacturer
            $deviceInfo.Model = $computerSystem.Model
            $deviceInfo.SystemFamily = $computerSystem.SystemFamily
            $deviceInfo.SystemSKUNumber = $computerSystem.SystemSKUNumber
        }
    }
    catch {
        Write-Log -Message "Failed to query Win32_ComputerSystem: $_" -Level Warning
    }
    
    # Get BIOS information
    try {
        $bios = Get-CimInstance -ClassName Win32_BIOS -ErrorAction SilentlyContinue
        if ($bios) {
            $deviceInfo.BIOSVersion = $bios.SMBIOSBIOSVersion
            $deviceInfo.BIOSName = $bios.Name
            $deviceInfo.BIOSManufacturer = $bios.Manufacturer
            $deviceInfo.BIOSSerialNumber = $bios.SerialNumber
            $deviceInfo.SMBIOSMajorVersion = $bios.SMBIOSMajorVersion
            $deviceInfo.SMBIOSMinorVersion = $bios.SMBIOSMinorVersion
            if ($bios.ReleaseDate) {
                $deviceInfo.BIOSReleaseDate = $bios.ReleaseDate.ToString('o')
            }
        }
    }
    catch {
        Write-Log -Message "Failed to query Win32_BIOS: $_" -Level Warning
    }
    
    # Get BaseBoard (motherboard) information
    try {
        $baseboard = Get-CimInstance -ClassName Win32_BaseBoard -ErrorAction SilentlyContinue
        if ($baseboard) {
            $deviceInfo.BaseBoardManufacturer = $baseboard.Manufacturer
            $deviceInfo.BaseBoardProduct = $baseboard.Product
            $deviceInfo.BaseBoardVersion = $baseboard.Version
            $deviceInfo.BaseBoardSerialNumber = $baseboard.SerialNumber
        }
    }
    catch {
        Write-Log -Message "Failed to query Win32_BaseBoard: $_" -Level Warning
    }
    
    # Get OS information
    try {
        $os = Get-CimInstance -ClassName Win32_OperatingSystem -ErrorAction SilentlyContinue
        if ($os) {
            $deviceInfo.OperatingSystem = $os.Caption
            $deviceInfo.OSVersion = $os.Version
            $deviceInfo.OSBuildNumber = $os.BuildNumber
            $deviceInfo.OSArchitecture = $os.OSArchitecture
        }
    }
    catch {
        Write-Log -Message "Failed to query Win32_OperatingSystem: $_" -Level Warning
    }
    
    # Detect virtual machine
    try {
        $deviceInfo.IsVirtualMachine = $false
        $deviceInfo.VirtualizationPlatform = $null
        
        if ($computerSystem) {
            $model = $computerSystem.Model.ToLower()
            $manufacturer = $computerSystem.Manufacturer.ToLower()
            
            if ($model -match 'virtual|vmware|kvm|xen' -or $manufacturer -match 'microsoft corporation|vmware|xen|qemu') {
                $deviceInfo.IsVirtualMachine = $true
                
                if ($manufacturer -match 'microsoft corporation' -and $model -match 'virtual') {
                    $deviceInfo.VirtualizationPlatform = 'Hyper-V'
                }
                elseif ($manufacturer -match 'vmware') {
                    $deviceInfo.VirtualizationPlatform = 'VMware'
                }
                elseif ($manufacturer -match 'xen') {
                    $deviceInfo.VirtualizationPlatform = 'Xen'
                }
                elseif ($manufacturer -match 'qemu') {
                    $deviceInfo.VirtualizationPlatform = 'QEMU/KVM'
                }
            }
        }
    }
    catch {
        Write-Log -Message "Failed to detect virtualization: $_" -Level Warning
    }
    
    Write-Log -Message "Device info collected: $($deviceInfo.Manufacturer) $($deviceInfo.Model)"
    
    return $deviceInfo
}

#endregion

#region Registry Functions

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

function Get-SecureBootRegistryDetails {
    [CmdletBinding()]
    param()
    
    Write-Log -Message "Capturing Secure Boot registry details"
    
    $basePath = 'HKLM:\SYSTEM\CurrentControlSet\Control\SecureBoot'
    $servicingPath = "$basePath\Servicing"
    $statePath = "$basePath\State"
    $sbatPath = "$basePath\SBAT"
    $deviceAttributesPath = "$servicingPath\DeviceAttributes"
    
    $registryData = @{
        CollectedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
        
        # Root level
        AvailableUpdates = $null
        HighConfidenceOptOut = $null
        MicrosoftUpdateManagedOptIn = $null
        
        # State
        UEFISecureBootEnabled = $null
        PolicyPublisher = $null
        PolicyVersion = $null
        
        # Servicing
        UefiCa2023Status = $null
        UefiCa2023Error = $null
        WindowsUEFICA2023Capable = $null
        BucketHash = $null
        ConfidenceLevel = $null
        RebootRequestedDB = $null
        RebootRequestedDBX = $null
        RebootRequestedKEK = $null
        
        # SBAT
        SbatLevel = $null
        SbatUpdateStatus = $null
        
        # Device Attributes
        OEMManufacturerName = $null
        OEMModelSystemVersion = $null
        FirmwareManufacturer = $null
        FirmwareVersion = $null
        FirmwareReleaseDate = $null
        OEMModelNumber = $null
        OEMModelSystemFamily = $null
        OEMName = $null
        OEMModelSKU = $null
    }
    
    # Read root level keys
    if (Test-Path $basePath) {
        $registryData.AvailableUpdates = Get-RegistryValue -Path $basePath -Name 'AvailableUpdates'
        $registryData.HighConfidenceOptOut = Get-RegistryValue -Path $basePath -Name 'HighConfidenceOptOut'
        $registryData.MicrosoftUpdateManagedOptIn = Get-RegistryValue -Path $basePath -Name 'MicrosoftUpdateManagedOptIn'
    }
    
    # Read State sub-key
    if (Test-Path $statePath) {
        $registryData.UEFISecureBootEnabled = Get-RegistryValue -Path $statePath -Name 'UEFISecureBootEnabled'
        $registryData.PolicyPublisher = Get-RegistryValue -Path $statePath -Name 'PolicyPublisher'
        $registryData.PolicyVersion = Get-RegistryValue -Path $statePath -Name 'PolicyVersion'
    }
    
    # Read Servicing sub-key
    if (Test-Path $servicingPath) {
        $registryData.UefiCa2023Status = Get-RegistryValue -Path $servicingPath -Name 'UEFICA2023Status'
        $registryData.UefiCa2023Error = Get-RegistryValue -Path $servicingPath -Name 'UefiCa2023Error'
        $registryData.WindowsUEFICA2023Capable = Get-RegistryValue -Path $servicingPath -Name 'WindowsUEFICA2023Capable'
        $registryData.BucketHash = Get-RegistryValue -Path $servicingPath -Name 'BucketHash'
        $registryData.ConfidenceLevel = Get-RegistryValue -Path $servicingPath -Name 'ConfidenceLevel'
        $registryData.RebootRequestedDB = Get-RegistryValue -Path $servicingPath -Name 'RebootRequestedDB'
        $registryData.RebootRequestedDBX = Get-RegistryValue -Path $servicingPath -Name 'RebootRequestedDBX'
        $registryData.RebootRequestedKEK = Get-RegistryValue -Path $servicingPath -Name 'RebootRequestedKEK'
    }
    
    # Read SBAT sub-key
    if (Test-Path $sbatPath) {
        $registryData.SbatLevel = Get-RegistryValue -Path $sbatPath -Name 'SbatLevel'
        $registryData.SbatUpdateStatus = Get-RegistryValue -Path $sbatPath -Name 'UpdateStatus'
    }
    
    # Read Device Attributes
    if (Test-Path $deviceAttributesPath) {
        $registryData.OEMManufacturerName = Get-RegistryValue -Path $deviceAttributesPath -Name 'OEMManufacturerName'
        $registryData.OEMModelSystemVersion = Get-RegistryValue -Path $deviceAttributesPath -Name 'OEMModelSystemVersion'
        $registryData.FirmwareManufacturer = Get-RegistryValue -Path $deviceAttributesPath -Name 'FirmwareManufacturer'
        $registryData.FirmwareVersion = Get-RegistryValue -Path $deviceAttributesPath -Name 'FirmwareVersion'
        $registryData.FirmwareReleaseDate = Get-RegistryValue -Path $deviceAttributesPath -Name 'FirmwareReleaseDate'
        $registryData.OEMModelNumber = Get-RegistryValue -Path $deviceAttributesPath -Name 'OEMModelNumber'
        $registryData.OEMModelSystemFamily = Get-RegistryValue -Path $deviceAttributesPath -Name 'OEMModelSystemFamily'
        $registryData.OEMName = Get-RegistryValue -Path $deviceAttributesPath -Name 'OEMName'
        $registryData.OEMModelSKU = Get-RegistryValue -Path $deviceAttributesPath -Name 'OEMModelSKU'
    }
    
    Write-Log -Message "Registry data captured: SecureBootEnabled=$($registryData.UEFISecureBootEnabled)"
    
    return $registryData
}

#endregion

#region Certificate Functions

function Get-SecureBootCertificates {
    [CmdletBinding()]
    param()
    
    Write-Log -Message "Enumerating Secure Boot certificates (PK, KEK, DB)"
    
    $certificateData = @{
        SecureBootEnabled = $false
        TotalCertificateCount = 0
        PKCertificates = @()
        KEKCertificates = @()
        DBCertificates = @()
        DBXHashCount = 0
        CollectedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
        ErrorMessage = $null
    }
    
    try {
        # Check if Secure Boot is enabled
        try {
            $secureBootEnabled = Confirm-SecureBootUEFI -ErrorAction Stop
            $certificateData.SecureBootEnabled = $secureBootEnabled
            
            if (-not $secureBootEnabled) {
                $certificateData.ErrorMessage = 'Secure Boot is not enabled on this device'
                Write-Log -Message "Secure Boot is not enabled" -Level Warning
                return $certificateData
            }
        }
        catch {
            $certificateData.ErrorMessage = "Unable to determine Secure Boot status: $_"
            Write-Log -Message $certificateData.ErrorMessage -Level Warning
            return $certificateData
        }
        
        # Get PK certificates
        try {
            Write-Log -Message "Enumerating PK database..."
            $pk = Get-SecureBootUEFI -Name PK -ErrorAction Stop
            Write-Log -Message "PK object type: $($pk.GetType().FullName)"
            Write-Log -Message "PK properties: $($pk | Get-Member -MemberType Property | Select-Object -ExpandProperty Name)"
            
            # Try different ways to access the bytes
            $pkBytes = $null
            if ($pk.Bytes) {
                $pkBytes = $pk.Bytes
                Write-Log -Message "PK.Bytes found, length: $($pkBytes.Length)"
            }
            elseif ($pk -is [byte[]]) {
                $pkBytes = $pk
                Write-Log -Message "PK is byte array, length: $($pkBytes.Length)"
            }
            else {
                # Try to get raw bytes from the object
                Write-Log -Message "Trying alternative access methods..."
                $members = $pk | Get-Member -MemberType Property,NoteProperty | Select-Object -ExpandProperty Name
                Write-Log -Message "Available members: $($members -join ', ')"
            }
            
            if ($pkBytes -and $pkBytes.Length -gt 0) {
                $pkCerts = @(Parse-EfiSignatureList -Bytes $pkBytes -Database 'PK')
                $certificateData.PKCertificates = $pkCerts
                $certificateData.TotalCertificateCount += $pkCerts.Count
                Write-Log -Message "PK certificates found: $($pkCerts.Count)"
            }
        }
        catch {
            Write-Log -Message "Failed to enumerate PK database: $_" -Level Warning
        }
        
        # Get KEK certificates
        try {
            Write-Log -Message "Enumerating KEK database..."
            $kek = Get-SecureBootUEFI -Name KEK -ErrorAction Stop
            
            $kekBytes = $null
            if ($kek.Bytes) {
                $kekBytes = $kek.Bytes
            }
            elseif ($kek -is [byte[]]) {
                $kekBytes = $kek
            }
            
            if ($kekBytes -and $kekBytes.Length -gt 0) {
                Write-Log -Message "KEK bytes length: $($kekBytes.Length)"
                $kekCerts = @(Parse-EfiSignatureList -Bytes $kekBytes -Database 'KEK')
                $certificateData.KEKCertificates = $kekCerts
                $certificateData.TotalCertificateCount += $kekCerts.Count
                Write-Log -Message "KEK certificates found: $($kekCerts.Count)"
            }
        }
        catch {
            Write-Log -Message "Failed to enumerate KEK database: $_" -Level Warning
        }
        
        # Get DB certificates
        try {
            Write-Log -Message "Enumerating DB database..."
            $db = Get-SecureBootUEFI -Name db -ErrorAction Stop
            
            $dbBytes = $null
            if ($db.Bytes) {
                $dbBytes = $db.Bytes
            }
            elseif ($db -is [byte[]]) {
                $dbBytes = $db
            }
            
            if ($dbBytes -and $dbBytes.Length -gt 0) {
                Write-Log -Message "DB bytes length: $($dbBytes.Length)"
                $dbCerts = @(Parse-EfiSignatureList -Bytes $dbBytes -Database 'db')
                $certificateData.DBCertificates = $dbCerts
                $certificateData.TotalCertificateCount += $dbCerts.Count
                Write-Log -Message "DB certificates found: $($dbCerts.Count)"
            }
        }
        catch {
            Write-Log -Message "Failed to enumerate DB database: $_" -Level Warning
        }
        
        # Get DBX count (revocation list - usually hashes, not certs)
        try {
            Write-Log -Message "Enumerating DBX database..."
            $dbx = Get-SecureBootUEFI -Name dbx -ErrorAction Stop
            
            $dbxBytes = $null
            if ($dbx.Bytes) {
                $dbxBytes = $dbx.Bytes
            }
            elseif ($dbx -is [byte[]]) {
                $dbxBytes = $dbx
            }
            
            if ($dbxBytes -and $dbxBytes.Length -gt 0) {
                # DBX contains mostly hashes, just count the approximate entries
                $certificateData.DBXHashCount = [Math]::Floor($dbxBytes.Length / 48)
                Write-Log -Message "DBX data length: $($dbxBytes.Length), estimated hashes: $($certificateData.DBXHashCount)"
            }
        }
        catch {
            Write-Log -Message "Failed to enumerate DBX database: $_" -Level Warning
        }
        
        Write-Log -Message "Enumerated $($certificateData.TotalCertificateCount) certificates (PK: $(@($certificateData.PKCertificates).Count), KEK: $(@($certificateData.KEKCertificates).Count), DB: $(@($certificateData.DBCertificates).Count))"
    }
    catch {
        $certificateData.ErrorMessage = "Certificate enumeration failed: $_"
        Write-Log -Message $certificateData.ErrorMessage -Level Error -Exception $_
    }
    
    return $certificateData
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
    
    if ($null -eq $Bytes -or $Bytes.Length -eq 0) {
        Write-Log -Message "$Database`: No bytes to parse" -Level Warning
        return $certificates
    }
    
    Write-Log -Message "$Database`: Parsing $($Bytes.Length) bytes..."
    
    # Dump first 64 bytes for debugging
    $hexDump = ($Bytes[0..([Math]::Min(63, $Bytes.Length - 1))] | ForEach-Object { '{0:X2}' -f $_ }) -join ' '
    Write-Log -Message "$Database`: First bytes: $hexDump"
    
    # Method: Scan for X.509 certificate headers (DER encoding starts with 0x30 0x82 or 0x30 0x81)
    try {
        $offset = 0
        $certCount = 0
        
        while ($offset -lt $Bytes.Length - 4) {
            # Look for ASN.1 SEQUENCE tag (0x30)
            if ($Bytes[$offset] -eq 0x30) {
                $certLength = 0
                $headerLength = 0
                $validLength = $false
                
                # Check length encoding
                if (($offset + 4) -le $Bytes.Length -and $Bytes[$offset + 1] -eq 0x82) {
                    # Two-byte length (certificates typically > 127 bytes)
                    $certLength = ([int]$Bytes[$offset + 2] -shl 8) + [int]$Bytes[$offset + 3] + 4
                    $headerLength = 4
                    $validLength = $true
                }
                elseif (($offset + 3) -le $Bytes.Length -and $Bytes[$offset + 1] -eq 0x81) {
                    # One-byte length (128-255 bytes)
                    $certLength = [int]$Bytes[$offset + 2] + 3
                    $headerLength = 3
                    $validLength = $true
                }
                elseif (($offset + 2) -le $Bytes.Length -and $Bytes[$offset + 1] -lt 0x80) {
                    # Direct length (< 128 bytes, unlikely for certs but handle it)
                    $certLength = [int]$Bytes[$offset + 1] + 2
                    $headerLength = 2
                    $validLength = $true
                }
                
                # Only try to parse if length is reasonable for a certificate (500 bytes to 5KB typically)
                if ($validLength -and $certLength -ge 200 -and $certLength -le 8192 -and ($offset + $certLength) -le $Bytes.Length) {
                    # Try to parse as X.509 certificate
                    try {
                        $certBytes = New-Object byte[] $certLength
                        [Array]::Copy($Bytes, $offset, $certBytes, 0, $certLength)
                        
                        $x509Cert = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($certBytes)
                        
                        # If we get here, it's a valid certificate
                        $isMicrosoft = $x509Cert.Subject -match 'Microsoft|Windows' -or $x509Cert.Issuer -match 'Microsoft|Windows'
                        $isUefiCa2023 = $x509Cert.Subject -match 'UEFI CA 2023' -or $x509Cert.Subject -match 'Windows UEFI CA 2023'
                        $isUefiCa2011 = $x509Cert.Subject -match 'UEFI CA 2011' -or $x509Cert.Subject -match 'Windows UEFI CA 2011' -or 
                                        ($x509Cert.NotBefore.Year -le 2015 -and $isMicrosoft)
                        
                        $certInfo = @{
                            Database = $Database
                            Subject = $x509Cert.Subject
                            Issuer = $x509Cert.Issuer
                            NotBefore = $x509Cert.NotBefore.ToString('o')
                            NotAfter = $x509Cert.NotAfter.ToString('o')
                            Thumbprint = $x509Cert.Thumbprint
                            SerialNumber = $x509Cert.SerialNumber
                            Version = $x509Cert.Version
                            SignatureAlgorithm = $x509Cert.SignatureAlgorithm.FriendlyName
                            IsMicrosoftCertificate = $isMicrosoft
                            IsUefiCa2023 = $isUefiCa2023
                            IsUefiCa2011 = $isUefiCa2011
                            IsExpired = ($x509Cert.NotAfter -lt (Get-Date))
                            DaysUntilExpiry = [Math]::Floor(($x509Cert.NotAfter - (Get-Date)).TotalDays)
                        }
                        
                        $certificates += $certInfo
                        $certCount++
                        
                        $subjectShort = if ($x509Cert.Subject.Length -gt 60) { $x509Cert.Subject.Substring(0, 60) + "..." } else { $x509Cert.Subject }
                        Write-Log -Message "$Database`: Found cert #$certCount at offset $offset - $subjectShort"
                        
                        $x509Cert.Dispose()
                        $offset += $certLength
                        continue
                    }
                    catch {
                        # Not a valid certificate at this position, continue scanning
                    }
                }
            }
            $offset++
        }
        
        Write-Log -Message "$Database`: Total certificates found: $certCount"
    }
    catch {
        Write-Log -Message "Failed to parse $Database`: $_" -Level Warning
    }
    
    return $certificates
}

#endregion

#region Main Report Functions

function Build-SecureBootReport {
    [CmdletBinding()]
    param()
    
    Write-Log -Message "========================================"
    Write-Log -Message "Building Secure Boot Inventory Report"
    Write-Log -Message "========================================"
    
    # Collect all data
    $deviceDetails = Get-DeviceDetails
    $registryDetails = Get-SecureBootRegistryDetails
    $certificates = Get-SecureBootCertificates
    
    # Build flattened report for Log Analytics
    $report = @{
        # Report metadata
        ReportId = [Guid]::NewGuid().ToString('N')
        CollectedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
        ClientVersion = $script:ClientVersion
        
        # Device information
        MachineName = $deviceDetails.MachineName
        DomainName = $deviceDetails.DomainName
        Manufacturer = $deviceDetails.Manufacturer
        Model = $deviceDetails.Model
        SystemFamily = $deviceDetails.SystemFamily
        SystemSKUNumber = $deviceDetails.SystemSKUNumber
        
        # BIOS information
        BIOSVersion = $deviceDetails.BIOSVersion
        BIOSName = $deviceDetails.BIOSName
        BIOSManufacturer = $deviceDetails.BIOSManufacturer
        BIOSReleaseDate = $deviceDetails.BIOSReleaseDate
        SMBIOSMajorVersion = $deviceDetails.SMBIOSMajorVersion
        SMBIOSMinorVersion = $deviceDetails.SMBIOSMinorVersion
        
        # BaseBoard information
        BaseBoardManufacturer = $deviceDetails.BaseBoardManufacturer
        BaseBoardProduct = $deviceDetails.BaseBoardProduct
        BaseBoardVersion = $deviceDetails.BaseBoardVersion
        
        # OS information
        OperatingSystem = $deviceDetails.OperatingSystem
        OSVersion = $deviceDetails.OSVersion
        OSBuildNumber = $deviceDetails.OSBuildNumber
        OSArchitecture = $deviceDetails.OSArchitecture
        
        # Virtualization
        IsVirtualMachine = $deviceDetails.IsVirtualMachine
        VirtualizationPlatform = $deviceDetails.VirtualizationPlatform
        
        # Secure Boot State
        SecureBootEnabled = $certificates.SecureBootEnabled
        UEFISecureBootEnabled = $registryDetails.UEFISecureBootEnabled
        
        # Secure Boot Servicing
        UefiCa2023Status = $registryDetails.UefiCa2023Status
        UefiCa2023Error = $registryDetails.UefiCa2023Error
        WindowsUEFICA2023Capable = $registryDetails.WindowsUEFICA2023Capable
        HighConfidenceOptOut = $registryDetails.HighConfidenceOptOut
        MicrosoftUpdateManagedOptIn = $registryDetails.MicrosoftUpdateManagedOptIn
        ConfidenceLevel = $registryDetails.ConfidenceLevel
        
        # SBAT
        SbatLevel = $registryDetails.SbatLevel
        SbatUpdateStatus = $registryDetails.SbatUpdateStatus
        
        # Certificate counts
        TotalCertificateCount = $certificates.TotalCertificateCount
        PKCertificateCount = @($certificates.PKCertificates).Count
        KEKCertificateCount = @($certificates.KEKCertificates).Count
        DBCertificateCount = @($certificates.DBCertificates).Count
        DBXHashCount = $certificates.DBXHashCount
        
        # Certificate details (JSON strings for complex data)
        PKCertificates = if ($certificates.PKCertificates) { ($certificates.PKCertificates | ConvertTo-Json -Compress -Depth 5) } else { '[]' }
        KEKCertificates = if ($certificates.KEKCertificates) { ($certificates.KEKCertificates | ConvertTo-Json -Compress -Depth 5) } else { '[]' }
        DBCertificates = if ($certificates.DBCertificates) { ($certificates.DBCertificates | ConvertTo-Json -Compress -Depth 5) } else { '[]' }
        
        # Error tracking
        CertificateEnumerationError = $certificates.ErrorMessage
    }
    
    Write-Log -Message "Report built successfully for $($report.MachineName)"
    
    return $report
}

#endregion

#region Main Execution

try {
    # Initialize logging (create folder, rotate old logs)
    Initialize-LogFolder
    
    Write-Log -Message "========================================"
    Write-Log -Message "SecureBoot Log Analytics Reporter v$script:ClientVersion"
    Write-Log -Message "========================================"
    Write-Log -Message "Log file location: $script:LogsPath"
    Write-Log -Message "Machine: $env:COMPUTERNAME"
    Write-Log -Message "User: $env:USERNAME"
    Write-Log -Message "Target Workspace: $WorkspaceId"
    Write-Log -Message "Log Type: $LogType"
    Write-Log -Message "========================================"
    
    # Build the report
    $report = Build-SecureBootReport
    
    # Convert to JSON
    $jsonBody = @($report) | ConvertTo-Json -Depth 10
    
    Write-Log -Message "Sending data to Log Analytics workspace: $WorkspaceId"
    
    # Send to Log Analytics
    $result = Send-LogAnalyticsData -WorkspaceId $WorkspaceId `
        -WorkspaceKey $WorkspaceKey `
        -Body $jsonBody `
        -LogType $LogType
    
    if ($result) {
        Write-Log -Message "========================================"
        Write-Log -Message "Report successfully sent to Log Analytics"
        Write-Log -Message "Custom Log Type: ${LogType}_CL"
        Write-Log -Message "========================================"
        
        # Output summary
        Write-Log -Message "Summary:"
        Write-Log -Message "  Machine: $($report.MachineName)"
        Write-Log -Message "  Manufacturer: $($report.Manufacturer)"
        Write-Log -Message "  Model: $($report.Model)"
        Write-Log -Message "  BIOS Version: $($report.BIOSVersion)"
        Write-Log -Message "  Secure Boot: $($report.SecureBootEnabled)"
        Write-Log -Message "  PK Certs: $($report.PKCertificateCount)"
        Write-Log -Message "  KEK Certs: $($report.KEKCertificateCount)"
        Write-Log -Message "  DB Certs: $($report.DBCertificateCount)"
        
        # Show certificate details
        Write-Log -Message " "
        Write-Log -Message "========================================"
        Write-Log -Message "Certificate Details"
        Write-Log -Message "========================================"
        
        # PK Certificates
        if ($report.PKCertificates -and $report.PKCertificates -ne '[]') {
            Write-Log -Message " "
            Write-Log -Message "--- PK (Platform Key) Certificates ---"
            $pkCerts = $report.PKCertificates | ConvertFrom-Json
            foreach ($cert in @($pkCerts)) {
                $expiredTag = if ($cert.IsExpired) { " [EXPIRED]" } else { "" }
                Write-Log -Message "  Subject: $($cert.Subject)$expiredTag"
                Write-Log -Message "    Issuer: $($cert.Issuer)"
                Write-Log -Message "    Valid: $($cert.NotBefore) to $($cert.NotAfter)"
                Write-Log -Message "    Thumbprint: $($cert.Thumbprint)"
                Write-Log -Message "    Days Until Expiry: $($cert.DaysUntilExpiry)"
                Write-Log -Message " "
            }
        }
        else {
            Write-Log -Message "--- PK (Platform Key) Certificates: None found ---"
        }
        
        # KEK Certificates
        if ($report.KEKCertificates -and $report.KEKCertificates -ne '[]') {
            Write-Log -Message "--- KEK (Key Exchange Key) Certificates ---"
            $kekCerts = $report.KEKCertificates | ConvertFrom-Json
            foreach ($cert in @($kekCerts)) {
                $expiredTag = if ($cert.IsExpired) { " [EXPIRED]" } else { "" }
                $ca2023Tag = if ($cert.IsUefiCa2023) { " [UEFI CA 2023]" } else { "" }
                Write-Log -Message "  Subject: $($cert.Subject)$expiredTag$ca2023Tag"
                Write-Log -Message "    Issuer: $($cert.Issuer)"
                Write-Log -Message "    Valid: $($cert.NotBefore) to $($cert.NotAfter)"
                Write-Log -Message "    Thumbprint: $($cert.Thumbprint)"
                Write-Log -Message "    Days Until Expiry: $($cert.DaysUntilExpiry)"
                Write-Log -Message " "
            }
        }
        else {
            Write-Log -Message "--- KEK (Key Exchange Key) Certificates: None found ---"
        }
        
        # DB Certificates
        if ($report.DBCertificates -and $report.DBCertificates -ne '[]') {
            Write-Log -Message "--- DB (Signature Database) Certificates ---"
            $dbCerts = $report.DBCertificates | ConvertFrom-Json
            foreach ($cert in @($dbCerts)) {
                $expiredTag = if ($cert.IsExpired) { " [EXPIRED]" } else { "" }
                $ca2023Tag = if ($cert.IsUefiCa2023) { " [UEFI CA 2023]" } else { "" }
                Write-Log -Message "  Subject: $($cert.Subject)$expiredTag$ca2023Tag"
                Write-Log -Message "    Issuer: $($cert.Issuer)"
                Write-Log -Message "    Valid: $($cert.NotBefore) to $($cert.NotAfter)"
                Write-Log -Message "    Thumbprint: $($cert.Thumbprint)"
                Write-Log -Message "    Days Until Expiry: $($cert.DaysUntilExpiry)"
                Write-Log -Message " "
            }
        }
        else {
            Write-Log -Message "--- DB (Signature Database) Certificates: None found ---"
        }
        
        Write-Log -Message "========================================"
        
        exit 0
    }
    else {
        Write-Log -Message "Failed to send report to Log Analytics" -Level Error
        exit 1
    }
}
catch {
    Write-Log -Message "Script execution failed: $_" -Level Error -Exception $_
    exit 1
}

#endregion
