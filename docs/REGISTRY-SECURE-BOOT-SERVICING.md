# Windows Registry Keys for Secure Boot Servicing

## Overview

The Windows registry contains critical information about Secure Boot certificate servicing state and device capabilities under:
```
HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\SecureBoot\Servicing
```

These keys are essential for understanding device readiness and tracking the progress of Secure Boot updates.

---

## Registry Path Structure

### Main Path
```
HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\SecureBoot\Servicing
```

**Sub-keys:**
1. `DeviceAttributes` - Device and firmware information
2. `UploadedForCurrentBootCycle` - Uploaded certificate tracking

---

## Key Value Descriptions

### Servicing Root Level

#### `WindowsUEFICA2023Capable`
- **Type**: `REG_DWORD`
- **Value Range**: 0, 1, or 2
- **Description**: Indicates the state of the Windows UEFI CA 2023 certificate in the Signature Database (db)
- **IMPORTANT**: Despite the name "Capable", this key does NOT indicate device firmware capability
- **Valid Values**:
  - `0` (or key does not exist): "Windows UEFI CA 2023" certificate is NOT in the DB
  - `1`: "Windows UEFI CA 2023" certificate is in the DB
  - `2`: "Windows UEFI CA 2023" certificate is in the DB AND system is starting from the 2023 signed boot manager
- **Deployment Note**: This key is intended for limited deployment scenarios only
- **Recommendation**: Use `UEFICA2023Status` instead for general readiness evaluation
- **Example**: `00000000` (certificate not present in DB)
- **Impact on Readiness**: Informational only - this key is not recommended for readiness decisions

#### `UEFICA2023Status`
- **Type**: `REG_SZ` (string)
- **Possible Values**: 
  - `"NotStarted"` - Update process hasn't been initiated
  - `"InProgress"` - Update is currently being processed
  - `"Completed"` - Update finished successfully
  - `"Failed"` - Update encountered an error
  - `"Blocked"` - Update is blocked (usually due to device incompatibility)
- **Description**: Current state of the Windows UEFI CA 2023 update process
- **Example**: `"NotStarted"`
- **Impact on Readiness**: Critical - Failed or Blocked status blocks readiness

#### `BucketHash`
- **Type**: `REG_SZ` (string)
- **Description**: Hash value used for telemetry and diagnostic purposes; tracks device configuration signature
- **Example**: `"4f6fc8a162c257cc40e65fb46a33b50b7bfd4e23b47899bf119ab33199b2494d"`
- **Length**: Typically 64 hex characters (SHA-256)
- **Impact on Readiness**: Informational only; helps Microsoft track device profiles

#### `ConfidenceLevel`
- **Type**: `REG_SZ` (string)
- **Possible Values**: 
  - Empty string `""` - Not assessed
  - `"High"` - High confidence in firmware support
  - `"Medium"` - Medium confidence
  - `"Low"` - Low confidence, may need firmware update
- **Description**: Microsoft's assessment of firmware confidence for supporting Secure Boot updates
- **Impact on Readiness**: Complements API-side firmware confidence assessment

---

### Root Level - Update Policy & Opt-in Options

#### `HighConfidenceOptOut`
- **Type**: `REG_DWORD`
- **Valid Values**:
  - `0` (or key does not exist) - Opt IN to high confidence buckets (default)
  - `1` - Opt OUT of high confidence buckets
- **Description**: Controls whether device is eligible for high confidence updates that will be automatically applied as part of Logical Component Update (LCU)
- **Purpose**: For enterprises that want to opt out of automatic high confidence updates
- **Example**: `00000001` (opted out)
- **Impact on Readiness**: Informational - tracks enterprise policy compliance, does not block readiness
- **MDM/SCCM**: Can be configured via Group Policy or Mobile Device Management

#### `MicrosoftUpdateManagedOptIn`
- **Type**: `REG_DWORD`
- **Valid Values**:
  - `0` (or key does not exist) - Opt OUT of CFR servicing (default)
  - `1` or any non-zero value - Opt IN to Controlled Feature Rollout (CFR) servicing
- **Description**: Controls whether device is enrolled in Microsoft Managed updates (Controlled Feature Rollout)
- **Requirements**: 
  - Device must opt in via this registry key AND
  - Device must allow sending of required diagnostic data (telemetry level 1 or higher)
- **Purpose**: For enterprises that want to participate in Microsoft's Controlled Feature Rollout program
- **Example**: `00000001` (opted in)
- **Impact on Readiness**: Important - affects update eligibility; combination with telemetry settings determines actual CFR enrollment
- **MDM/SCCM**: Can be configured via Group Policy or Mobile Device Management
- **Related**: Requires `SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection\AllowTelemetry` >= 1

---

## Enterprise Policy Considerations

### HighConfidenceOptOut & MicrosoftUpdateManagedOptIn Interaction

These two registry keys work together to control update servicing for enterprises:

#### Scenario 1: Default Settings (Both Not Set)
- `HighConfidenceOptOut` = 0 (not set) → OPT IN to high confidence buckets
- `MicrosoftUpdateManagedOptIn` = 0 (not set) → OPT OUT of Microsoft Managed updates
- **Result**: Device gets high confidence updates automatically, but NOT enrolled in CFR

#### Scenario 2: Conservative Deployment
- `HighConfidenceOptOut` = 1 → OPT OUT of high confidence buckets
- `MicrosoftUpdateManagedOptIn` = 0 → OPT OUT of Microsoft Managed updates
- **Result**: Device receives only tested, stable updates; no rapid deployments

#### Scenario 3: Microsoft Managed Deployment
- `HighConfidenceOptOut` = 0 → OPT IN to high confidence buckets
- `MicrosoftUpdateManagedOptIn` = 1 → OPT IN to Microsoft Managed updates
- **Requirement**: Must also set `AllowTelemetry` >= 1 (Basic diagnostic data)
- **Result**: Device eligible for CFR servicing with faster update cadence

### Impact on Readiness Evaluation

**For Current Phase (Platform Key Validation)**:
- ✅ These settings are INFORMATIONAL ONLY
- ✅ Do NOT block readiness

**For Future Phase (Registry Integration)**:
- ⚠️ Can be checked for compliance with enterprise policy
- ⚠️ Can be reported in device readiness details
- ℹ️ Combined with `MicrosoftUpdateManagedOptIn` to assess CFR enrollment status

### Typical Enterprise Configurations

| Use Case | HighConfidenceOptOut | MicrosoftUpdateManagedOptIn | Telemetry | Update Cadence |
|----------|----------------------|-----------------------------|-----------|----------------|
| **Stable/Production** | 1 | 0 | Any | Slow/Monthly |
| **Standard** | 0 | 0 | Any | Normal/Monthly |
| **Microsoft Managed** | 0 | 1 | ≥ 1 | Fast/Weekly |
| **Fast Track** | 0 | 1 | 2-3 | Very Fast/Daily |

---

## Data Collection Strategy

### Client-Side (SecureBootWatcher.Client)

The PowerShell-based certificate enumeration should also collect these registry values:

```powershell
# Registry collection scope
$registryPath = "HKLM:\SYSTEM\CurrentControlSet\Control\SecureBoot\Servicing"
$deviceAttributesPath = "$registryPath\DeviceAttributes"

# Collect root-level values
$rootValues = Get-ItemProperty -Path $registryPath -ErrorAction SilentlyContinue

# Collect DeviceAttributes
$deviceAttributes = Get-ItemProperty -Path $deviceAttributesPath -ErrorAction SilentlyContinue

# Include in report sent to API
```

### Data Model Enhancement

Consider extending `SecureBootCertificateCollection` or creating a new model:

```csharp
public class SecureBootServicingState
{
    public bool? WindowsUEFICA2023Capable { get; set; }
    public string? UEFICA2023Status { get; set; }
    public string? BucketHash { get; set; }
    public string? ConfidenceLevel { get; set; }
    
    // DeviceAttributes
    public DateTime? CanAttemptUpdateAfter { get; set; }
    public string? OEMManufacturerName { get; set; }
    public string? OEMModelSystemVersion { get; set; }
    public string? FirmwareManufacturer { get; set; }
    public string? FirmwareVersion { get; set; }
    public DateTime? FirmwareReleaseDate { get; set; }
    public string? OSArchitecture { get; set; }
    public string? StateAttributes { get; set; }
}
```

---

## Monitoring & Troubleshooting

### Key Registry Monitoring Points

| Key | Expected Value | Indicates |
|-----|---|---|
| `WindowsUEFICA2023Capable` | `1` | Device firmware supports update |
| `UEFICA2023Status` | `"NotStarted"` or `"Completed"` | Healthy state |
| `CanAttemptUpdateAfter` | Past timestamp | Update window is open |
| `FirmwareReleaseDate` | >= 2024-01-01 | Confident firmware |
| `OSArchitecture` | `"AMD64"` | 64-bit system |
| `StateAttributes` | Contains `Completed` | Update progressed |

### Common Issues

**Issue**: `WindowsUEFICA2023Capable` = 0
- **Cause**: Firmware doesn't support UEFI CA 2023
- **Resolution**: Firmware update required before Secure Boot update

**Issue**: `UEFICA2023Status` = "Failed"
- **Cause**: Update encountered an error during execution
- **Resolution**: Review Windows Update logs, clear `StateAttributes`, retry

**Issue**: `CanAttemptUpdateAfter` in future
- **Cause**: Update window hasn't opened yet (rate limiting)
- **Resolution**: Wait until timestamp, then retry

**Issue**: `FirmwareReleaseDate` before 2024
- **Cause**: Old firmware with uncertain compatibility
- **Resolution**: Recommend firmware update (already done in `EvaluateFirmwareConfidence`)

---

## References

- [Microsoft Secure Boot Documentation](https://docs.microsoft.com/en-us/windows/security/operating-system-security/system-security/secure-boot/)
- [Windows UEFI CA 2023 Update](https://support.microsoft.com/kb/XXXXX) (KB article TBD)
- [FILETIME Format](https://docs.microsoft.com/en-us/windows/win32/api/minwinbase/ns-minwinbase-filetime)

---

## Notes

- These registry values are Windows system-managed and typically should not be modified manually
- Values are set by Windows during boot and servicing operations
- Backup these values before any manual maintenance
- Registry paths may vary on non-English Windows installations
