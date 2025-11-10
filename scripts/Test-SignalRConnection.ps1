<#
.SYNOPSIS
    Tests SignalR connection between Web App and API.

.DESCRIPTION
    This script helps diagnose SignalR connection issues by testing:
    - API health endpoint
    - SignalR negotiate endpoint
    - CORS configuration
    - SSL certificates

.PARAMETER ApiBaseUrl
    Base URL of the API (default: https://localhost:5001)

.PARAMETER WebAppUrl
    Base URL of the Web App (default: https://localhost:7001)

.EXAMPLE
    .\Test-SignalRConnection.ps1

.EXAMPLE
    .\Test-SignalRConnection.ps1 -ApiBaseUrl "https://api.contoso.com" -WebAppUrl "https://dashboard.contoso.com"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$ApiBaseUrl = "https://localhost:5001",

    [Parameter(Mandatory = $false)]
    [string]$WebAppUrl = "https://localhost:7001"
)

$ErrorActionPreference = "Continue"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  SignalR Connection Diagnostic Tool" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "API Base URL: $ApiBaseUrl" -ForegroundColor Yellow
Write-Host "Web App URL:  $WebAppUrl" -ForegroundColor Yellow
Write-Host ""

# Test 1: API Health Endpoint
Write-Host "[1/5] Testing API Health Endpoint..." -ForegroundColor Cyan
try {
    $healthUrl = "$ApiBaseUrl/health"
    $healthResponse = Invoke-RestMethod -Uri $healthUrl -Method Get -TimeoutSec 5
    Write-Host "  ? API is healthy" -ForegroundColor Green
    Write-Host "    Response: $healthResponse" -ForegroundColor Gray
} catch {
    Write-Host "  ? API health check failed" -ForegroundColor Red
    Write-Host "    Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "  Possible causes:" -ForegroundColor Yellow
    Write-Host "    - API is not running" -ForegroundColor Yellow
    Write-Host "    - Firewall blocking port 5001" -ForegroundColor Yellow
    Write-Host "    - SSL certificate not trusted" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "  Solutions:" -ForegroundColor Yellow
    Write-Host "    - Start API: cd SecureBootDashboard.Api; dotnet run" -ForegroundColor Yellow
    Write-Host "    - Trust certificate: dotnet dev-certs https --trust" -ForegroundColor Yellow
    exit 1
}

Write-Host ""

# Test 2: SignalR Negotiate Endpoint
Write-Host "[2/5] Testing SignalR Negotiate Endpoint..." -ForegroundColor Cyan
try {
    $negotiateUrl = "$ApiBaseUrl/dashboardHub/negotiate"
    
    # Add Origin header to test CORS
    $headers = @{
        "Origin" = $WebAppUrl
    }
    
    $negotiateResponse = Invoke-WebRequest -Uri $negotiateUrl -Method Post -Headers $headers -TimeoutSec 5
    
    if ($negotiateResponse.StatusCode -eq 200) {
        Write-Host "  ? SignalR negotiate endpoint accessible" -ForegroundColor Green
        
        # Check for CORS headers
        if ($negotiateResponse.Headers["Access-Control-Allow-Origin"]) {
            Write-Host "  ? CORS header present: $($negotiateResponse.Headers['Access-Control-Allow-Origin'])" -ForegroundColor Green
        } else {
            Write-Host "  ? CORS header missing" -ForegroundColor Red
            Write-Host "    This may cause connection failures from browser" -ForegroundColor Yellow
        }
        
        # Parse connection details
        $negotiateData = $negotiateResponse.Content | ConvertFrom-Json
        Write-Host "    Connection ID: $($negotiateData.connectionId)" -ForegroundColor Gray
        Write-Host "    Available Transports: $($negotiateData.availableTransports.transport -join ', ')" -ForegroundColor Gray
    } else {
        Write-Host "  ? Unexpected status code: $($negotiateResponse.StatusCode)" -ForegroundColor Red
    }
} catch {
    Write-Host "  ? SignalR negotiate endpoint failed" -ForegroundColor Red
    Write-Host "    Error: $($_.Exception.Message)" -ForegroundColor Red
    
    # Check if it's a 404
    if ($_.Exception.Response.StatusCode -eq 404) {
        Write-Host ""
        Write-Host "  Possible causes:" -ForegroundColor Yellow
        Write-Host "    - SignalR hub not mapped in API" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "  Solutions:" -ForegroundColor Yellow
        Write-Host "    - Verify Program.cs contains: app.MapHub<DashboardHub>('/dashboardHub')" -ForegroundColor Yellow
    }
}

Write-Host ""

# Test 3: CORS Preflight (OPTIONS request)
Write-Host "[3/5] Testing CORS Preflight..." -ForegroundColor Cyan
try {
    $negotiateUrl = "$ApiBaseUrl/dashboardHub/negotiate"
    
    $headers = @{
        "Origin" = $WebAppUrl
        "Access-Control-Request-Method" = "POST"
        "Access-Control-Request-Headers" = "content-type"
    }
    
    $corsResponse = Invoke-WebRequest -Uri $negotiateUrl -Method Options -Headers $headers -TimeoutSec 5
    
    if ($corsResponse.Headers["Access-Control-Allow-Origin"]) {
        Write-Host "  ? CORS preflight successful" -ForegroundColor Green
        Write-Host "    Allow-Origin: $($corsResponse.Headers['Access-Control-Allow-Origin'])" -ForegroundColor Gray
        Write-Host "    Allow-Methods: $($corsResponse.Headers['Access-Control-Allow-Methods'])" -ForegroundColor Gray
        Write-Host "    Allow-Headers: $($corsResponse.Headers['Access-Control-Allow-Headers'])" -ForegroundColor Gray
        
        if ($corsResponse.Headers["Access-Control-Allow-Credentials"] -eq "true") {
            Write-Host "  ? Credentials allowed (required for SignalR)" -ForegroundColor Green
        } else {
            Write-Host "  ? Credentials not allowed" -ForegroundColor Red
            Write-Host "    SignalR requires AllowCredentials to be true" -ForegroundColor Yellow
        }
    } else {
        Write-Host "  ? CORS preflight failed - no Allow-Origin header" -ForegroundColor Red
    }
} catch {
    Write-Host "  ? CORS preflight failed" -ForegroundColor Red
    Write-Host "    Error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# Test 4: Check SSL Certificate
Write-Host "[4/5] Checking SSL Certificate..." -ForegroundColor Cyan
try {
    $uri = [System.Uri]$ApiBaseUrl
    $tcpClient = New-Object System.Net.Sockets.TcpClient($uri.Host, $uri.Port)
    $sslStream = New-Object System.Net.Security.SslStream($tcpClient.GetStream(), $false, 
        { param($sender, $certificate, $chain, $errors) return $true })
    
    $sslStream.AuthenticateAsClient($uri.Host)
    
    $cert = $sslStream.RemoteCertificate
    $cert2 = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($cert)
    
    Write-Host "  ? SSL certificate found" -ForegroundColor Green
    Write-Host "    Subject: $($cert2.Subject)" -ForegroundColor Gray
    Write-Host "    Issuer: $($cert2.Issuer)" -ForegroundColor Gray
    Write-Host "    Valid From: $($cert2.NotBefore)" -ForegroundColor Gray
    Write-Host "    Valid To: $($cert2.NotAfter)" -ForegroundColor Gray
    
    # Check if self-signed
    if ($cert2.Subject -eq $cert2.Issuer) {
        Write-Host "  ? Certificate is self-signed" -ForegroundColor Yellow
        Write-Host "    Run: dotnet dev-certs https --trust" -ForegroundColor Yellow
    }
    
    # Check if expired
    if ($cert2.NotAfter -lt (Get-Date)) {
        Write-Host "  ? Certificate has expired!" -ForegroundColor Red
    }
    
    $sslStream.Close()
    $tcpClient.Close()
} catch {
    Write-Host "  ? SSL certificate check failed" -ForegroundColor Red
    Write-Host "    Error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# Test 5: Check API Configuration
Write-Host "[5/5] Checking API Configuration..." -ForegroundColor Cyan
$apiConfigPath = Join-Path (Split-Path $PSScriptRoot) "SecureBootDashboard.Api\appsettings.json"
if (Test-Path $apiConfigPath) {
    try {
        $apiConfig = Get-Content $apiConfigPath -Raw | ConvertFrom-Json
        
        Write-Host "  API Listening URLs:" -ForegroundColor Gray
        Write-Host "    $($apiConfig.Urls)" -ForegroundColor Gray
        
        Write-Host "  CORS Configuration:" -ForegroundColor Gray
        Write-Host "    WebAppUrl: $($apiConfig.WebAppUrl)" -ForegroundColor Gray
        if ($apiConfig.AlternativeWebUrls) {
            Write-Host "    Alternative URLs:" -ForegroundColor Gray
            foreach ($url in $apiConfig.AlternativeWebUrls) {
                Write-Host "      - $url" -ForegroundColor Gray
            }
        }
        
        # Check if Web App URL is in CORS list
        $corsUrls = @($apiConfig.WebAppUrl) + $apiConfig.AlternativeWebUrls
        if ($corsUrls -contains $WebAppUrl) {
            Write-Host "  ? Web App URL is in CORS allowed origins" -ForegroundColor Green
        } else {
            Write-Host "  ? Web App URL NOT in CORS allowed origins" -ForegroundColor Red
            Write-Host "    Add '$WebAppUrl' to appsettings.json AlternativeWebUrls" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "  ? Failed to parse appsettings.json" -ForegroundColor Red
        Write-Host "    Error: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "  ? API appsettings.json not found at: $apiConfigPath" -ForegroundColor Yellow
}

Write-Host ""

# Summary
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "If all tests passed, SignalR should work correctly." -ForegroundColor Green
Write-Host "If tests failed, review the errors above and apply suggested solutions." -ForegroundColor Yellow
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Cyan
Write-Host "  1. Start API: cd SecureBootDashboard.Api; dotnet run" -ForegroundColor White
Write-Host "  2. Start Web: cd SecureBootDashboard.Web; dotnet run" -ForegroundColor White
Write-Host "  3. Open browser: https://localhost:7001" -ForegroundColor White
Write-Host "  4. Check browser console (F12) for SignalR connection logs" -ForegroundColor White
Write-Host ""
Write-Host "Expected browser console output:" -ForegroundColor Cyan
Write-Host '  [SignalR] Connected successfully with ID: xyz123' -ForegroundColor Gray
Write-Host '  [SignalR] Subscribed to dashboard updates' -ForegroundColor Gray
Write-Host ""

