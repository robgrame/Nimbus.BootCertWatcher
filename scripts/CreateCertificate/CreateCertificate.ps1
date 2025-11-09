# 1. Create self-signed certificate
$certSubject = "CN=SecureBootWatcher"
$cert = New-SelfSignedCertificate `
    -Subject $certSubject `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -KeyExportPolicy Exportable `
    -KeySpec Signature `
    -KeyLength 2048 `
    -KeyAlgorithm RSA `
    -HashAlgorithm SHA256 `
    -NotAfter (Get-Date).AddYears(2)

Write-Host "Certificate created with thumbprint: $($cert.Thumbprint)" -ForegroundColor Green

# 2. Export to PFX
$pfxPassword = ConvertTo-SecureString -String "apDkUa.cJgC2LRiyxbHEX4MufeN9FbJ" -AsPlainText -Force
Export-PfxCertificate `
    -Cert $cert `
    -FilePath ".\SecureBootWatcher.pfx" `
    -Password $pfxPassword

# 3. Upload public key to Azure App Registration
$cerPath = ".\SecureBootWatcher.cer"
Export-Certificate -Cert $cert -FilePath $cerPath

Write-Host "Upload $cerPath to Azure App Registration > Certificates & secrets" -ForegroundColor Yellow