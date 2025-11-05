# Certificate Enumeration - Quick Start Guide

## What Changed?

The Secure Boot Certificate Watcher now enumerates **actual boot certificates** from UEFI firmware, not just deployment status. You now get full certificate details including thumbprints, expiration dates, and issuer information.

## Deployment Steps

### 1. Update the Database

Apply the new migration to add certificate storage:

```bash
dotnet ef database update --project SecureBootDashboard.Api
```

This adds the `CertificatesJson` column to the `SecureBootReports` table.

### 2. Build and Deploy

```bash
# Build
dotnet build --configuration Release

# Publish API
dotnet publish SecureBootDashboard.Api -c Release -o ./publish/api

# Publish Client
dotnet publish SecureBootWatcher.Client -c Release -r win-x86 --self-contained false -o ./publish/client
```

### 3. Deploy Client

**No configuration changes required!** Certificate enumeration is automatic.

Requirements:
- Windows 10/11 or Server 2016+ with **UEFI** firmware
- **Secure Boot enabled** in BIOS/UEFI
- Client running as **Administrator** or **SYSTEM**
- PowerShell 5.0+ (built into Windows)

## What You'll See

### New Certificate Data

Reports now include certificate collections:

```json
{
  "certificates": {
    "signatureDatabase": [ ... ],      // Authorized to boot
    "forbiddenDatabase": [ ... ],// Blocked from booting
    "keyExchangeKeys": [ ... ],        // Can update db/dbx
    "platformKeys": [ ... ],     // Platform owner
    "totalCertificateCount": 15,
    "expiredCertificateCount": 0,
    "expiringCertificateCount": 2,
    "secureBootEnabled": true
  }
}
```

### New Alerts

- "Secure Boot is not enabled on this device."
- "X expired certificate(s) detected in Secure Boot databases."
- "X certificate(s) expiring within 90 days."

### Certificate Details

Each certificate includes:
- **Thumbprint** - SHA-1 hash for identification
- **Subject/Issuer** - Who issued the certificate
- **Expiration** - NotBefore and NotAfter dates
- **Status** - IsExpired and DaysUntilExpiration
- **Microsoft Detection** - IsMicrosoftCertificate flag
- **Algorithms** - SignatureAlgorithm, PublicKeyAlgorithm, KeySize

## Testing

### Test Certificate Enumeration

On a client machine:

```powershell
# 1. Verify Secure Boot is enabled
Confirm-SecureBootUEFI
# Should return: True

# 2. Check certificate databases exist
Get-SecureBootUEFI -Name db
Get-SecureBootUEFI -Name dbx
Get-SecureBootUEFI -Name KEK
Get-SecureBootUEFI -Name PK

# 3. Run client
cd "C:\Program Files\SecureBootWatcher"
.\SecureBootWatcher.Client.exe

# 4. Check logs for certificate counts
# Look for: "Enumerated X certificates: db=Y, dbx=Z, KEK=A, PK=B"
```

### View Results

**Via API:**
```http
GET https://your-api.azurewebsites.net/api/SecureBootReports/{id}
```

Look for the `certificates` property in the response.

**Via Database:**
```sql
SELECT TOP 10 
    Id, 
    MachineName,
    CreatedAtUtc,
    LEN(CertificatesJson) as CertDataSize
FROM SecureBootReports
ORDER BY CreatedAtUtc DESC;

-- View certificate details
SELECT 
    Id,
    JSON_VALUE(CertificatesJson, '$.TotalCertificateCount') as TotalCerts,
    JSON_VALUE(CertificatesJson, '$.ExpiredCertificateCount') as ExpiredCerts,
    JSON_VALUE(CertificatesJson, '$.SecureBootEnabled') as SecureBootEnabled
FROM SecureBootReports
WHERE CertificatesJson IS NOT NULL;
```

## Troubleshooting

### No Certificate Data in Reports

**Check Secure Boot Status:**
```powershell
Confirm-SecureBootUEFI
```
If returns `False`, enable Secure Boot in UEFI/BIOS.

**Check Client Privileges:**
Client must run as SYSTEM or Administrator. Verify scheduled task:
```powershell
Get-ScheduledTask -TaskName "SecureBootWatcher" | Select-Object -ExpandProperty Principal
```

**Check Client Logs:**
Look for:
- "Secure Boot is not enabled on this device."
- "Failed to enumerate Secure Boot certificates"

### Empty Certificate Lists

**Legacy BIOS System:**
Certificate enumeration only works on UEFI systems. Check:
```powershell
$env:firmware_type
# Should be "UEFI", not "Legacy"
```

**PowerShell Cmdlet Missing:**
Verify SecureBoot module:
```powershell
Get-Module -ListAvailable SecureBoot
Get-Command Get-SecureBootUEFI
```

### Certificate Parsing Errors

**Non-X.509 Signatures:**
UEFI databases can contain SHA256 hashes instead of full certificates. These are logged but not parsed. This is normal.

**Corrupted Data:**
Rare, but re-run the client. If persistent, UEFI database may need repair.

## Performance Impact

- **Enumeration Time:** +1-3 seconds per report
- **Storage:** +50-500 KB per report (typical: 100 KB)
- **CPU:** Minimal (PowerShell process)
- **Network:** No additional bandwidth (included in existing report)

## What Devices Report Certificates?

? **Will Report:**
- Windows 10/11 desktops and laptops with UEFI
- Windows Server 2016+ with UEFI
- Azure VMs with Secure Boot enabled
- Physical servers with UEFI firmware

? **Will Not Report:**
- Legacy BIOS systems (non-UEFI)
- Systems with Secure Boot disabled
- Virtual machines without UEFI support
- Systems without PowerShell SecureBoot module

## Migration Path

### Existing Deployments

1. Apply database migration (adds column, does not modify existing data)
2. Redeploy API (backward compatible)
3. Redeploy client (automatically starts collecting certificates)
4. Existing reports remain unchanged
5. New reports include certificate data

### Rollback Plan

If issues occur:

1. Client continues to work (certificate enumeration errors are caught)
2. API continues to accept reports with or without certificate data
3. Database column can be left in place (nullable)
4. To remove feature: Deploy previous client version

No data loss occurs from enabling this feature.

## API Compatibility

The API remains **fully backward compatible**:

- Old clients (without certificate data) ? Works
- New clients (with certificate data) ? Works
- Mix of old and new clients ? Works
- CertificatesJson is nullable ? No breaking changes

## Support

For detailed information, see:
- **Certificate Enumeration Guide:** `docs/CERTIFICATE_ENUMERATION.md`
- **Implementation Summary:** `docs/CERTIFICATE_IMPLEMENTATION_SUMMARY.md`
- **Main README:** `README.md`

For issues:
- Check client logs for enumeration errors
- Verify Secure Boot enabled: `Confirm-SecureBootUEFI`
- Test PowerShell cmdlet: `Get-SecureBootUEFI -Name db`
- Review API logs for certificate data storage errors

## Summary

**? Key Benefits:**

- Track actual certificate inventory, not just deployment status
- Early warning for expiring certificates
- Identify expired or unauthorized certificates
- Microsoft certificate detection
- Automatic collection with no configuration changes

**?? Zero-Config Deployment:**

1. Update database: `dotnet ef database update`
2. Deploy client: Existing deployment process
3. Done! Certificates automatically collected

**?? Rich Data:**

- Full X.509 certificate properties
- Expiration tracking and alerts
- Database-level organization (db, dbx, KEK, PK)
- Microsoft certificate identification

Ready to deploy? Run the migration and the feature is live! ??
