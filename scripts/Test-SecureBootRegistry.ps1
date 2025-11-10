#Requires -RunAsAdministrator

Write-Host "=== Secure Boot Registry Diagnostic ===" -ForegroundColor Cyan
Write-Host ""

# 1. Verifica Secure Boot tramite cmdlet
Write-Host "[1/6] Checking Secure Boot status via Confirm-SecureBootUEFI..." -ForegroundColor Yellow
try {
    $secureBootEnabled = Confirm-SecureBootUEFI
    Write-Host "  Result: $secureBootEnabled" -ForegroundColor $(if ($secureBootEnabled) { "Green" } else { "Red" })
} catch {
    Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# 2. Verifica chiave base
Write-Host "[2/6] Checking base registry key..." -ForegroundColor Yellow
$basePath = "HKLM:\SYSTEM\CurrentControlSet\Control\SecureBoot"
if (Test-Path $basePath) {
    Write-Host "  ✓ Base key exists: $basePath" -ForegroundColor Green
    $baseProps = Get-ItemProperty $basePath -ErrorAction SilentlyContinue
    Write-Host "  Properties:" -ForegroundColor Gray
    $baseProps.PSObject.Properties | Where-Object { $_.Name -notlike "PS*" } | ForEach-Object {
        Write-Host "    - $($_.Name) = $($_.Value)" -ForegroundColor Gray
    }
} else {
    Write-Host "  ✗ Base key NOT FOUND!" -ForegroundColor Red
}

Write-Host ""

# 3. Verifica chiave State
Write-Host "[3/6] Checking State subkey..." -ForegroundColor Yellow
$statePath = "$basePath\State"
if (Test-Path $statePath) {
    Write-Host "  ✓ State key exists: $statePath" -ForegroundColor Green
    $stateProps = Get-ItemProperty $statePath -ErrorAction SilentlyContinue
    
    Write-Host "  Properties:" -ForegroundColor Gray
    $stateProps.PSObject.Properties | Where-Object { $_.Name -notlike "PS*" } | ForEach-Object {
        Write-Host "    - $($_.Name) = $($_.Value)" -ForegroundColor Gray
    }
    
    # Verifica UEFISecureBootEnabled
    $uefiSecureBootEnabled = $stateProps.UEFISecureBootEnabled
    if ($null -ne $uefiSecureBootEnabled) {
        Write-Host "  UEFISecureBootEnabled: $uefiSecureBootEnabled" -ForegroundColor $(if ($uefiSecureBootEnabled -eq 1) { "Green" } else { "Red" })
    } else {
        Write-Host "  ✗ UEFISecureBootEnabled property NOT FOUND!" -ForegroundColor Red
    }
} else {
    Write-Host "  ✗ State key NOT FOUND!" -ForegroundColor Red
}

Write-Host ""

# 4. Verifica chiave Servicing
Write-Host "[4/6] Checking Servicing subkey..." -ForegroundColor Yellow
$servicingPath = "$basePath\Servicing"
if (Test-Path $servicingPath) {
    Write-Host "  ✓ Servicing key exists: $servicingPath" -ForegroundColor Green
    $servicingProps = Get-ItemProperty $servicingPath -ErrorAction SilentlyContinue
    Write-Host "  Properties:" -ForegroundColor Gray
    $servicingProps.PSObject.Properties | Where-Object { $_.Name -notlike "PS*" } | ForEach-Object {
        Write-Host "    - $($_.Name) = $($_.Value)" -ForegroundColor Gray
    }
} else {
    Write-Host "  ⚠ Servicing key NOT FOUND (this is normal for some devices)" -ForegroundColor Yellow
}

Write-Host ""

# 5. Verifica chiave DeviceAttributes
Write-Host "[5/6] Checking DeviceAttributes subkey..." -ForegroundColor Yellow
$deviceAttrsPath = "$servicingPath\DeviceAttributes"
if (Test-Path $deviceAttrsPath) {
    Write-Host "  ✓ DeviceAttributes key exists: $deviceAttrsPath" -ForegroundColor Green
    $deviceAttrsProps = Get-ItemProperty $deviceAttrsPath -ErrorAction SilentlyContinue
    Write-Host "  Properties:" -ForegroundColor Gray
    $deviceAttrsProps.PSObject.Properties | Where-Object { $_.Name -notlike "PS*" } | ForEach-Object {
        Write-Host "    - $($_.Name) = $($_.Value)" -ForegroundColor Gray
    }
} else {
    Write-Host "  ⚠ DeviceAttributes key NOT FOUND (this is normal for some devices)" -ForegroundColor Yellow
}

Write-Host ""

# 6. Verifica accesso con .NET Registry API (come fa il client)
Write-Host "[6/6] Testing .NET Registry API access (as client does)..." -ForegroundColor Yellow
try {
    $regKey = [Microsoft.Win32.Registry]::LocalMachine.OpenSubKey("SYSTEM\CurrentControlSet\Control\SecureBoot")
    if ($null -ne $regKey) {
        Write-Host "  ✓ Successfully opened base key via .NET API" -ForegroundColor Green
        
        $stateKey = $regKey.OpenSubKey("State")
        if ($null -ne $stateKey) {
            Write-Host "  ✓ Successfully opened State subkey via .NET API" -ForegroundColor Green
            
            $value = $stateKey.GetValue("UEFISecureBootEnabled")
            if ($null -ne $value) {
                Write-Host "  ✓ UEFISecureBootEnabled value: $value (type: $($value.GetType().Name))" -ForegroundColor Green
                
                # Converti come fa il client
                $boolValue = [System.Convert]::ToInt32($value) -ne 0
                Write-Host "  ✓ Converted to bool: $boolValue" -ForegroundColor Green
            } else {
                Write-Host "  ✗ UEFISecureBootEnabled value is NULL!" -ForegroundColor Red
            }
            
            $stateKey.Close()
        } else {
            Write-Host "  ✗ Failed to open State subkey via .NET API!" -ForegroundColor Red
        }
        
        $regKey.Close()
    } else {
        Write-Host "  ✗ Failed to open base key via .NET API!" -ForegroundColor Red
    }
} catch {
    Write-Host "  ✗ Error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "=== Diagnostic Complete ===" -ForegroundColor Cyan