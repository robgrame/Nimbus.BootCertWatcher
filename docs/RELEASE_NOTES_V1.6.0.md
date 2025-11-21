# Release Notes - Version 1.6.0

**Release Date:** 2025-01-13  
**Type:** Feature Release  
**Status:** ? Ready for Production

---

## ?? Overview

Version 1.6.0 introduces comprehensive **Telemetry and CFR (Controlled Feature Rollout) Tracking** capabilities, enabling IT administrators to monitor Windows diagnostic data settings and determine device eligibility for Microsoft-managed Secure Boot updates.

---

## ?? New Features

### 1. Telemetry Policy Tracking

Track Windows diagnostic data collection levels that impact CFR eligibility:

**Registry Key Monitored:**
- `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection\AllowTelemetry`

**Telemetry Levels:**
| Value | Level | CFR Eligible | Description |
|-------|-------|--------------|-------------|
| 0 | Security | ? No | Enterprise/Education/Server only |
| 1 | Basic | ? Yes | Required diagnostic data |
| 2 | Enhanced | ? Yes | Optional diagnostic data |
| 3 | Full | ? Yes | Full diagnostic data |

**Impact:**
- Devices with telemetry level < 1 (Basic) are **NOT eligible** for CFR
- Dashboard displays aggregate telemetry compliance metrics
- Alerts generated for non-compliant devices

---

### 2. Controlled Feature Rollout (CFR) Eligibility Detection

Automatically detect and report device eligibility for Microsoft-managed Secure Boot update rollout.

**Eligibility Criteria:**
1. ? `MicrosoftUpdateManagedOptIn = 1` (Opt-in to CFR)
2. ? `AllowTelemetry >= 1` (Basic telemetry or higher)
3. ? Windows 10/11 with KB support for Secure Boot updates

**New Registry Keys Tracked:**
- `HKLM\SYSTEM\CurrentControlSet\Control\SecureBoot\MicrosoftUpdateManagedOptIn`
- `HKLM\SYSTEM\CurrentControlSet\Control\SecureBoot\UpdateType`
- `HKLM\SYSTEM\CurrentControlSet\Control\SecureBoot\Servicing\WindowsUEFICA2023Capable`

**Dashboard Features:**
- **CFR Opt-in Statistics**: Count of devices opted into Microsoft-managed rollout
- **Telemetry Compliance**: Percentage of devices meeting CFR telemetry requirements
- **CA 2023 Capable Devices**: Track Windows UEFI CA 2023 capability

---

### 3. Enhanced Data Models

**New Properties:**

`TelemetryPolicySnapshot`:
```csharp
public uint? AllowTelemetry { get; set; }
public string TelemetryLevelDescription { get; }
public bool MeetsCfrTelemetryRequirement { get; }
```

`SecureBootRegistrySnapshot` (additions):
```csharp
public uint? UpdateType { get; set; }  // 0=None, 1=DB, 2=Boot Manager
```

`SecureBootStatusReport` (additions):
```csharp
public TelemetryPolicySnapshot? TelemetryPolicy { get; set; }
```

---

### 4. Dashboard Enhancements

**New Statistics Cards** (Planned for future update):
- Telemetry Enabled Count
- CFR Opt-in Count
- CA 2023 Capable Devices

**Device List Columns** (Planned for future update):
- Telemetry Level
- CFR Opt-in Status
- CA 2023 Capable

**Device Details Page** (Planned for future update):
- Complete CFR eligibility breakdown
- Telemetry policy details
- Recommendations for compliance

---

## ?? Technical Changes

### Client (SecureBootWatcher.Client)

**Modified Files:**
- `Services/IRegistrySnapshotProvider.cs`
  - Added `CaptureTelemetryPolicyAsync()` method
- `Services/RegistrySnapshotProvider.cs`
  - Implemented telemetry policy capture from registry
  - Added reading of `UpdateType` key
- `Services/ReportBuilder.cs`
  - Integrated telemetry policy snapshot into report building
  - Added CFR eligibility alerts

**New Registry Paths:**
```
HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection
  ?? AllowTelemetry (DWORD)

HKLM\SYSTEM\CurrentControlSet\Control\SecureBoot
  ?? UpdateType (DWORD)
  ?? MicrosoftUpdateManagedOptIn (DWORD)
```

### Shared Models (SecureBootWatcher.Shared)

**New Classes:**
- `TelemetryPolicySnapshot` - Captures diagnostic data settings

**Modified Classes:**
- `SecureBootRegistrySnapshot` - Added `UpdateType` property
- `SecureBootStatusReport` - Added `TelemetryPolicy` property

### API (SecureBootDashboard.Api)

**Modified Files:**
- `Controllers/DevicesController.cs`
  - Extended `DeviceSummaryResponse` with telemetry/CFR fields
  - Deserializes registry JSON to extract telemetry data

**Database Impact:**
- ? **NO MIGRATION REQUIRED** - Data stored in existing `RegistryStateJson` field

---

## ?? Alerts & Monitoring

### New Alert Types

**CFR Telemetry Compliance:**
```
? Telemetry level (Security) does not meet CFR requirements. 
Basic (1) or higher required for Microsoft managed rollout.
```

**CFR Eligible:**
```
? Telemetry level (Basic) meets CFR requirements.
```

### Logging Enhancements

- Debug-level logs for telemetry policy reads
- Warning logs for non-compliant devices
- Info logs for CFR eligibility status changes

---

## ?? Deployment

### Upgrade Steps

1. **Stop Client Service** (if running as service)
   ```powershell
   Stop-Service -Name "SecureBootWatcher"
   ```

2. **Deploy Updated Client** (v1.6.0)
   ```powershell
   # Copy new binaries
   Copy-Item -Path ".\SecureBootWatcher-Client-v1.6.0\*" -Destination "C:\Program Files\SecureBootWatcher\" -Recurse -Force
   ```

3. **Restart Client Service**
   ```powershell
   Start-Service -Name "SecureBootWatcher"
   ```

4. **Verify Version**
   ```powershell
   Get-Content "C:\Program Files\SecureBootWatcher\SecureBootWatcher.Client.dll" | Select-String "AssemblyFileVersion"
   ```

### No API/Web Changes Required

- ? API v1.5.x is **fully compatible** with Client v1.6.0
- ? Web Dashboard v1.5.x can **read and display** new telemetry data
- Future update will add UI components for visualization

---

## ?? Documentation

**New Documents:**
- `docs/REGISTRY_TELEMETRY_TRACKING.md` - Complete registry key reference
- `docs/RELEASE_NOTES_V1.6.0.md` - This document

**Updated Documents:**
- `README.md` - Added v1.6 feature summary
- `version.json` - Version bump to 1.6

---

## ?? Testing

### Validation Steps

1. **Verify Telemetry Capture**
   ```powershell
   # Check telemetry level
   Get-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection" -Name AllowTelemetry
   
   # Run client
   .\SecureBootWatcher.Client.exe
   
   # Verify in logs
   Get-Content ".\logs\securebootwatcher-*.log" | Select-String "Telemetry"
   ```

2. **Verify CFR Data in Report**
   ```powershell
   # Check latest report JSON
   $report = Get-Content "C:\ProgramData\SecureBootDashboard\reports\*.json" | Select-Object -Last 1 | ConvertFrom-Json
   $report.Registry.MicrosoftUpdateManagedOptIn
   $report.TelemetryPolicy.AllowTelemetry
   ```

3. **Verify API Response**
   ```powershell
   # Query devices endpoint
   $devices = Invoke-RestMethod -Uri "https://your-api/api/Devices" -Method Get
   $devices | Select-Object MachineName, AllowTelemetry, MicrosoftUpdateManagedOptIn
   ```

---

## ?? Breaking Changes

**None** - Fully backward compatible with v1.5.x

---

## ?? Bug Fixes

None - Pure feature addition release

---

## ?? Future Enhancements (v1.7+)

### Dashboard Visualization (Planned)

1. **Statistics Cards:**
   - Telemetry Compliance Percentage
   - CFR Opt-in Devices Count
   - CA 2023 Capable Devices

2. **Device List Columns:**
   - Telemetry Level badge
   - CFR Status icon
   - Quick filters for compliance

3. **Device Details:**
   - CFR Eligibility Scorecard
   - Telemetry Configuration Guide
   - Remediation Steps for Non-Compliant Devices

### Advanced Analytics

- Trend analysis for CFR adoption
- Fleet-wide telemetry compliance reports
- Predictive analytics for rollout readiness

---

## ?? Support

For questions or issues:
- **Documentation**: [docs/REGISTRY_TELEMETRY_TRACKING.md](REGISTRY_TELEMETRY_TRACKING.md)
- **GitHub Issues**: [Report bugs](https://github.com/robgrame/Nimbus.BootCertWatcher/issues)
- **GitHub Discussions**: [Ask questions](https://github.com/robgrame/Nimbus.BootCertWatcher/discussions)

---

## ?? Contributors

- **Development Team** - Telemetry tracking implementation
- **Microsoft Documentation Team** - CFR specifications and guidance

---

**Version:** 1.6.0  
**Build Date:** 2025-01-13  
**Git Tag:** `v1.6.0`  

