<#
.SYNOPSIS
    Applies EF Core migrations to the remote SQL Server database.

.DESCRIPTION
    This script runs 'dotnet ef database update' to apply pending migrations
    to the SQL Server database on the remote server.

.PARAMETER Configuration
    Build configuration (Debug or Release). Default is Release.

.PARAMETER ProjectPath
    Path to the API project. Default is SecureBootDashboard.Api.

.PARAMETER Verbose
    Show detailed migration output.

.EXAMPLE
    .\Update-RemoteDatabase.ps1
    Applies all pending migrations in Release mode.

.EXAMPLE
    .\Update-RemoteDatabase.ps1 -Configuration Debug -Verbose
    Applies migrations with detailed output.
#>

[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [Parameter()]
    [string]$ProjectPath = 'SecureBootDashboard.Api\SecureBootDashboard.Api.csproj',

    [Parameter()]
    [switch]$VerboseMode
)

$ErrorActionPreference = 'Stop'

function Write-Header {
    param([string]$Message)
    Write-Host "`n$('=' * 80)" -ForegroundColor Cyan
    Write-Host "  $Message" -ForegroundColor Cyan
    Write-Host "$('=' * 80)" -ForegroundColor Cyan
}

function Write-Step {
    param([string]$Message)
    Write-Host "`n? $Message" -ForegroundColor Yellow
}

function Write-Success {
    param([string]$Message)
    Write-Host "? $Message" -ForegroundColor Green
}

function Write-Error {
    param([string]$Message)
    Write-Host "? $Message" -ForegroundColor Red
}

try {
    Write-Header "Database Migration Update"

    # Verify we're in the solution root
    if (-not (Test-Path "SecureBootWatcher.sln")) {
        Write-Error "Solution file not found. Run this script from the solution root."
        exit 1
    }

    # Verify project exists
    if (-not (Test-Path $ProjectPath)) {
        Write-Error "Project not found: $ProjectPath"
        exit 1
    }

    Write-Host "`nConfiguration:" -ForegroundColor Cyan
    Write-Host "   Project: $ProjectPath" -ForegroundColor White
    Write-Host "   Build Configuration: $Configuration" -ForegroundColor White

    # Check if dotnet-ef tool is installed
    Write-Step "Checking for dotnet-ef tool..."
    
    $efToolCheck = dotnet tool list --global | Select-String "dotnet-ef"
    
    if (-not $efToolCheck) {
        Write-Host "   dotnet-ef tool not found. Installing..." -ForegroundColor Yellow
        dotnet tool install --global dotnet-ef
        Write-Success "dotnet-ef tool installed"
    } else {
        Write-Success "dotnet-ef tool is installed"
    }

    # List pending migrations
    Write-Step "Checking for pending migrations..."
    
    $migrationsArgs = @(
        'ef', 'migrations', 'list',
        '--project', $ProjectPath,
        '--configuration', $Configuration,
        '--no-build'
    )
    
    $migrationsList = & dotnet @migrationsArgs 2>&1
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to list migrations"
        Write-Host $migrationsList -ForegroundColor Red
        exit 1
    }
    
    Write-Host $migrationsList -ForegroundColor Gray

    # Apply migrations
    Write-Step "Applying database migrations..."
    
    $updateArgs = @(
        'ef', 'database', 'update',
        '--project', $ProjectPath,
        '--configuration', $Configuration
    )
    
    if ($VerboseMode) {
        $updateArgs += '--verbose'
    }
    
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $updateResult = & dotnet @updateArgs 2>&1
    $stopwatch.Stop()
    
    if ($LASTEXITCODE -eq 0) {
        Write-Success "Database migrations applied successfully in $($stopwatch.Elapsed.TotalSeconds.ToString('F2'))s"
        Write-Host $updateResult -ForegroundColor Gray
    } else {
        Write-Error "Failed to apply migrations"
        Write-Host $updateResult -ForegroundColor Red
        exit 1
    }

    # Verify tables exist
    Write-Step "Verifying database schema..."
    
    $sqlQuery = @"
SELECT TABLE_NAME 
FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_TYPE = 'BASE TABLE' 
  AND TABLE_NAME IN ('Devices', 'SecureBootReports', 'PendingCommands', 'WindowsVersions', 'ApplicationSettings', 'MutualTlsConfig', 'TrustedCertificateAuthorities', 'ClientSinkConfig', 'ApiConfiguration')
ORDER BY TABLE_NAME
"@

    Write-Host "`nExpected Tables:" -ForegroundColor Cyan
    Write-Host "   - Devices" -ForegroundColor White
    Write-Host "   - SecureBootReports" -ForegroundColor White
    Write-Host "   - SecureBootEvents" -ForegroundColor White
    Write-Host "   - PendingCommands ?? Required for Command Management" -ForegroundColor Yellow
    Write-Host "   - WindowsVersions" -ForegroundColor White
    Write-Host "   - WindowsBuilds" -ForegroundColor White
    Write-Host "   - DeviceCleanupConfig" -ForegroundColor White
    Write-Host "   - ApplicationSettings" -ForegroundColor White
    Write-Host "   - MutualTlsConfig" -ForegroundColor White
    Write-Host "   - TrustedCertificateAuthorities" -ForegroundColor White
    Write-Host "   - ClientSinkConfig" -ForegroundColor White
    Write-Host "   - ApiConfiguration" -ForegroundColor White

    Write-Header "Migration Complete"
    Write-Success "All database migrations have been applied successfully!"
    Write-Host "`nNext Steps:" -ForegroundColor Cyan
    Write-Host "   1. Verify the API can connect to the database" -ForegroundColor White
    Write-Host "   2. Test the /api/CommandManagement/statistics endpoint" -ForegroundColor White
    Write-Host "   3. Try accessing the Commands pages in the web dashboard" -ForegroundColor White
    
    exit 0

} catch {
    Write-Error "An unexpected error occurred: $_"
    Write-Host $_.ScriptStackTrace -ForegroundColor Red
    exit 1
}
