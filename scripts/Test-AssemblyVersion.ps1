# Test-AssemblyVersion.ps1
# Verifica che la versione dell'assembly venga letta correttamente

param(
    [string]$AssemblyPath = ".\SecureBootDashboard.Web\bin\Debug\net10.0\SecureBootDashboard.Web.dll"
)

Write-Host "Testing Assembly Version Retrieval" -ForegroundColor Cyan
Write-Host "=" * 60

# Check if assembly exists
if (-not (Test-Path $AssemblyPath)) {
    Write-Host "Assembly not found at: $AssemblyPath" -ForegroundColor Red
    Write-Host "Building project first..." -ForegroundColor Yellow
    
    dotnet build SecureBootDashboard.Web\SecureBootDashboard.Web.csproj -c Debug
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed!" -ForegroundColor Red
        exit 1
    }
}

# Load assembly
Write-Host "`nLoading assembly: $AssemblyPath" -ForegroundColor Yellow
try {
    $assembly = [System.Reflection.Assembly]::LoadFrom($AssemblyPath)
    Write-Host "? Assembly loaded successfully" -ForegroundColor Green
} catch {
    Write-Host "? Failed to load assembly: $_" -ForegroundColor Red
    exit 1
}

# Get InformationalVersion attribute
Write-Host "`nRetrieving version information..." -ForegroundColor Yellow

$informationalVersionAttr = $assembly.GetCustomAttributes(
    [System.Reflection.AssemblyInformationalVersionAttribute], 
    $false
)

if ($informationalVersionAttr.Count -gt 0) {
    $fullVersion = $informationalVersionAttr[0].InformationalVersion
    Write-Host "? InformationalVersion: $fullVersion" -ForegroundColor Green
} else {
    $fullVersion = $assembly.GetName().Version.ToString()
    Write-Host "? InformationalVersion not found, using AssemblyVersion: $fullVersion" -ForegroundColor Yellow
}

# Parse short version (same logic as _Layout.cshtml)
Write-Host "`nParsing short version..." -ForegroundColor Yellow

$shortVersion = $fullVersion

# Remove commit hash (everything after '+')
if ($shortVersion.Contains('+')) {
    $shortVersion = $shortVersion.Substring(0, $shortVersion.IndexOf('+'))
    Write-Host "  - Removed commit hash" -ForegroundColor Gray
}

# Remove pre-release suffix (everything after '-')
if ($shortVersion.Contains('-')) {
    $shortVersion = $shortVersion.Substring(0, $shortVersion.IndexOf('-'))
    Write-Host "  - Removed pre-release suffix" -ForegroundColor Gray
}

# Ensure not empty
if ([string]::IsNullOrWhiteSpace($shortVersion)) {
    $shortVersion = "1.0.0"
    Write-Host "  - Used fallback version" -ForegroundColor Gray
}

# Display results
Write-Host "`nVersion Information:" -ForegroundColor Cyan
Write-Host "=" * 60
Write-Host "Full Version:       $fullVersion" -ForegroundColor White
Write-Host "Short Version:      $shortVersion" -ForegroundColor Green
Write-Host "Display in Footer:  v$shortVersion" -ForegroundColor Cyan

# Additional assembly info
Write-Host "`nAdditional Assembly Information:" -ForegroundColor Cyan
Write-Host "=" * 60
Write-Host "Assembly Name:      $($assembly.GetName().Name)" -ForegroundColor White
Write-Host "Assembly Version:   $($assembly.GetName().Version)" -ForegroundColor White

$fileVersionAttr = $assembly.GetCustomAttributes(
    [System.Reflection.AssemblyFileVersionAttribute], 
    $false
)
if ($fileVersionAttr.Count -gt 0) {
    Write-Host "File Version:       $($fileVersionAttr[0].Version)" -ForegroundColor White
}

$productVersionAttr = $assembly.GetCustomAttributes(
    [System.Reflection.AssemblyProductAttribute], 
    $false
)
if ($productVersionAttr.Count -gt 0) {
    Write-Host "Product:            $($productVersionAttr[0].Product)" -ForegroundColor White
}

# Test version.json
Write-Host "`nChecking version.json..." -ForegroundColor Cyan
$versionJsonPath = ".\version.json"
if (Test-Path $versionJsonPath) {
    $versionJson = Get-Content $versionJsonPath | ConvertFrom-Json
    Write-Host "Base Version:       $($versionJson.version)" -ForegroundColor White
} else {
    Write-Host "? version.json not found" -ForegroundColor Yellow
}

Write-Host "`n" -ForegroundColor White
Write-Host "? Test completed successfully!" -ForegroundColor Green
