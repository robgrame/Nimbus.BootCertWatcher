# ===============================================================================
# Configure-Dashboard-Authorization.ps1
#
# Configure authorization for SecureBootDashboard.Web
# ===============================================================================

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$SettingsPath = "C:\inetpub\SecureBootDashboard.Web\appsettings.Production.json",
    
    [Parameter(Mandatory = $false)]
    [string[]]$AllowedUsers = @(),
    
    [Parameter(Mandatory = $false)]
    [string]$RequiredGroup = ""
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host "Configure Dashboard Authorization" -ForegroundColor Cyan
Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host ""

if (-not (Test-Path $SettingsPath)) {
    Write-Host "? appsettings.Production.json not found!" -ForegroundColor Red
    exit 1
}

# Read current settings
$settings = Get-Content $SettingsPath -Raw | ConvertFrom-Json

# Add or update Authorization section
if (-not $settings.PSObject.Properties["Authorization"]) {
    $settings | Add-Member -MemberType NoteProperty -Name "Authorization" -Value ([PSCustomObject]@{})
}

# Configure allowed users
if ($AllowedUsers.Count -gt 0) {
    Write-Host "Configuring allowed users:" -ForegroundColor Yellow
    $AllowedUsers | ForEach-Object { Write-Host "  • $_" -ForegroundColor White }
    
    $settings.Authorization | Add-Member -MemberType NoteProperty -Name "AllowedUsers" -Value $AllowedUsers -Force
}

# Configure required group
if ($RequiredGroup) {
    Write-Host "`nConfiguring required group:" -ForegroundColor Yellow
    Write-Host "  • $RequiredGroup" -ForegroundColor White
    
    $settings.Authorization | Add-Member -MemberType NoteProperty -Name "RequiredGroup" -Value $RequiredGroup -Force
}

# Backup original
$backup = "$SettingsPath.backup_$(Get-Date -Format 'yyyyMMddHHmmss')"
Copy-Item $SettingsPath $backup
Write-Host "`nBackup created: $(Split-Path $backup -Leaf)" -ForegroundColor Gray

# Save updated settings
$settings | ConvertTo-Json -Depth 10 | Set-Content $SettingsPath -Encoding UTF8
Write-Host "? Configuration updated" -ForegroundColor Green

Write-Host "`nCurrent Authorization Configuration:" -ForegroundColor Cyan
$settings.Authorization | ConvertTo-Json -Depth 5

Write-Host ""
Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host "NEXT STEPS" -ForegroundColor Cyan
Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "1. Modify Program.cs to use the configuration:" -ForegroundColor Yellow
Write-Host "   See: SecureBootDashboard.Web\Program.cs around line 179" -ForegroundColor Gray
Write-Host ""
Write-Host "2. Rebuild and redeploy:" -ForegroundColor Yellow
Write-Host "   dotnet build -c Release" -ForegroundColor Cyan
Write-Host "   Copy binaries to C:\inetpub\SecureBootDashboard.Web" -ForegroundColor Cyan
Write-Host ""
Write-Host "3. Restart App Pool:" -ForegroundColor Yellow
Write-Host "   Restart-WebAppPool 'SecureBootDashboard.Web'" -ForegroundColor Cyan
Write-Host ""

