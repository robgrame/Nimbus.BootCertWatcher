# Secure Boot Report Proxy - Configuration

## Overview

The Azure Function now uses strongly-typed configuration classes (POCO) instead of reading environment variables directly in the function code. This improves maintainability, testability, and provides IntelliSense support.

## Configuration Classes

### `ProxyFunctionOptions`
Main configuration class for the proxy function. Maps environment variables to strongly-typed properties.

**Properties:**
- `ApiKey` (string, required): API key for authenticating incoming requests from clients
- `QueueStorageUri` (string, required): Azure Queue Storage URI where reports are forwarded
- `QueueName` (string, default: "secureboot-reports"): Target queue name
- `RequireCertificateAuthentication` (bool, default: false): Enable mutual TLS authentication
- `CertificateAuthentication` (CertificateAuthenticationOptions): Certificate validation settings

### `CertificateAuthenticationOptions`
Certificate authentication and validation settings.

**Properties:**
- `AllowedThumbprints` (string): Comma-separated list of allowed certificate thumbprints
- `ValidateExpiration` (bool, default: true): Validate certificate expiration dates
- `ValidateCertificateChain` (bool, default: true): Validate certificate chain to trusted root
- `CheckCertificateRevocation` (bool, default: false): Check CRL for revoked certificates
- `ExpectedCARootName` (string, optional): Expected Root CA Subject name
- `ExpectedCARootThumbprint` (string, optional): Expected Root CA thumbprint
- `ExpectedSubordinateCAsJson` (string, optional): JSON array of expected Subordinate CAs
- `AllowedThumbprintsArray` (string[]): Parsed array of thumbprints for efficient lookup
- `ExpectedSubordinateCAs` (List<CertificateAuthorityInfo>): Parsed list of Subordinate CAs

### `CertificateAuthorityInfo`
Information about a Certificate Authority for validation.

**Properties:**
- `Name` (string): CA certificate Subject name
- `Thumbprint` (string): CA certificate thumbprint (SHA-1)

## Environment Variables Mapping

The function reads these environment variables and maps them to the configuration classes:

| Environment Variable | Configuration Property | Default | Required |
|---------------------|------------------------|---------|----------|
| `ApiKey` | `ProxyFunctionOptions.ApiKey` | - | Yes |
| `QueueStorageUri` | `ProxyFunctionOptions.QueueStorageUri` | - | Yes |
| `QueueName` | `ProxyFunctionOptions.QueueName` | "secureboot-reports" | No |
| `RequireCertificateAuthentication` | `ProxyFunctionOptions.RequireCertificateAuthentication` | false | No |
| `CertificateThumbprints` | `CertificateAuthentication.AllowedThumbprints` | "" | No |
| `CertificateValidateExpiration` | `CertificateAuthentication.ValidateExpiration` | true | No |
| `CertificateValidateChain` | `CertificateAuthentication.ValidateCertificateChain` | true | No |
| `CertificateCheckRevocation` | `CertificateAuthentication.CheckCertificateRevocation` | false | No |
| `CertificateExpectedCARootName` | `CertificateAuthentication.ExpectedCARootName` | null | No |
| `CertificateExpectedCARootThumbprint` | `CertificateAuthentication.ExpectedCARootThumbprint` | null | No |
| `CertificateExpectedSubordinateCAsJson` | `CertificateAuthentication.ExpectedSubordinateCAsJson` | null | No |

## Example Configuration

### local.settings.json (Local Development)
```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "ApiKey": "your-api-key-here",
    "QueueStorageUri": "https://yourstorageaccount.queue.core.windows.net",
    "QueueName": "secureboot-reports",
    "RequireCertificateAuthentication": "false",
    "CertificateThumbprints": "ABC123,DEF456",
    "CertificateValidateExpiration": "true",
    "CertificateValidateChain": "true",
    "CertificateCheckRevocation": "false"
  }
}
```

### Azure Portal Configuration (Production with CA Validation)
1. Navigate to your Function App in Azure Portal
2. Go to **Configuration** → **Application settings**
3. Add/edit the following settings:
   - `ApiKey`: Secure API key (use Key Vault reference)
   - `QueueStorageUri`: `https://yourstorageaccount.queue.core.windows.net`
   - `QueueName`: `secureboot-reports`
   - `RequireCertificateAuthentication`: `true` (for production)
   - `CertificateThumbprints`: Comma-separated list of allowed certificate thumbprints
   - `CertificateExpectedCARootName`: `CN=Contoso Root CA, O=Contoso, C=US`
   - `CertificateExpectedCARootThumbprint`: `ABC123DEF456789...`
   - `CertificateExpectedSubordinateCAsJson`: `[{"name":"CN=Contoso Issuing CA 01","thumbprint":"123ABC"},{"name":"CN=Contoso Issuing CA 02","thumbprint":"456DEF"}]`

## Certificate Authority Validation

### Root CA Validation
When specified, the function validates that client certificates chain to the expected Root CA:

**Environment Variables:**
- `CertificateExpectedCARootName`: Subject name of the Root CA (e.g., `"CN=Contoso Root CA, O=Contoso, C=US"`)
- `CertificateExpectedCARootThumbprint`: SHA-1 thumbprint of the Root CA (e.g., `"ABC123DEF456..."`)

**Validation Logic:**
1. Build certificate chain from client certificate
2. Extract root certificate (last element in chain)
3. Compare Subject name (if `ExpectedCARootName` is set)
4. Compare thumbprint (if `ExpectedCARootThumbprint` is set)
5. Reject if either check fails

**Example:**
```json
{
  "CertificateExpectedCARootName": "CN=Contoso Root CA, O=Contoso, C=US",
  "CertificateExpectedCARootThumbprint": "A1B2C3D4E5F6G7H8I9J0K1L2M3N4O5P6Q7R8S9T0"
}
```

### Subordinate CA Validation
When specified, the function validates that expected Intermediate CAs are present in the certificate chain:

**Environment Variable:**
- `CertificateExpectedSubordinateCAsJson`: JSON array of expected Subordinate CAs

**JSON Format:**
```json
[
  {
    "name": "CN=Contoso Issuing CA 01, O=Contoso, C=US",
    "thumbprint": "ABC123DEF456789..."
  },
  {
    "name": "CN=Contoso Issuing CA 02, O=Contoso, C=US",
    "thumbprint": "789GHI012JKL345..."
  }
]
```

**Validation Logic:**
1. Build certificate chain from client certificate
2. Extract all intermediate certificates (between leaf and root)
3. For each expected Subordinate CA:
   - Check if a matching intermediate certificate exists in chain
   - Match by Subject name (if `name` is specified)
   - Match by thumbprint (if `thumbprint` is specified)
   - Reject if expected CA is not found

**Use Case:**
Ensure client certificates are issued by specific organizational Issuing CAs, preventing acceptance of certificates from other CAs even if they chain to the same Root CA.

## Dependency Injection

The configuration is registered in `Program.cs` using the Options pattern:

```csharp
services.Configure<ProxyFunctionOptions>(options =>
{
    var config = context.Configuration;
    options.ApiKey = config["ApiKey"] ?? string.Empty;
    options.QueueStorageUri = config["QueueStorageUri"] ?? string.Empty;
    
    // ... other mappings including CA validation
    options.CertificateAuthentication.ExpectedCARootName = config["CertificateExpectedCARootName"];
    options.CertificateAuthentication.ExpectedCARootThumbprint = config["CertificateExpectedCARootThumbprint"];
    options.CertificateAuthentication.ExpectedSubordinateCAsJson = config["CertificateExpectedSubordinateCAsJson"];
});
```

The function receives the configuration via constructor injection:

```csharp
public SecureBootReportProxyFunction(
    ILogger<SecureBootReportProxyFunction> logger,
    IOptions<ProxyFunctionOptions> options)
{
    _logger = logger;
    _options = options.Value;
}
```

## Client Configuration

Clients should now use the **Azure Function sink** instead of connecting directly to Azure Queue:

```json
{
  "Sinks": {
    "EnableAzureFunction": true,
    "EnableAzureQueue": false,
    "ExecutionStrategy": "StopOnFirstSuccess",
    "SinkPriority": "AzureFunction,WebApi,FileShare",
    "AzureFunction": {
      "FunctionUrl": "https://your-function-app.azurewebsites.net/api/reports",
      "ApiKey": "your-api-key-here",
      "HttpTimeout": "00:00:30",
      "UseApiKeyAsQueryParameter": false,
      "UseCertificateAuth": false
    }
  }
}
```

## Migration Guide

### Before (Direct Azure Queue Access)
Clients configured with:
- `EnableAzureQueue: true`
- `QueueServiceUri`, `QueueName`, authentication credentials

### After (Azure Function Proxy)
Clients configured with:
- `EnableAzureFunction: true`
- `EnableAzureQueue: false`
- `FunctionUrl`, `ApiKey`

### Benefits
1. **Security**: Clients no longer need Azure Storage credentials
2. **Authentication**: Centralized API key + optional certificate authentication
3. **Monitoring**: All requests logged in Function App
4. **Flexibility**: Can change backend queue without client reconfiguration
5. **Throttling**: Azure Functions provides built-in throttling/rate limiting
6. **CA Validation**: Enforces organizational PKI policies server-side

## Security Considerations

### API Key Management
- Store API keys in **Azure Key Vault**
- Use Key Vault references in Function App configuration:
  ```
  @Microsoft.KeyVault(SecretUri=https://your-vault.vault.azure.net/secrets/ApiKey/)
  ```
- Rotate keys regularly
- Use different keys per environment (dev, staging, prod)

### Certificate Authentication
- Enable mutual TLS (`RequireCertificateAuthentication: true`) for production
- Maintain allowlist of trusted certificate thumbprints
- Use certificates issued by your organization's PKI
- Monitor certificate expiration dates
- Consider certificate revocation checking for high-security scenarios

### Certificate Authority Validation
- **Root CA Validation**: Ensures client certificates are issued by your organization's PKI
  - Protects against certificates from public CAs or other organizations
  - Use both name and thumbprint for strongest validation
- **Subordinate CA Validation**: Ensures client certificates are issued by specific Issuing CAs
  - Useful in multi-tier PKI environments
  - Allows granular control over which Issuing CAs are trusted
  - Example: Only accept certificates from "Device Management Issuing CA", not "User Issuing CA"

### Queue Access
- Function uses **Managed Identity** to access Azure Queue
- Assign **Storage Queue Data Contributor** role to the Function App
- No connection strings stored in configuration

## Troubleshooting

### "Invalid or missing API key"
- Verify `ApiKey` is configured in Function App settings
- Check client is sending key via `X-API-Key` header or `?code=` query parameter
- Ensure no extra spaces or special characters in key

### "Invalid or missing client certificate"
- Verify mutual TLS is enabled in Azure App Service
- Check certificate is in allowlist (`CertificateThumbprints`)
- Verify certificate is not expired
- Check certificate chain validation settings

### "Root CA name mismatch" or "Root CA thumbprint mismatch"
- Verify `CertificateExpectedCARootName` matches the Root CA Subject exactly
- Check `CertificateExpectedCARootThumbprint` is formatted correctly (no spaces/colons)
- Use `openssl x509 -in cert.pem -noout -subject -fingerprint` to extract values
- Ensure client certificate chains to the expected Root CA

### "Expected Subordinate CA not found in chain"
- Verify `CertificateExpectedSubordinateCAsJson` is valid JSON
- Check that Subordinate CA names match exactly (case-insensitive)
- Ensure Subordinate CA thumbprints are formatted correctly
- Verify client certificate includes the expected Intermediate CAs in its chain
- Use `openssl verify -verbose -CAfile root.pem -untrusted intermediate.pem client.pem` to check chain

### "Queue storage is not configured"
- Verify `QueueStorageUri` is set in Function App settings
- Check URI format: `https://account.queue.core.windows.net`
- Ensure Managed Identity has permissions to access queue

### "Failed to queue report"
- Check Function App Managed Identity has **Storage Queue Data Contributor** role
- Verify queue exists (Function does not auto-create queues)
- Check Azure Storage firewall rules allow Function App access
- Review Application Insights logs for detailed error messages

## Related Documentation
- [Azure Function Sink Guide](../docs/AZURE_FUNCTION_SINK_GUIDE.md)
- [Client Configuration Guide](../docs/CLIENT_CONFIGURATION.md)
- [Deployment Guide](../docs/DEPLOYMENT_GUIDE.md)
- [PKI Best Practices](https://docs.microsoft.com/en-us/windows/security/identity-protection/hello-for-business/hello-planning-guide)
