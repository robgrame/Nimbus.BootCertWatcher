<#
.SYNOPSIS
    Tests certificate data collection and storage in SecureBootWatcher.

.DESCRIPTION
    This script checks:
    1. If the client can enumerate certificates via PowerShell
    2. If certificates are stored in the database
    3. If certificates are returned by the API

.EXAMPLE
    .\Test-CertificateData.ps1
    
.EXAMPLE
    .\Test-CertificateData.ps1 -ApiUrl "http://localhost:5000"
#>

param(
    [Parameter()]
    [string]$ApiUrl = "http://localhost:5000",
    
    [Parameter()]
    [string]$SqlServer = "SRVSQL",
    
    [Parameter()]
    [string]$Database = "SecureBootDashboard"
)

Write-Host "==================================" -ForegroundColor Cyan
Write-Host "Certificate Data Collection Test" -ForegroundColor Cyan
Write-Host "==================================" -ForegroundColor Cyan
Write-Host ""

# Test 1: Check if PowerShell can enumerate certificates
Write-Host "Test 1: PowerShell Certificate Enumeration" -ForegroundColor Yellow
Write-Host "--------------------------------------------" -ForegroundColor Yellow

try {
    $secureBootEnabled = Confirm-SecureBootUEFI
    Write-Host "? Secure Boot Enabled: $secureBootEnabled" -ForegroundColor Green
} catch {
    Write-Host "? Failed to check Secure Boot status: $_" -ForegroundColor Red
    $secureBootEnabled = $null
}

$databases = @("db", "dbx", "KEK", "PK")
$certificateCounts = @{}

foreach ($dbName in $databases) {
    try {
        $uefiData = Get-SecureBootUEFI -Name $dbName -ErrorAction Stop
        if ($uefiData -and $uefiData.Bytes) {
            $certificateCounts[$dbName] = $uefiData.Bytes.Length
            Write-Host "? $dbName : $($uefiData.Bytes.Length) bytes" -ForegroundColor Green
        } else {
            $certificateCounts[$dbName] = 0
            Write-Host "! $dbName : No data" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "? $dbName : Error - $_" -ForegroundColor Red
        $certificateCounts[$dbName] = -1
    }
}

Write-Host ""

# Test 2: Check database for certificate data
Write-Host "Test 2: Database Certificate Storage" -ForegroundColor Yellow
Write-Host "------------------------------------" -ForegroundColor Yellow

try {
    $connectionString = "Server=$SqlServer;Database=$Database;Trusted_Connection=True;TrustServerCertificate=True"
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $connection.Open()
    
    # Check if CertificatesJson column exists
    $checkColumnQuery = @"
SELECT COUNT(*) 
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'SecureBootReports' 
  AND COLUMN_NAME = 'CertificatesJson'
"@
    
    $cmd = New-Object System.Data.SqlClient.SqlCommand($checkColumnQuery, $connection)
    $columnExists = [int]$cmd.ExecuteScalar() -gt 0
    
    if ($columnExists) {
        Write-Host "? CertificatesJson column exists" -ForegroundColor Green
        
        # Count reports with certificate data
        $countQuery = @"
SELECT 
    COUNT(*) as TotalReports,
    SUM(CASE WHEN CertificatesJson IS NOT NULL AND LEN(CertificatesJson) > 0 THEN 1 ELSE 0 END) as WithCertificates,
    SUM(CASE WHEN CertificatesJson IS NULL OR LEN(CertificatesJson) = 0 THEN 1 ELSE 0 END) as WithoutCertificates
FROM SecureBootReports
"@
        
        $cmd = New-Object System.Data.SqlClient.SqlCommand($countQuery, $connection)
        $reader = $cmd.ExecuteReader()
        
        if ($reader.Read()) {
            $totalReports = $reader["TotalReports"]
            $withCerts = $reader["WithCertificates"]
            $withoutCerts = $reader["WithoutCertificates"]
            
            Write-Host "  Total Reports: $totalReports" -ForegroundColor Cyan
            Write-Host "  With Certificates: $withCerts" -ForegroundColor $(if ($withCerts -gt 0) { "Green" } else { "Yellow" })
            Write-Host "  Without Certificates: $withoutCerts" -ForegroundColor $(if ($withoutCerts -gt 0) { "Yellow" } else { "Green" })
            
            if ($withCerts -eq 0 -and $totalReports -gt 0) {
                Write-Host "! WARNING: No reports have certificate data!" -ForegroundColor Red
            }
        }
        $reader.Close()
        
        # Get most recent report with certificates
        $recentQuery = @"
SELECT TOP 1 
    Id, 
    CreatedAtUtc,
    LEN(CertificatesJson) as CertJsonLength,
    LEFT(CertificatesJson, 200) as CertJsonSample
FROM SecureBootReports
WHERE CertificatesJson IS NOT NULL AND LEN(CertificatesJson) > 0
ORDER BY CreatedAtUtc DESC
"@
        
        $cmd = New-Object System.Data.SqlClient.SqlCommand($recentQuery, $connection)
        $reader = $cmd.ExecuteReader()
        
        if ($reader.Read()) {
            Write-Host ""
            Write-Host "Most Recent Report with Certificates:" -ForegroundColor Cyan
            Write-Host "  Report ID: $($reader["Id"])" -ForegroundColor White
            Write-Host "  Created: $($reader["CreatedAtUtc"])" -ForegroundColor White
            Write-Host "  Certificate JSON Length: $($reader["CertJsonLength"]) characters" -ForegroundColor White
            Write-Host "  Sample: $($reader["CertJsonSample"])..." -ForegroundColor Gray
        } else {
            Write-Host "! No reports with certificate data found in database" -ForegroundColor Yellow
        }
        $reader.Close()
        
    } else {
        Write-Host "? CertificatesJson column does NOT exist!" -ForegroundColor Red
        Write-Host "  Run database migration: dotnet ef database update" -ForegroundColor Yellow
    }
    
    $connection.Close()
    
} catch {
    Write-Host "? Database query failed: $_" -ForegroundColor Red
}

Write-Host ""

# Test 3: Check API response
Write-Host "Test 3: API Certificate Data" -ForegroundColor Yellow
Write-Host "-----------------------------" -ForegroundColor Yellow

try {
    # Get most recent report
    $recentReportsUrl = "$ApiUrl/api/SecureBootReports/recent?limit=1"
    Write-Host "Fetching: $recentReportsUrl" -ForegroundColor Gray
    
    $recentReports = Invoke-RestMethod -Uri $recentReportsUrl -Method Get -ErrorAction Stop
    
    if ($recentReports -and $recentReports.Count -gt 0) {
        $reportId = $recentReports[0].id
        Write-Host "? Found recent report: $reportId" -ForegroundColor Green
        
        # Get full report details
        $reportUrl = "$ApiUrl/api/SecureBootReports/$reportId"
        Write-Host "Fetching: $reportUrl" -ForegroundColor Gray
        
        $report = Invoke-RestMethod -Uri $reportUrl -Method Get -ErrorAction Stop
        
        if ($report.certificatesJson) {
            Write-Host "? Report contains CertificatesJson" -ForegroundColor Green
            
            # Parse certificate data
            $certificates = $report.certificatesJson | ConvertFrom-Json
            
            Write-Host ""
            Write-Host "Certificate Summary:" -ForegroundColor Cyan
            Write-Host "  Secure Boot Enabled: $($certificates.secureBootEnabled)" -ForegroundColor White
            Write-Host "  Total Certificates: $($certificates.totalCertificateCount)" -ForegroundColor White
            Write-Host "  Signature Database (db): $($certificates.signatureDatabase.Count)" -ForegroundColor White
            Write-Host "  Forbidden Database (dbx): $($certificates.forbiddenDatabase.Count)" -ForegroundColor White
            Write-Host "  Key Exchange Keys (KEK): $($certificates.keyExchangeKeys.Count)" -ForegroundColor White
            Write-Host "  Platform Keys (PK): $($certificates.platformKeys.Count)" -ForegroundColor White
            Write-Host "  Expired: $($certificates.expiredCertificateCount)" -ForegroundColor $(if ($certificates.expiredCertificateCount -gt 0) { "Red" } else { "Green" })
            Write-Host "  Expiring Soon: $($certificates.expiringCertificateCount)" -ForegroundColor $(if ($certificates.expiringCertificateCount -gt 0) { "Yellow" } else { "Green" })
            
            if ($certificates.totalCertificateCount -eq 0) {
                Write-Host ""
                Write-Host "! WARNING: Certificate collection is empty!" -ForegroundColor Red
                Write-Host "  This could mean:" -ForegroundColor Yellow
                Write-Host "  - Client is not running with sufficient privileges (needs SYSTEM/Admin)" -ForegroundColor Yellow
                Write-Host "  - PowerShell cmdlet Get-SecureBootUEFI is not available" -ForegroundColor Yellow
                Write-Host "  - System is not UEFI" -ForegroundColor Yellow
            }
            
            # Show a sample certificate if available
            $allCerts = @()
            if ($certificates.signatureDatabase) { $allCerts += $certificates.signatureDatabase }
            if ($certificates.keyExchangeKeys) { $allCerts += $certificates.keyExchangeKeys }
            if ($certificates.platformKeys) { $allCerts += $certificates.platformKeys }
            
            if ($allCerts.Count -gt 0) {
                Write-Host ""
                Write-Host "Sample Certificate:" -ForegroundColor Cyan
                $sample = $allCerts[0]
                Write-Host "  Subject: $($sample.subject)" -ForegroundColor White
                Write-Host "  Issuer: $($sample.issuer)" -ForegroundColor White
                Write-Host "  Not After: $($sample.notAfter)" -ForegroundColor White
                Write-Host "  Days Until Expiration: $($sample.daysUntilExpiration)" -ForegroundColor White
                Write-Host "  Database: $($sample.database)" -ForegroundColor White
                Write-Host "  Microsoft Certificate: $($sample.isMicrosoftCertificate)" -ForegroundColor White
            }
            
        } else {
            Write-Host "? Report does NOT contain CertificatesJson" -ForegroundColor Red
            Write-Host "  Available fields:" -ForegroundColor Gray
            $report.PSObject.Properties.Name | ForEach-Object { Write-Host "    - $_" -ForegroundColor Gray }
        }
    } else {
        Write-Host "! No recent reports found" -ForegroundColor Yellow
    }
    
} catch {
    Write-Host "? API request failed: $_" -ForegroundColor Red
    Write-Host "  Make sure the API is running at: $ApiUrl" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "==================================" -ForegroundColor Cyan
Write-Host "Test Complete" -ForegroundColor Cyan
Write-Host "==================================" -ForegroundColor Cyan
