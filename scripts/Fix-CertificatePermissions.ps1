# Fix Certificate Private Key Permissions for IIS Application Pool
# Run this script as Administrator on the server

$thumbprint = "61FC110D5BABD61419B106862B304C2FFF57A262"
$appPoolIdentity = "IIS APPPOOL\SecureBootDashboard.Api"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Certificate Permission Fix Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 1. Find certificate
Write-Host "[1/4] Locating certificate..." -ForegroundColor Yellow
$cert = Get-ChildItem Cert:\LocalMachine\My | Where-Object {
    $_.Thumbprint -eq $thumbprint
}

if (-not $cert) {
    Write-Host "  ? Certificate not found with thumbprint: $thumbprint" -ForegroundColor Red
    Write-Host ""
    Write-Host "  Searching all certificates in LocalMachine\My:" -ForegroundColor Yellow
    Get-ChildItem Cert:\LocalMachine\My | ForEach-Object {
        Write-Host "     Thumbprint: $($_.Thumbprint)" -ForegroundColor Gray
        Write-Host "     Subject: $($_.Subject)" -ForegroundColor Gray
        Write-Host ""
    }
    exit 1
}

Write-Host "  ? Certificate found" -ForegroundColor Green
Write-Host "     Subject: $($cert.Subject)" -ForegroundColor Gray
Write-Host "     Valid until: $($cert.NotAfter)" -ForegroundColor Gray
Write-Host ""

# 2. Check private key
Write-Host "[2/4] Checking private key..." -ForegroundColor Yellow
if (-not $cert.HasPrivateKey) {
    Write-Host "  ? Certificate does NOT have a private key!" -ForegroundColor Red
    Write-Host ""
    Write-Host "  ACTION REQUIRED:" -ForegroundColor Yellow
    Write-Host "  1. Export certificate with private key (PFX format)" -ForegroundColor White
    Write-Host "  2. Re-import the PFX file with 'Mark this key as exportable' option" -ForegroundColor White
    exit 1
}

Write-Host "  ? Certificate has private key" -ForegroundColor Green
Write-Host ""

# 3. Get private key file path
Write-Host "[3/4] Locating private key file..." -ForegroundColor Yellow
try {
    $rsaCert = [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($cert)
    $keyName = $rsaCert.Key.UniqueName
    $keyPath = "C:\ProgramData\Microsoft\Crypto\Keys\$keyName"
    
    if (-not (Test-Path $keyPath)) {
        # Try RSA folder
        $keyPath = "C:\ProgramData\Microsoft\Crypto\RSA\MachineKeys\$keyName"
    }
    
    if (Test-Path $keyPath) {
        Write-Host "  ? Private key file found" -ForegroundColor Green
        Write-Host "     Path: $keyPath" -ForegroundColor Gray
    } else {
        Write-Host "  ? Private key file not found!" -ForegroundColor Red
        Write-Host "     Expected at: C:\ProgramData\Microsoft\Crypto\Keys\$keyName" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "  ? Error accessing private key: $_" -ForegroundColor Red
    exit 1
}
Write-Host ""

# 4. Grant permissions
Write-Host "[4/4] Granting permissions to Application Pool..." -ForegroundColor Yellow
Write-Host "     Identity: $appPoolIdentity" -ForegroundColor Gray

try {
    # Check current permissions
    $acl = Get-Acl $keyPath
    $hasAccess = $acl.Access | Where-Object {
        $_.IdentityReference.Value -eq $appPoolIdentity
    }
    
    if ($hasAccess) {
        Write-Host "  ??  Application Pool already has permissions:" -ForegroundColor Cyan
        foreach ($access in $hasAccess) {
            Write-Host "     - $($access.FileSystemRights)" -ForegroundColor Gray
        }
        Write-Host ""
        Write-Host "  Updating permissions to ensure Read access..." -ForegroundColor Yellow
    }
    
    # Grant Read permission
    $result = icacls $keyPath /grant "${appPoolIdentity}:(R)"
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  ? Permissions granted successfully!" -ForegroundColor Green
    } else {
        Write-Host "  ? Failed to grant permissions" -ForegroundColor Red
        Write-Host "     Error code: $LASTEXITCODE" -ForegroundColor Red
        exit 1
    }
    
    # Verify permissions
    Write-Host ""
    Write-Host "  Verifying permissions..." -ForegroundColor Yellow
    $aclAfter = Get-Acl $keyPath
    $hasAccessAfter = $aclAfter.Access | Where-Object {
        $_.IdentityReference.Value -eq $appPoolIdentity
    }
    
    if ($hasAccessAfter) {
        Write-Host "  ? Verification successful" -ForegroundColor Green
        Write-Host "     Permissions:" -ForegroundColor Gray
        foreach ($access in $hasAccessAfter) {
            Write-Host "     - $($access.IdentityReference): $($access.FileSystemRights)" -ForegroundColor Gray
        }
    }
    
} catch {
    Write-Host "  ? Error granting permissions: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  ? SUCCESS!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Certificate private key permissions have been configured." -ForegroundColor White
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Enable QueueProcessor in appsettings.Production.json" -ForegroundColor White
Write-Host "  2. Restart IIS: iisreset" -ForegroundColor White
Write-Host "  3. Check logs: Get-Content 'C:\path\to\api\logs\api-*.log' -Tail 50" -ForegroundColor White
Write-Host ""
Write-Host "If the issue persists, run the diagnostic script:" -ForegroundColor Yellow
Write-Host "  .\scripts\Diagnose-QueueCertificate.ps1" -ForegroundColor Cyan
Write-Host ""
