# Integration Plan: Registry Servicing State with Readiness Evaluation

## Overview

The new `SecureBootServicingState` model provides critical registry-based information that complements the existing `SecureBootCertificateCollection` and enhances the readiness evaluation process.

## Data Collection Flow

### 1. Client Collection (SecureBootWatcher.Client)

**Location**: PowerShell certificate enumeration script

**Registry Paths to Collect**:
```powershell
$rootPath = "HKLM:\SYSTEM\CurrentControlSet\Control\SecureBoot\Servicing"
$deviceAttributesPath = "$rootPath\DeviceAttributes"
$uploadedPath = "$rootPath\UploadedForCurrentBootCycle"

# Collect all values from these paths
Get-ItemProperty -Path $rootPath -Name "WindowsUEFICA2023Capable", "UEFICA2023Status", "BucketHash", "ConfidenceLevel"
Get-ItemProperty -Path $deviceAttributesPath
```

**Integration Point**: Extend existing device report to include `SecureBootServicingState`

### 2. API Ingestion (SecureBootDashboard.Api)

**Location**: Device report processing

**Current Flow**:
```
Client → API → Database
         ↓
    SecureBootCertificateCollection (already stored)
    SecureBootServicingState (NEW - needs storage)
```

**Database Changes Required**:
- Extend `DeviceReport` entity to include `SecureBootServicingState`
- Create migration to add new columns or related table
- Update `DbContext` to map new properties

### 3. Readiness Evaluation (SecureBootReadinessService)

**Current Evaluation Criteria**:
- ✅ OS Version
- ✅ OEM Certificates
- ✅ Platform Keys
- ✅ Firmware Confidence (from FirmwareReleaseDate)

**New Evaluation Criteria** (Phase 2): 
Based on registry values that are appropriate for readiness evaluation:

**Blocking Criteria:**
- ❌ `UEFICA2023Status` = "Failed" (update failed)
- ❌ `UEFICA2023Status` = "Blocked" (update blocked, device not suitable)

**Informational Criteria:**
- ℹ️ `UEFICA2023Status` = "InProgress" (update in process)
- ℹ️ `UEFICA2023Status` = "NotStarted" (not yet attempted)
- ℹ️ `UEFICA2023Status` = "Completed" (successfully completed)

**Note**: 
- `WindowsUEFICA2023Capable` is NOT used for readiness evaluation
- It only tracks certificate presence in DB, not firmware capability
- It's intended for limited deployment scenarios only
- Use `UEFICA2023Status` as the primary registry-based readiness indicator

---

## Implementation Phases

### Phase 1: Data Collection & Storage (Current)
**Status**: In Progress

**Tasks**:
- [x] Create `SecureBootServicingState` model
- [ ] Extend client PowerShell script to collect registry values
- [ ] Update device report schema
- [ ] Create database migration
- [ ] Update API to store servicing state

### Phase 2: Readiness Evaluation Integration
**Status**: Planned

**Tasks**:
- [ ] Extend `SecureBootReadinessService.EvaluateReadiness()` to accept `SecureBootServicingState`
- [ ] Add validation methods:
  - `ValidateWindowsUEFICA2023Capable()`
  - `ValidateCanAttemptUpdateAfter()`
  - `ValidateOSArchitecture()`
- [ ] Update `ReadinessEvaluation` properties to track registry-based issues
- [ ] Update tests for new validation logic

### Phase 3: Dashboard Display
**Status**: Planned

**Tasks**:
- [ ] Update device details page to show servicing state
- [ ] Display registry values in certificate view
- [ ] Add status badges for capability checks
- [ ] Show CanAttemptUpdateAfter countdown if in future

### Phase 4: Monitoring & Alerts
**Status**: Future

**Tasks**:
- [ ] Alert when device is not Windows UEFI CA 2023 capable
- [ ] Notify when update window is closed (CanAttemptUpdateAfter in future)
- [ ] Track StateAttributes changes over time
- [ ] Generate reports on capability distribution

---

## API Endpoint Updates

### Current Endpoint
```
GET /api/devices/{deviceId}
```

**Current Response**:
```json
{
  "id": "...",
  "osVersion": "...",
  "certificates": { ... }
}
```

### Enhanced Response (Phase 2)
```json
{
  "id": "...",
  "osVersion": "...",
  "certificates": { ... },
  "servicingState": {
    "windowsUEFICA2023Capable": 1,
    "uefiCA2023Status": "NotStarted",
    "canAttemptUpdateAfter": "2025-01-13T15:30:00Z",
    "oemManufacturerName": "LENOVO",
    "firmwareReleaseDate": "08/22/2025",
    "osArchitecture": "AMD64",
    "collectedAtUtc": "2025-01-13T12:57:00Z"
  }
}
```

---

## Readiness Evaluation Logic Updates

### Current Logic
```csharp
public ReadinessEvaluation EvaluateReadiness(
    SecureBootCertificateCollection? certificates,
    string? osVersion,
    string? osBuildNumber,
    DateTime? firmwareReleaseDate = null)
```

### Enhanced Logic (Phase 2)
```csharp
public ReadinessEvaluation EvaluateReadiness(
    SecureBootCertificateCollection? certificates,
    SecureBootServicingState? servicingState,  // NEW
    string? osVersion,
    string? osBuildNumber,
    DateTime? firmwareReleaseDate = null)
{
    // ... existing evaluations ...
    
    // NEW: Evaluate registry-based criteria
    if (servicingState != null)
    {
        EvaluateRegistryCriteria(servicingState, evaluation);
    }
    
    // Updated: Include registry issues in final readiness
    evaluation.IsReadyToUpdate = 
        evaluation.IsOSReady &&
        evaluation.AreOemCertificatesValid &&
        evaluation.AreFirmwareCertificatesValid &&
        evaluation.AreRegistryCriteriaValid &&  // NEW
        firmwareAcceptable;
}

private void EvaluateRegistryCriteria(
    SecureBootServicingState servicingState,
    ReadinessEvaluation evaluation)
{
    var issues = new List<string>();

    // Check UEFI CA 2023 update status (NOT WindowsUEFICA2023Capable which is for limited scenarios only)
    if (!string.IsNullOrEmpty(servicingState.UEFICA2023Status))
    {
        switch (servicingState.UEFICA2023Status.ToLower())
        {
            case "failed":
                issues.Add("❌ Windows UEFI CA 2023 update failed. Manual intervention required.");
                evaluation.IsReadyToUpdate = false;
                break;
            case "blocked":
                issues.Add("❌ Windows UEFI CA 2023 update blocked. Device may not be suitable.");
                evaluation.IsReadyToUpdate = false;
                break;
            case "inprogress":
                issues.Add("⏳ Windows UEFI CA 2023 update currently in progress.");
                evaluation.IsReadyToUpdate = false;
                break;
            case "notstarted":
                // This is expected state - update hasn't been attempted yet
                break;
            case "completed":
                // This is expected state - update completed successfully
                break;
        }
    }

    // Check architecture compatibility
    if (!string.IsNullOrEmpty(servicingState.OSArchitecture) &&
        servicingState.OSArchitecture != "AMD64")
    {
        issues.Add($"❌ Unsupported OS architecture: {servicingState.OSArchitecture}");
        evaluation.IsReadyToUpdate = false;
    }

    // Check update window (if applicable)
    if (servicingState.CanAttemptUpdateAfter.HasValue &&
        servicingState.CanAttemptUpdateAfter.Value > DateTimeOffset.UtcNow)
    {
        var daysRemaining = 
            (servicingState.CanAttemptUpdateAfter.Value - DateTimeOffset.UtcNow).Days;
        issues.Add($"⏱️ Update window not yet open. Can attempt in {daysRemaining} days");
    }

    evaluation.AreRegistryCriteriaValid = issues.Count == 0;
    if (issues.Any())
    {
        evaluation.RegistryCriteriaDetails = string.Join("; ", issues);
    }
}
```

---

## ReadinessEvaluation Class Updates

### New Properties
```csharp
/// <summary>
/// Indicates if registry-based Secure Boot servicing criteria are met.
/// </summary>
public bool AreRegistryCriteriaValid { get; set; } = true;

/// <summary>
/// Number of registry criteria issues preventing readiness.
/// </summary>
public int RegistryCriteriaIssueCount { get; set; }

/// <summary>
/// Details about registry-based readiness issues.
/// </summary>
public string RegistryCriteriaDetails { get; set; } = string.Empty;

/// <summary>
/// Device firmware capability for Windows UEFI CA 2023.
/// 0 = Not capable, 1 = Capable, null = Not collected
/// </summary>
public int? WindowsUEFICA2023Capable { get; set; }

/// <summary>
/// When the update window opens (if in future).
/// </summary>
public DateTimeOffset? CanAttemptUpdateAfter { get; set; }
```

---

## Testing Strategy

### Unit Tests
```csharp
[Fact]
public void EvaluateReadiness_WithFailedUEFICA2023Update_ShouldNotBeReady()
{
    // Arrange
    var servicingState = new SecureBootServicingState
    {
        UEFICA2023Status = "Failed"  // Update failed - blocking condition
    };
    
    // Act
    var result = _service.EvaluateReadiness(
        certificates, 
        servicingState,
        osVersion, 
        null
    );
    
    // Assert
    Assert.False(result.IsReadyToUpdate);
    Assert.Contains("failed", result.RegistryCriteriaDetails, StringComparison.OrdinalIgnoreCase);
}

[Fact]
public void EvaluateReadiness_WithBlockedUEFICA2023Update_ShouldNotBeReady()
{
    // Arrange
    var servicingState = new SecureBootServicingState
    {
        UEFICA2023Status = "Blocked"  // Update blocked - blocking condition
    };
    
    // Act
    var result = _service.EvaluateReadiness(
        certificates, 
        servicingState,
        osVersion, 
        null
    );
    
    // Assert
    Assert.False(result.IsReadyToUpdate);
    Assert.Contains("blocked", result.RegistryCriteriaDetails, StringComparison.OrdinalIgnoreCase);
}

[Fact]
public void EvaluateReadiness_WithNotStartedUEFICA2023_ShouldBeReady()
{
    // Arrange
    var servicingState = new SecureBootServicingState
    {
        UEFICA2023Status = "NotStarted"  // Not yet attempted - this is normal, should not block
    };
    
    // Act
    var result = _service.EvaluateReadiness(
        certificates, 
        servicingState,
        osVersion, 
        null
    );
    
    // Assert - should be ready (assuming other criteria pass)
    // Note: actual readiness depends on other factors (OS, certs, etc.)
}
```

---

## Migration Path

### Backward Compatibility
- `SecureBootServicingState` is **optional** in `EvaluateReadiness()`
- If not provided, evaluation continues with existing logic
- Devices without registry data can still be evaluated based on certificates & OS

### Gradual Rollout
1. **Week 1**: Deploy new model and client collection
2. **Week 2**: Deploy API storage layer
3. **Week 3**: Deploy readiness evaluation updates
4. **Week 4**: Deploy dashboard display updates
5. **Week 5+**: Monitor, gather data, optimize

---

## Success Metrics

| Metric | Target | Method |
|--------|--------|--------|
| Registry collection rate | 95%+ | Monitor client logs |
| False "Ready" reduction | -50% | Compare readiness data |
| Firmware capability accuracy | 99%+ | Validate against OEM specs |
| Update failure reduction | -30% | Track update success rate |

---

## Known Limitations & Future Enhancements

### Current Limitations
- `CanAttemptUpdateAfter` uses simple timestamp comparison
- `StateAttributes` parsing not yet implemented
- No version comparison for `FirmwareVersion`

### Future Enhancements
1. **StateAttributes Parsing**: Decode state machine progression
2. **Firmware Version Validation**: Compare against known good versions per OEM
3. **Telemetry Integration**: Use `BucketHash` for device grouping
4. **Registry Change Tracking**: Monitor state changes between reports
5. **ML-based Prediction**: Predict update success based on registry history

---

## Related Documentation

- [Registry Keys Overview](REGISTRY-SECURE-BOOT-SERVICING.md)
- [Readiness Service](../SecureBootDashboard.Api/Services/SecureBootReadinessService.cs)
- [Platform Key Fix](FIXES/FIX-2024-FIRMWARE-CERTIFICATE-READINESS.md)
