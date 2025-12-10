# SSL Certificate Validation Bypass Configuration

## Problem

When deploying to staging/production environments with self-signed SSL certificates, you may encounter:

```
System.Security.Authentication.AuthenticationException: 
The remote certificate is invalid according to the validation procedure: RemoteCertificateNameMismatch
```

This occurs when the SSL certificate's Common Name (CN) doesn't match the hostname being accessed.

## Solution

### Option 1: Bypass SSL Validation (Staging/Testing Only) ??

**WARNING**: Only use in non-production environments!

Add to `appsettings.json` (Web app):

```json
{
  "ApiSettings": {
    "BaseUrl": "https://your-api-server:5001",
    "BypassSslValidation": true,
    "UseCertificateAuth": false
  }
}
```

This will:
- ? Accept any SSL certificate (self-signed, expired, name mismatch, etc.)
- ? Work in Development, Staging, and Production
- ?? **Security Risk**: Vulnerable to man-in-the-middle attacks

### Option 2: Proper SSL Certificate (Production) ?

**Recommended for production environments**

#### Step 1: Generate Certificate with Correct Hostname

On the **API server**, run PowerShell as Administrator:

```powershell
# Get server hostname and IP
$hostname = [System.Net.Dns]::GetHostName()
$ip = (Get-NetIPAddress -AddressFamily IPv4 | Where-Object {$_.IPAddress -like "192.168.*" -or $_.IPAddress -like "10.*"}).IPAddress

# Create certificate with multiple DNS names
$cert = New-SelfSignedCertificate `
    -DnsName $hostname, "localhost", "127.0.0.1", $ip `
    -CertStoreLocation "Cert:\LocalMachine\My" `
    -KeyExportPolicy Exportable `
    -KeySpec KeyExchange `
    -KeyLength 2048 `
    -HashAlgorithm SHA256 `
    -NotAfter (Get-Date).AddYears(2) `
    -FriendlyName "SecureBoot API Server Certificate"

Write-Host "? Certificate created successfully"
Write-Host "  Thumbprint: $($cert.Thumbprint)"
Write-Host "  Subject: $($cert.Subject)"
Write-Host "  DNS Names: $($cert.DnsNameList.Unicode -join ', ')"

# Export to file (optional - for client installation)
$certPath = "C:\Temp\secureboot-api-cert.pfx"
$certPassword = ConvertTo-SecureString -String "YourSecurePassword123!" -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath $certPath -Password $certPassword

Write-Host "? Certificate exported to: $certPath"
Write-Host ""
Write-Host "NEXT STEPS:"
Write-Host "1. Configure Kestrel to use this certificate (thumbprint: $($cert.Thumbprint))"
Write-Host "2. Install certificate on client machines (optional for mutual TLS)"
Write-Host "3. Update appsettings.json: Set BypassSslValidation = false"
```

#### Step 2: Configure Kestrel to Use Certificate

Update `appsettings.json` (API server):

```json
{
  "Kestrel": {
    "Endpoints": {
      "Https": {
        "Url": "https://*:5001",
        "Certificate": {
          "Subject": "CN=YOUR-HOSTNAME",
          "Store": "My",
          "Location": "LocalMachine",
          "AllowInvalid": false
        }
      }
    }
  }
}
```

Or use thumbprint:

```json
{
  "Kestrel": {
    "Endpoints": {
      "Https": {
        "Url": "https://*:5001",
        "Certificate": {
          "Thumbprint": "YOUR-CERT-THUMBPRINT-HERE",
          "Store": "My",
          "Location": "LocalMachine"
        }
      }
    }
  }
}
```

#### Step 3: Update Web App Configuration

Update `appsettings.json` (Web server):

```json
{
  "ApiSettings": {
    "BaseUrl": "https://YOUR-API-HOSTNAME:5001",
    "BypassSslValidation": false,
    "UseCertificateAuth": false
  }
}
```

**Important**: Use the **exact hostname** that's in the certificate's DNS names!

### Option 3: Use HTTP (Development Only) ??

**NOT RECOMMENDED** - Only for local development

```json
{
  "ApiSettings": {
    "BaseUrl": "http://localhost:5000",
    "BypassSslValidation": false
  }
}
```

## Configuration Reference

### ApiSettings Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `BaseUrl` | string | - | API base URL (required) |
| `BypassSslValidation` | bool | `false` | Skip SSL certificate validation ?? |
| `UseCertificateAuth` | bool | `false` | Enable mutual TLS |
| `CertificateThumbprint` | string? | `null` | Client cert thumbprint |
| `CertificatePath` | string? | `null` | Client cert file path |
| `CertificatePassword` | string? | `null` | Client cert password |
| `CertificateStoreLocation` | string | `"LocalMachine"` | Cert store location |
| `CertificateStoreName` | string | `"My"` | Cert store name |

## Environment-Specific Configuration

### Development (localhost)

```json
{
  "ApiSettings": {
    "BaseUrl": "https://localhost:5001",
    "BypassSslValidation": false
  }
}
```

SSL bypass is **automatic** in Development environment.

### Staging (self-signed cert)

```json
{
  "ApiSettings": {
    "BaseUrl": "https://staging-api.contoso.com",
    "BypassSslValidation": true
  }
}
```

?? Acceptable for internal staging environments only.

### Production (valid cert)

```json
{
  "ApiSettings": {
    "BaseUrl": "https://api.contoso.com",
    "BypassSslValidation": false
  }
}
```

? **Always** use valid SSL certificates in production.

## Troubleshooting

### Error: RemoteCertificateNameMismatch

**Cause**: SSL certificate CN doesn't match hostname

**Fix**:
1. Check what hostname you're using in `BaseUrl`
2. Verify certificate DNS names: `openssl x509 -in cert.pem -noout -text`
3. Either:
   - Generate new cert with correct hostname (Option 2)
   - Enable `BypassSslValidation: true` (Option 1)

### Error: The remote certificate is invalid because of errors in the certificate chain

**Cause**: Self-signed certificate not trusted

**Fix**:
1. Install certificate in Trusted Root CA store on client
2. Or enable `BypassSslValidation: true`

### Error: SSL connection timeout

**Cause**: Firewall or incorrect URL

**Fix**:
1. Verify API is accessible: `Test-NetConnection your-api-server -Port 5001`
2. Check firewall rules
3. Verify URL in `appsettings.json`

## Security Best Practices

### ? DO

- Use proper SSL certificates in production
- Store certificate passwords in Azure Key Vault
- Rotate certificates before expiration
- Use mutual TLS for client authentication in production

### ?? DON'T

- Don't bypass SSL validation in production
- Don't commit certificate passwords to Git
- Don't use HTTP in production
- Don't ignore SSL errors without understanding the risk

## Deployment Checklist

- [ ] Generate SSL certificate with correct hostname
- [ ] Configure Kestrel to use certificate
- [ ] Update Web app `appsettings.json` with correct `BaseUrl`
- [ ] Set `BypassSslValidation: false` in production
- [ ] Test SSL connection: `curl -v https://your-api-server:5001/health`
- [ ] Verify logs show no SSL errors
- [ ] Remove any development certificates from production servers

## Reference

- **Kestrel HTTPS**: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/endpoints
- **Certificate Authentication**: https://learn.microsoft.com/en-us/aspnet/core/security/authentication/certauth
- **HttpClient SSL**: https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpclienthandler.servercertificatecustomvalidationcallback

---

**Status**: ? Configuration option available  
**Version**: 1.14.0  
**Last Updated**: 2025-01-23
