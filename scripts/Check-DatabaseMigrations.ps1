# Quick DB Migration Check
$connectionString = "Server=SRVSQL;Database=SecureBootDashboard;Integrated Security=True;TrustServerCertificate=True"

try {
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $connection.Open()
    
    $command = $connection.CreateCommand()
    $command.CommandText = @"
        -- Check if migrations table exists
        IF OBJECT_ID('__EFMigrationsHistory', 'U') IS NOT NULL
        BEGIN
            SELECT TOP 10 
                MigrationId,
                ProductVersion,
                CASE 
                    WHEN MigrationId LIKE '%AddOperatingSystemInfo%' THEN '? OS Info'
                    WHEN MigrationId LIKE '%AddChassisTypes%' THEN '? Chassis'
                    WHEN MigrationId LIKE '%AddVirtualMachineDetection%' THEN '? VM Detection'
                    ELSE MigrationId
                END AS Description
            FROM __EFMigrationsHistory
            ORDER BY MigrationId DESC
        END
        ELSE
        BEGIN
            SELECT 'Migrations table not found' AS Error
        END
"@
    
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($command)
    $dataset = New-Object System.Data.DataSet
    $adapter.Fill($dataset) | Out-Null
    
    Write-Host "`n?? Database Migration Status:" -ForegroundColor Cyan
    Write-Host "================================" -ForegroundColor Cyan
    $dataset.Tables[0] | Format-Table -AutoSize
    
    $connection.Close()
    
    # Check if new columns exist
    $connection.Open()
    $command.CommandText = @"
        SELECT 
            COLUMN_NAME,
            DATA_TYPE,
            IS_NULLABLE,
            CASE 
                WHEN COLUMN_NAME IN ('OperatingSystem', 'OSVersion', 'OSProductType') THEN '? Migration 1'
                WHEN COLUMN_NAME = 'ChassisTypesJson' THEN '? Migration 2'
                WHEN COLUMN_NAME IN ('IsVirtualMachine', 'VirtualizationPlatform') THEN '? Migration 3'
                ELSE ''
            END AS FromMigration
        FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_NAME = 'Devices'
            AND COLUMN_NAME IN (
                'OperatingSystem', 
                'OSVersion', 
                'OSProductType', 
                'ChassisTypesJson',
                'IsVirtualMachine',
                'VirtualizationPlatform'
            )
        ORDER BY COLUMN_NAME
"@
    
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($command)
    $dataset2 = New-Object System.Data.DataSet
    $adapter.Fill($dataset2) | Out-Null
    
    Write-Host "`n??? New Columns in Devices Table:" -ForegroundColor Green
    Write-Host "===================================" -ForegroundColor Green
    
    if ($dataset2.Tables[0].Rows.Count -eq 0) {
        Write-Host "? NO NEW COLUMNS FOUND - Migrations NOT applied!" -ForegroundColor Red
    } else {
        $dataset2.Tables[0] | Format-Table -AutoSize
        Write-Host "`n? Found $($dataset2.Tables[0].Rows.Count) new columns" -ForegroundColor Green
    }
    
    $connection.Close()
    
} catch {
    Write-Host "? Error connecting to database: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "`nConnection String: $connectionString" -ForegroundColor Yellow
}
