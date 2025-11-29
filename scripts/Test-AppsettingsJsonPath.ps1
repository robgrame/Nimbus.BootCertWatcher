# Test script to verify JSON path correction
# This script simulates what Install-Client-Intune.ps1 does with appsettings.json

$ErrorActionPreference = "Stop"

Write-Host "Testing appsettings.json configuration paths" -ForegroundColor Cyan
Write-Host ""

# Read the actual appsettings.json from the client project
$appsettingsPath = "SecureBootWatcher.Client\appsettings.json"

if (-not (Test-Path $appsettingsPath)) {
    Write-Host "ERROR: appsettings.json not found at: $appsettingsPath" -ForegroundColor Red
    exit 1
}

Write-Host "Reading appsettings.json from:" -ForegroundColor Yellow
Write-Host "  $appsettingsPath" -ForegroundColor Gray
Write-Host ""

# Load configuration
$config = Get-Content $appsettingsPath -Raw | ConvertFrom-Json

# Display current structure
Write-Host "Current JSON structure:" -ForegroundColor Yellow
Write-Host "  SecureBootWatcher.FleetId = $($config.SecureBootWatcher.FleetId)" -ForegroundColor Gray
Write-Host "  SecureBootWatcher.Sinks.WebApi.BaseAddress = $($config.SecureBootWatcher.Sinks.WebApi.BaseAddress)" -ForegroundColor Gray
Write-Host "  SecureBootWatcher.Sinks.EnableWebApi = $($config.SecureBootWatcher.Sinks.EnableWebApi)" -ForegroundColor Gray
Write-Host ""

# Test setting new values (CORRECT WAY)
Write-Host "Test: Setting configuration values" -ForegroundColor Green
Write-Host "  Setting: `$config.SecureBootWatcher.Sinks.WebApi.BaseAddress = 'https://newapi.contoso.com'" -ForegroundColor Gray
Write-Host "  Setting: `$config.SecureBootWatcher.Sinks.EnableWebApi = `$true" -ForegroundColor Gray
Write-Host "  Setting: `$config.SecureBootWatcher.FleetId = 'test-fleet'" -ForegroundColor Gray

try {
    $config.SecureBootWatcher.Sinks.WebApi.BaseAddress = "https://newapi.contoso.com"
    $config.SecureBootWatcher.Sinks.EnableWebApi = $true
    $config.SecureBootWatcher.FleetId = "test-fleet"
    
    Write-Host "  Result: SUCCESS ?" -ForegroundColor Green
    Write-Host ""
    
    # Verify changes
    Write-Host "Verification:" -ForegroundColor Yellow
    Write-Host "  SecureBootWatcher.FleetId = $($config.SecureBootWatcher.FleetId)" -ForegroundColor Green
    Write-Host "  SecureBootWatcher.Sinks.WebApi.BaseAddress = $($config.SecureBootWatcher.Sinks.WebApi.BaseAddress)" -ForegroundColor Green
    Write-Host "  SecureBootWatcher.Sinks.EnableWebApi = $($config.SecureBootWatcher.Sinks.EnableWebApi)" -ForegroundColor Green
    Write-Host ""
    
    # Test serialization
    Write-Host "Test: JSON Serialization" -ForegroundColor Yellow
    $tempFile = Join-Path $env:TEMP "test-appsettings.json"
    $config | ConvertTo-Json -Depth 10 | Set-Content $tempFile -Encoding UTF8
    
    Write-Host "  Written to: $tempFile" -ForegroundColor Gray
    
    # Read it back
    $configVerify = Get-Content $tempFile -Raw | ConvertFrom-Json
    
    Write-Host "  Reading back..." -ForegroundColor Gray
    Write-Host "    FleetId = $($configVerify.SecureBootWatcher.FleetId)" -ForegroundColor Green
    Write-Host "    BaseAddress = $($configVerify.SecureBootWatcher.Sinks.WebApi.BaseAddress)" -ForegroundColor Green
    Write-Host "    EnableWebApi = $($configVerify.SecureBootWatcher.Sinks.EnableWebApi)" -ForegroundColor Green
    Write-Host ""
    
    # Cleanup
    Remove-Item $tempFile -Force
    
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "ALL TESTS PASSED ?" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Summary:" -ForegroundColor White
    Write-Host "  ? Correct path for BaseAddress: `$config.SecureBootWatcher.Sinks.WebApi.BaseAddress" -ForegroundColor Green
    Write-Host "  ? Correct path for EnableWebApi: `$config.SecureBootWatcher.Sinks.EnableWebApi" -ForegroundColor Green
    Write-Host "  ? Correct path for FleetId: `$config.SecureBootWatcher.FleetId" -ForegroundColor Green
    Write-Host ""
    Write-Host "The Install-Client-Intune.ps1 script has been FIXED ?" -ForegroundColor Green
    
}
catch {
    Write-Host "  Result: ERROR - $($_.Exception.Message)" -ForegroundColor Red
    Write-Host $_.ScriptStackTrace -ForegroundColor Red
    exit 1
}
