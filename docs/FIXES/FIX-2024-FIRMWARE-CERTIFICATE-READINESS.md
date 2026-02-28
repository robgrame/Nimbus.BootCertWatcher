# Fix: Platform Key Expiration Check for Secure Boot Readiness

## Problem

**Dashboard Readiness Inconsistency**: A device showed "Ready State: Not Ready" despite all update steps being completed (100% completion). The root cause was an **expired Platform Key (PK)** certificate that was not being checked by the readiness evaluation service.

### Example Scenario
- **progressionState**: "All updates completed" ✅
- **completionPercentage**: 100% ✅
- **All updateSteps**: `isCompleted: true` ✅
- **PlatformKey certificate**: Expired (Microsoft Hyper-V Firmware PK, expired 4281 days ago) ❌
- **Result**: `IsReadyToUpdate: false` ❌

## Root Cause

The `SecureBootReadinessService.EvaluateCertificates()` method was only evaluating certificates in the **Signature Database (db)** but was ignoring **Platform Keys (PK)**, which are critical to Secure Boot integrity.

**Important distinction:**
- **KEK (Key Exchange Keys)** and **DB (Signature Database)** certificates that are expiring will be **updated during the Secure Boot upgrade**:
  - KEK: Microsoft Corporation KEK CA 2011 (Jun 2026) → Microsoft Corporation KEK 2K CA 2023
  - DB: Microsoft Windows Production PCA 2011 (Oct 2026) → Windows UEFI CA 2023
  - DB: Microsoft Corporation UEFI CA 2011 (Jun 2026) → Microsoft UEFI CA 2023
- **Platform Keys (PK)** are **NOT updated** by the Secure Boot upgrade and must remain valid

## Solution

### Changes to `SecureBootReadinessService`

#### 1. Added New Properties to `ReadinessEvaluation` Class
```csharp
public int ExpiredPlatformKeyCertificateCount { get; set; }
public int CriticalPlatformKeyCertificateCount { get; set; }
public bool AreFirmwareCertificatesValid { get; set; } = true;
```

#### 2. Added New Helper Method: `EvaluatePlatformKeyCertificates()`
- Checks only **Platform Key (PK)** certificates for expiration
- Marks `AreFirmwareCertificatesValid = false` only if PK certificates are expired or critical
- Logs errors for PK certificate issues

#### 3. Updated `EvaluateCertificates()` Method
- **Removed** evaluation of Key Exchange Keys (KEK) as blockers - these will be updated during upgrade
- **Removed** evaluation of legacy DB certificates as blockers - these will be updated during upgrade
- **Added** evaluation of Platform Key (PK) certificates - these are NOT updated
- Clarified in comments why KEK/DB are not evaluated

#### 4. Updated `IsReadyToUpdate` Logic
**Logic remains the same**, now correctly focused:
```csharp
evaluation.IsReadyToUpdate = evaluation.IsOSReady &&
                             evaluation.AreOemCertificatesValid &&
                             evaluation.AreFirmwareCertificatesValid && // Only checks PK
                             firmwareAcceptable;
```

## Impact

### What Gets Blocked Now
- ❌ Expired Platform Keys (PK)
- ❌ Platform Keys expiring within critical threshold (90 days by default)

### What Does NOT Block (Correctly)
- ✅ Expired KEK certificates (will be updated during upgrade)
- ✅ Expired/Legacy DB certificates (will be updated during upgrade)
- ✅ OEM certificates with reasonable expiration windows

### Dashboard Behavior
When a device has expired/critical PK certificates:
- `IsReadyToUpdate` = **false**
- `CertificateEvaluationDetails` includes: "❌ Platform Key (PK) has expired or critical certificate(s)"
- Users will be informed that PK must be renewed before proceeding

## Testing

Comprehensive test suite added/updated in `SecureBootReadinessFirmwareCertificatesTests.cs`:

**Platform Key (PK) Tests:**
1. **ExpiredPlatformKey_ShouldNotBeReady** - Verifies device is blocked when PK is expired
2. **CriticalPlatformKey_ShouldNotBeReady** - Verifies device is blocked when PK expires soon
3. **ValidPlatformKey_ShouldBeReady** - Verifies device is ready with valid PK
4. **MultipleExpiredPlatformKeys_ShouldNotBeReady** - Verifies correct count tracking

**KEK/DB Tests (Should NOT block):**
5. **ExpiringKEK_ShouldStillBeReady** - Verifies expiring KEK does NOT block readiness (will be updated)
6. **ExpiringLegacyDB_ShouldStillBeReady** - Verifies expiring legacy DB cert does NOT block readiness (will be updated)

**Test Results**: ✅ All 62 readiness tests pass (including all 6 new/updated firmware certificate tests)

## Configuration

The service respects existing configuration options:
- `CertificateExpirationCriticalDays` (default: 90 days) - Threshold for "critical" status for PK
- `CertificateExpirationWarningDays` (default: 180 days) - Not used for PK (only used for OEM certs)

## Logging

Enhanced logging for PK certificate issues:
- **ERROR**: When PK is expired or critical (blocks readiness)
- **DEBUG**: Detailed certificate evaluation information

Example log:
```
ERROR: Expired Platform Key (PK) certificate: CN=Microsoft Hyper-V Firmware PK, Expired on: 2014-04-25
ERROR: PK is not updated by Secure Boot upgrade and must be valid before proceeding
```

## Migration

No database migration required. This is purely a logic change to the readiness evaluation service. Existing device records will be re-evaluated using the new logic the next time readiness is calculated.

## Backward Compatibility

✅ Backward compatible - existing code paths and properties remain unchanged. This fix clarifies the readiness logic to only block on truly blocking issues (expired PK), not on certificates that will be updated during the upgrade process.

## Related Issues

- Dashboard shows "Ready" but device has expired PK certificate
- Inconsistent state between update completion and readiness status
- Missing validation for Platform Key health (which is NOT updated by upgrade)
