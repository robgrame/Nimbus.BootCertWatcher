# Test-OSVersionCollection.ps1
# Script to verify OS version collection with UBR from Registry

$ErrorActionPreference = "Stop"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "OS Version Collection Test" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

# Test 1: WMI Version
Write-Host "Test 1: WMI Win32_OperatingSystem.Version" -ForegroundColor Yellow
try {
    $os = Get-CimInstance -ClassName Win32_OperatingSystem
    $wmiVersion = $os.Version
    $parts = $wmiVersion.Split('.')
    
    Write-Host "  WMI Version: $wmiVersion" -ForegroundColor White
    Write-Host "  Parts Count: $($parts.Length)" -ForegroundColor $(if ($parts.Length -eq 4) { "Green" } else { "Yellow" })
    
    if ($parts.Length -lt 4) {
        Write-Host "  ?? WARNING: WMI Version has only $($parts.Length) parts (expected 4)" -ForegroundColor Yellow
        Write-Host "  This is a known Windows limitation - UBR must be read from Registry" -ForegroundColor Yellow
    } else {
        Write-Host "  ? WMI Version has 4 parts" -ForegroundColor Green
    }
} catch {
    Write-Host "  ? ERROR: Failed to query WMI" -ForegroundColor Red
    Write-Host "    $_" -ForegroundColor Red
}

Write-Host ""

# Test 2: Registry UBR
Write-Host "Test 2: Registry UBR (Update Build Revision)" -ForegroundColor Yellow
try {
    $regPath = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion"
    $ubr = (Get-ItemProperty -Path $regPath -Name UBR).UBR
    
    Write-Host "  Registry Path: $regPath" -ForegroundColor Gray
    Write-Host "  UBR Value: $ubr" -ForegroundColor White
    Write-Host "  ? UBR successfully read from Registry" -ForegroundColor Green
} catch {
    Write-Host "  ? ERROR: Failed to read UBR from Registry" -ForegroundColor Red
    Write-Host "    $_" -ForegroundColor Red
    $ubr = $null
}

Write-Host ""

# Test 3: Constructed Full Version
Write-Host "Test 3: Construct Full OS Version" -ForegroundColor Yellow
try {
    if ($wmiVersion -and $ubr) {
        $parts = $wmiVersion.Split('.')
        
        if ($parts.Length -eq 4) {
            $fullVersion = $wmiVersion
            Write-Host "  Full Version (from WMI): $fullVersion" -ForegroundColor Green
            Write-Host "  ? WMI already provides 4 parts" -ForegroundColor Green
        } elseif ($parts.Length -eq 3) {
            $fullVersion = "$wmiVersion.$ubr"
            Write-Host "  WMI Version (3 parts): $wmiVersion" -ForegroundColor Yellow
            Write-Host "  Full Version (WMI + Registry UBR): $fullVersion" -ForegroundColor Green
            Write-Host "  ? Successfully constructed 4-part version" -ForegroundColor Green
        } else {
            Write-Host "  ? ERROR: WMI Version has unexpected format: $wmiVersion" -ForegroundColor Red
        }
        
        # Extract Build.UBR
        $fullParts = $fullVersion.Split('.')
        if ($fullParts.Length -ge 3) {
            $buildNumber = "$($fullParts[2]).$($fullParts[3])"
            Write-Host "  Build Number: $buildNumber" -ForegroundColor White
        }
    } else {
        Write-Host "  ? ERROR: Missing WMI Version or Registry UBR" -ForegroundColor Red
    }
} catch {
    Write-Host "  ? ERROR: Failed to construct full version" -ForegroundColor Red
    Write-Host "    $_" -ForegroundColor Red
}

Write-Host ""

# Test 4: Additional Registry Info
Write-Host "Test 4: Additional Registry Information" -ForegroundColor Yellow
try {
    $regPath = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion"
    $props = Get-ItemProperty -Path $regPath
    
    Write-Host "  Product Name: $($props.ProductName)" -ForegroundColor Gray
    Write-Host "  Display Version: $($props.DisplayVersion)" -ForegroundColor Gray
    Write-Host "  Current Build: $($props.CurrentBuild)" -ForegroundColor Gray
    Write-Host "  Current Build Number: $($props.CurrentBuildNumber)" -ForegroundColor Gray
    Write-Host "  UBR: $($props.UBR)" -ForegroundColor Gray
    Write-Host "  Release ID: $($props.ReleaseId)" -ForegroundColor Gray
} catch {
    Write-Host "  ?? Some registry values may not be available" -ForegroundColor Yellow
}

Write-Host ""

# Summary
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "Summary" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

$wmiParts = $wmiVersion.Split('.').Length
if ($wmiParts -eq 4 -and $ubr) {
    Write-Host "? PASS: System provides 4-part WMI version" -ForegroundColor Green
    Write-Host "  Client will use WMI version directly" -ForegroundColor Gray
} elseif ($wmiParts -eq 3 -and $ubr) {
    Write-Host "? PASS: System provides 3-part WMI + Registry UBR" -ForegroundColor Green
    Write-Host "  Client will construct full version: $wmiVersion.$ubr" -ForegroundColor Gray
} elseif ($wmiParts -eq 3 -and -not $ubr) {
    Write-Host "? FAIL: WMI has 3 parts but Registry UBR not available" -ForegroundColor Red
    Write-Host "  Client will report incomplete version" -ForegroundColor Yellow
} else {
    Write-Host "?? WARNING: Unexpected configuration" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Expected client behavior:" -ForegroundColor White
Write-Host "  OSVersion: $fullVersion" -ForegroundColor Gray
Write-Host "  OSBuildNumber: $buildNumber" -ForegroundColor Gray
Write-Host ""
