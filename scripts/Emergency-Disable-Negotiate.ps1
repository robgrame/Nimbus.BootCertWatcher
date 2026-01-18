# ===============================================================================
# Emergency-Disable-Negotiate.ps1
#
# EMERGENCY: Temporarily disable Negotiate handler to use IIS Windows Auth
# ===============================================================================

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$ProjectPath = "C:\Users\nefario\source\repos\robgrame\Nimbus.BootCertWatcher\SecureBootDashboard.Web"
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "===============================================================================" -ForegroundColor Red
Write-Host "EMERGENCY - Disable Negotiate Handler" -ForegroundColor Red
Write-Host "===============================================================================" -ForegroundColor Red
Write-Host ""

Write-Host "This will temporarily comment out AddNegotiate() to use IIS Windows Auth" -ForegroundColor Yellow
Write-Host ""

$programCs = Join-Path $ProjectPath "Program.cs"

if (-not (Test-Path $programCs)) {
    Write-Host "? Program.cs not found at: $programCs" -ForegroundColor Red
    exit 1
}

# Backup
$backup = "$programCs.backup_$(Get-Date -Format 'yyyyMMddHHmmss')"
Copy-Item $programCs $backup
Write-Host "? Backup created: $(Split-Path $backup -Leaf)" -ForegroundColor Green

# Read content
$content = Get-Content $programCs -Raw

# Check if already modified
if ($content -match "// EMERGENCY: Negotiate disabled") {
    Write-Host "? File already modified" -ForegroundColor Yellow
    Write-Host "Restoring from backup..." -ForegroundColor Yellow
    
    # Find most recent backup
    $latestBackup = Get-ChildItem "$programCs.backup_*" |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    
    if ($latestBackup) {
        Copy-Item $latestBackup.FullName $programCs -Force
        Write-Host "? Restored from: $($latestBackup.Name)" -ForegroundColor Green
    }
}

# Modify: Comment out AddNegotiate and replace with IIS Windows Auth
$modifiedContent = $content -replace `
    '(?s)(else if \(string\.Equals\(authProvider, "Windows".*?\{.*?)(\s*builder\.Services\.AddAuthentication.*?\.AddNegotiate\(\);.*?)(Log\.Information\("Windows authentication configured"\);)', `
    '$1
        // EMERGENCY: Negotiate handler disabled to avoid conflict with IIS Windows Auth
        // Original code:
        // builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.Negotiate.NegotiateDefaults.AuthenticationScheme)
        //     .AddNegotiate();
        
        // USE IIS WINDOWS AUTH INSTEAD
        builder.Services.AddAuthentication(Microsoft.AspNetCore.Server.IISIntegration.IISDefaults.AuthenticationScheme);
        
        $3'

if ($modifiedContent -ne $content) {
    Set-Content -Path $programCs -Value $modifiedContent -Encoding UTF8
    Write-Host "? Program.cs modified" -ForegroundColor Green
    Write-Host "  - Commented out AddNegotiate()" -ForegroundColor Gray
    Write-Host "  - Added IIS Windows Auth integration" -ForegroundColor Gray
} else {
    Write-Host "? No changes made - pattern not found" -ForegroundColor Yellow
    Write-Host "  This might mean the code structure has changed" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Rebuild the project:" -ForegroundColor White
Write-Host "     dotnet build '$ProjectPath\SecureBootDashboard.Web.csproj' -c Release" -ForegroundColor Cyan
Write-Host ""
Write-Host "  2. Republish to IIS:" -ForegroundColor White
Write-Host "     Copy new binaries to C:\inetpub\SecureBootDashboard.Web" -ForegroundColor Cyan
Write-Host ""
Write-Host "  3. Enable Windows Auth in IIS:" -ForegroundColor White
Write-Host "     - Open IIS Manager" -ForegroundColor Cyan
Write-Host "     - Site ? Authentication ? Enable 'Windows Authentication'" -ForegroundColor Cyan
Write-Host "     - Disable 'Anonymous Authentication'" -ForegroundColor Cyan
Write-Host ""
Write-Host "  4. Restart App Pool:" -ForegroundColor White
Write-Host "     Restart-WebAppPool 'SecureBootDashboard.Web'" -ForegroundColor Cyan
Write-Host ""

