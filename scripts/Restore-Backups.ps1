<#
.SYNOPSIS
    Restores backup files created by Translate-ToEnglish.ps1.

.DESCRIPTION
    This script restores .bak files created during translation,
    useful if the translation introduced errors.

.EXAMPLE
    .\Restore-Backups.ps1
#>

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Restore Translation Backups" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Get project root
$scriptPath = $PSScriptRoot
$projectRoot = Split-Path $scriptPath
$webProjectPath = Join-Path $projectRoot "SecureBootDashboard.Web"

if (-not (Test-Path $webProjectPath)) {
    Write-Host "? Web project not found at: $webProjectPath" -ForegroundColor Red
    exit 1
}

# Find all backup files
$backupFiles = Get-ChildItem -Path $webProjectPath -Filter "*.bak" -Recurse |
    Where-Object { $_.FullName -notlike "*\obj\*" -and $_.FullName -notlike "*\bin\*" }

if ($backupFiles.Count -eq 0) {
    Write-Host "No backup files found." -ForegroundColor Yellow
    Write-Host "Translation has not been run or backups were already restored/deleted." -ForegroundColor Yellow
    exit 0
}

Write-Host "Found $($backupFiles.Count) backup files" -ForegroundColor Cyan
Write-Host ""

$restored = 0

foreach ($backup in $backupFiles) {
    $originalPath = $backup.FullName -replace '\.bak$', ''
    $relativePath = $originalPath.Substring($projectRoot.Length + 1)
    
    if (Test-Path $originalPath) {
        Write-Host "  Restoring: $relativePath" -ForegroundColor Green
        Copy-Item $backup.FullName $originalPath -Force
        Remove-Item $backup.FullName -Force
        $restored++
    } else {
        Write-Host "  ? Original file not found: $relativePath" -ForegroundColor Yellow
        Write-Host "    Backup file remains: $($backup.FullName)" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Restore Complete" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Files Restored: $restored" -ForegroundColor Green
Write-Host ""

if ($restored -gt 0) {
    Write-Host "All backup files have been restored." -ForegroundColor Green
    Write-Host "You can now review the original (Italian) files." -ForegroundColor Yellow
}

Write-Host ""

