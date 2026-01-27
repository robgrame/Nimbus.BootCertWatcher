# Secure Boot Event IDs - Complete Reference

Reference: https://support.microsoft.com/en-us/topic/secure-boot-db-and-dbx-variable-update-events-37e47cf8-608b-4a87-8175-bdead630eb69

## Event Log Channels

- **Microsoft-Windows-SecureBoot-Servicing/Operational**: Servicing events (1032-1045)
- **Microsoft-Windows-SecureBoot-State/Operational**: State/boot events (1795-1808)
- **System**: General system events

---

## Servicing Events (Microsoft-Windows-SecureBoot-Servicing/Operational)

### Event ID 1032: Update Installation Started
**Level**: Informational  
**Description**: Indicates that the installation of Secure Boot database updates has started.

**Fields**:
- Standard event fields only

---

### Event ID 1033: Update Installation Succeeded
**Level**: Informational  
**Description**: Indicates that the installation of Secure Boot database updates succeeded.

**Fields**:
- `HResult`: Operation result (typically S_OK / 0 for success)

---

### Event ID 1034: Update Installation Failed
**Level**: Error  
**Description**: Indicates that the installation of Secure Boot database updates failed.

**Fields**:
- `ErrorCode`: Error code indicating the failure reason
- `HResult`: Operation result code

---

### Event ID 1036: Update Applied to Firmware (Reboot Required)
**Level**: Informational  
**Description**: The updates have been applied to the device's firmware but require a reboot to take effect.

**Fields**:
- `UpdateType`: Update type identifier (0 or 0x5944)
- `FirmwareManufacturer`: Firmware manufacturer name
- `FirmwareVersion`: Firmware version string
- `OEMModelNumber`: OEM model number
- `OEMManufacturerName`: OEM manufacturer name
- `OSArchitecture`: OS architecture (e.g., x64, ARM64)
- `RebootRequired`: true

---

### Event ID 1037: Update Applied After Reboot
**Level**: Informational  
**Description**: The updates have been successfully applied after a system reboot.

**Fields**:
- `UpdateType`: Update type identifier (0 or 0x5944)
- `FirmwareManufacturer`: Firmware manufacturer name
- `FirmwareVersion`: Firmware version string
- `OEMModelNumber`: OEM model number
- `OEMManufacturerName`: OEM manufacturer name
- `OSArchitecture`: OS architecture
- `RebootRequired`: false

---

### Event ID 1043: Update Not Applicable
**Level**: Warning  
**Description**: The Secure Boot update is not applicable to this device.

**Fields**:
- `UpdateType`: Update type identifier
- `ErrorCode`: Reason code for non-applicability

---

### Event ID 1044: More Updates Available After Reboot
**Level**: Informational  
**Description**: Additional Secure Boot updates are available after the system reboots.

**Fields**:
- `UpdatesAvailable`: Number of additional updates available
- `UpdateType`: Update type identifier
- `RebootRequired`: true

---

### Event ID 1045: All Updates Completed
**Level**: Informational  
**Description**: All available Secure Boot updates have been completed successfully.

**Fields**:
- `UpdatesAvailable`: Should be 0
- `UpdateType`: Update type identifier

---

## State Events (Microsoft-Windows-SecureBoot-State/Operational)

### Event ID 1795-1801: Boot State Events
**Level**: Informational/Warning  
**Description**: Various boot state and validation events.

**Fields**:
- Varies by specific event ID
- Generally contains boot validation results

---

### Event ID 1808: Device Has Updated Secure Boot CA/Keys ⭐
**Level**: Informational  
**Description**: **CRITICAL EVENT** - Indicates that the device has the required new Secure Boot certificates applied to the device's firmware. This event confirms that all needed certificates have been applied and **the boot manager has been updated to the boot manager signed by the "Windows UEFI CA 2023" certificate**.

**Fields**:
- `UpdateType`: 
  - `0` = Successful update
  - `0x5944` (22852) = High Confidence deployment
- `BucketConfidenceLevel`: Deployment confidence level
  - `"High Confidence"`: Device verified compatible
  - `"Needs More Data"`: Requires more telemetry
  - `"Unknown"`: Confidence level unknown
  - `"Paused"`: Rollout paused
- `BucketId`: Unique identifier for the device's update bucket
- `FirmwareManufacturer`: Firmware manufacturer name
- `FirmwareVersion`: Firmware version string
- `OEMModelNumber`: OEM model number
- `OEMManufacturerName`: OEM manufacturer name
- `OSArchitecture`: OS architecture
- `HResult`: Operation result (typically S_OK / 0)

**Example Message**:
```
This device has updated Secure Boot CA/keys. This device signature information is included here.
DeviceAttributes:
FirmwareManufacturer: American Megatrends Inc. 
FirmwareVersion: 1.2.3 
OEMModelNumber: XPS 15 9530.
Machine:
OEMManufacturerName: Dell Inc. 
OSArchitecture: x64 
Bucketld: 4e22d051e8c143d2875b9d16ef2241c7ec548985a21e5073126d3c1f9bf53bb2 
BucketConfidenceLevel: High Confidence 
UpdateType: 0
HResult: The operation completed successfully.
```

---

## UpdateType Values

| Value | Hex | Description |
|-------|-----|-------------|
| 0 | 0x0000 | Standard successful update |
| 22852 | 0x5944 | High Confidence deployment |

---

## BucketConfidenceLevel Values

| Value | Description |
|-------|-------------|
| High Confidence | Device has been verified as compatible and update is safe |
| Needs More Data | More telemetry data needed before determining confidence |
| Unknown | Confidence level cannot be determined |
| Paused | Update rollout has been paused (potential issues detected) |

---

## Deployment State Correlation

### Successful Deployment Indicators:
1. ✅ Event 1808 present with `UpdateType = 0` or `0x5944`
2. ✅ Event 1808 has `BucketConfidenceLevel = "High Confidence"`
3. ✅ Event 1808 has `HResult = 0` (S_OK)
4. ✅ Event 1045 present (all updates completed)
5. ✅ Registry: `UefiCa2023Status = Updated`
6. ✅ Certificate: Windows UEFI CA 2023 present in db (thumbprint: 45a0fa32604773c82433c3b7d59e7466b3ac0c67)

### In-Progress Indicators:
- 🔄 Event 1036 present (update applied, reboot pending)
- 🔄 Event 1044 present (more updates available)
- 🔄 Registry: `UefiCa2023Status = InProgress`

### Failed/Error Indicators:
- ❌ Event 1034 present (installation failed)
- ❌ Event 1043 present (not applicable)
- ❌ Registry: `UefiCa2023Status = Error`
- ❌ Registry: `UefiCa2023Error != 0`

---

## Best Practices for Monitoring

1. **Primary Validation**: Event ID 1808 with proper UpdateType and ConfidenceLevel
2. **Secondary Validation**: Registry key `HKLM\SYSTEM\CurrentControlSet\Control\SecureBoot\Servicing\UefiCa2023Status`
3. **Tertiary Validation**: Physical presence of Windows UEFI CA 2023 certificate in firmware db
4. **Error Detection**: Monitor events 1034, 1043 and registry UefiCa2023Error key
5. **Timeline Tracking**: Use event timestamps to track deployment progress

---

## Code Implementation

All these events are monitored in `EventLogReader.cs` and parsed by `SecureBootEventParser.cs`:
- Extracts structured fields from event messages
- Provides strongly-typed properties in `SecureBootEventRecord`
- Enables comprehensive deployment state analysis
