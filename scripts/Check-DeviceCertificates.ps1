<#
.SYNOPSIS
    Checks certificate data for a specific device.

.DESCRIPTION
    Queries the database to retrieve and analyze certificate data for a specific device.

.PARAMETER DeviceName
    The name of the device to check (e.g., "robgrame-P16V")

.EXAMPLE
    .\Check-DeviceCertificates.ps1 -DeviceName "robgrame-P16V"
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$DeviceName,
    
    [Parameter()]
    [string]$SqlServer = "SRVSQL",
    
    [Parameter()]
    [string]$Database = "SecureBootDashboard"
)

Write-Host "======================================" -ForegroundColor Cyan
Write-Host "Device Certificate Analysis" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "Device: $DeviceName" -ForegroundColor White
Write-Host ""

$connectionString = "Server=$SqlServer;Database=$Database;Trusted_Connection=True;TrustServerCertificate=True"
$connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)

try {
    $connection.Open()
    Write-Host "? Connected to database" -ForegroundColor Green
    Write-Host ""
    
    # Get device information
    $deviceQuery = @"
SELECT 
    Id,
    MachineName,
    DomainName,
    Manufacturer,
    Model,
    FirmwareVersion,
    UEFISecureBootEnabled,
    CreatedAtUtc,
    LastSeenUtc
FROM Devices
WHERE MachineName LIKE @DeviceName
"@
    
    $cmd = New-Object System.Data.SqlClient.SqlCommand($deviceQuery, $connection)
    $cmd.Parameters.AddWithValue("@DeviceName", "%$DeviceName%") | Out-Null
    $reader = $cmd.ExecuteReader()
    
    if (-not $reader.Read()) {
        Write-Host "? Device '$DeviceName' not found in database" -ForegroundColor Red
        $reader.Close()
        return
    }
    
    $deviceId = $reader["Id"]
    $machineName = $reader["MachineName"]
    $secureBootEnabled = $reader["UEFISecureBootEnabled"]
    
    Write-Host "Device Information:" -ForegroundColor Cyan
    Write-Host "  Machine Name: $machineName" -ForegroundColor White
    Write-Host "  Domain: $($reader['DomainName'])" -ForegroundColor White
    Write-Host "  Manufacturer: $($reader['Manufacturer'])" -ForegroundColor White
    Write-Host "  Model: $($reader['Model'])" -ForegroundColor White
    Write-Host "  Firmware: $($reader['FirmwareVersion'])" -ForegroundColor White
    Write-Host "  Secure Boot Enabled: $secureBootEnabled" -ForegroundColor $(if ($secureBootEnabled -eq 1) { "Green" } else { "Yellow" })
    Write-Host "  First Seen: $($reader['CreatedAtUtc'])" -ForegroundColor Gray
    Write-Host "  Last Seen: $($reader['LastSeenUtc'])" -ForegroundColor Gray
    Write-Host ""
    
    $reader.Close()
    
    # Get report count
    $countQuery = @"
SELECT 
    COUNT(*) as TotalReports,
    SUM(CASE WHEN CertificatesJson IS NOT NULL AND LEN(CertificatesJson) > 0 THEN 1 ELSE 0 END) as WithCertificates,
    MAX(CreatedAtUtc) as LatestReport
FROM SecureBootReports
WHERE DeviceId = @DeviceId
"@
    
    $cmd = New-Object System.Data.SqlClient.SqlCommand($countQuery, $connection)
    $cmd.Parameters.AddWithValue("@DeviceId", $deviceId) | Out-Null
    $reader = $cmd.ExecuteReader()
    
    if ($reader.Read()) {
        $totalReports = $reader["TotalReports"]
        $withCerts = $reader["WithCertificates"]
        $latestReport = $reader["LatestReport"]
        
        Write-Host "Report Summary:" -ForegroundColor Cyan
        Write-Host "  Total Reports: $totalReports" -ForegroundColor White
        Write-Host "  With Certificates: $withCerts" -ForegroundColor $(if ($withCerts -gt 0) { "Green" } else { "Yellow" })
        Write-Host "  Latest Report: $latestReport" -ForegroundColor White
        Write-Host ""
    }
    
    $reader.Close()
    
    # Get most recent report with certificates
    $reportQuery = @"
SELECT TOP 1
    r.Id,
    r.CreatedAtUtc,
    r.DeploymentState,
    r.CertificatesJson,
    LEN(r.CertificatesJson) as CertJsonLength
FROM SecureBootReports r
WHERE r.DeviceId = @DeviceId
  AND r.CertificatesJson IS NOT NULL 
  AND LEN(r.CertificatesJson) > 0
ORDER BY r.CreatedAtUtc DESC
"@
    
    $cmd = New-Object System.Data.SqlClient.SqlCommand($reportQuery, $connection)
    $cmd.Parameters.AddWithValue("@DeviceId", $deviceId) | Out-Null
    $reader = $cmd.ExecuteReader()
    
    if (-not $reader.Read()) {
        Write-Host "? No reports with certificate data found for this device" -ForegroundColor Red
        $reader.Close()
        return
    }
    
    $reportId = $reader["Id"]
    $createdAt = $reader["CreatedAtUtc"]
    $deploymentState = $reader["DeploymentState"]
    $certJson = $reader["CertificatesJson"]
    $certJsonLength = $reader["CertJsonLength"]
    
    Write-Host "Latest Report with Certificates:" -ForegroundColor Cyan
    Write-Host "  Report ID: $reportId" -ForegroundColor White
    Write-Host "  Created: $createdAt" -ForegroundColor White
    Write-Host "  Deployment State: $deploymentState" -ForegroundColor White
    Write-Host "  Certificate JSON Length: $certJsonLength characters" -ForegroundColor White
    Write-Host ""
    
    $reader.Close()
    
    # Parse certificate data
    try {
        $certs = $certJson | ConvertFrom-Json
        
        Write-Host "======================================" -ForegroundColor Cyan
        Write-Host "Certificate Details" -ForegroundColor Cyan
        Write-Host "======================================" -ForegroundColor Cyan
        Write-Host ""
        
        Write-Host "Collection Information:" -ForegroundColor Yellow
        Write-Host "  Collected At: $($certs.collectedAtUtc)" -ForegroundColor White
        Write-Host "  Secure Boot Enabled: $($certs.secureBootEnabled)" -ForegroundColor $(if ($certs.secureBootEnabled) { "Green" } else { "Yellow" })
        Write-Host "  Total Certificates: $($certs.totalCertificateCount)" -ForegroundColor White
        Write-Host "  Expired: $($certs.expiredCertificateCount)" -ForegroundColor $(if ($certs.expiredCertificateCount -gt 0) { "Red" } else { "Green" })
        Write-Host "  Expiring Soon (90 days): $($certs.expiringCertificateCount)" -ForegroundColor $(if ($certs.expiringCertificateCount -gt 0) { "Yellow" } else { "Green" })
        
        if ($certs.errorMessage) {
            Write-Host "  Error: $($certs.errorMessage)" -ForegroundColor Red
        }
        
        Write-Host ""
        
        # Database breakdown
        Write-Host "Database Breakdown:" -ForegroundColor Yellow
        Write-Host "  Signature Database (db): $($certs.signatureDatabase.Count) certificates" -ForegroundColor Green
        Write-Host "  Forbidden Database (dbx): $($certs.forbiddenDatabase.Count) certificates" -ForegroundColor Yellow
        Write-Host "  Key Exchange Keys (KEK): $($certs.keyExchangeKeys.Count) certificates" -ForegroundColor Cyan
        Write-Host "  Platform Keys (PK): $($certs.platformKeys.Count) certificates" -ForegroundColor Magenta
        Write-Host ""
        
        # Show certificates expiring in 2026
        $allCerts = @()
        if ($certs.signatureDatabase) { $allCerts += $certs.signatureDatabase }
        if ($certs.forbiddenDatabase) { $allCerts += $certs.forbiddenDatabase }
        if ($certs.keyExchangeKeys) { $allCerts += $certs.keyExchangeKeys }
        if ($certs.platformKeys) { $allCerts += $certs.platformKeys }
        
        $certs2026 = @($allCerts | Where-Object {
            $_.notAfter -and ([datetime]$_.notAfter).Year -eq 2026
        })
        
        if ($certs2026.Count -gt 0) {
            Write-Host "Certificates Expiring in 2026:" -ForegroundColor Yellow
            Write-Host "  Count: $($certs2026.Count)" -ForegroundColor White
            Write-Host ""
            
            foreach ($cert in $certs2026) {
                $expiryDate = ([datetime]$cert.notAfter).ToString("yyyy-MM-dd")
                $daysLeft = $cert.daysUntilExpiration
                
                Write-Host "  • [$($cert.database)] $($cert.subject)" -ForegroundColor White
                Write-Host "    Expires: $expiryDate ($daysLeft days)" -ForegroundColor $(if ($daysLeft -lt 90) { "Red" } elseif ($daysLeft -lt 180) { "Yellow" } else { "Green" })
                Write-Host "    Issuer: $($cert.issuer)" -ForegroundColor Gray
                Write-Host "    Microsoft: $($cert.isMicrosoftCertificate)" -ForegroundColor Gray
                Write-Host ""
            }
        } else {
            Write-Host "? No certificates expiring in 2026" -ForegroundColor Green
            Write-Host ""
        }
        
        # Show all db certificates
        if ($certs.signatureDatabase -and $certs.signatureDatabase.Count -gt 0) {
            Write-Host "======================================" -ForegroundColor Cyan
            Write-Host "Signature Database (db) - Authorized Certificates" -ForegroundColor Cyan
            Write-Host "======================================" -ForegroundColor Cyan
            Write-Host ""
            
            foreach ($cert in $certs.signatureDatabase) {
                $expiryDate = if ($cert.notAfter) { ([datetime]$cert.notAfter).ToString("yyyy-MM-dd") } else { "N/A" }
                $daysLeft = $cert.daysUntilExpiration
                
                $statusColor = "Green"
                $statusText = "Valid"
                if ($cert.isExpired) {
                    $statusColor = "Red"
                    $statusText = "EXPIRED"
                } elseif ($daysLeft -lt 90) {
                    $statusColor = "Red"
                    $statusText = "Expiring Soon"
                } elseif ($daysLeft -lt 180) {
                    $statusColor = "Yellow"
                    $statusText = "Attention"
                }
                
                Write-Host "  Subject: $($cert.subject)" -ForegroundColor White
                Write-Host "  Issuer: $($cert.issuer)" -ForegroundColor Gray
                Write-Host "  Thumbprint: $($cert.thumbprint)" -ForegroundColor Gray
                Write-Host "  Valid: $($cert.notBefore -replace 'T.*', '') to $expiryDate" -ForegroundColor Gray
                Write-Host "  Status: $statusText ($daysLeft days)" -ForegroundColor $statusColor
                Write-Host "  Algorithm: $($cert.signatureAlgorithm) / $($cert.publicKeyAlgorithm) ($($cert.keySize)-bit)" -ForegroundColor Gray
                Write-Host "  Microsoft: $($cert.isMicrosoftCertificate)" -ForegroundColor $(if ($cert.isMicrosoftCertificate) { "Cyan" } else { "Gray" })
                Write-Host ""
            }
        }
        
        # Show KEK certificates
        if ($certs.keyExchangeKeys -and $certs.keyExchangeKeys.Count -gt 0) {
            Write-Host "======================================" -ForegroundColor Cyan
            Write-Host "Key Exchange Keys (KEK)" -ForegroundColor Cyan
            Write-Host "======================================" -ForegroundColor Cyan
            Write-Host ""
            
            foreach ($cert in $certs.keyExchangeKeys) {
                $expiryDate = if ($cert.notAfter) { ([datetime]$cert.notAfter).ToString("yyyy-MM-dd") } else { "N/A" }
                $daysLeft = $cert.daysUntilExpiration
                
                Write-Host "  Subject: $($cert.subject)" -ForegroundColor White
                Write-Host "  Expires: $expiryDate ($daysLeft days)" -ForegroundColor $(if ($daysLeft -lt 90) { "Red" } elseif ($daysLeft -lt 180) { "Yellow" } else { "Green" })
                Write-Host "  Microsoft: $($cert.isMicrosoftCertificate)" -ForegroundColor $(if ($cert.isMicrosoftCertificate) { "Cyan" } else { "Gray" })
                Write-Host ""
            }
        }
        
        # Show PK certificates
        if ($certs.platformKeys -and $certs.platformKeys.Count -gt 0) {
            Write-Host "======================================" -ForegroundColor Cyan
            Write-Host "Platform Key (PK)" -ForegroundColor Cyan
            Write-Host "======================================" -ForegroundColor Cyan
            Write-Host ""
            
            foreach ($cert in $certs.platformKeys) {
                $expiryDate = if ($cert.notAfter) { ([datetime]$cert.notAfter).ToString("yyyy-MM-dd") } else { "N/A" }
                $daysLeft = $cert.daysUntilExpiration
                
                Write-Host "  Subject: $($cert.subject)" -ForegroundColor White
                Write-Host "  Expires: $expiryDate ($daysLeft days)" -ForegroundColor $(if ($daysLeft -lt 90) { "Red" } elseif ($daysLeft -lt 180) { "Yellow" } else { "Green" })
                Write-Host "  Microsoft: $($cert.isMicrosoftCertificate)" -ForegroundColor $(if ($cert.isMicrosoftCertificate) { "Cyan" } else { "Gray" })
                Write-Host ""
            }
        }
        
        # Dashboard URL
        Write-Host "======================================" -ForegroundColor Cyan
        Write-Host "View in Dashboard" -ForegroundColor Cyan
        Write-Host "======================================" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "Certificate Details Page:" -ForegroundColor Yellow
        Write-Host "  https://localhost:7001/Certificates/Details/$reportId" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "Device Details Page:" -ForegroundColor Yellow
        Write-Host "  https://localhost:7001/Devices/Details/$deviceId" -ForegroundColor Cyan
        Write-Host ""
        
    } catch {
        Write-Host "? Failed to parse certificate JSON: $_" -ForegroundColor Red
    }
    
} catch {
    Write-Host "? Error: $_" -ForegroundColor Red
    Write-Host $_.Exception.StackTrace -ForegroundColor Gray
} finally {
    if ($connection.State -eq 'Open') {
        $connection.Close()
    }
}

Write-Host "======================================" -ForegroundColor Cyan
Write-Host "Analysis Complete" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
