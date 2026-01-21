# CA Certificate Validation Configuration Guide

## Overview

The Secure Boot Watcher client now supports validation of Certificate Authority (CA) certificates in the certificate chain. This feature enhances security by ensuring that client certificates are issued by specific, trusted Certificate Authorities.

## Configuration Options

### CA Root Certificate Validation

You can specify the expected CA Root certificate that should be at the top of the certificate chain:

- **`ExpectedCARootName`**: The Subject name of the CA Root certificate (e.g., "CN=Contoso Root CA, O=Contoso, C=US")
- **`ExpectedCARootThumbprint`**: The SHA-1 thumbprint of the CA Root certificate

### Subordinate (Intermediate) CA Validation

You can specify one or more subordinate/intermediate CA certificates that should be present in the certificate chain:

- **`ExpectedSubordinateCAs`**: An array of CA configurations, each containing:
  - **`Name`**: The Subject name of the subordinate CA certificate
  - **`Thumbprint`**: The SHA-1 thumbprint of the subordinate CA certificate

## Configuration Examples

### Example 1: Azure Function with CA Validation

```json
{
  "SecureBootWatcher": {
    "Sinks": {
      "EnableAzureFunction": true,
      "AzureFunction": {
        "FunctionUrl": "https://your-function-app.azurewebsites.net/api/reports",
        "ApiKey": "your-api-key",
        "UseCertificateAuth": true,
        "CertificateThumbprint": "ABC123DEF456...",
        "ValidateCertificateChain": true,
        
        "ExpectedCARootName": "CN=Contoso Root CA, O=Contoso, C=US",
        "ExpectedCARootThumbprint": "1234567890ABCDEF1234567890ABCDEF12345678",
        "ExpectedSubordinateCAs": [
          {
            "Name": "CN=Contoso Issuing CA 01, OU=IT, O=Contoso, C=US",
            "Thumbprint": "AABBCCDDEEFF00112233445566778899AABBCCDD"
          }
        ]
      }
    }
  }
}
```

### Example 2: Web API with CA Validation

```json
{
  "SecureBootWatcher": {
    "Sinks": {
      "EnableWebApi": true,
      "WebApi": {
        "BaseAddress": "https://your-api.azurewebsites.net",
        "UseCertificateAuth": true,
        "CertificateThumbprint": "ABC123DEF456...",
        "ValidateCertificateChain": true,
        
        "ExpectedCARootName": "CN=Corporate Root CA",
        "ExpectedCARootThumbprint": "1234567890ABCDEF1234567890ABCDEF12345678",
        "ExpectedSubordinateCAs": [
          {
            "Name": "CN=Corporate Intermediate CA 01",
            "Thumbprint": "AABBCCDDEEFF00112233445566778899AABBCCDD"
          },
          {
            "Name": "CN=Corporate Intermediate CA 02",
            "Thumbprint": "112233445566778899AABBCCDDEEFF0011223344"
          }
        ]
      }
    }
  }
}
```

### Example 3: Minimal Configuration (Name Only)

You can validate by name only if you don't need thumbprint validation:

```json
{
  "AzureFunction": {
    "UseCertificateAuth": true,
    "ValidateCertificateChain": true,
    "ExpectedCARootName": "CN=Contoso Root CA",
    "ExpectedSubordinateCAs": [
      {
        "Name": "CN=Contoso Issuing CA 01"
      }
    ]
  }
}
```

### Example 4: Thumbprint Only

Or validate by thumbprint only:

```json
{
  "AzureFunction": {
    "UseCertificateAuth": true,
    "ValidateCertificateChain": true,
    "ExpectedCARootThumbprint": "1234567890ABCDEF1234567890ABCDEF12345678",
    "ExpectedSubordinateCAs": [
      {
        "Thumbprint": "AABBCCDDEEFF00112233445566778899AABBCCDD"
      }
    ]
  }
}
```

## How It Works

### Certificate Chain Structure

A typical certificate chain looks like this:

```
[0] Client Certificate (leaf certificate)
    ↓ Issued by
[1] Subordinate CA 01 (intermediate)
    ↓ Issued by
[2] Subordinate CA 02 (intermediate - optional, may have multiple levels)
    ↓ Issued by
[n] CA Root Certificate (self-signed)
```

### Validation Process

1. **Client Certificate Loading**: The client certificate is loaded from the certificate store or file
2. **Chain Building**: The certificate chain is built automatically by the operating system
3. **Chain Validation**: Standard certificate chain validation is performed (validity dates, signatures)
4. **CA Root Validation**: 
   - If `ExpectedCARootName` is specified, the root certificate's Subject must contain this name
   - If `ExpectedCARootThumbprint` is specified, the root certificate's thumbprint must match exactly
5. **Subordinate CA Validation**:
   - For each configured subordinate CA, the chain is searched for a matching certificate
   - Matching is done by name (if specified) AND thumbprint (if specified)
   - All configured subordinate CAs must be found in the chain

### Validation Failures

The certificate validation will fail and prevent authentication if:

- The CA Root name doesn't match the expected name
- The CA Root thumbprint doesn't match the expected thumbprint
- Any expected subordinate CA is not found in the certificate chain
- The certificate has expired
- The certificate chain is invalid
- The certificate doesn't have a private key

## Getting Certificate Information

### Using PowerShell

To get certificate information from your certificate store:

```powershell
# List certificates in LocalMachine\My store
Get-ChildItem Cert:\LocalMachine\My

# Get specific certificate details
$cert = Get-ChildItem Cert:\LocalMachine\My | Where-Object { $_.Subject -like "*YourCertName*" }
$cert | Format-List Subject, Issuer, Thumbprint, NotBefore, NotAfter

# View the entire certificate chain
$chain = New-Object System.Security.Cryptography.X509Certificates.X509Chain
$chain.Build($cert) | Out-Null
$chain.ChainElements | ForEach-Object {
    $_.Certificate | Format-List Subject, Issuer, Thumbprint
}
```

### Using Certificate Manager (certmgr.msc)

1. Open Certificate Manager: Run `certmgr.msc` (for CurrentUser) or `certlm.msc` (for LocalMachine)
2. Navigate to Personal → Certificates
3. Double-click your certificate
4. Go to "Certification Path" tab to see the full chain
5. Select each certificate in the chain to view its properties
6. In the "Details" tab, find the "Thumbprint" field

### Example Output

```
Subject: CN=client.contoso.com
Issuer: CN=Contoso Issuing CA 01, OU=IT, O=Contoso, C=US
Thumbprint: ABC123DEF456789ABC123DEF456789ABC123DEF4

Subject: CN=Contoso Issuing CA 01, OU=IT, O=Contoso, C=US
Issuer: CN=Contoso Root CA, O=Contoso, C=US
Thumbprint: AABBCCDDEEFF00112233445566778899AABBCCDD

Subject: CN=Contoso Root CA, O=Contoso, C=US
Issuer: CN=Contoso Root CA, O=Contoso, C=US
Thumbprint: 1234567890ABCDEF1234567890ABCDEF12345678
```

## Security Best Practices

### 1. Use Both Name and Thumbprint

For maximum security, specify both name and thumbprint:

```json
{
  "ExpectedCARootName": "CN=Contoso Root CA",
  "ExpectedCARootThumbprint": "1234567890ABCDEF..."
}
```

This prevents attacks where an attacker creates a certificate with the same name but different key.

### 2. Validate All CAs in Chain

Specify all subordinate CAs in your organization's PKI hierarchy:

```json
{
  "ExpectedSubordinateCAs": [
    { "Name": "CN=Issuing CA 01", "Thumbprint": "..." },
    { "Name": "CN=Policy CA", "Thumbprint": "..." }
  ]
}
```

### 3. Keep Thumbprints Up to Date

When CA certificates are renewed or replaced:

1. Update the configuration with new thumbprints
2. Deploy the new configuration before the old certificates expire
3. Test the new configuration in a non-production environment first

### 4. Enable Chain Validation

Always enable certificate chain validation:

```json
{
  "ValidateCertificateChain": true,
  "CheckCertificateRevocation": true
}
```

### 5. Monitor Logs

The client logs detailed information about CA validation:

```
[INFO] Loading client certificate for AzureFunction from store: ABC123...
[INFO] Certificate chain validation passed successfully
[INFO] Certificate chain for AzureFunction has 3 certificates:
  [0] Subject=CN=client.contoso.com
  [1] Subject=CN=Contoso Issuing CA 01
  [2] Subject=CN=Contoso Root CA
[INFO] Validating expected CA Root for AzureFunction...
[INFO] CA Root name validation passed: CN=Contoso Root CA
[INFO] CA Root thumbprint validation passed: 1234567890ABCDEF...
[INFO] Validating 1 expected Subordinate CAs for AzureFunction...
[INFO] Subordinate CA found in chain: Subject=CN=Contoso Issuing CA 01, Thumbprint=AABBCCDD...
[INFO] All expected Subordinate CAs validated successfully
[INFO] Client certificate has private key and is ready for use
```

## Troubleshooting

### CA Root Validation Failed

**Error**: "CA Root name validation failed"

**Cause**: The name in the configuration doesn't match the actual CA Root certificate Subject.

**Solution**:
1. Check the actual CA Root certificate Subject using PowerShell or Certificate Manager
2. Update the configuration with the exact name (case-insensitive, partial match is allowed)
3. Ensure there are no typos in the configuration

### CA Root Thumbprint Validation Failed

**Error**: "CA Root thumbprint validation failed"

**Cause**: The thumbprint doesn't match the actual CA Root certificate.

**Solution**:
1. Get the thumbprint from the CA Root certificate
2. Remove spaces and colons from the thumbprint
3. Update the configuration with the correct thumbprint

### Subordinate CA Not Found

**Error**: "Expected Subordinate CA not found in certificate chain"

**Cause**: The expected subordinate CA is not in the certificate chain, or the name/thumbprint doesn't match.

**Solution**:
1. View the entire certificate chain using PowerShell (see above)
2. Verify the subordinate CA is actually in the chain
3. Check that the name and/or thumbprint match exactly
4. Ensure you're specifying intermediate CAs, not the leaf certificate or root

### Certificate Chain Has Warnings

**Warning**: "Certificate chain validation has warnings but no critical errors"

**Cause**: Non-critical issues like untrusted root (if not in Windows trust store).

**Solution**: If CA validation is configured, these warnings are typically acceptable. The explicit CA validation provides the necessary security.

## Backward Compatibility

- All existing configurations continue to work without changes
- CA validation is optional - only activated if `ExpectedCARootName`, `ExpectedCARootThumbprint`, or `ExpectedSubordinateCAs` are configured
- If chain validation is disabled (`ValidateCertificateChain: false`), CA validation is also skipped

## Migration Guide

### From No CA Validation to CA Validation

1. **Identify your CAs**: Use PowerShell to examine your certificate chains
2. **Test configuration**: Create a test configuration with CA settings
3. **Deploy gradually**: Roll out to test machines first
4. **Monitor logs**: Check for validation failures
5. **Deploy to production**: Once validated, deploy to all clients

### Example Migration Script

```powershell
# Get certificate information for configuration
$cert = Get-ChildItem Cert:\LocalMachine\My | Where-Object { $_.Subject -like "*client*" }
$chain = New-Object System.Security.Cryptography.X509Certificates.X509Chain
$chain.Build($cert) | Out-Null

Write-Host "Copy these values to your configuration:"
Write-Host ""

# Root CA
$rootCert = $chain.ChainElements[$chain.ChainElements.Count - 1].Certificate
Write-Host "ExpectedCARootName: '$($rootCert.Subject)'"
Write-Host "ExpectedCARootThumbprint: '$($rootCert.Thumbprint)'"

# Subordinate CAs
Write-Host "ExpectedSubordinateCAs:"
for ($i = 1; $i -lt $chain.ChainElements.Count - 1; $i++) {
    $ca = $chain.ChainElements[$i].Certificate
    Write-Host "  - Name: '$($ca.Subject)'"
    Write-Host "    Thumbprint: '$($ca.Thumbprint)'"
}
```

## Database Configuration

CA certificate settings can also be stored in the database via `ClientSinkConfigEntity`:

- `WebApiExpectedCARootName`
- `WebApiExpectedCARootThumbprint`
- `WebApiExpectedSubordinateCAsJson` (JSON array)
- `AzureFunctionExpectedCARootName`
- `AzureFunctionExpectedCARootThumbprint`
- `AzureFunctionExpectedSubordinateCAsJson` (JSON array)

The JSON format for subordinate CAs:

```json
[
  {"name":"CN=Issuing CA 01","thumbprint":"AABBCCDD..."},
  {"name":"CN=Intermediate CA","thumbprint":"11223344..."}
]
```
