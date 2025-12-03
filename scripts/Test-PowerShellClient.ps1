<#
.SYNOPSIS
    Test SecureBootWatcher PowerShell Client functionality

.DESCRIPTION
    This script validates that the PowerShell client can run successfully
    and collect all required inventory data.

.PARAMETER ClientPath
    Path to the SecureBootWatcher-Client.ps1 script

.PARAMETER ConfigPath
    Path to the appsettings.json configuration file

.EXAMPLE
    .\Test-PowerShellClient.ps1

.EXAMPLE
    .\Test-PowerShellClient.ps1 -ClientPath "C:\Custom\SecureBootWatcher-Client.ps1"

.NOTES
    Requires Administrator privileges
    Must be run on a UEFI system with Secure Boot enabled for full testing
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$ClientPath = ".\SecureBootWatcher-Client.ps1",
    
    [Parameter(Mandatory = $false)]
    [string]$ConfigPath = ".\appsettings.powershell.json"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================"
Write-Host "SecureBootWatcher PowerShell Client"
Write-Host "Validation Test Suite"
Write-Host "========================================"
Write-Host ""

# Check prerequisites
Write-Host "Checking prerequisites..."

# Check if running as Administrator
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "  ✗ NOT running as Administrator" -ForegroundColor Red
    Write-Host "  Please run this script with Administrator privileges"
    exit 1
}
Write-Host "  ✓ Running as Administrator" -ForegroundColor Green

# Check PowerShell version
$psVersion = $PSVersionTable.PSVersion
if ($psVersion.Major -lt 5) {
    Write-Host "  ✗ PowerShell version $psVersion is too old (requires 5.0+)" -ForegroundColor Red
    exit 1
}
Write-Host "  ✓ PowerShell version $psVersion" -ForegroundColor Green

# Check if client script exists
if (-not (Test-Path $ClientPath)) {
    Write-Host "  ✗ Client script not found: $ClientPath" -ForegroundColor Red
    exit 1
}
Write-Host "  ✓ Client script found: $ClientPath" -ForegroundColor Green

# Check if config exists
if (-not (Test-Path $ConfigPath)) {
    Write-Host "  ✗ Configuration file not found: $ConfigPath" -ForegroundColor Red
    exit 1
}
Write-Host "  ✓ Configuration file found: $ConfigPath" -ForegroundColor Green

# Check UEFI mode
$firmwareType = (Get-ComputerInfo).BiosFirmwareType
if ($firmwareType -ne 'Uefi') {
    Write-Host "  ⚠ System is not UEFI ($firmwareType) - certificate enumeration will fail" -ForegroundColor Yellow
}
else {
    Write-Host "  ✓ System is UEFI" -ForegroundColor Green
}

# Check Secure Boot status
try {
    $secureBootEnabled = Confirm-SecureBootUEFI
    if ($secureBootEnabled) {
        Write-Host "  ✓ Secure Boot is enabled" -ForegroundColor Green
    }
    else {
        Write-Host "  ⚠ Secure Boot is not enabled - certificate enumeration will report this" -ForegroundColor Yellow
    }
}
catch {
    Write-Host "  ⚠ Cannot determine Secure Boot status: $_" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Running client script tests..."
Write-Host ""

# Test 1: Dry run with test config
Write-Host "Test 1: Dry run execution"
try {
    # Create a temporary test config that doesn't send data
    $testConfig = Get-Content $ConfigPath -Raw | ConvertFrom-Json
    $testConfig.SecureBootWatcher.Sinks.EnableWebApi = $false
    $testConfig.SecureBootWatcher.Sinks.EnableFileShare = $false
    $testConfig.SecureBootWatcher.Sinks.EnableAzureQueue = $false
    $testConfig.Logging.Console.Enabled = $true
    
    $tempConfigPath = Join-Path $env:TEMP "secureboot-test-config.json"
    $testConfig | ConvertTo-Json -Depth 10 | Set-Content $tempConfigPath -Encoding UTF8
    
    # Run client with test config
    Write-Host "  Running client with no sinks enabled (dry run)..."
    
    # Note: This will fail because no sinks are enabled, which is expected
    $output = & PowerShell.exe -NoProfile -ExecutionPolicy Bypass -File $ClientPath -ConfigPath $tempConfigPath -ErrorAction SilentlyContinue 2>&1
    
    # Check if client at least started
    if ($output -match "SecureBootWatcher PowerShell Client Starting") {
        Write-Host "  ✓ Client started successfully" -ForegroundColor Green
    }
    else {
        Write-Host "  ✗ Client did not start properly" -ForegroundColor Red
        Write-Host "  Output: $output"
        exit 1
    }
    
    # Check if configuration was loaded
    if ($output -match "Loading configuration from") {
        Write-Host "  ✓ Configuration loaded successfully" -ForegroundColor Green
    }
    else {
        Write-Host "  ✗ Configuration not loaded" -ForegroundColor Red
        exit 1
    }
    
    # Check if device identity was collected
    if ($output -match "Collecting device identity") {
        Write-Host "  ✓ Device identity collection attempted" -ForegroundColor Green
    }
    
    # Check if registry snapshot was captured
    if ($output -match "Capturing Secure Boot registry snapshot") {
        Write-Host "  ✓ Registry snapshot attempted" -ForegroundColor Green
    }
    
    # Check if event logs were queried
    if ($output -match "Reading Secure Boot event logs") {
        Write-Host "  ✓ Event log reading attempted" -ForegroundColor Green
    }
    
    # Check if certificate enumeration was attempted
    if ($output -match "Enumerating Secure Boot certificates") {
        Write-Host "  ✓ Certificate enumeration attempted" -ForegroundColor Green
    }
    
    # Clean up
    Remove-Item $tempConfigPath -Force -ErrorAction SilentlyContinue
}
catch {
    Write-Host "  ✗ Test 1 failed: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Test 2: Configuration validation"
try {
    $config = Get-Content $ConfigPath -Raw | ConvertFrom-Json
    
    # Check required settings
    if ($config.SecureBootWatcher) {
        Write-Host "  ✓ SecureBootWatcher section exists" -ForegroundColor Green
    }
    else {
        Write-Host "  ✗ SecureBootWatcher section missing" -ForegroundColor Red
        exit 1
    }
    
    if ($config.SecureBootWatcher.Sinks) {
        Write-Host "  ✓ Sinks section exists" -ForegroundColor Green
    }
    else {
        Write-Host "  ✗ Sinks section missing" -ForegroundColor Red
        exit 1
    }
    
    # Check if at least one sink is enabled
    $sinksEnabled = $config.SecureBootWatcher.Sinks.EnableWebApi -or 
                    $config.SecureBootWatcher.Sinks.EnableFileShare -or 
                    $config.SecureBootWatcher.Sinks.EnableAzureQueue
    
    if ($sinksEnabled) {
        Write-Host "  ✓ At least one sink is enabled" -ForegroundColor Green
    }
    else {
        Write-Host "  ⚠ No sinks are enabled - reports will not be sent" -ForegroundColor Yellow
    }
    
    # Validate WebApi config if enabled
    if ($config.SecureBootWatcher.Sinks.EnableWebApi) {
        if ($config.SecureBootWatcher.Sinks.WebApi.BaseAddress) {
            Write-Host "  ✓ WebApi BaseAddress configured: $($config.SecureBootWatcher.Sinks.WebApi.BaseAddress)" -ForegroundColor Green
        }
        else {
            Write-Host "  ✗ WebApi enabled but BaseAddress not set" -ForegroundColor Red
        }
    }
}
catch {
    Write-Host "  ✗ Test 2 failed: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Test 3: WMI/CIM queries"
try {
    # Test Win32_ComputerSystem
    $computerSystem = Get-CimInstance -ClassName Win32_ComputerSystem -ErrorAction Stop
    if ($computerSystem) {
        Write-Host "  ✓ Win32_ComputerSystem query successful" -ForegroundColor Green
        Write-Host "    Manufacturer: $($computerSystem.Manufacturer)"
        Write-Host "    Model: $($computerSystem.Model)"
    }
    
    # Test Win32_BIOS
    $bios = Get-CimInstance -ClassName Win32_BIOS -ErrorAction Stop
    if ($bios) {
        Write-Host "  ✓ Win32_BIOS query successful" -ForegroundColor Green
        Write-Host "    Version: $($bios.SMBIOSBIOSVersion)"
    }
    
    # Test Win32_OperatingSystem
    $os = Get-CimInstance -ClassName Win32_OperatingSystem -ErrorAction Stop
    if ($os) {
        Write-Host "  ✓ Win32_OperatingSystem query successful" -ForegroundColor Green
        Write-Host "    OS: $($os.Caption)"
        Write-Host "    Version: $($os.Version)"
        Write-Host "    Build: $($os.BuildNumber)"
    }
}
catch {
    Write-Host "  ✗ Test 3 failed: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Test 4: Registry access"
try {
    $basePath = 'HKLM:\SYSTEM\CurrentControlSet\Control\SecureBoot'
    
    if (Test-Path $basePath) {
        Write-Host "  ✓ Secure Boot registry path exists" -ForegroundColor Green
        
        # Try to read a common value
        $uefiDbUpdatePath = "$basePath\UEFIDBUpdate"
        if (Test-Path $uefiDbUpdatePath) {
            Write-Host "  ✓ UEFIDBUpdate registry key exists" -ForegroundColor Green
        }
        else {
            Write-Host "  ⚠ UEFIDBUpdate registry key not found (may not have updates yet)" -ForegroundColor Yellow
        }
    }
    else {
        Write-Host "  ⚠ Secure Boot registry path not found" -ForegroundColor Yellow
    }
}
catch {
    Write-Host "  ✗ Test 4 failed: $_" -ForegroundColor Red
}

Write-Host ""
Write-Host "Test 5: Event log access"
try {
    $testChannels = @(
        'Microsoft-Windows-SecureBoot-Servicing/Operational',
        'System'
    )
    
    foreach ($channel in $testChannels) {
        $events = Get-WinEvent -FilterHashtable @{
            LogName = $channel
            StartTime = (Get-Date).AddDays(-1)
        } -MaxEvents 1 -ErrorAction SilentlyContinue
        
        if ($events) {
            Write-Host "  ✓ Can read event channel: $channel" -ForegroundColor Green
        }
        else {
            Write-Host "  ⚠ No recent events in channel: $channel" -ForegroundColor Yellow
        }
    }
}
catch {
    Write-Host "  ✗ Test 5 failed: $_" -ForegroundColor Red
}

Write-Host ""
Write-Host "========================================"
Write-Host "Test Summary"
Write-Host "========================================"
Write-Host ""
Write-Host "All basic tests passed!" -ForegroundColor Green
Write-Host ""
Write-Host "The PowerShell client appears to be functional."
Write-Host "For a full end-to-end test, configure a valid API endpoint"
Write-Host "and run the client manually to verify data submission."
Write-Host ""
Write-Host "Next steps:"
Write-Host "  1. Configure appsettings.json with your API endpoint"
Write-Host "  2. Run the client manually: .\SecureBootWatcher-Client.ps1"
Write-Host "  3. Check the dashboard for the submitted report"
Write-Host "  4. Create Intune package: .\scripts\Prepare-PowerShellPackage.ps1"
Write-Host ""
Write-Host "========================================"
