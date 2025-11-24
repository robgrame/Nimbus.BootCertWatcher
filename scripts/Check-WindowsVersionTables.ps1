# Check-WindowsVersionTables.ps1
# Verifies that WindowsVersion tables were created successfully

[CmdletBinding()]
param(
    [Parameter()]
    [string]$ServerInstance = "SRVSQL",
    
    [Parameter()]
    [string]$Database = "SecureBootDashboard"
)

Write-Host "?? Checking Windows Version tables in database..." -ForegroundColor Cyan
Write-Host "Server: $ServerInstance" -ForegroundColor Gray
Write-Host "Database: $Database" -ForegroundColor Gray
Write-Host ""

# Connection string
$connectionString = "Server=$ServerInstance;Database=$Database;Integrated Security=True;TrustServerCertificate=True;"

function Invoke-SqlQuery {
    param([string]$Query)
    
    try {
        $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
        $connection.Open()
        
        $command = $connection.CreateCommand()
        $command.CommandText = $Query
        
        $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($command)
        $dataset = New-Object System.Data.DataSet
        $adapter.Fill($dataset) | Out-Null
        
        $connection.Close()
        
        return $dataset.Tables[0]
    } catch {
        Write-Host "? SQL Error: $_" -ForegroundColor Red
        throw
    }
}

# Query to check if tables exist
$query = @"
SELECT 
    OBJECT_NAME(object_id) AS TableName,
    create_date AS CreatedDate,
    modify_date AS ModifiedDate
FROM sys.tables
WHERE name IN ('WindowsVersions', 'WindowsBuilds')
ORDER BY name;
"@

try {
    $tables = Invoke-SqlQuery -Query $query
    
    if ($tables.Rows.Count -eq 2) {
        Write-Host "? Both tables found!" -ForegroundColor Green
        Write-Host ""
        
        foreach ($row in $tables.Rows) {
            Write-Host "?? Table: $($row.TableName)" -ForegroundColor Yellow
            Write-Host "   Created: $($row.CreatedDate)" -ForegroundColor Gray
            Write-Host "   Modified: $($row.ModifiedDate)" -ForegroundColor Gray
            Write-Host ""
        }
        
        # Check columns for WindowsVersions
        Write-Host "?? Columns in WindowsVersions:" -ForegroundColor Cyan
        $columnsQuery = @"
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH,
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'WindowsVersions'
ORDER BY ORDINAL_POSITION;
"@
        $columns = Invoke-SqlQuery -Query $columnsQuery
        $columns | Format-Table -AutoSize
        
        # Check columns for WindowsBuilds
        Write-Host "?? Columns in WindowsBuilds:" -ForegroundColor Cyan
        $columnsQuery = @"
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH,
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'WindowsBuilds'
ORDER BY ORDINAL_POSITION;
"@
        $columns = Invoke-SqlQuery -Query $columnsQuery
        $columns | Format-Table -AutoSize
        
        # Check indexes
        Write-Host "?? Indexes on WindowsVersions and WindowsBuilds:" -ForegroundColor Cyan
        $indexQuery = @"
SELECT 
    t.name AS TableName,
    i.name AS IndexName,
    i.is_unique AS IsUnique,
    i.type_desc AS IndexType
FROM sys.indexes i
INNER JOIN sys.tables t ON i.object_id = t.object_id
WHERE t.name IN ('WindowsVersions', 'WindowsBuilds')
    AND i.name IS NOT NULL
ORDER BY t.name, i.name;
"@
        $indexes = Invoke-SqlQuery -Query $indexQuery
        $indexes | Format-Table -AutoSize
        
        Write-Host "? Migration verified successfully!" -ForegroundColor Green
        
    } else {
        Write-Host "? Expected 2 tables, found $($tables.Rows.Count)" -ForegroundColor Red
        Write-Host "Tables found:" -ForegroundColor Yellow
        $tables | Format-Table -AutoSize
    }
    
} catch {
    Write-Host "? Error checking tables: $_" -ForegroundColor Red
    Write-Host "Stack trace: $($_.ScriptStackTrace)" -ForegroundColor Gray
    exit 1
}

Write-Host ""
Write-Host "? Verification complete!" -ForegroundColor Green
