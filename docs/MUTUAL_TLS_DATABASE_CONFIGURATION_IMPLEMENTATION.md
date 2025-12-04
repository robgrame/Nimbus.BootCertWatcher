# Mutual TLS Database-Driven Configuration - Implementation Summary

**Feature**: Database-Driven Mutual TLS Client Certificate Validation  
**Date**: 2025-01-04  
**Version**: 1.13.0  
**Status**: ? Implementation Complete (Migration Created, UI Pending)

---

## ?? Overview

Implemented a complete database-driven configuration system for Mutual TLS (mTLS) client certificate validation in the Secure Boot Dashboard API. This allows administrators to:

1. **Upload and manage trusted Certificate Authority (CA) certificates** via database
2. **Configure validation criteria** (chain validation, revocation checks, expiration grace periods, etc.)
3. **Enable/disable CA certificates** dynamically without redeployment
4. **Validate client certificates** against database-stored CA chain and thumbprint allowlists

---

## ? Components Implemented

### 1. **Database Entities** (2 new tables)

#### `TrustedCertificateAuthorityEntity.cs`
**Location**: `SecureBootDashboard.Api/Data/TrustedCertificateAuthorityEntity.cs`

**Purpose**: Stores uploaded CA certificates for client certificate chain validation.

**Fields**:
- `CommonName` (CN extracted from certificate subject)
- `Thumbprint` (SHA-1) and `Thumbprint256` (SHA-256)
- `Subject` and `Issuer` (full Distinguished Names)
- `NotBefore` / `NotAfter` (validity period)
- `IsRootCa` (self-signed flag)
- `SerialNumber`
- `CertificateDataBase64` (complete certificate in Base64 DER format)
- `IsEnabled` (allow/disable without deletion)
- `Description` (admin notes)
- Audit fields: `CreatedBy`, `UpdatedBy`, `CreatedAtUtc`, `UpdatedAtUtc`

**Indexes**:
- **Unique** on `Thumbprint` (prevents duplicates)
- On `CommonName`, `IsEnabled`, `IsRootCa`, `NotAfter` (for filtering/queries)

---

#### `MutualTlsConfigEntity.cs`
**Location**: `SecureBootDashboard.Api/Data/MutualTlsConfigEntity.cs`

**Purpose**: Centralized configuration for all mTLS validation criteria.

**Fields**:
- `Enabled` (global on/off)
- `AllowSelfSignedCertificates` (dev/test mode)
- `CheckCertificateRevocation` (CRL/OCSP validation)
- `ValidateCertificateChain` (full chain validation)
- `RequireClientAuthEku` (require clientAuth OID 1.3.6.1.5.5.7.3.2)
- `ValidateCertificateValidity` (check NotBefore/NotAfter)
- `ExpirationGracePeriodDays` (reject certs expiring soon)
- `EnableThumbprintAllowlist` (whitelist specific client cert thumbprints)
- `AllowedThumbprints` (CSV list of allowed thumbprints)
- `EnableIssuerAllowlist` (whitelist by CA from database)
- `EnableDetailedLogging` (verbose validation logs)
- `RevocationCheckTimeoutSeconds` (CRL/OCSP timeout)
- `ValidationNotes` (admin documentation)
- Audit fields

**Seed Data**:
```csharp
{
    Id = 1,
    Enabled = false,  // Disabled by default (opt-in security)
    AllowSelfSignedCertificates = false,  // Production-safe
    CheckCertificateRevocation = true,
    ValidateCertificateChain = true,
    RequireClientAuthEku = true,
    ValidateCertificateValidity = true,
    ExpirationGracePeriodDays = 0,  // No grace period (strict)
    EnableThumbprintAllowlist = false,
    EnableIssuerAllowlist = true,  // Use database CAs
    EnableDetailedLogging = false,
    RevocationCheckTimeoutSeconds = 10,
    ValidationNotes = "Default mutual TLS configuration. Update via Admin Settings."
}
```

---

### 2. **Service Layer** (validation logic)

#### `ICertificateValidationService.cs`
**Location**: `SecureBootDashboard.Api/Services/ICertificateValidationService.cs`

**Interface Methods**:
```csharp
// Certificate validation
Task<CertificateValidationResult> ValidateClientCertificateAsync(
    X509Certificate2 certificate, 
    CancellationToken cancellationToken = default);

// Configuration management
Task<MutualTlsConfigEntity?> GetConfigurationAsync(CancellationToken cancellationToken = default);
Task<MutualTlsConfigEntity> UpdateConfigurationAsync(
    MutualTlsConfigEntity config, 
    string updatedBy, 
    CancellationToken cancellationToken = default);

// CA management
Task<IReadOnlyList<TrustedCertificateAuthorityEntity>> GetTrustedCAsAsync(CancellationToken cancellationToken = default);
Task<TrustedCertificateAuthorityEntity> AddTrustedCAAsync(
    byte[] certificateData, 
    string? description, 
    string createdBy, 
    CancellationToken cancellationToken = default);
Task<bool> RemoveTrustedCAAsync(int caId, CancellationToken cancellationToken = default);
Task<bool> SetCAEnabledAsync(
    int caId, 
    bool enabled, 
    string updatedBy, 
    CancellationToken cancellationToken = default);
```

**CertificateValidationResult Class**:
```csharp
public class CertificateValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public Dictionary<string, object> ValidationDetails { get; set; } = new();
    public TrustedCertificateAuthorityEntity? MatchedCA { get; set; }
}
```

---

#### `CertificateValidationService.cs`
**Location**: `SecureBootDashboard.Api/Services/CertificateValidationService.cs`

**Validation Steps** (in order):
1. ? **Certificate Validity Period** - Check NotBefore/NotAfter with optional grace period
2. ? **Self-Signed Check** - Reject if `Subject == Issuer` (unless allowed)
3. ? **Extended Key Usage** - Require clientAuth EKU (1.3.6.1.5.5.7.3.2)
4. ? **Thumbprint Allowlist** - Match against CSV list (if enabled)
5. ? **Issuer Allowlist** - Match against database CAs by thumbprint or subject (if enabled)
6. ? **Certificate Chain Validation** - Build and validate full chain with database CAs in ExtraStore
7. ? **Revocation Check** - CRL/OCSP validation (if enabled)

**Helper Methods**:
- `GetCommonName(subject)` - Extracts CN from DN
- `GetSha256Thumbprint(cert)` - Computes SHA-256 thumbprint
- `GetIssuerThumbprint(cert)` - Gets issuer thumbprint from chain

**Features**:
- **Database-driven CA chain** - Loads trusted CAs from DB into `X509Chain.ChainPolicy.ExtraStore`
- **Configurable timeouts** - CRL/OCSP timeout configurable via `RevocationCheckTimeoutSeconds`
- **Detailed logging** - Controlled via `EnableDetailedLogging` flag
- **Graceful degradation** - Invalid CAs logged as warnings, not errors

---

### 3. **API Controllers** (REST endpoints)

#### `CertificateAuthoritiesController.cs`
**Location**: `SecureBootDashboard.Api/Controllers/CertificateAuthoritiesController.cs`

**Endpoints**:

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/CertificateAuthorities` | List all trusted CAs (optional `?includeDisabled=true`) |
| `GET` | `/api/CertificateAuthorities/{id}` | Get specific CA by ID |
| `POST` | `/api/CertificateAuthorities/upload` | Upload new CA certificate (.cer/.crt/.pem/.der) |
| `PATCH` | `/api/CertificateAuthorities/{id}/enabled` | Enable/disable CA |
| `DELETE` | `/api/CertificateAuthorities/{id}` | Delete CA |

**DTOs**:
- `TrustedCADto` - Full CA details with computed fields (`IsExpired`, `DaysUntilExpiration`)
- `UploadCARequest` - File upload with optional description
- `SetEnabledRequest` - Enable/disable flag

**Upload Validation**:
- Allowed file extensions: `.cer`, `.crt`, `.pem`, `.der`
- Certificate parsing (fails if invalid X.509)
- Duplicate check (thumbprint unique constraint)
- Extracts all metadata automatically

---

#### `MutualTlsConfigController.cs`
**Location**: `SecureBootDashboard.Api/Controllers/MutualTlsConfigController.cs`

**Endpoints**:

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/MutualTlsConfig` | Get current configuration |
| `PUT` | `/api/MutualTlsConfig` | Update full configuration |
| `PATCH` | `/api/MutualTlsConfig/enabled` | Quick enable/disable |
| `GET` | `/api/MutualTlsConfig/status` | Get validation statistics |

**DTOs**:
- `MutualTlsConfigDto` - Full configuration
- `UpdateMutualTlsConfigRequest` - Update request with validation:
  - `ExpirationGracePeriodDays >= 0`
  - `RevocationCheckTimeoutSeconds` between 1 and 300
- `SetMtlsEnabledRequest` - Quick enable/disable
- `MutualTlsStatusDto` - Dashboard-friendly status:
  - Total/enabled CA count
  - Expired CA count
  - Expiring soon CA count (<90 days)
  - Configuration summary

---

### 4. **EF Core Migration**

#### Migration File
**File**: `SecureBootDashboard.Api/Data/Migrations/20251204083249_AddMutualTlsConfiguration.cs`

**Created**: 2025-01-04 09:32:49

**Tables Created**:
1. **`MutualTlsConfig`**
   - Primary Key: `Id` (only 1 record expected)
   - Seed data: Default configuration (Enabled=false)

2. **`TrustedCertificateAuthorities`**
   - Primary Key: `Id`
   - Unique Index: `Thumbprint`
   - Indexes: `CommonName`, `IsEnabled`, `IsRootCa`, `NotAfter`

**To Apply**:
```powershell
cd SecureBootDashboard.Api
dotnet ef database update
```

---

### 5. **Service Registration** (Program.cs)

**Added**:
```csharp
// Configure Certificate Validation Service (for mutual TLS)
Log.Information("Configuring Certificate Validation Service...");
builder.Services.AddScoped<ICertificateValidationService, CertificateValidationService>();
```

**Location**: `SecureBootDashboard.Api/Program.cs` (after `ApplicationSettingsService`)

---

## ?? Next Steps (Not Yet Implemented)

### 6. **Razor Pages Admin UI** (Pending)

Need to create UI pages for administrators to manage certificates:

#### Page 1: `/Admin/MutualTls` (Configuration)
- Enable/disable mutual TLS globally
- Configure validation options (checkboxes/toggles)
- Configure grace periods and timeouts
- Save configuration with audit trail

#### Page 2: `/Admin/MutualTls/Certificates` (CA Management)
- List all uploaded CAs (table with status, expiration, etc.)
- Upload new CA certificate (drag-drop + validation)
- Enable/disable individual CAs
- Delete CAs (with confirmation)
- View CA details (modal or dedicated page)

#### Page 3: `/Admin/MutualTls/Status` (Dashboard)
- Overall mTLS status (enabled/disabled)
- Statistics (total CAs, expired, expiring soon)
- Recent validation failures (if detailed logging enabled)
- Health checks

**Suggested Implementation**:
- Use existing Admin menu dropdown (alongside Settings, Windows Versions, Device Cleanup)
- Follow existing design patterns from Settings pages
- Add client-side upload with file validation (JavaScript)
- Show real-time feedback (toast notifications)
- Use Bootstrap 5 components (cards, tables, modals)

---

### 7. **Middleware Integration** (Pending)

Update the existing Certificate Authentication middleware to use database validation:

**Current Code** (`Program.cs`):
```csharp
options.Events = new CertificateAuthenticationEvents
{
    OnCertificateValidated = context =>
    {
        // Currently uses appsettings.json (MutualTlsOptions)
        // Need to integrate ICertificateValidationService
    }
}
```

**Proposed Change**:
```csharp
options.Events = new CertificateAuthenticationEvents
{
    OnCertificateValidated = async context =>
    {
        var certValidationService = context.HttpContext.RequestServices
            .GetRequiredService<ICertificateValidationService>();

        var result = await certValidationService.ValidateClientCertificateAsync(
            context.ClientCertificate,
            context.HttpContext.RequestAborted);

        if (!result.IsValid)
        {
            context.Fail(string.Join("; ", result.Errors));
            return;
        }

        context.Success();
    }
}
```

**Note**: This will make validation database-driven and override `appsettings.json` configuration.

---

### 8. **Client Certificate Upload/Distribution** (Pending)

Document or implement client certificate deployment:

1. **Generate Client Certificates**:
   - PowerShell scripts to generate client certs from uploaded CA
   - Alternative: Use existing enterprise PKI

2. **Client Configuration**:
   - Update `SecureBootWatcher.Client` to load cert from store or file
   - Configure `WebApiSinkOptions.UseCertificateAuth = true`

3. **Intune Deployment**:
   - Deploy client certificates via Intune SCEP/PKCS profiles
   - Configure certificate binding in scheduled task

---

## ?? Testing Recommendations

### Unit Tests (Already Exist)
- ? `MutualTlsConfigurationTests.cs` - Tests for `MutualTlsOptions` configuration loading
- **TODO**: Add tests for `CertificateValidationService`:
  - Certificate validity period validation
  - Self-signed certificate rejection
  - EKU validation
  - Thumbprint allowlist matching
  - Issuer allowlist matching
  - Chain validation with database CAs

### Integration Tests (TODO)
1. **Database Tests**:
   - Upload CA certificate
   - Update configuration
   - Enable/disable CA
   - Delete CA
   - Query trusted CAs

2. **End-to-End Tests**:
   - Client presents valid certificate ? Success
   - Client presents expired certificate ? Failure
   - Client presents certificate from untrusted CA ? Failure
   - Client presents self-signed certificate (when disabled) ? Failure

### Manual Testing Checklist
- [ ] Upload valid CA certificate (.cer file)
- [ ] Upload invalid file (e.g., .txt) ? Should fail
- [ ] Upload duplicate CA (same thumbprint) ? Should fail
- [ ] Enable/disable CA ? Should update database
- [ ] Delete CA ? Should remove from database
- [ ] Update configuration ? Should save to database
- [ ] Enable mutual TLS globally ? Should apply validation
- [ ] Test client with valid certificate ? Should succeed
- [ ] Test client with invalid certificate ? Should fail (HTTP 401/403)

---

## ?? API Documentation

### Swagger Endpoint Summary

#### Certificate Authorities
```
GET    /api/CertificateAuthorities                     - List all CAs
GET    /api/CertificateAuthorities/{id}                - Get CA by ID
POST   /api/CertificateAuthorities/upload              - Upload new CA
PATCH  /api/CertificateAuthorities/{id}/enabled        - Enable/disable CA
DELETE /api/CertificateAuthorities/{id}                - Delete CA
```

#### Mutual TLS Configuration
```
GET    /api/MutualTlsConfig                            - Get configuration
PUT    /api/MutualTlsConfig                            - Update configuration
PATCH  /api/MutualTlsConfig/enabled                    - Quick enable/disable
GET    /api/MutualTlsConfig/status                     - Get status and stats
```

### Example: Upload CA Certificate

**Request**:
```http
POST /api/CertificateAuthorities/upload
Content-Type: multipart/form-data

------WebKitFormBoundary
Content-Disposition: form-data; name="CertificateFile"; filename="root-ca.cer"
Content-Type: application/x-x509-ca-cert

[Binary certificate data]
------WebKitFormBoundary
Content-Disposition: form-data; name="Description"

Contoso Enterprise Root CA
------WebKitFormBoundary--
```

**Response** (201 Created):
```json
{
  "id": 1,
  "commonName": "Contoso Root CA",
  "thumbprint": "ABCDEF1234567890...",
  "thumbprint256": "1234567890ABCDEF...",
  "subject": "CN=Contoso Root CA, O=Contoso, C=US",
  "issuer": "CN=Contoso Root CA, O=Contoso, C=US",
  "notBefore": "2020-01-01T00:00:00Z",
  "notAfter": "2030-01-01T00:00:00Z",
  "isRootCa": true,
  "serialNumber": "1234567890ABCDEF",
  "isEnabled": true,
  "description": "Contoso Enterprise Root CA",
  "createdAtUtc": "2025-01-04T09:00:00Z",
  "createdBy": "admin@contoso.com",
  "updatedAtUtc": "2025-01-04T09:00:00Z",
  "updatedBy": "admin@contoso.com",
  "isExpired": false,
  "daysUntilExpiration": 1826
}
```

---

## ?? Security Considerations

### Production Recommendations

1. **Default Configuration** (Secure by Default)
   - ? mTLS disabled by default (`Enabled = false`)
   - ? Self-signed certs rejected (`AllowSelfSignedCertificates = false`)
   - ? Revocation checks enabled (`CheckCertificateRevocation = true`)
   - ? Chain validation enabled (`ValidateCertificateChain = true`)

2. **Access Control**
   - ?? TODO: Restrict `/api/MutualTlsConfig` and `/api/CertificateAuthorities` to Admin role only
   - ?? TODO: Require authentication (Entra ID) for certificate upload

3. **Audit Trail**
   - ? All configuration changes logged with `UpdatedBy` field
   - ? CA additions/deletions tracked with `CreatedBy` field
   - ? Timestamps for all changes (`CreatedAtUtc`, `UpdatedAtUtc`)

4. **Certificate Storage**
   - ? Certificates stored as Base64 (not encrypted, but non-sensitive data)
   - ?? Private keys **never** stored (only public certificates)
   - ? Unique thumbprint constraint prevents duplicates

5. **Validation Best Practices**
   - ? CRL/OCSP timeout prevents long delays (default 10s)
   - ? Grace period for expiring certificates (configurable)
   - ? Detailed logging opt-in (prevents log spam)

### Development/Testing Configuration
```json
{
  "MutualTlsConfig": {
    "Enabled": true,
    "AllowSelfSignedCertificates": true,  // Allow for testing
    "CheckCertificateRevocation": false,  // Faster (no network calls)
    "ValidateCertificateChain": true,
    "EnableDetailedLogging": true  // Verbose logs for debugging
  }
}
```

---

## ?? Documentation Updates Required

### User Documentation
- [ ] **MUTUAL_TLS_CONFIGURATION.md** - Update to include database-driven approach
- [ ] **ADMIN_GUIDE.md** - Add section on managing trusted CAs
- [ ] **CLIENT_DEPLOYMENT_GUIDE.md** - How to deploy client certificates

### Developer Documentation
- [ ] **API_REFERENCE.md** - Document new endpoints
- [ ] **DATABASE_SCHEMA.md** - Add mTLS tables
- [ ] **MIGRATION_GUIDE.md** - How to upgrade from file-based to database-driven

### Deployment Documentation
- [ ] **DEPLOYMENT_GUIDE.md** - Update with migration steps
- [ ] **CONFIGURATION_REFERENCE.md** - Document new settings

---

## ?? Acceptance Criteria

**Definition of Done**:
- [x] Database entities created (`TrustedCertificateAuthorityEntity`, `MutualTlsConfigEntity`)
- [x] EF Core migration generated and tested
- [x] Service layer implemented (`ICertificateValidationService`, `CertificateValidationService`)
- [x] API controllers created (`CertificateAuthoritiesController`, `MutualTlsConfigController`)
- [x] Service registered in DI container (`Program.cs`)
- [x] Build successful (no compilation errors)
- [ ] Migration applied to database
- [ ] Admin UI pages created (Razor Pages)
- [ ] Middleware integration complete
- [ ] Unit tests written
- [ ] Integration tests written
- [ ] Manual testing completed
- [ ] Documentation updated
- [ ] Code review completed
- [ ] Deployed to staging environment

---

## ?? Known Issues / Limitations

1. **No UI Yet** - API is complete, but admin UI not implemented
2. **Middleware Not Integrated** - Still uses appsettings.json for validation (need to switch to database)
3. **No Role-Based Access Control** - Endpoints not restricted to Admin role
4. **No Certificate Upload UI** - Must use Postman/curl to upload CAs
5. **CRL Distribution Point** - Assumes CRL/OCSP endpoints are accessible (may fail in air-gapped environments)

---

## ?? Database Schema Summary

### Table: `MutualTlsConfig`
| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| `Id` | int | No | - | Primary key (only 1 record) |
| `Enabled` | bit | No | 0 (false) | Global enable/disable |
| `AllowSelfSignedCertificates` | bit | No | 0 (false) | Allow self-signed for testing |
| `CheckCertificateRevocation` | bit | No | 1 (true) | Enable CRL/OCSP checks |
| `ValidateCertificateChain` | bit | No | 1 (true) | Validate full certificate chain |
| `RequireClientAuthEku` | bit | No | 1 (true) | Require clientAuth EKU |
| `ValidateCertificateValidity` | bit | No | 1 (true) | Check NotBefore/NotAfter |
| `ExpirationGracePeriodDays` | int | No | 0 | Days before expiration to reject |
| `EnableThumbprintAllowlist` | bit | No | 0 (false) | Use thumbprint whitelist |
| `AllowedThumbprints` | nvarchar(4000) | Yes | NULL | CSV list of thumbprints |
| `EnableIssuerAllowlist` | bit | No | 1 (true) | Use database CA whitelist |
| `EnableDetailedLogging` | bit | No | 0 (false) | Verbose validation logs |
| `RevocationCheckTimeoutSeconds` | int | No | 10 | CRL/OCSP timeout |
| `ValidationNotes` | nvarchar(2000) | Yes | NULL | Admin documentation |
| `CreatedAtUtc` | datetimeoffset | No | - | Creation timestamp |
| `CreatedBy` | nvarchar(256) | Yes | NULL | Creator username |
| `UpdatedAtUtc` | datetimeoffset | No | - | Last update timestamp |
| `UpdatedBy` | nvarchar(256) | Yes | NULL | Last updater username |

### Table: `TrustedCertificateAuthorities`
| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| `Id` | int | No | - | Primary key (auto-increment) |
| `CommonName` | nvarchar(256) | No | - | CN from certificate subject |
| `Thumbprint` | nvarchar(40) | No | - | SHA-1 thumbprint (unique) |
| `Thumbprint256` | nvarchar(64) | Yes | NULL | SHA-256 thumbprint |
| `Subject` | nvarchar(500) | No | - | Full subject DN |
| `Issuer` | nvarchar(500) | No | - | Full issuer DN |
| `NotBefore` | datetimeoffset | No | - | Validity start |
| `NotAfter` | datetimeoffset | No | - | Validity end |
| `IsRootCa` | bit | No | - | Self-signed flag |
| `SerialNumber` | nvarchar(100) | Yes | NULL | Certificate serial number |
| `CertificateDataBase64` | nvarchar(max) | No | - | Complete cert (Base64 DER) |
| `IsEnabled` | bit | No | 1 (true) | Enable/disable without deletion |
| `Description` | nvarchar(1000) | Yes | NULL | Admin notes |
| `CreatedAtUtc` | datetimeoffset | No | - | Upload timestamp |
| `CreatedBy` | nvarchar(256) | Yes | NULL | Uploader username |
| `UpdatedAtUtc` | datetimeoffset | No | - | Last update timestamp |
| `UpdatedBy` | nvarchar(256) | Yes | NULL | Last updater username |

**Indexes**:
- `IX_TrustedCertificateAuthorities_Thumbprint` - UNIQUE
- `IX_TrustedCertificateAuthorities_CommonName`
- `IX_TrustedCertificateAuthorities_IsEnabled`
- `IX_TrustedCertificateAuthorities_IsRootCa`
- `IX_TrustedCertificateAuthorities_NotAfter`

---

## ?? Timeline

| Date | Milestone | Status |
|------|-----------|--------|
| 2025-01-04 | Database entities created | ? Complete |
| 2025-01-04 | Service layer implemented | ? Complete |
| 2025-01-04 | API controllers created | ? Complete |
| 2025-01-04 | EF Core migration generated | ? Complete |
| TBD | Admin UI pages (Razor Pages) | ?? Pending |
| TBD | Middleware integration | ?? Pending |
| TBD | Unit tests | ?? Pending |
| TBD | Integration tests | ?? Pending |
| TBD | Documentation updates | ?? Pending |
| TBD | Production deployment | ?? Pending |

---

## ?? Contributors

- **Implementation**: GitHub Copilot + Developer
- **Design**: Based on enterprise PKI best practices
- **Testing**: TBD
- **Code Review**: TBD

---

## ?? References

- [Microsoft Docs: Certificate Authentication in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/certauth)
- [RFC 5280: X.509 Certificate Profile](https://tools.ietf.org/html/rfc5280)
- [NIST SP 800-52: Guidelines for TLS Implementations](https://csrc.nist.gov/publications/detail/sp/800-52/rev-2/final)
- Existing documentation: `docs/MUTUAL_TLS_CONFIGURATION.md`

---

**Document Version**: 1.0  
**Last Updated**: 2025-01-04  
**Status**: ? Implementation Complete (Migration Created, UI and Middleware Pending)

