# Mutual TLS Authentication Configuration Guide

This guide explains how to configure mutual TLS (mTLS) authentication between the SecureBootWatcher client, Web dashboard, and API components.

## Overview

Mutual TLS provides strong authentication by requiring both the client and server to present valid X.509 certificates during the TLS handshake. This ensures:

- **Server Authentication**: Clients verify they're connecting to the legitimate API server
- **Client Authentication**: The API verifies the identity of connecting clients
- **Encryption**: All communication is encrypted using TLS 1.2 or higher
- **Non-repudiation**: Certificate-based authentication provides audit trails

## Architecture

```
┌─────────────────────────────────────────────┐
│  Client (.NET Framework 4.8)                 │
│  - Presents client certificate              │
│  - Validates server certificate             │
└─────────────┬───────────────────────────────┘
              │ mTLS (Client Cert)
              ▼
┌─────────────────────────────────────────────┐
│  API (ASP.NET Core 10)                       │
│  - Validates client certificate             │
│  - Presents server certificate              │
└─────────────────────────────────────────────┘
              ▲ mTLS (Client Cert)
              │
┌─────────────┴───────────────────────────────┐
│  Web Dashboard (ASP.NET Core 10)             │
│  - Presents client certificate              │
│  - Validates server certificate             │
└─────────────────────────────────────────────┘
```

## Certificate Requirements

### Server Certificate (API)
- Must be trusted by the client systems
- Should have appropriate Subject Alternative Names (SANs) for all API endpoints
- Typically issued by an internal CA or public CA (Let's Encrypt, DigiCert, etc.)

### Client Certificates
Each client component (Client app, Web app) needs its own certificate with:
- **Key Usage**: Digital Signature, Key Encipherment
- **Extended Key Usage**: Client Authentication (1.3.6.1.5.5.7.3.2)
- **Subject**: Unique identifier for the client (CN=client-name)
- **Validity**: Recommended 1-2 years

## Generating Certificates

### Option 1: Using PowerShell (Self-Signed for Testing)

**Generate Root CA Certificate:**
```powershell
# Create a self-signed root CA certificate
$rootCert = New-SelfSignedCertificate `
    -Subject "CN=SecureBootWatcher Root CA" `
    -KeyExportPolicy Exportable `
    -KeyUsage CertSign,CRLSign,DigitalSignature `
    -KeyLength 4096 `
    -NotAfter (Get-Date).AddYears(5) `
    -CertStoreLocation "Cert:\LocalMachine\My" `
    -HashAlgorithm SHA256

# Export root CA certificate
Export-Certificate -Cert $rootCert -FilePath "SecureBootWatcher-RootCA.cer"

# Install to Trusted Root (for testing only)
Import-Certificate -FilePath "SecureBootWatcher-RootCA.cer" -CertStoreLocation "Cert:\LocalMachine\Root"
```

**Generate Client Certificate:**
```powershell
# Create client certificate signed by root CA
$clientCert = New-SelfSignedCertificate `
    -Subject "CN=SecureBootWatcher-Client" `
    -Signer $rootCert `
    -KeyExportPolicy Exportable `
    -KeyUsage DigitalSignature,KeyEncipherment `
    -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.2") `
    -KeyLength 2048 `
    -NotAfter (Get-Date).AddYears(2) `
    -CertStoreLocation "Cert:\LocalMachine\My" `
    -HashAlgorithm SHA256

# Export client certificate with private key
$password = ConvertTo-SecureString -String "YourSecurePassword" -Force -AsPlainText
Export-PfxCertificate -Cert $clientCert -FilePath "SecureBootWatcher-Client.pfx" -Password $password

# Note the thumbprint for configuration
Write-Host "Client Certificate Thumbprint: $($clientCert.Thumbprint)"
```

**Generate Web App Client Certificate:**
```powershell
# Create web app certificate signed by root CA
$webCert = New-SelfSignedCertificate `
    -Subject "CN=SecureBootDashboard-Web" `
    -Signer $rootCert `
    -KeyExportPolicy Exportable `
    -KeyUsage DigitalSignature,KeyEncipherment `
    -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.2") `
    -KeyLength 2048 `
    -NotAfter (Get-Date).AddYears(2) `
    -CertStoreLocation "Cert:\LocalMachine\My" `
    -HashAlgorithm SHA256

# Export web certificate with private key
Export-PfxCertificate -Cert $webCert -FilePath "SecureBootDashboard-Web.pfx" -Password $password

# Note the thumbprint for configuration
Write-Host "Web Certificate Thumbprint: $($webCert.Thumbprint)"
```

### Option 2: Using OpenSSL (Production)

**Generate Root CA:**
```bash
# Create root CA private key
openssl genrsa -out root-ca.key 4096

# Create root CA certificate
openssl req -x509 -new -nodes -key root-ca.key -sha256 -days 1825 \
    -out root-ca.crt -subj "/CN=SecureBootWatcher Root CA"
```

**Generate Client Certificate:**
```bash
# Create client private key
openssl genrsa -out client.key 2048

# Create certificate signing request
openssl req -new -key client.key -out client.csr \
    -subj "/CN=SecureBootWatcher-Client"

# Create certificate extensions file
cat > client-ext.cnf << EOF
basicConstraints = CA:FALSE
keyUsage = digitalSignature, keyEncipherment
extendedKeyUsage = clientAuth
EOF

# Sign client certificate with root CA
openssl x509 -req -in client.csr -CA root-ca.crt -CAkey root-ca.key \
    -CAcreateserial -out client.crt -days 730 -sha256 -extfile client-ext.cnf

# Convert to PKCS#12 format (.pfx)
openssl pkcs12 -export -out client.pfx -inkey client.key -in client.crt \
    -certfile root-ca.crt -password pass:YourSecurePassword
```

### Option 3: Using Enterprise PKI (Recommended for Production)

If you have an enterprise Certificate Authority (Active Directory Certificate Services):

1. Request a certificate from your CA using the **User** or **Computer** certificate template
2. Ensure the template includes:
   - Client Authentication EKU (1.3.6.1.5.5.7.3.2)
   - Appropriate key usage flags
3. Export the certificate with private key as .pfx format
4. Note the certificate thumbprint

## Configuration

### API Configuration

Edit `SecureBootDashboard.Api/appsettings.json`:

```json
{
  "MutualTls": {
    "Enabled": true,
    "AllowSelfSignedCertificates": false,
    "AllowedThumbprints": [
      "ABCDEF1234567890ABCDEF1234567890ABCDEF12",
      "1234567890ABCDEF1234567890ABCDEF12345678"
    ],
    "AllowedIssuers": [
      "SecureBootWatcher Root CA",
      "Contoso Enterprise CA"
    ],
    "CheckCertificateRevocation": true,
    "ValidateCertificateChain": true
  }
}
```

**Configuration Options:**

- `Enabled`: Set to `true` to enable mutual TLS authentication
- `AllowSelfSignedCertificates`: Allow self-signed certificates (only for testing)
- `AllowedThumbprints`: Whitelist of allowed client certificate thumbprints (leave empty to allow any valid certificate)
- `AllowedIssuers`: Whitelist of allowed Certificate Authority common names
- `CheckCertificateRevocation`: Enable certificate revocation checking (requires CRL/OCSP access)
- `ValidateCertificateChain`: Validate the complete certificate chain

**For Testing (Development):**
```json
{
  "MutualTls": {
    "Enabled": true,
    "AllowSelfSignedCertificates": true,
    "AllowedThumbprints": [],
    "AllowedIssuers": ["SecureBootWatcher Root CA"],
    "CheckCertificateRevocation": false,
    "ValidateCertificateChain": true
  }
}
```

**For Production:**
```json
{
  "MutualTls": {
    "Enabled": true,
    "AllowSelfSignedCertificates": false,
    "AllowedThumbprints": [],
    "AllowedIssuers": ["Contoso Enterprise CA"],
    "CheckCertificateRevocation": true,
    "ValidateCertificateChain": true
  }
}
```

### Client Configuration

Edit `SecureBootWatcher.Client/appsettings.json`:

**Using Certificate from Windows Certificate Store (Recommended):**
```json
{
  "SecureBootWatcher": {
    "Sinks": {
      "EnableWebApi": true,
      "WebApi": {
        "BaseAddress": "https://api.contoso.com:5001",
        "IngestionRoute": "/api/SecureBootReports",
        "HttpTimeout": "00:00:30",
        "UseCertificateAuth": true,
        "CertificateThumbprint": "ABCDEF1234567890ABCDEF1234567890ABCDEF12",
        "CertificateStoreLocation": "LocalMachine",
        "CertificateStoreName": "My"
      }
    }
  }
}
```

**Using Certificate from File:**
```json
{
  "SecureBootWatcher": {
    "Sinks": {
      "EnableWebApi": true,
      "WebApi": {
        "BaseAddress": "https://api.contoso.com:5001",
        "IngestionRoute": "/api/SecureBootReports",
        "HttpTimeout": "00:00:30",
        "UseCertificateAuth": true,
        "CertificatePath": "C:\\Certificates\\client.pfx",
        "CertificatePassword": "",
        "CertificateStoreLocation": "LocalMachine",
        "CertificateStoreName": "My"
      }
    }
  }
}
```

**Configuration Options:**

- `UseCertificateAuth`: Enable client certificate authentication
- `CertificateThumbprint`: SHA-1 thumbprint of the certificate in the Windows Certificate Store
- `CertificatePath`: Path to .pfx certificate file (alternative to thumbprint)
- `CertificatePassword`: Password for .pfx file (store securely, not in config)
- `CertificateStoreLocation`: `LocalMachine` or `CurrentUser`
- `CertificateStoreName`: Usually `My` (Personal certificates)

### Web App Configuration

Edit `SecureBootDashboard.Web/appsettings.json`:

**Using Certificate from Windows Certificate Store:**
```json
{
  "ApiSettings": {
    "BaseUrl": "https://api.contoso.com:5001",
    "UseCertificateAuth": true,
    "CertificateThumbprint": "1234567890ABCDEF1234567890ABCDEF12345678",
    "CertificateStoreLocation": "LocalMachine",
    "CertificateStoreName": "My"
  }
}
```

**Using Certificate from File:**
```json
{
  "ApiSettings": {
    "BaseUrl": "https://api.contoso.com:5001",
    "UseCertificateAuth": true,
    "CertificatePath": "C:\\Certificates\\web.pfx",
    "CertificatePassword": ""
  }
}
```

## Deployment

### Installing Certificates

**On Client Machines (Windows):**
```powershell
# Import client certificate to LocalMachine\My store
$password = ConvertTo-SecureString -String "YourSecurePassword" -Force -AsPlainText
Import-PfxCertificate -FilePath "C:\Certificates\client.pfx" `
    -CertStoreLocation "Cert:\LocalMachine\My" `
    -Password $password

# Grant NETWORK SERVICE read access to the private key (if running as service)
$cert = Get-ChildItem -Path "Cert:\LocalMachine\My" | Where-Object {$_.Thumbprint -eq "ABCDEF1234567890..."}
$rsaCert = [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($cert)
$fileName = $rsaCert.Key.UniqueName
$path = "$env:ALLUSERSPROFILE\Microsoft\Crypto\Keys\$fileName"
$permissions = Get-Acl -Path $path
$rule = New-Object System.Security.AccessControl.FileSystemAccessRule("NETWORK SERVICE", "Read", "Allow")
$permissions.AddAccessRule($rule)
Set-Acl -Path $path -AclObject $permissions
```

**On Web Server:**
```powershell
# Import web certificate to LocalMachine\My store
Import-PfxCertificate -FilePath "C:\Certificates\web.pfx" `
    -CertStoreLocation "Cert:\LocalMachine\My" `
    -Password $password

# Grant IIS AppPool identity read access
$appPoolIdentity = "IIS AppPool\SecureBootDashboard"
$cert = Get-ChildItem -Path "Cert:\LocalMachine\My" | Where-Object {$_.Thumbprint -eq "1234567890ABCDEF..."}
$rsaCert = [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($cert)
$fileName = $rsaCert.Key.UniqueName
$path = "$env:ALLUSERSPROFILE\Microsoft\Crypto\Keys\$fileName"
$permissions = Get-Acl -Path $path
$rule = New-Object System.Security.AccessControl.FileSystemAccessRule($appPoolIdentity, "Read", "Allow")
$permissions.AddAccessRule($rule)
Set-Acl -Path $path -AclObject $permissions
```

**On API Server:**

Install the root CA certificate to the Trusted Root store:
```powershell
Import-Certificate -FilePath "C:\Certificates\root-ca.cer" `
    -CertStoreLocation "Cert:\LocalMachine\Root"
```

### Azure App Service

For Azure App Service deployments:

1. **Upload Certificates:**
   - Navigate to Azure Portal → App Service → TLS/SSL settings
   - Upload the client certificate (.pfx) under "Private Key Certificates"
   - Note the certificate thumbprint

2. **Configure App Settings:**
   ```
   ApiSettings__UseCertificateAuth = true
   ApiSettings__CertificateThumbprint = <thumbprint>
   WEBSITE_LOAD_CERTIFICATES = <thumbprint or * for all>
   ```

3. **Update appsettings.json:**
   - Set `CertificateStoreLocation` to `CurrentUser`
   - Set `CertificateStoreName` to `My`

## Testing

### Verify Certificate Installation

**Check certificate in store:**
```powershell
Get-ChildItem -Path "Cert:\LocalMachine\My" | Where-Object {
    $_.Thumbprint -eq "ABCDEF1234567890ABCDEF1234567890ABCDEF12"
} | Format-List Subject, Issuer, NotAfter, HasPrivateKey
```

**Test client certificate:**
```powershell
# Test HTTPS request with client certificate
$cert = Get-ChildItem -Path "Cert:\LocalMachine\My" | Where-Object {
    $_.Thumbprint -eq "ABCDEF1234567890ABCDEF1234567890ABCDEF12"
}

Invoke-RestMethod -Uri "https://api.contoso.com:5001/health" `
    -Certificate $cert -Method Get
```

### Check API Logs

When mutual TLS is enabled, the API logs certificate validation events:

```
[INF] Mutual TLS is ENABLED
[INF]   Allow Self-Signed Certificates: False
[INF]   Check Certificate Revocation: True
[INF]   Validate Certificate Chain: True
[INF]   Allowed Thumbprints: 2 configured
[INF]     - ABCDEF1234567890ABCDEF1234567890ABCDEF12
[INF]     - 1234567890ABCDEF1234567890ABCDEF12345678
[DBG] Certificate validation requested for: CN=SecureBootWatcher-Client
[DBG] Certificate thumbprint validated: ABCDEF1234567890ABCDEF1234567890ABCDEF12
[INF] Certificate validated successfully: Subject=CN=SecureBootWatcher-Client, Issuer=CN=SecureBootWatcher Root CA, Thumbprint=ABCDEF...
```

### Common Issues

**Certificate not found:**
- Verify the certificate is installed in the correct store location
- Check the thumbprint matches exactly (no spaces or special characters)
- Ensure the certificate has a private key (`HasPrivateKey = True`)

**Access denied to private key:**
- Grant the application identity read access to the certificate's private key
- For services: NETWORK SERVICE or LOCAL SYSTEM
- For IIS: IIS AppPool\<AppPoolName>
- For Azure: The app service identity has automatic access

**Certificate validation failed:**
- Ensure the root CA is installed in Trusted Root Certification Authorities
- Check certificate has not expired
- Verify the certificate chain is complete
- Disable revocation checking for testing if CRL/OCSP is not accessible

**TLS/SSL errors:**
- Ensure the API server has a valid server certificate
- Check that TLS 1.2 or higher is enabled on both client and server
- Verify firewall allows HTTPS traffic

## Security Considerations

1. **Certificate Storage**: 
   - Never commit certificates or passwords to source control
   - Use Azure Key Vault or similar for storing certificates in production
   - Set appropriate file permissions on certificate files

2. **Certificate Rotation**:
   - Implement a certificate renewal process before expiration
   - Plan for zero-downtime certificate rotation
   - Monitor certificate expiration dates

3. **Revocation**:
   - Enable certificate revocation checking in production
   - Ensure CRL/OCSP endpoints are accessible
   - Have a process for revoking compromised certificates

4. **Least Privilege**:
   - Grant minimal permissions to certificate private keys
   - Use separate certificates for each client component
   - Implement certificate allowlisting when possible

5. **Monitoring**:
   - Log all certificate authentication events
   - Alert on certificate validation failures
   - Track certificate expiration dates

## Disabling Mutual TLS

To disable mutual TLS authentication:

**API (`appsettings.json`):**
```json
{
  "MutualTls": {
    "Enabled": false
  }
}
```

**Client (`appsettings.json`):**
```json
{
  "SecureBootWatcher": {
    "Sinks": {
      "WebApi": {
        "UseCertificateAuth": false
      }
    }
  }
}
```

**Web App (`appsettings.json`):**
```json
{
  "ApiSettings": {
    "UseCertificateAuth": false
  }
}
```

## Additional Resources

- [Microsoft Docs: Configure certificate authentication in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/certauth)
- [RFC 5280: X.509 Certificate Profile](https://tools.ietf.org/html/rfc5280)
- [NIST Guidelines for TLS Implementations](https://csrc.nist.gov/publications/detail/sp/800-52/rev-2/final)
