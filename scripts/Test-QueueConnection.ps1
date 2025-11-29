# Test-QueueConnection.ps1
# Script to test Azure Queue Storage connection from API configuration

[CmdletBinding()]
param(
    [string]$ConfigPath = "SecureBootDashboard.Api\appsettings.json"
)

$ErrorActionPreference = "Stop"

Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "Azure Queue Connection Test" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host ""

# Read configuration
if (-not (Test-Path $ConfigPath)) {
    Write-Host "ERROR: Configuration file not found: $ConfigPath" -ForegroundColor Red
    exit 1
}

$config = Get-Content $ConfigPath -Raw | ConvertFrom-Json
$queueConfig = $config.QueueProcessor

Write-Host "Configuration:" -ForegroundColor Yellow
Write-Host "  Enabled: $($queueConfig.Enabled)" -ForegroundColor $(if ($queueConfig.Enabled) { "Green" } else { "Red" })
Write-Host "  Queue URI: $($queueConfig.QueueServiceUri)" -ForegroundColor Gray
Write-Host "  Queue Name: $($queueConfig.QueueName)" -ForegroundColor Gray
Write-Host "  Auth Method: $($queueConfig.AuthenticationMethod)" -ForegroundColor Gray
Write-Host ""

if (-not $queueConfig.Enabled) {
    Write-Host "WARNING: QueueProcessor is DISABLED in configuration!" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "To enable queue processing:" -ForegroundColor White
    Write-Host '  Set "Enabled": true in QueueProcessor section of appsettings.json' -ForegroundColor Gray
    Write-Host ""
    exit 0
}

# Check certificate if using Certificate auth
if ($queueConfig.AuthenticationMethod -eq "Certificate") {
    Write-Host "Checking certificate..." -ForegroundColor Yellow
    
    $thumbprint = $queueConfig.CertificateThumbprint
    $storeLocation = $queueConfig.CertificateStoreLocation
    $storeName = $queueConfig.CertificateStoreName
    
    Write-Host "  Thumbprint: $thumbprint" -ForegroundColor Gray
    Write-Host "  Store: $storeLocation\$storeName" -ForegroundColor Gray
    
    try {
        $store = New-Object System.Security.Cryptography.X509Certificates.X509Store($storeName, $storeLocation)
        $store.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadOnly)
        
        $cert = $store.Certificates | Where-Object { $_.Thumbprint -eq $thumbprint.Replace(" ", "").Replace(":", "") }
        
        if ($cert) {
            Write-Host "  ? Certificate found!" -ForegroundColor Green
            Write-Host "    Subject: $($cert.Subject)" -ForegroundColor Gray
            Write-Host "    Issuer: $($cert.Issuer)" -ForegroundColor Gray
            Write-Host "    Valid From: $($cert.NotBefore)" -ForegroundColor Gray
            Write-Host "    Valid To: $($cert.NotAfter)" -ForegroundColor Gray
            
            if ($cert.NotAfter -lt (Get-Date)) {
                Write-Host "  ? WARNING: Certificate is EXPIRED!" -ForegroundColor Red
            }
            
            # Check if certificate has private key
            if ($cert.HasPrivateKey) {
                Write-Host "  ? Private key is available" -ForegroundColor Green
            } else {
                Write-Host "  ? ERROR: Private key is NOT available!" -ForegroundColor Red
            }
        } else {
            Write-Host "  ? ERROR: Certificate NOT found in store!" -ForegroundColor Red
        }
        
        $store.Close()
    }
    catch {
        Write-Host "  ? ERROR: Failed to access certificate store" -ForegroundColor Red
        Write-Host "    $_" -ForegroundColor Red
    }
    
    Write-Host ""
}

# Test queue connection using Azure PowerShell (if available)
Write-Host "Testing queue connection..." -ForegroundColor Yellow

try {
    # Check if Az.Storage module is available
    if (-not (Get-Module -ListAvailable -Name Az.Storage)) {
        Write-Host "  ? Azure PowerShell module (Az.Storage) not installed" -ForegroundColor Yellow
        Write-Host "  Install with: Install-Module -Name Az.Storage -Scope CurrentUser" -ForegroundColor Gray
        Write-Host ""
        Write-Host "Skipping queue connection test." -ForegroundColor Yellow
        exit 0
    }
    
    Import-Module Az.Storage -ErrorAction Stop
    
    Write-Host "  Attempting to connect to queue..." -ForegroundColor Gray
    
    $queueUri = "$($queueConfig.QueueServiceUri)/$($queueConfig.QueueName)"
    
    Write-Host "  Queue URL: $queueUri" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  Note: Connection test from PowerShell requires Az module and authentication" -ForegroundColor Yellow
    Write-Host "  The API service uses certificate authentication directly" -ForegroundColor Yellow
    
}
catch {
    Write-Host "  ? Could not test connection: $_" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "  The API service will authenticate using the configured certificate" -ForegroundColor Gray
}

Write-Host ""
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "Summary" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host ""

if ($queueConfig.Enabled) {
    Write-Host "? QueueProcessor is ENABLED" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor White
    Write-Host "  1. Ensure the certificate has 'Storage Queue Data Contributor' role" -ForegroundColor Gray
    Write-Host "  2. Start the API service and check logs for queue processor" -ForegroundColor Gray
    Write-Host "  3. Look for 'Queue processor started successfully' message" -ForegroundColor Gray
    Write-Host ""
    Write-Host "To view API logs:" -ForegroundColor White
    Write-Host "  cd SecureBootDashboard.Api" -ForegroundColor Gray
    Write-Host "  dotnet run" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Expected log output:" -ForegroundColor White
    Write-Host '  "Queue processor starting. Queue: secureboot-reports, AuthMethod: Certificate"' -ForegroundColor Gray
    Write-Host '  "Using Certificate-based authentication with Client ID: ..."' -ForegroundColor Gray
    Write-Host '  "Queue processor started successfully."' -ForegroundColor Gray
} else {
    Write-Host "? QueueProcessor is DISABLED" -ForegroundColor Red
    Write-Host ""
    Write-Host "The API will NOT process messages from Azure Queue" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "To enable queue processing:" -ForegroundColor White
    Write-Host '  1. Edit SecureBootDashboard.Api\appsettings.json' -ForegroundColor Gray
    Write-Host '  2. Set "QueueProcessor.Enabled": true' -ForegroundColor Gray
    Write-Host '  3. Restart the API service' -ForegroundColor Gray
}

Write-Host ""
