<#
.SYNOPSIS
    Examines the raw certificate JSON structure from database.

.PARAMETER ReportId
    The GUID of the report to examine
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$ReportId = "20ba6ace-890f-4c0c-8213-eff6457f5c6d"
)

Write-Host "Examining Certificate JSON Structure" -ForegroundColor Cyan
Write-Host "Report ID: $ReportId" -ForegroundColor White
Write-Host ""

$connectionString = "Server=SRVSQL;Database=SecureBootDashboard;Trusted_Connection=True;TrustServerCertificate=True"
$connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)

try {
    $connection.Open()
    
    $query = "SELECT CertificatesJson FROM SecureBootReports WHERE Id = @ReportId"
    $cmd = New-Object System.Data.SqlClient.SqlCommand($query, $connection)
    $cmd.Parameters.AddWithValue("@ReportId", $ReportId) | Out-Null
    
    $reader = $cmd.ExecuteReader()
    
    if ($reader.Read()) {
        $json = $reader["CertificatesJson"]
        $reader.Close()
        
        Write-Host "Raw JSON (first 1000 chars):" -ForegroundColor Yellow
        Write-Host $json.Substring(0, [Math]::Min(1000, $json.Length)) -ForegroundColor Gray
        Write-Host ""
        
        # Parse and analyze structure
        $certs = $json | ConvertFrom-Json
        
        Write-Host "JSON Structure Analysis:" -ForegroundColor Cyan
        Write-Host "------------------------" -ForegroundColor Cyan
        
        Write-Host "`nTop-level properties:" -ForegroundColor Yellow
        $certs.PSObject.Properties.Name | ForEach-Object {
            $propName = $_
            $propValue = $certs.$propName
            $propType = if ($propValue -is [array]) { "Array[$($propValue.Count)]" } else { $propValue.GetType().Name }
            
            Write-Host "  - $propName : $propType" -ForegroundColor White
        }
        
        Write-Host "`nProperty Details:" -ForegroundColor Yellow
        Write-Host "  secureBootEnabled: $($certs.secureBootEnabled)" -ForegroundColor $(if ($certs.secureBootEnabled) { "Green" } else { "Red" })
        Write-Host "  totalCertificateCount: $($certs.totalCertificateCount)" -ForegroundColor White
        Write-Host "  expiredCertificateCount: $($certs.expiredCertificateCount)" -ForegroundColor White
        Write-Host "  expiringCertificateCount: $($certs.expiringCertificateCount)" -ForegroundColor White
        Write-Host "  collectedAtUtc: $($certs.collectedAtUtc)" -ForegroundColor White
        
        if ($certs.errorMessage) {
            Write-Host "  errorMessage: $($certs.errorMessage)" -ForegroundColor Red
        }
        
        Write-Host "`nDatabase Collections:" -ForegroundColor Yellow
        
        # Check each database with exact property names from JSON
        $databases = @(
            @{Name="signatureDatabase"; DisplayName="Signature Database (db)"},
            @{Name="forbiddenDatabase"; DisplayName="Forbidden Database (dbx)"},
            @{Name="keyExchangeKeys"; DisplayName="Key Exchange Keys (KEK)"},
            @{Name="platformKeys"; DisplayName="Platform Keys (PK)"}
        )
        
        foreach ($db in $databases) {
            $dbName = $db.Name
            $dbDisplay = $db.DisplayName
            
            if ($certs.PSObject.Properties.Name -contains $dbName) {
                $count = if ($certs.$dbName) { $certs.$dbName.Count } else { 0 }
                Write-Host "  $dbDisplay ($dbName): $count certificates" -ForegroundColor $(if ($count -gt 0) { "Green" } else { "Yellow" })
                
                if ($count -gt 0 -and $certs.$dbName[0]) {
                    Write-Host "    Sample cert properties:" -ForegroundColor Gray
                    $certs.$dbName[0].PSObject.Properties.Name | Select-Object -First 5 | ForEach-Object {
                        Write-Host "      - $_" -ForegroundColor DarkGray
                    }
                }
            } else {
                Write-Host "  $dbDisplay ($dbName): PROPERTY NOT FOUND" -ForegroundColor Red
            }
        }
        
        # Check for property name casing issues
        Write-Host "`nProperty Name Casing Check:" -ForegroundColor Yellow
        $expectedProps = @(
            @{Expected="signatureDatabase"; PascalCase="SignatureDatabase"},
            @{Expected="forbiddenDatabase"; PascalCase="ForbiddenDatabase"},
            @{Expected="keyExchangeKeys"; PascalCase="KeyExchangeKeys"},
            @{Expected="platformKeys"; PascalCase="PlatformKeys"},
            @{Expected="secureBootEnabled"; PascalCase="SecureBootEnabled"},
            @{Expected="totalCertificateCount"; PascalCase="TotalCertificateCount"}
        )
        
        foreach ($prop in $expectedProps) {
            $camelExists = $certs.PSObject.Properties.Name -contains $prop.Expected
            $pascalExists = $certs.PSObject.Properties.Name -contains $prop.PascalCase
            
            if ($camelExists) {
                Write-Host "  ? $($prop.Expected) (camelCase) - FOUND" -ForegroundColor Green
            } elseif ($pascalExists) {
                Write-Host "  ! $($prop.PascalCase) (PascalCase) - FOUND (should be camelCase)" -ForegroundColor Yellow
            } else {
                Write-Host "  ? $($prop.Expected) - NOT FOUND" -ForegroundColor Red
            }
        }
        
        Write-Host ""
        Write-Host "DIAGNOSIS:" -ForegroundColor Cyan
        Write-Host "----------" -ForegroundColor Cyan
        
        $dbCount = if ($certs.signatureDatabase) { $certs.signatureDatabase.Count } else { 0 }
        $dbxCount = if ($certs.forbiddenDatabase) { $certs.forbiddenDatabase.Count } else { 0 }
        $kekCount = if ($certs.keyExchangeKeys) { $certs.keyExchangeKeys.Count } else { 0 }
        $pkCount = if ($certs.platformKeys) { $certs.platformKeys.Count } else { 0 }
        
        $actualCount = $dbCount + $dbxCount + $kekCount + $pkCount
        
        if ($actualCount -eq 0) {
            Write-Host "? Certificate collections are EMPTY in JSON" -ForegroundColor Red
            Write-Host "   This suggests a serialization/deserialization mismatch" -ForegroundColor Yellow
            Write-Host ""
            Write-Host "   Possible causes:" -ForegroundColor Yellow
            Write-Host "   1. Property name casing (camelCase vs PascalCase)" -ForegroundColor White
            Write-Host "   2. JSON serializer options not matching" -ForegroundColor White
            Write-Host "   3. Data was saved before collections were populated" -ForegroundColor White
        } else {
            Write-Host "? Certificate data is present in JSON" -ForegroundColor Green
            Write-Host "  Total certificates found: $actualCount (db:$dbCount, dbx:$dbxCount, KEK:$kekCount, PK:$pkCount)" -ForegroundColor Green
            Write-Host "  If the dashboard shows 0 certificates, check the C# model property names" -ForegroundColor Yellow
        }
        
    } else {
        Write-Host "Report not found" -ForegroundColor Red
    }
    
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
    Write-Host $_.Exception.StackTrace -ForegroundColor Gray
} finally {
    if ($connection.State -eq 'Open') {
        $connection.Close()
    }
}
