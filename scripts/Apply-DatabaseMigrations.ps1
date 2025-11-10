# Apply Database Migrations Script
# This script applies pending Entity Framework migrations to the SecureBootDashboard database

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$ProjectPath = "SecureBootDashboard.Api",
    
    [Parameter(Mandatory = $false)]
    [string]$ConnectionString = $null,
    
    [Parameter(Mandatory = $false)]
    [switch]$Force
)

$ErrorActionPreference = "Stop"

# Get script directory (repository root)
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir

Write-Host "======================================" -ForegroundColor Cyan
Write-Host "Database Migration Application Script" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Check if dotnet EF tools are installed
Write-Host "[1/5] Checking for dotnet-ef tools..." -ForegroundColor Yellow

$efToolsInstalled = dotnet tool list --global | Select-String "dotnet-ef"

if (-not $efToolsInstalled) {
    Write-Host "dotnet-ef tools not found. Installing..." -ForegroundColor Yellow
    dotnet tool install --global dotnet-ef
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to install dotnet-ef tools"
        exit 1
    }
    
    Write-Host "? dotnet-ef tools installed successfully" -ForegroundColor Green
} else {
    Write-Host "? dotnet-ef tools already installed" -ForegroundColor Green
}

Write-Host ""

# Step 2: Navigate to project directory
$projectFullPath = Join-Path $repoRoot $ProjectPath

if (-not (Test-Path $projectFullPath)) {
    Write-Error "Project directory not found: $projectFullPath"
    exit 1
}

Write-Host "[2/5] Project directory: $projectFullPath" -ForegroundColor Yellow
Write-Host ""

# Step 3: List pending migrations
Write-Host "[3/5] Checking for pending migrations..." -ForegroundColor Yellow

try {
    Push-Location $projectFullPath
    
    $migrationsList = dotnet ef migrations list 2>&1 | Out-String
    Write-Host $migrationsList
    
    if ($migrationsList -match "No migrations were found") {
        Write-Host "No migrations found. Database schema is up to date." -ForegroundColor Green
        Pop-Location
        exit 0
    }
    
    Write-Host ""
    
    # Step 4: Remove duplicate migration if exists
    Write-Host "[4/5] Checking for duplicate migrations..." -ForegroundColor Yellow
    
    $duplicateMigration = "20251109202105_AddClientVersionToDevice"
    $duplicatePath = Join-Path $projectFullPath "Data\Migrations\$duplicateMigration.cs"
    
    if (Test-Path $duplicatePath) {
        Write-Host "Found duplicate migration: $duplicateMigration" -ForegroundColor Yellow
        
        if ($Force) {
            Write-Host "Removing duplicate migration files..." -ForegroundColor Yellow
            Remove-Item "$duplicatePath" -Force
            Remove-Item "$duplicatePath.Designer.cs" -Force -ErrorAction SilentlyContinue
            Write-Host "? Duplicate migration removed" -ForegroundColor Green
        } else {
            Write-Host "WARNING: Duplicate migration found but -Force not specified" -ForegroundColor Red
            Write-Host "Run with -Force to automatically remove duplicate migration" -ForegroundColor Yellow
        }
    } else {
        Write-Host "? No duplicate migrations found" -ForegroundColor Green
    }
    
    Write-Host ""
    
    # Step 5: Apply migrations
    Write-Host "[5/5] Applying database migrations..." -ForegroundColor Yellow
    
    if ($ConnectionString) {
        Write-Host "Using provided connection string" -ForegroundColor Cyan
        dotnet ef database update --connection "$ConnectionString"
    } else {
        Write-Host "Using connection string from appsettings.json" -ForegroundColor Cyan
        dotnet ef database update
    }
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Migration failed with exit code $LASTEXITCODE"
        Pop-Location
        exit $LASTEXITCODE
    }
    
    Write-Host ""
    Write-Host "? Database migrations applied successfully!" -ForegroundColor Green
    
} catch {
    Write-Error "Error during migration: $_"
    Pop-Location
    exit 1
} finally {
    Pop-Location
}

Write-Host ""
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "Migration Complete" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
