<#
.SYNOPSIS
    Tests API endpoints to verify they are responding correctly.

.DESCRIPTION
    Quick health check script to test key API endpoints.

.PARAMETER ApiBaseUrl
    Base URL of the API. Default is https://localhost:5001

.EXAMPLE
    .\Test-ApiEndpoints.ps1
    Tests API on localhost

.EXAMPLE
    .\Test-ApiEndpoints.ps1 -ApiBaseUrl "https://SRVCM00.MSINTUNE.LAB:5001"
    Tests API on remote server
#>

[CmdletBinding()]
param(
    [Parameter()]
    [string]$ApiBaseUrl = "https://localhost:5001"
)

$ErrorActionPreference = 'Continue'

function Test-Endpoint {
    param(
        [string]$Url,
        [string]$Description
    )
    
    Write-Host "`n? Testing: $Description" -ForegroundColor Yellow
    Write-Host "   URL: $Url" -ForegroundColor Gray
    
    try {
        $response = Invoke-WebRequest -Uri $Url -Method Get -SkipCertificateCheck -ErrorAction Stop
        
        if ($response.StatusCode -eq 200) {
            Write-Host "   ? SUCCESS (200 OK)" -ForegroundColor Green
            
            # Try to parse JSON
            try {
                $json = $response.Content | ConvertFrom-Json
                Write-Host "   Response preview:" -ForegroundColor Gray
                Write-Host "   $($json | ConvertTo-Json -Depth 2 -Compress)" -ForegroundColor DarkGray
            } catch {
                Write-Host "   Response: $($response.Content.Substring(0, [Math]::Min(200, $response.Content.Length)))..." -ForegroundColor DarkGray
            }
            
            return $true
        } else {
            Write-Host "   ? FAILED (Status: $($response.StatusCode))" -ForegroundColor Red
            return $false
        }
    } catch {
        Write-Host "   ? ERROR: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

Write-Host "=" * 80 -ForegroundColor Cyan
Write-Host "  API Endpoint Tests" -ForegroundColor Cyan
Write-Host "=" * 80 -ForegroundColor Cyan
Write-Host "`nBase URL: $ApiBaseUrl" -ForegroundColor White

$results = @{
    Total = 0
    Passed = 0
    Failed = 0
}

# Test endpoints
$endpoints = @(
    @{ Url = "$ApiBaseUrl/health"; Description = "Health Check" },
    @{ Url = "$ApiBaseUrl/api/Devices"; Description = "Devices List" },
    @{ Url = "$ApiBaseUrl/api/CommandManagement/statistics"; Description = "Command Statistics ?? NEW" },
    @{ Url = "$ApiBaseUrl/api/WindowsVersion/versions"; Description = "Windows Versions ?? NEW" },
    @{ Url = "$ApiBaseUrl/api/SecureBootReports/recent?limit=5"; Description = "Recent Reports" }
)

foreach ($endpoint in $endpoints) {
    $results.Total++
    $success = Test-Endpoint -Url $endpoint.Url -Description $endpoint.Description
    if ($success) {
        $results.Passed++
    } else {
        $results.Failed++
    }
}

# Summary
Write-Host "`n" + ("=" * 80) -ForegroundColor Cyan
Write-Host "  Test Summary" -ForegroundColor Cyan
Write-Host ("=" * 80) -ForegroundColor Cyan

Write-Host "`nResults:" -ForegroundColor White
Write-Host "   Total Tests: $($results.Total)" -ForegroundColor White
Write-Host "   Passed: $($results.Passed)" -ForegroundColor Green
Write-Host "   Failed: $($results.Failed)" -ForegroundColor $(if ($results.Failed -gt 0) { 'Red' } else { 'White' })

if ($results.Failed -eq 0) {
    Write-Host "`n? All tests passed!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "`n? Some tests failed. Check output above." -ForegroundColor Red
    exit 1
}
