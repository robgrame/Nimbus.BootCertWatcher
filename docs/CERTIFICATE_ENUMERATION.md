# Secure Boot Certificate Enumeration

This document describes the certificate enumeration feature that captures detailed information about Secure Boot certificates stored in UEFI firmware databases.

## Overview

The certificate enumeration feature collects detailed information about all certificates stored in the UEFI Secure Boot databases:
- **db** (Signature Database) - Certificates authorized to load
- **dbx** (Forbidden Signature Database) - Certificates blocked from loading
- **KEK** (Key Exchange Keys) - Certificates authorized to update db and dbx
- **PK** (Platform Key) - Top-level platform owner key

## Certificate Information Collected

For each certificate, the following details are captured:

| Property | Description |
|----------|-------------|
| **Database** | Which UEFI database contains this certificate (db, dbx, KEK, PK) |
| **Thumbprint** | SHA-1 hash of the certificate |
| **Subject** | Certificate subject name |
| **Issuer** | Certificate issuer name |
| **SerialNumber** | Certificate serial number |
| **NotBefore** | Certificate validity start date |
| **NotAfter** | Certificate expiration date |
| **SignatureAlgorithm** | Algorithm used for certificate signature |
| **PublicKeyAlgorithm** | Public key algorithm (RSA, ECC, etc.) |
| **KeySize** | Key size in bits |
| **IsExpired** | Whether the certificate is currently expired |
| **DaysUntilExpiration** | Days remaining until expiration (negative if expired) |
| **Version** | X.509 certificate version |
| **IsMicrosoftCertificate** | Whether this is a Microsoft-issued certificate |
| **RawData** | Base64-encoded certificate data (DER format) |

## Collection Statistics

The system also calculates aggregate statistics:

- **TotalCertificateCount** - Total certificates across all databases
- **ExpiredCertificateCount** - Number of expired certificates
- **ExpiringCertificateCount** - Number of certificates expiring within 90 days
- **SecureBootEnabled** - Whether Secure Boot is currently enabled

### Important Note About Secure Boot Status

**As of version 1.3.1**, certificate enumeration proceeds **regardless of whether Secure Boot is enabled or disabled**. This change allows organizations to:

- **Inventory certificates on all UEFI devices**, not just those with Secure Boot enabled
- **Plan Secure Boot deployment** by understanding the certificate landscape before enabling
- **Maintain compliance tracking** even on devices where Secure Boot is temporarily disabled
- **Monitor certificate expiration** proactively across the entire fleet

The `SecureBootEnabled` field in the report indicates whether Secure Boot was active at the time of enumeration, but this no longer blocks certificate collection. On UEFI systems, certificate databases exist in firmware even when Secure Boot is disabled - they're just not being enforced for boot validation.

## Implementation Details

### PowerShell-Based Enumeration

The preferred implementation uses PowerShell's `Get-SecureBootUEFI` cmdlet, which reliably accesses UEFI variables:

```powershell
# Check if Secure Boot is enabled
Confirm-SecureBootUEFI

# Get database contents
Get-SecureBootUEFI -Name db
Get-SecureBootUEFI -Name dbx
Get-SecureBootUEFI -Name KEK
Get-SecureBootUEFI -Name PK
```

The cmdlet returns EFI Signature List structures, which are parsed to extract individual certificates.

### EFI Signature List Format

UEFI databases use the EFI_SIGNATURE_LIST structure:

```
struct EFI_SIGNATURE_LIST {
    GUID   SignatureType;          // Type of signatures (X509, SHA256, etc.)
    UINT32 SignatureListSize;      // Total size of this list
    UINT32 SignatureHeaderSize;    // Size of header
    UINT32 SignatureSize;          // Size of each signature entry
    UINT8  SignatureHeader[];      // Optional header data
    EFI_SIGNATURE_DATA Signatures[]; // Array of signatures
}

struct EFI_SIGNATURE_DATA {
    GUID  SignatureOwner;   // GUID of entity that enrolled this signature
    UINT8 SignatureData[];   // The actual signature (certificate, hash, etc.)
}
```

### Certificate Types

The system primarily focuses on X.509 certificates (signature type GUID: `a5c059a1-94e4-4aa7-87b5-ab155c2bf072`). Other signature types (SHA256 hashes, etc.) are logged but not parsed as full certificates.

## Requirements

### Client Requirements

- **Windows 10/11** or **Windows Server 2016+** with UEFI firmware
- **PowerShell 5.0+** with `Get-SecureBootUEFI` cmdlet available
- **Elevated privileges** (SYSTEM or Administrator) to read UEFI variables
- **Note**: Certificate enumeration works regardless of whether Secure Boot is enabled or disabled

### API/Database Requirements

- Database migration `AddCertificateCollection` must be applied
- `CertificatesJson` column added to `SecureBootReports` table (nvarchar(max))

## Usage

### Client Configuration

Certificate enumeration is **enabled by default**. The client automatically attempts to enumerate certificates when building each report.

If certificate enumeration fails (e.g., Secure Boot disabled, insufficient permissions), the error is logged but the report continues without certificate data.

### Viewing Certificate Data

#### Via API

```http
GET /api/SecureBootReports/{id}
```

Response includes `CertificatesJson` field with full certificate collection.

#### Via Dashboard

Certificate details are displayed in the report detail view, organized by database type with expiration warnings for expired or expiring certificates.

## Alerts Generated

The system generates alerts based on certificate status:

- **Expired Certificates**: "{count} expired certificate(s) detected in Secure Boot databases."
- **Expiring Soon**: "{count} certificate(s) expiring within 90 days."
- **Enumeration Error**: "Certificate enumeration error: {error message}"

**Note**: The "Secure Boot Not Enabled" alert is no longer generated as a blocking error. The Secure Boot status is recorded in the report metadata but does not prevent certificate enumeration.

## Troubleshooting

### No Certificate Data Collected

**Cause**: Insufficient permissions
**Resolution**: Ensure client runs as SYSTEM or Administrator

**Cause**: PowerShell cmdlet not available
**Resolution**: Verify Windows version supports `Get-SecureBootUEFI` (Windows 8+)

**Note**: As of v1.3.1, certificate enumeration proceeds even when Secure Boot is disabled. The Secure Boot status is captured in the report but does not prevent certificate inventory.

### Empty Certificate Lists

**Cause**: Legacy BIOS (non-UEFI) system
**Resolution**: Certificate enumeration only works on UEFI systems

**Cause**: Secure Boot databases not provisioned
**Resolution**: Some systems may have Secure Boot enabled but empty databases (unusual)

### Certificate Parsing Errors

**Cause**: Non-X.509 signature types in databases
**Resolution**: Only X.509 certificates are fully parsed; hashes and other types are logged but skipped

## Security Considerations

- Certificate raw data is stored base64-encoded for analysis but should be treated as sensitive
- Private keys are **never** accessible via UEFI variables (only public certificates)
- Certificate thumbprints can be used to correlate with public certificate databases
- Microsoft certificates can be validated against Microsoft's public root CA list

## Performance Impact

Certificate enumeration adds approximately:
- **1-3 seconds** to report generation time
- **50-500 KB** per report (depending on certificate count)
- Minimal CPU impact (PowerShell process execution)

The PowerShell execution is asynchronous and does not block other report operations.

## Future Enhancements

Potential improvements for future versions:

1. **Certificate Revocation Checking** - Query CRL/OCSP for revocation status
2. **Certificate Chain Validation** - Validate certificate trust chains
3. **Historical Tracking** - Track certificate changes over time
4. **Custom Certificate Alerts** - User-defined rules for certificate monitoring
5. **Certificate Export** - Bulk export certificates for offline analysis
6. **WMI-Based Enumeration** - Alternative enumeration method without PowerShell dependency

## References

- [UEFI Specification](https://uefi.org/specifications) - EFI_SIGNATURE_LIST structure definition
- [Get-SecureBootUEFI cmdlet](https://learn.microsoft.com/en-us/powershell/module/secureboot/get-securebootuefi) - Microsoft documentation
- [Secure Boot Overview](https://learn.microsoft.com/en-us/windows-hardware/design/device-experiences/oem-secure-boot) - Windows Hardware Dev Center
- [UEFI CA 2023 Update](https://support.microsoft.com/en-us/topic/kb5021131-secure-boot-dbx-update) - Microsoft support article
