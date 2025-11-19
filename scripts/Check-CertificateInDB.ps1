# Check certificate data in database
$connectionString = "Server=SRVSQL;Database=SecureBootDashboard;Trusted_Connection=True;TrustServerCertificate=True"
$connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)

try {
    $connection.Open()
    Write-Host "Connected to database successfully" -ForegroundColor Green
    
    # Get latest report with certificates
    $query = @"
SELECT TOP 1 
    r.Id,
    r.CreatedAtUtc,
    d.MachineName,
    LEN(r.CertificatesJson) as CertLength,
    r.CertificatesJson
FROM SecureBootReports r
INNER JOIN Devices d ON r.DeviceId = d.Id
WHERE r.CertificatesJson IS NOT NULL AND LEN(r.CertificatesJson) > 0
ORDER BY r.CreatedAtUtc DESC
"@
    
    $command = New-Object System.Data.SqlClient.SqlCommand($query, $connection)
    $reader = $command.ExecuteReader()
    
    if ($reader.Read()) {
        Write-Host "`nMost Recent Report:" -ForegroundColor Cyan
        Write-Host "  Report ID: $($reader['Id'])" -ForegroundColor White
        Write-Host "  Device: $($reader['MachineName'])" -ForegroundColor White
        Write-Host "  Created: $($reader['CreatedAtUtc'])" -ForegroundColor White
        Write-Host "  JSON Length: $($reader['CertLength']) characters" -ForegroundColor White
        
        $certJson = $reader['CertificatesJson']
        $certs = $certJson | ConvertFrom-Json
        
        Write-Host "`nCertificate Summary:" -ForegroundColor Cyan
        Write-Host "  Secure Boot Enabled: $($certs.secureBootEnabled)" -ForegroundColor White
        Write-Host "  Total Certificates: $($certs.totalCertificateCount)" -ForegroundColor White
        Write-Host "  Signature Database (db): $($certs.signatureDatabase.Count)" -ForegroundColor Green
        Write-Host "  Forbidden Database (dbx): $($certs.forbiddenDatabase.Count)" -ForegroundColor Yellow
        Write-Host "  Key Exchange Keys (KEK): $($certs.keyExchangeKeys.Count)" -ForegroundColor Cyan
        Write-Host "  Platform Keys (PK): $($certs.platformKeys.Count)" -ForegroundColor Magenta
        Write-Host "  Expired: $($certs.expiredCertificateCount)" -ForegroundColor Red
        Write-Host "  Expiring Soon (90 days): $($certs.expiringCertificateCount)" -ForegroundColor Yellow
        
        if ($certs.signatureDatabase.Count -gt 0) {
            Write-Host "`nSample Certificate from db:" -ForegroundColor Cyan
            $sample = $certs.signatureDatabase[0]
            Write-Host "  Subject: $($sample.subject)" -ForegroundColor White
            Write-Host "  Issuer: $($sample.issuer)" -ForegroundColor White
            Write-Host "  Not After: $($sample.notAfter)" -ForegroundColor White
            Write-Host "  Days Until Expiration: $($sample.daysUntilExpiration)" -ForegroundColor White
            Write-Host "  Is Microsoft: $($sample.isMicrosoftCertificate)" -ForegroundColor White
        }
        
        # Check for certificates expiring in 2026
        $certs2026 = @($certs.signatureDatabase + $certs.keyExchangeKeys + $certs.platformKeys | Where-Object {
            $_.notAfter -and ([datetime]$_.notAfter).Year -eq 2026
        })
        
        if ($certs2026.Count -gt 0) {
            Write-Host "`nCertificates expiring in 2026: $($certs2026.Count)" -ForegroundColor Yellow
            $certs2026 | ForEach-Object {
                Write-Host "  - $($_.subject) (expires: $(([datetime]$_.notAfter).ToString('yyyy-MM-dd')))" -ForegroundColor White
            }
        } else {
            Write-Host "`nNo certificates expiring in 2026 found" -ForegroundColor Green
        }
    } else {
        Write-Host "No reports with certificate data found" -ForegroundColor Red
    }
    
    $reader.Close()
    
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
} finally {
    if ($connection.State -eq 'Open') {
        $connection.Close()
    }
}
