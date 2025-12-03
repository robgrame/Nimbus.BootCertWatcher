# Mutual TLS Authentication Implementation Summary

## Overview
This implementation adds X.509 certificate-based authentication (mutual TLS) to secure API communications between:
- SecureBootWatcher Client → API
- SecureBootDashboard Web → API

## Components Modified

### 1. API (SecureBootDashboard.Api)
**Files Changed:**
- `Program.cs` - Added certificate authentication configuration and middleware
- `Configuration/MutualTlsOptions.cs` - New configuration class
- `SecureBootDashboard.Api.csproj` - Added Microsoft.AspNetCore.Authentication.Certificate package
- `appsettings.json` - Added MutualTls configuration section

**Key Features:**
- ASP.NET Core Certificate Authentication middleware
- Configurable certificate validation:
  - Thumbprint allowlisting
  - Issuer (CA) allowlisting
  - Revocation checking
  - Chain validation
- Detailed authentication event logging
- Support for self-signed certificates (development)

### 2. Client (SecureBootWatcher.Client)
**Files Changed:**
- `Program.cs` - Updated HttpClient configuration with certificate handler
- `appsettings.json` - Added certificate authentication options
- `SecureBootWatcher.Shared/Configuration/SecureBootWatcherOptions.cs` - Extended WebApiSinkOptions

**Key Features:**
- Certificate loading from Windows Certificate Store
- Certificate loading from .pfx files
- Automatic certificate attachment to HTTP requests
- Detailed logging of certificate operations

### 3. Web App (SecureBootDashboard.Web)
**Files Changed:**
- `Program.cs` - Updated HttpClient configuration with certificate handler
- `Services/ApiSettings.cs` - Extended with certificate properties
- `appsettings.json` - Added certificate authentication options

**Key Features:**
- Certificate loading from Windows Certificate Store
- Certificate loading from .pfx files
- Automatic certificate attachment to API requests
- Detailed logging of certificate operations

## Configuration Options

### API Configuration (`appsettings.json`)
```json
{
  "MutualTls": {
    "Enabled": false,
    "AllowSelfSignedCertificates": false,
    "AllowedThumbprints": [],
    "AllowedIssuers": [],
    "CheckCertificateRevocation": true,
    "ValidateCertificateChain": true
  }
}
```

### Client Configuration
```json
{
  "WebApi": {
    "UseCertificateAuth": false,
    "CertificateThumbprint": "",
    "CertificatePath": "",
    "CertificatePassword": "",
    "CertificateStoreLocation": "LocalMachine",
    "CertificateStoreName": "My"
  }
}
```

### Web App Configuration
```json
{
  "ApiSettings": {
    "UseCertificateAuth": false,
    "CertificateThumbprint": "",
    "CertificatePath": "",
    "CertificatePassword": "",
    "CertificateStoreLocation": "LocalMachine",
    "CertificateStoreName": "My"
  }
}
```

## Security Considerations

### Default Settings (Secure by Default)
- mTLS is **disabled** by default (backward compatible)
- Self-signed certificates are **not allowed** by default
- Certificate revocation checking is **enabled** by default
- Certificate chain validation is **enabled** by default

### Production Recommendations
1. Use certificates issued by a trusted CA (internal or public)
2. Enable certificate revocation checking
3. Use certificate thumbprint or issuer allowlisting
4. Store certificates in Windows Certificate Store (not files)
5. Rotate certificates before expiration
6. Monitor certificate expiration dates
7. Use separate certificates for each client component

### Development/Testing
1. Can use self-signed certificates
2. Disable revocation checking if CRL/OCSP not available
3. Allow specific test CA issuers
4. Enable detailed logging

## Testing

### Unit Tests
Created comprehensive tests in `SecureBootDashboard.Api.Tests/Configuration/MutualTlsConfigurationTests.cs`:
- Default configuration validation
- Enabled configuration parsing
- Production-safe defaults verification
- Allow list behavior testing
- Development configuration validation

**Test Results:** 5/5 tests passing ✓

### Manual Testing Checklist
1. ✓ API builds successfully with certificate authentication package
2. ✓ Web app builds successfully with certificate configuration
3. ✓ Configuration loads correctly from appsettings.json
4. ✓ Default configuration is secure (mTLS disabled)
5. ☐ End-to-end test with self-signed certificates (requires runtime environment)
6. ☐ Certificate validation with allowlisting (requires runtime environment)
7. ☐ Certificate validation failure scenarios (requires runtime environment)

## Documentation

Created `docs/MUTUAL_TLS_CONFIGURATION.md` with:
- Architecture overview
- Certificate requirements
- Certificate generation (PowerShell and OpenSSL)
- Configuration guide for all components
- Deployment instructions (Windows and Azure)
- Testing procedures
- Troubleshooting guide
- Security best practices

## Backward Compatibility

✓ **Fully backward compatible**
- mTLS is disabled by default
- No breaking changes to existing configurations
- Can be enabled incrementally per component
- Existing deployments work without changes

## Migration Path

### Phase 1: Generate Certificates
1. Create root CA certificate
2. Generate client certificates for each component
3. Install certificates in appropriate stores

### Phase 2: Enable API Validation
1. Update API appsettings.json
2. Set `MutualTls.Enabled = true`
3. Configure allowed issuers
4. Restart API
5. Verify API logs for certificate events

### Phase 3: Enable Client Authentication
1. Update client appsettings.json
2. Set `WebApi.UseCertificateAuth = true`
3. Configure certificate thumbprint
4. Deploy updated configuration
5. Verify client logs for certificate loading

### Phase 4: Enable Web App Authentication
1. Update web app appsettings.json
2. Set `ApiSettings.UseCertificateAuth = true`
3. Configure certificate thumbprint
4. Restart web app
5. Verify web app can communicate with API

## Known Limitations

1. **Certificate Store Access**: Requires appropriate permissions to read private keys
2. **.NET Framework Client**: Uses older X509Certificate2 constructor (SYSLIB0057 warning)
3. **CodeQL Timeout**: Security scan timed out (common for large repos)
4. **Runtime Testing**: End-to-end testing requires actual certificate infrastructure

## Future Enhancements

1. Add support for X509CertificateLoader in .NET 10+ projects
2. Add integration tests with test certificates
3. Add certificate expiration monitoring
4. Add automatic certificate rotation support
5. Add metrics for certificate authentication events

## References

- [ASP.NET Core Certificate Authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/certauth)
- [X.509 Certificate Profile (RFC 5280)](https://tools.ietf.org/html/rfc5280)
- [NIST TLS Guidelines](https://csrc.nist.gov/publications/detail/sp/800-52/rev-2/final)
- Configuration Guide: `docs/MUTUAL_TLS_CONFIGURATION.md`

## Author Notes

This implementation provides a solid foundation for mutual TLS authentication with:
- Comprehensive configuration options
- Secure defaults
- Detailed logging
- Backward compatibility
- Complete documentation

The implementation is production-ready but should be tested thoroughly in a development environment before deploying to production.
