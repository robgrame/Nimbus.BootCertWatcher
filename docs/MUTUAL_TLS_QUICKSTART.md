# Mutual TLS Quick Start Guide

This guide provides a quick walkthrough to enable mutual TLS authentication in a test environment.

## Prerequisites
- Windows 10/11 or Windows Server 2016+ with PowerShell 5.0+
- SecureBootDashboard.Api running
- SecureBootWatcher.Client or SecureBootDashboard.Web to test

## Quick Setup (5 Minutes)

### Step 1: Generate Self-Signed Certificates

Open PowerShell as Administrator:

```powershell
# Create root CA certificate
$rootCert = New-SelfSignedCertificate `
    -Subject "CN=SecureBootWatcher Test CA" `
    -KeyExportPolicy Exportable `
    -KeyUsage CertSign,CRLSign,DigitalSignature `
    -KeyLength 4096 `
    -NotAfter (Get-Date).AddYears(5) `
    -CertStoreLocation "Cert:\LocalMachine\My" `
    -HashAlgorithm SHA256

# Export and install root CA to Trusted Root
Export-Certificate -Cert $rootCert -FilePath "$env:TEMP\test-root-ca.cer"
Import-Certificate -FilePath "$env:TEMP\test-root-ca.cer" -CertStoreLocation "Cert:\LocalMachine\Root"

Write-Host "Root CA Thumbprint: $($rootCert.Thumbprint)" -ForegroundColor Green

# Create client certificate
$clientCert = New-SelfSignedCertificate `
    -Subject "CN=SecureBootWatcher-TestClient" `
    -Signer $rootCert `
    -KeyExportPolicy Exportable `
    -KeyUsage DigitalSignature,KeyEncipherment `
    -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.2") `
    -KeyLength 2048 `
    -NotAfter (Get-Date).AddYears(2) `
    -CertStoreLocation "Cert:\LocalMachine\My" `
    -HashAlgorithm SHA256

Write-Host "Client Certificate Thumbprint: $($clientCert.Thumbprint)" -ForegroundColor Green
Write-Host "Save these thumbprints - you'll need them for configuration!" -ForegroundColor Yellow
```

### Step 2: Configure API

Edit `SecureBootDashboard.Api/appsettings.json`:

```json
{
  "MutualTls": {
    "Enabled": true,
    "AllowSelfSignedCertificates": true,
    "AllowedIssuers": ["SecureBootWatcher Test CA"],
    "CheckCertificateRevocation": false,
    "ValidateCertificateChain": true
  }
}
```

Restart the API and check logs for:
```
[INF] Mutual TLS is ENABLED
[INF]   Allow Self-Signed Certificates: True
[INF]   Allowed Issuers: 1 configured
[INF]     - SecureBootWatcher Test CA
```

### Step 3: Configure Client

Edit `SecureBootWatcher.Client/appsettings.json`:

```json
{
  "SecureBootWatcher": {
    "Sinks": {
      "EnableWebApi": true,
      "WebApi": {
        "BaseAddress": "https://localhost:5001",
        "IngestionRoute": "/api/SecureBootReports",
        "UseCertificateAuth": true,
        "CertificateThumbprint": "PASTE_CLIENT_THUMBPRINT_HERE",
        "CertificateStoreLocation": "LocalMachine",
        "CertificateStoreName": "My"
      }
    }
  }
}
```

Replace `PASTE_CLIENT_THUMBPRINT_HERE` with the client certificate thumbprint from Step 1.

### Step 4: Test Connection

Run the client:
```powershell
cd SecureBootWatcher.Client\bin\Debug\net48
.\SecureBootWatcher.Client.exe
```

Check client logs for:
```
[INF] Loading client certificate from store: {Thumbprint}
[INF] Client certificate loaded from store: Subject=CN=SecureBootWatcher-TestClient, Issuer=CN=SecureBootWatcher Test CA
[INF] Client certificate added to HttpClient handler
```

Check API logs for:
```
[DBG] Certificate validation requested for: CN=SecureBootWatcher-TestClient
[DBG] Certificate issuer validated: CN=SecureBootWatcher Test CA
[INF] Certificate validated successfully: Subject=CN=SecureBootWatcher-TestClient, ...
```

## Success Indicators

✅ **Client successfully connected if you see:**
- Client logs show certificate loaded
- API logs show certificate validated
- Report successfully submitted to API

❌ **Connection failed if you see:**
- Client: "The SSL connection could not be established"
- API: "Certificate authentication failed"
- Check certificate configuration and thumbprints

## Troubleshooting

### Certificate not found
```powershell
# Verify certificate exists
Get-ChildItem -Path "Cert:\LocalMachine\My" | Where-Object {
    $_.Subject -like "*SecureBootWatcher*"
} | Format-List Subject, Thumbprint, HasPrivateKey
```

### Access denied to private key
```powershell
# Grant NETWORK SERVICE access (if running as service)
$cert = Get-ChildItem -Path "Cert:\LocalMachine\My" | Where-Object {
    $_.Thumbprint -eq "YOUR_THUMBPRINT"
}
$rsaCert = [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($cert)
$fileName = $rsaCert.Key.UniqueName
$path = "$env:ALLUSERSPROFILE\Microsoft\Crypto\Keys\$fileName"
$permissions = Get-Acl -Path $path
$rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
    "NETWORK SERVICE", "Read", "Allow"
)
$permissions.AddAccessRule($rule)
Set-Acl -Path $path -AclObject $permissions
```

### SSL/TLS errors
- Ensure API is using HTTPS (not HTTP)
- Verify API certificate is trusted
- Check that TLS 1.2+ is enabled

## Clean Up

To remove test certificates:

```powershell
# Remove client certificate
Get-ChildItem -Path "Cert:\LocalMachine\My" | Where-Object {
    $_.Subject -like "*SecureBootWatcher-TestClient*"
} | Remove-Item

# Remove root CA from My (Personal)
Get-ChildItem -Path "Cert:\LocalMachine\My" | Where-Object {
    $_.Subject -like "*SecureBootWatcher Test CA*"
} | Remove-Item

# Remove root CA from Trusted Root
Get-ChildItem -Path "Cert:\LocalMachine\Root" | Where-Object {
    $_.Subject -like "*SecureBootWatcher Test CA*"
} | Remove-Item

Write-Host "Test certificates removed" -ForegroundColor Green
```

## Next Steps

For production deployment:
1. Use certificates from your enterprise CA
2. Enable certificate revocation checking
3. Use certificate thumbprint allowlisting
4. Store certificates securely (not in files)
5. Monitor certificate expiration dates

See [MUTUAL_TLS_CONFIGURATION.md](MUTUAL_TLS_CONFIGURATION.md) for complete documentation.

## Configuration for Web App

To enable mTLS for the web app → API communication:

1. Generate a separate client certificate for the web app
2. Edit `SecureBootDashboard.Web/appsettings.json`:

```json
{
  "ApiSettings": {
    "BaseUrl": "https://localhost:5001",
    "UseCertificateAuth": true,
    "CertificateThumbprint": "PASTE_WEB_CLIENT_THUMBPRINT_HERE",
    "CertificateStoreLocation": "LocalMachine",
    "CertificateStoreName": "My"
  }
}
```

3. Restart the web app and verify it can connect to the API

## Security Notes

⚠️ **This quick start is for testing only!**

For production:
- Do NOT use self-signed certificates
- Do NOT disable revocation checking
- Do NOT commit certificates to source control
- DO use enterprise PKI or public CA
- DO enable all security validations
- DO monitor certificate expiration

## Time Estimate

- Certificate generation: 2 minutes
- API configuration: 1 minute
- Client configuration: 1 minute
- Testing: 1 minute
- **Total: ~5 minutes**

## Support

For issues or questions:
- Check API logs for certificate validation errors
- Check client logs for certificate loading errors
- Review [MUTUAL_TLS_CONFIGURATION.md](MUTUAL_TLS_CONFIGURATION.md) for detailed guidance
- See [MUTUAL_TLS_IMPLEMENTATION_SUMMARY.md](MUTUAL_TLS_IMPLEMENTATION_SUMMARY.md) for technical details
