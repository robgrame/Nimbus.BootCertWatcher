# Client Certificate Authentication Guide

This guide explains how to configure certificate-based authentication for API calls in the Secure Boot Certificate Watcher solution.

## Overview

Certificate-based authentication adds a security layer to API communications by requiring clients (both the Windows client and the Web dashboard) to present valid X.509 certificates when making requests to the API.

### Benefits

- **Mutual TLS (mTLS)**: Both client and server authenticate each other
- **Strong Authentication**: Based on cryptographic keys, more secure than passwords
- **No Secrets in Configuration**: Certificates can be stored in Windows Certificate Store
- **Compliance**: Meets enterprise security requirements for authenticated communication

## Architecture

```
┌─────────────────────────────┐
│  SecureBootWatcher.Client   │
│  (Windows .NET 4.8)         │
│                             │
│  + Client Certificate       │
└──────────┬──────────────────┘
           │ HTTPS + mTLS
           ▼
┌─────────────────────────────┐
│  SecureBootDashboard.Api    │
│  (ASP.NET Core 8)           │
│                             │
│  + Certificate Validation   │
└──────────┬──────────────────┘
           │
           ▼
┌─────────────────────────────┐
│  SecureBootDashboard.Web    │
│  (ASP.NET Core 8)           │
│                             │
│  + Client Certificate       │
└─────────────────────────────┘
```

## Configuration

### 1. API Configuration

The API must be configured to accept and validate client certificates.

**File**: `SecureBootDashboard.Api/appsettings.json`

```json
{
  "ClientCertificateAuthentication": {
    "Enabled": true,
    "RequireClientCertificate": true,
    "ValidateValidityPeriod": true,
    "ValidateCertificateChain": true,
    "AllowedCertificateThumbprints": [
      "522172C364D58BB50EA08C60055ACC095A161D12",
      "45A0FA32604773C82433C3B7D59E7466B3AC0C67"
    ]
  }
}
```

**Options**:
- `Enabled`: Enable/disable certificate authentication (default: `false`)
- `RequireClientCertificate`: Reject requests without certificates (default: `false`)
- `ValidateValidityPeriod`: Check NotBefore and NotAfter dates (default: `true`)
- `ValidateCertificateChain`: Validate certificate chain and issuer (default: `true`)
- `AllowedCertificateThumbprints`: Whitelist of allowed certificate thumbprints. Empty = accept any valid certificate

### 2. Client Configuration

Configure the Windows client to send a client certificate.

**File**: `SecureBootWatcher.Client/appsettings.json`

**Option A: Certificate from Windows Certificate Store (Recommended)**

```json
{
  "SecureBootWatcher": {
    "Sinks": {
      "EnableWebApi": true,
      "WebApi": {
        "BaseAddress": "https://your-api.azurewebsites.net",
        "IngestionRoute": "/api/SecureBootReports",
        "UseClientCertificate": true,
        "ClientCertificateThumbprint": "522172C364D58BB50EA08C60055ACC095A161D12",
        "ClientCertificateStoreLocation": "LocalMachine",
        "ClientCertificateStoreName": "My"
      }
    }
  }
}
```

**Option B: Certificate from File**

```json
{
  "SecureBootWatcher": {
    "Sinks": {
      "EnableWebApi": true,
      "WebApi": {
        "BaseAddress": "https://your-api.azurewebsites.net",
        "IngestionRoute": "/api/SecureBootReports",
        "UseClientCertificate": true,
        "ClientCertificatePath": "C:\\Certificates\\client-cert.pfx",
        "ClientCertificatePassword": ""
      }
    }
  }
}
```

### 3. Web Dashboard Configuration

Configure the Web application to send a client certificate when calling the API.

**File**: `SecureBootDashboard.Web/appsettings.json`

```json
{
  "ApiSettings": {
    "BaseUrl": "https://your-api.azurewebsites.net",
    "UseClientCertificate": true,
    "ClientCertificateThumbprint": "522172C364D58BB50EA08C60055ACC095A161D12",
    "ClientCertificateStoreLocation": "LocalMachine",
    "ClientCertificateStoreName": "My"
  }
}
```

## Certificate Setup

### Development/Test Environment

For development and testing, you can use self-signed certificates.

#### Generate Self-Signed Certificate (PowerShell)

```powershell
# Generate certificate
$cert = New-SelfSignedCertificate `
    -Subject "CN=SecureBootClient" `
    -CertStoreLocation "Cert:\LocalMachine\My" `
    -KeyExportPolicy Exportable `
    -KeySpec Signature `
    -KeyLength 2048 `
    -KeyAlgorithm RSA `
    -HashAlgorithm SHA256 `
    -NotAfter (Get-Date).AddYears(2)

# Display thumbprint
$thumbprint = $cert.Thumbprint
Write-Host "Certificate Thumbprint: $thumbprint"
```

#### Export Certificate to .pfx (Optional)

```powershell
$password = ConvertTo-SecureString -String "YourPassword" -Force -AsPlainText
Export-PfxCertificate `
    -Cert "Cert:\LocalMachine\My\$thumbprint" `
    -FilePath "C:\Temp\client-cert.pfx" `
    -Password $password
```

#### Install Certificate on Other Machines

```cmd
certutil -importpfx -p "YourPassword" "C:\Temp\client-cert.pfx"
```

#### Verify Installation

```cmd
certutil -store My
```

### Production Environment

For production, use certificates issued by a trusted Certificate Authority (CA).

#### Request Certificate from Enterprise CA

```powershell
# Using Windows Certificate Request
certreq -new -f request.inf cert.req
certreq -submit -f cert.req cert.cer
certreq -accept cert.cer
```

**request.inf example**:
```ini
[NewRequest]
Subject = "CN=SecureBootClient,O=YourCompany,C=IT"
KeyLength = 2048
Exportable = TRUE
KeySpec = 1
KeyUsage = 0xa0
MachineKeySet = TRUE
ProviderName = "Microsoft RSA SChannel Cryptographic Provider"
RequestType = PKCS10

[Extensions]
2.5.29.37 = "{text}1.3.6.1.5.5.7.3.2" ; Client Authentication
```

## Security Best Practices

### Certificate Management

✓ **Use trusted CA certificates in production** - Avoid self-signed certificates  
✓ **Rotate certificates before expiration** - Set up expiration monitoring  
✓ **Use strong passwords for .pfx files** - Minimum 12 characters  
✓ **Store passwords securely** - Use Azure Key Vault or environment variables  
✓ **Configure AllowedCertificateThumbprints** - Whitelist specific certificates  
✓ **Monitor authentication logs** - Track failed authentication attempts  
✓ **Use separate certificates per environment** - dev/test/prod isolation  
✓ **Revoke compromised certificates immediately** - Maintain CRL/OCSP  

### File System Security

✓ **Protect .pfx files with NTFS permissions** - Only administrators  
✓ **Never commit .pfx files to version control** - Use .gitignore  
✓ **Never commit passwords to version control** - Use secret managers  
✓ **Store certificates in secure locations** - Encrypted drives preferred  

### Configuration Security

✓ **Use environment variables for passwords**:
```powershell
$env:SECUREBOOT_Sinks__WebApi__ClientCertificatePassword = "cert-password"
```

✓ **Use Azure Key Vault in production**:
```json
{
  "KeyVault": {
    "VaultUri": "https://your-keyvault.vault.azure.net/"
  }
}
```

## Troubleshooting

### Certificate Not Found

**Error**: `Certificate with thumbprint XXX not found in LocalMachine\My`

**Solution**:
1. Verify certificate is installed: `certutil -store My`
2. Check thumbprint matches exactly (no spaces)
3. Ensure correct store location (LocalMachine vs CurrentUser)
4. Verify application has permission to access the certificate store

### Certificate Validation Failed

**Error**: `Client certificate validation failed: Certificate has expired`

**Solution**:
1. Check certificate validity period: `certutil -store My`
2. Renew or replace expired certificate
3. If using self-signed certificates, generate a new one

### 401 Unauthorized

**Error**: HTTP 401 response from API

**Solution**:
1. Verify `ClientCertificateAuthentication.Enabled = true` on API
2. Check certificate thumbprint is in `AllowedCertificateThumbprints` (if configured)
3. Verify certificate is being sent by client (check API logs)
4. Ensure certificate is valid and not expired

### Self-Signed Certificate Not Trusted

**Error**: Certificate chain validation failed

**Solution**:
1. For development, set `ValidateCertificateChain = false` on API
2. For production, use certificates from trusted CA
3. Or install the self-signed certificate root in Trusted Root Certification Authorities store

## Logging

### Enable Debug Logging

**API** (`appsettings.json`):
```json
{
  "Logging": {
    "LogLevel": {
      "SecureBootDashboard.Api.Middleware.ClientCertificateAuthenticationMiddleware": "Debug"
    }
  }
}
```

**Client** (`appsettings.json`):
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug"
    }
  }
}
```

### Log Messages to Look For

**API Logs**:
- `Client certificate validated successfully. Thumbprint: XXX`
- `Client certificate validation failed: [reason]`
- `Client certificate required but not provided`

**Client Logs**:
- `Client certificate configured for API authentication (Thumbprint: XXX)`
- `Client certificate loaded from store: LocalMachine\My`
- `Certificate with thumbprint XXX not found`

## Example Scenarios

### Scenario 1: Development with Self-Signed Certificates

1. Generate self-signed certificate on development machine
2. Export to .pfx file
3. Copy .pfx to client machines
4. Import on each client machine
5. Configure client with thumbprint
6. Configure API with `Enabled=true`, `RequireClientCertificate=false`, `ValidateCertificateChain=false`

### Scenario 2: Production with Enterprise CA

1. Request certificates from enterprise CA
2. Install certificates on client machines and web server
3. Configure client and web with certificate thumbprint
4. Configure API with `Enabled=true`, `RequireClientCertificate=true`, `AllowedCertificateThumbprints=[list]`
5. Monitor certificate expiration dates
6. Set up automatic certificate renewal

### Scenario 3: Mixed Environment (Optional Authentication)

1. Configure API with `Enabled=true`, `RequireClientCertificate=false`
2. Some clients send certificates, others don't
3. API logs which clients are using certificates
4. Gradually migrate all clients to use certificates
5. Once complete, set `RequireClientCertificate=true`

## References

- [ASP.NET Core Certificate Authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/certauth)
- [X.509 Certificates Overview](https://learn.microsoft.com/en-us/dotnet/framework/wcf/feature-details/working-with-certificates)
- [Windows Certificate Management](https://learn.microsoft.com/en-us/windows-server/identity/ad-cs/certification-authority-overview)
