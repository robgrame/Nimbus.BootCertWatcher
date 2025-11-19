<#
.SYNOPSIS
    Tests API endpoint and web page for a specific report.

.DESCRIPTION
    Checks if the API returns certificate data correctly and if the web page can access it.

.PARAMETER ReportId
    The GUID of the report to check

.EXAMPLE
    .\Test-ReportEndpoint.ps1 -ReportId "20ba6ace-890f-4c0c-8213-eff6457f5c6d"
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$ReportId,
    
    [Parameter()]
    [string]$ApiBaseUrl = "https://localhost:5001",
    
    [Parameter()]
    [string]$WebBaseUrl = "https://localhost:7001"
)

Write-Host "======================================" -ForegroundColor Cyan
Write-Host "Report Endpoint Testing" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "Report ID: $ReportId" -ForegroundColor White
Write-Host ""

# Disable SSL certificate validation for localhost testing
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}

# Test 1: Check database directly
Write-Host "Test 1: Database Direct Query" -ForegroundColor Yellow
Write-Host "------------------------------" -ForegroundColor Yellow

try {
    $connectionString = "Server=SRVSQL;Database=SecureBootDashboard;Trusted_Connection=True;TrustServerCertificate=True"
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $connection.Open()
    
    $query = @"
SELECT 
    r.Id,
    r.CreatedAtUtc,
    d.MachineName,
    LEN(r.CertificatesJson) as CertLength,
    CASE 
        WHEN r.CertificatesJson IS NULL THEN 'NULL'
        WHEN LEN(r.CertificatesJson) = 0 THEN 'EMPTY'
        ELSE 'PRESENT'
    END as CertStatus
FROM SecureBootReports r
INNER JOIN Devices d ON r.DeviceId = d.Id
WHERE r.Id = @ReportId
"@
    
    $cmd = New-Object System.Data.SqlClient.SqlCommand($query, $connection)
    $cmd.Parameters.AddWithValue("@ReportId", $ReportId) | Out-Null
    $reader = $cmd.ExecuteReader()
    
    if ($reader.Read()) {
        Write-Host "? Report found in database" -ForegroundColor Green
        Write-Host "  Device: $($reader['MachineName'])" -ForegroundColor White
        Write-Host "  Created: $($reader['CreatedAtUtc'])" -ForegroundColor White
        Write-Host "  Certificate Status: $($reader['CertStatus'])" -ForegroundColor $(if ($reader['CertStatus'] -eq 'PRESENT') { "Green" } else { "Red" })
        Write-Host "  Certificate JSON Length: $($reader['CertLength']) chars" -ForegroundColor White
    } else {
        Write-Host "? Report NOT found in database" -ForegroundColor Red
        $reader.Close()
        $connection.Close()
        return
    }
    
    $reader.Close()
    $connection.Close()
    
} catch {
    Write-Host "? Database error: $_" -ForegroundColor Red
}

Write-Host ""

# Test 2: Check API endpoint
Write-Host "Test 2: API Endpoint" -ForegroundColor Yellow
Write-Host "---------------------" -ForegroundColor Yellow

$apiUrl = "$ApiBaseUrl/api/SecureBootReports/$ReportId"
Write-Host "URL: $apiUrl" -ForegroundColor Gray

try {
    $response = Invoke-WebRequest -Uri $apiUrl -Method Get -UseBasicParsing -ErrorAction Stop
    
    if ($response.StatusCode -eq 200) {
        Write-Host "? API returned 200 OK" -ForegroundColor Green
        
        $json = $response.Content | ConvertFrom-Json
        
        Write-Host ""
        Write-Host "Response Structure:" -ForegroundColor Cyan
        Write-Host "  Properties:" -ForegroundColor White
        $json.PSObject.Properties.Name | ForEach-Object {
            Write-Host "    - $_" -ForegroundColor Gray
        }
        
        Write-Host ""
        Write-Host "Certificate Data Check:" -ForegroundColor Cyan
        
        if ($json.certificatesJson) {
            Write-Host "  ? certificatesJson field is PRESENT" -ForegroundColor Green
            Write-Host "  Length: $($json.certificatesJson.Length) characters" -ForegroundColor White
            
            # Try to parse it
            try {
                $certs = $json.certificatesJson | ConvertFrom-Json
                
                Write-Host ""
                Write-Host "  Certificate Summary:" -ForegroundColor Cyan
                Write-Host "    Secure Boot Enabled: $($certs.secureBootEnabled)" -ForegroundColor White
                Write-Host "    Total Certificates: $($certs.totalCertificateCount)" -ForegroundColor White
                Write-Host "    Signature DB: $($certs.signatureDatabase.Count)" -ForegroundColor Green
                Write-Host "    Forbidden DB: $($certs.forbiddenDatabase.Count)" -ForegroundColor Yellow
                Write-Host "    KEK: $($certs.keyExchangeKeys.Count)" -ForegroundColor Cyan
                Write-Host "    PK: $($certs.platformKeys.Count)" -ForegroundColor Magenta
                
            } catch {
                Write-Host "  ? Failed to parse certificatesJson: $_" -ForegroundColor Red
            }
            
        } elseif ($null -eq $json.certificatesJson) {
            Write-Host "  ? certificatesJson field is NULL" -ForegroundColor Red
        } else {
            Write-Host "  ? certificatesJson field is EMPTY STRING" -ForegroundColor Red
        }
        
        # Show device info
        if ($json.device) {
            Write-Host ""
            Write-Host "Device Information:" -ForegroundColor Cyan
            Write-Host "  Machine Name: $($json.device.machineName)" -ForegroundColor White
            Write-Host "  Domain: $($json.device.domainName)" -ForegroundColor White
        }
        
    } else {
        Write-Host "? API returned status code: $($response.StatusCode)" -ForegroundColor Red
    }
    
} catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    Write-Host "? API request failed" -ForegroundColor Red
    Write-Host "  Status Code: $statusCode" -ForegroundColor Gray
    Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Gray
    
    if ($statusCode -eq 404) {
        Write-Host "  ? Report not found via API" -ForegroundColor Yellow
    } elseif ($statusCode -eq 500) {
        Write-Host "  ? Internal server error" -ForegroundColor Yellow
    }
}

Write-Host ""

# Test 3: Check if Web page route works
Write-Host "Test 3: Web Page Routes" -ForegroundColor Yellow
Write-Host "------------------------" -ForegroundColor Yellow

$webUrls = @(
    "$WebBaseUrl/Certificates/Details/$ReportId",
    "$WebBaseUrl/certificates/$ReportId"
)

foreach ($url in $webUrls) {
    Write-Host "Testing: $url" -ForegroundColor Gray
    
    try {
        $response = Invoke-WebRequest -Uri $url -Method Get -UseBasicParsing -ErrorAction Stop
        
        if ($response.StatusCode -eq 200) {
            Write-Host "  ? Page accessible (200 OK)" -ForegroundColor Green
            
            # Check if page contains certificate data
            if ($response.Content -match "Certificate Details|Secure Boot Certificates") {
                Write-Host "  ? Page contains certificate-related content" -ForegroundColor Green
            } else {
                Write-Host "  ! Page may not be showing certificates" -ForegroundColor Yellow
            }
            
            # Check for "No Certificate Data" message
            if ($response.Content -match "No Certificate Data|Certificate enumeration data is not available") {
                Write-Host "  ? Page shows 'No Certificate Data' message" -ForegroundColor Red
            }
            
            # Check for certificate count
            if ($response.Content -match "(\d+)\s+certificate") {
                Write-Host "  ? Page shows certificate count" -ForegroundColor Green
            }
            
        } else {
            Write-Host "  ! Status: $($response.StatusCode)" -ForegroundColor Yellow
        }
        
    } catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        
        if ($statusCode -eq 404) {
            Write-Host "  ? Page not found (404)" -ForegroundColor Red
        } elseif ($statusCode -eq 302 -or $statusCode -eq 301) {
            Write-Host "  ? Redirect ($statusCode)" -ForegroundColor Yellow
            $location = $_.Exception.Response.Headers["Location"]
            if ($location) {
                Write-Host "    Location: $location" -ForegroundColor Gray
            }
        } else {
            Write-Host "  ? Error: $statusCode - $($_.Exception.Message)" -ForegroundColor Red
        }
    }
    
    Write-Host ""
}

# Test 4: Check Razor Page code-behind
Write-Host "Test 4: Razor Page Code Analysis" -ForegroundColor Yellow
Write-Host "---------------------------------" -ForegroundColor Yellow

$detailsPagePath = "SecureBootDashboard.Web\Pages\Certificates\Details.cshtml.cs"

if (Test-Path $detailsPagePath) {
    Write-Host "? Details.cshtml.cs found" -ForegroundColor Green
    
    $content = Get-Content $detailsPagePath -Raw
    
    # Check for key methods and properties
    $checks = @{
        "OnGetAsync method" = $content -match "public async Task<IActionResult> OnGetAsync"
        "reportId parameter" = $content -match "string reportId"
        "GetReportAsync call" = $content -match "GetReportAsync\(reportGuid\)"
        "Certificates property" = $content -match "Certificates\s*="
    }
    
    Write-Host ""
    Write-Host "Code Structure:" -ForegroundColor Cyan
    foreach ($check in $checks.GetEnumerator()) {
        $status = if ($check.Value) { "?" } else { "?" }
        $color = if ($check.Value) { "Green" } else { "Red" }
        Write-Host "  $status $($check.Key)" -ForegroundColor $color
    }
    
} else {
    Write-Host "? Details.cshtml.cs not found at: $detailsPagePath" -ForegroundColor Red
}

Write-Host ""

# Summary
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "Summary & Recommendations" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "Direct URL to test in browser:" -ForegroundColor Yellow
Write-Host "  $WebBaseUrl/Certificates/Details/$ReportId" -ForegroundColor Cyan
Write-Host ""

Write-Host "Alternative URL (lowercase):" -ForegroundColor Yellow
Write-Host "  $WebBaseUrl/certificates/$ReportId" -ForegroundColor Cyan
Write-Host ""

Write-Host "If page shows 'No Certificate Data':" -ForegroundColor Yellow
Write-Host "  1. Check if API is running and accessible" -ForegroundColor White
Write-Host "  2. Verify SecureBootApiClient is calling correct endpoint" -ForegroundColor White
Write-Host "  3. Check browser console for JavaScript errors" -ForegroundColor White
Write-Host "  4. Verify deserialization in Details.cshtml.cs" -ForegroundColor White
Write-Host ""

Write-Host "Debugging steps:" -ForegroundColor Yellow
Write-Host "  1. Check Web application logs for errors" -ForegroundColor White
Write-Host "  2. Add breakpoint in Details.cshtml.cs OnGetAsync" -ForegroundColor White
Write-Host "  3. Verify report.Certificates is not null" -ForegroundColor White
Write-Host "  4. Check Model.Certificates in Details.cshtml" -ForegroundColor White
Write-Host ""
