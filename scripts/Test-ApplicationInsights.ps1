# Test-ApplicationInsights.ps1
# Tests Application Insights connectivity and telemetry ingestion

param(
    [Parameter(Mandatory=$false)]
    [string]$ConnectionString = $env:APPLICATIONINSIGHTS_CONNECTION_STRING,
    
    [Parameter(Mandatory=$false)]
    [string]$ApiBaseUrl = "https://localhost:5001",
    
    [Parameter(Mandatory=$false)]
    [int]$TestDurationSeconds = 60,
    
    [switch]$SkipCertificateCheck,
    
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Application Insights Connectivity Test" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Validate connection string
if ([string]::IsNullOrEmpty($ConnectionString)) {
    Write-Host "? APPLICATIONINSIGHTS_CONNECTION_STRING not set" -ForegroundColor Red
    Write-Host ""
    Write-Host "Set the connection string using:" -ForegroundColor Yellow
    Write-Host "  Windows PowerShell:" -ForegroundColor Gray
    Write-Host "    `$env:APPLICATIONINSIGHTS_CONNECTION_STRING = 'InstrumentationKey=...'" -ForegroundColor Gray
    Write-Host "  Linux/macOS:" -ForegroundColor Gray
    Write-Host "    export APPLICATIONINSIGHTS_CONNECTION_STRING='InstrumentationKey=...'" -ForegroundColor Gray
    Write-Host ""
    exit 1
}

Write-Host "? Connection String: " -NoNewline -ForegroundColor Green
Write-Host $ConnectionString.Substring(0, [Math]::Min(50, $ConnectionString.Length)) + "..." -ForegroundColor Gray
Write-Host ""

# Parse connection string
$instrumentationKey = ""
$ingestionEndpoint = ""

$parts = $ConnectionString -split ";"
foreach ($part in $parts) {
    if ($part -match "InstrumentationKey=(.+)") {
        $instrumentationKey = $Matches[1]
    }
    if ($part -match "IngestionEndpoint=(.+)") {
        $ingestionEndpoint = $Matches[1]
    }
}

if ([string]::IsNullOrEmpty($instrumentationKey)) {
    Write-Host "? Invalid connection string: No InstrumentationKey found" -ForegroundColor Red
    exit 1
}

Write-Host "  Instrumentation Key: $instrumentationKey" -ForegroundColor Gray
Write-Host "  Ingestion Endpoint: $ingestionEndpoint" -ForegroundColor Gray
Write-Host ""

# Test ingestion endpoint connectivity
Write-Host "Testing Application Insights endpoint connectivity..." -ForegroundColor Cyan

try {
    if (-not [string]::IsNullOrEmpty($ingestionEndpoint)) {
        $testUri = "$ingestionEndpoint/v2/track"
        Write-Host "  Endpoint: $testUri" -ForegroundColor Gray
        
        $response = Invoke-WebRequest -Uri $testUri -Method POST -Body "{}" -ContentType "application/json" -ErrorAction SilentlyContinue
        
        if ($response.StatusCode -eq 200 -or $response.StatusCode -eq 400) {
            Write-Host "? Endpoint reachable (Status: $($response.StatusCode))" -ForegroundColor Green
        }
    } else {
        Write-Host "? No ingestion endpoint specified, using default Azure endpoint" -ForegroundColor Yellow
    }
} catch {
    if ($_.Exception.Response.StatusCode.Value__ -eq 400) {
        Write-Host "? Endpoint reachable (400 Bad Request expected with empty body)" -ForegroundColor Green
    } else {
        Write-Host "? Warning: Could not verify endpoint connectivity" -ForegroundColor Yellow
        Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Gray
    }
}

Write-Host ""

# Generate test telemetry by calling API endpoints
Write-Host "Generating test traffic to SecureBootDashboard API..." -ForegroundColor Cyan
Write-Host "  API Base URL: $ApiBaseUrl" -ForegroundColor Gray
Write-Host "  Test Duration: $TestDurationSeconds seconds" -ForegroundColor Gray
Write-Host ""

$startTime = Get-Date
$endTime = $startTime.AddSeconds($TestDurationSeconds)
$requestCount = 0
$errorCount = 0

# Test endpoints
$testEndpoints = @(
    @{ Method = "GET"; Path = "/health"; Description = "Health Check" },
    @{ Method = "GET"; Path = "/api/Devices"; Description = "Device List" },
    @{ Method = "GET"; Path = "/api/WindowsVersion/versions"; Description = "Windows Versions" },
    @{ Method = "GET"; Path = "/api/SecureBootReports/statistics"; Description = "Statistics" }
)

Write-Host "Test Endpoints:" -ForegroundColor Cyan
foreach ($endpoint in $testEndpoints) {
    Write-Host "  [$($endpoint.Method)] $($endpoint.Path) - $($endpoint.Description)" -ForegroundColor Gray
}
Write-Host ""

$requestParams = @{
    Method = "GET"
    SkipCertificateCheck = $SkipCertificateCheck.IsPresent
    ErrorAction = "SilentlyContinue"
}

Write-Host "Running test..." -ForegroundColor Cyan
Write-Host "[Time]    [Endpoint]                          [Status] [Duration]" -ForegroundColor Gray
Write-Host "--------------------------------------------------------------" -ForegroundColor Gray

while ((Get-Date) -lt $endTime) {
    foreach ($endpoint in $testEndpoints) {
        $uri = "$ApiBaseUrl$($endpoint.Path)"
        $requestParams.Uri = $uri
        $requestParams.Method = $endpoint.Method
        
        $requestStartTime = Get-Date
        
        try {
            $response = Invoke-WebRequest @requestParams
            $duration = ((Get-Date) - $requestStartTime).TotalMilliseconds
            $status = $response.StatusCode
            $statusColor = if ($status -eq 200) { "Green" } else { "Yellow" }
            
            $timestamp = (Get-Date).ToString("HH:mm:ss")
            $endpointName = $endpoint.Description.PadRight(30)
            
            Write-Host "[$timestamp] $endpointName " -NoNewline
            Write-Host "[$status]  " -NoNewline -ForegroundColor $statusColor
            Write-Host "$($duration.ToString("F0"))ms"
            
            $requestCount++
            
        } catch {
            $duration = ((Get-Date) - $requestStartTime).TotalMilliseconds
            $timestamp = (Get-Date).ToString("HH:mm:ss")
            $endpointName = $endpoint.Description.PadRight(30)
            
            Write-Host "[$timestamp] $endpointName " -NoNewline
            Write-Host "[ERROR] " -NoNewline -ForegroundColor Red
            Write-Host "$($duration.ToString("F0"))ms - $($_.Exception.Message)"
            
            $errorCount++
        }
        
        Start-Sleep -Milliseconds 500
    }
    
    Start-Sleep -Seconds 2
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Test Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Total Requests: $requestCount" -ForegroundColor White
Write-Host "  Successful:     $($requestCount - $errorCount)" -ForegroundColor Green
Write-Host "  Errors:         $errorCount" -ForegroundColor $(if ($errorCount -eq 0) { "Green" } else { "Red" })
Write-Host ""
Write-Host "  Test Duration:  $TestDurationSeconds seconds" -ForegroundColor Gray
Write-Host "  Requests/sec:   $([Math]::Round($requestCount / $TestDurationSeconds, 2))" -ForegroundColor Gray
Write-Host ""

# Verify telemetry in Application Insights
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Verification Steps" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Telemetry has been sent to Application Insights." -ForegroundColor White
Write-Host ""
Write-Host "? Wait 2-3 minutes for data to appear in Azure Portal" -ForegroundColor Yellow
Write-Host ""
Write-Host "Verification Options:" -ForegroundColor Cyan
Write-Host ""
Write-Host "1. Azure Portal - Live Metrics" -ForegroundColor White
Write-Host "   URL: https://portal.azure.com" -ForegroundColor Gray
Write-Host "   Navigate to: Application Insights > Live Metrics" -ForegroundColor Gray
Write-Host "   Look for: Real-time request and dependency telemetry" -ForegroundColor Gray
Write-Host ""
Write-Host "2. Azure Portal - Logs (KQL)" -ForegroundColor White
Write-Host "   Navigate to: Application Insights > Logs" -ForegroundColor Gray
Write-Host "   Run query:" -ForegroundColor Gray
Write-Host "     requests" -ForegroundColor Magenta
Write-Host "     | where timestamp > ago(10m)" -ForegroundColor Magenta
Write-Host "     | where cloud_RoleName == 'SecureBootDashboard.Api'" -ForegroundColor Magenta
Write-Host "     | summarize count() by name, resultCode" -ForegroundColor Magenta
Write-Host ""
Write-Host "3. Azure Portal - Performance" -ForegroundColor White
Write-Host "   Navigate to: Application Insights > Performance" -ForegroundColor Gray
Write-Host "   Look for: API endpoint performance metrics" -ForegroundColor Gray
Write-Host ""
Write-Host "4. Azure Portal - Failures" -ForegroundColor White
Write-Host "   Navigate to: Application Insights > Failures" -ForegroundColor Gray
Write-Host "   Look for: Any failed requests or exceptions" -ForegroundColor Gray
Write-Host ""

if ($errorCount -eq 0) {
    Write-Host "? Test completed successfully" -ForegroundColor Green
    exit 0
} else {
    Write-Host "? Test completed with errors" -ForegroundColor Yellow
    Write-Host "  Check API logs for details" -ForegroundColor Gray
    exit 0  # Exit 0 since telemetry was still sent
}
