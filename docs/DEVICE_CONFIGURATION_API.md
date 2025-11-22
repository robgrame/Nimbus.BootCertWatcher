# Device Configuration API Reference

## Overview

The Device Configuration API provides endpoints to remotely manage device settings for certificate updates and Controlled Feature Rollout (CFR) eligibility.

## Base URL

```
/api/DeviceConfiguration
```

## Endpoints

### 1. Command Certificate Update

Command a specific device to update its Secure Boot certificates.

**Endpoint:** `POST /{deviceId}/certificate-update`

**Request Body:**
```json
{
  "commandId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "updateType": 1,
  "forceUpdate": false,
  "description": "Update UEFI CA 2023 certificates"
}
```

**Parameters:**
- `deviceId` (path, required): GUID of the target device
- `updateType` (body, optional): Type of certificate update (0=DB, 1=Boot Manager, etc.)
- `forceUpdate` (body, optional): Force update even if device is not marked as capable

**Response (200 OK):**
```json
{
  "commandId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "deviceId": "device-guid",
  "success": true,
  "message": "Certificate update command queued successfully. UpdateType: 1, Force: false. Device will process on next check-in.",
  "resultTimestampUtc": "2025-11-21T23:00:00Z",
  "currentState": {
    "microsoftUpdateManagedOptIn": true,
    "allowTelemetry": 2,
    "windowsUEFICA2023Capable": 1,
    "isCfrEligible": true,
    "snapshotTimestampUtc": "2025-11-21T22:55:00Z"
  }
}
```

**Error Responses:**
- `404 Not Found`: Device not found
- `400 Bad Request`: Device does not have UEFI Secure Boot enabled or is not capable

---

### 2. Configure Microsoft Update Opt-In

Enable or disable Microsoft Update Managed Opt-In for CFR eligibility.

**Endpoint:** `POST /microsoft-update-optin`

**Query Parameters:**
- `deviceIds` (optional): Array of device GUIDs (e.g., `?deviceIds=guid1&deviceIds=guid2`)
- `fleetId` (optional): Fleet identifier to target all devices in a fleet

**Request Body:**
```json
{
  "commandId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "optIn": true,
  "description": "Enable CFR opt-in for production fleet"
}
```

**Response (200 OK):**
```json
{
  "commandId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "totalDevices": 25,
  "successCount": 25,
  "failureCount": 0,
  "results": [
    {
      "commandId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "deviceId": "device-guid-1",
      "success": true,
      "message": "Microsoft Update Managed Opt-In command queued. Target state: Enabled. Device will process on next check-in.",
      "currentState": { ... }
    },
    // ... more results
  ]
}
```

**Examples:**

Target specific devices:
```bash
POST /api/DeviceConfiguration/microsoft-update-optin?deviceIds=guid1&deviceIds=guid2
```

Target entire fleet:
```bash
POST /api/DeviceConfiguration/microsoft-update-optin?fleetId=production
```

---

### 3. Configure/Validate Telemetry Level

Validate current telemetry levels or configure devices to meet CFR requirements.

**Endpoint:** `POST /telemetry-configuration`

**Query Parameters:**
- `deviceIds` (optional): Array of device GUIDs
- `fleetId` (optional): Fleet identifier

**Request Body:**
```json
{
  "commandId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "requiredTelemetryLevel": 1,
  "validateOnly": true,
  "description": "Validate Basic telemetry requirement for CFR"
}
```

**Parameters:**
- `requiredTelemetryLevel`: 0=Security, 1=Basic, 2=Enhanced, 3=Full
- `validateOnly`: If true, only validates current level; if false, queues configuration command

**Response (200 OK):**
```json
{
  "commandId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "totalDevices": 10,
  "successCount": 8,
  "failureCount": 2,
  "results": [
    {
      "commandId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "deviceId": "device-guid-1",
      "success": true,
      "message": "Telemetry validation passed. Current level: Basic (1), Required: Basic (1)",
      "currentState": { ... }
    },
    {
      "commandId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "deviceId": "device-guid-2",
      "success": false,
      "message": "Telemetry validation failed. Current level: Security (0), Required: Basic (1)",
      "currentState": { ... }
    }
  ]
}
```

**Telemetry Levels:**
- `0` = Security (Enterprise/Education/Server only) - **Not CFR eligible**
- `1` = Basic - **Minimum for CFR eligibility**
- `2` = Enhanced
- `3` = Full

---

### 4. Get Device Configuration State

Retrieve current configuration state for a specific device.

**Endpoint:** `GET /{deviceId}/state`

**Response (200 OK):**
```json
{
  "microsoftUpdateManagedOptIn": true,
  "allowTelemetry": 2,
  "windowsUEFICA2023Capable": 1,
  "isCfrEligible": true,
  "snapshotTimestampUtc": "2025-11-21T22:55:00Z"
}
```

**CFR Eligibility Logic:**
- Device is CFR eligible if:
  - `microsoftUpdateManagedOptIn == true` AND
  - `allowTelemetry >= 1` (Basic or higher)

---

## Common Workflows

### Enable CFR for a Fleet

1. **Check current telemetry levels:**
```bash
POST /api/DeviceConfiguration/telemetry-configuration?fleetId=production
{
  "requiredTelemetryLevel": 1,
  "validateOnly": true
}
```

2. **Configure devices that don't meet requirement:**
```bash
POST /api/DeviceConfiguration/telemetry-configuration?fleetId=production
{
  "requiredTelemetryLevel": 1,
  "validateOnly": false
}
```

3. **Enable Microsoft Update Opt-In:**
```bash
POST /api/DeviceConfiguration/microsoft-update-optin?fleetId=production
{
  "optIn": true
}
```

### Update Certificates on Specific Devices

```bash
POST /api/DeviceConfiguration/{deviceId}/certificate-update
{
  "updateType": 1,
  "forceUpdate": false,
  "description": "UEFI CA 2023 deployment - Phase 1"
}
```

### Audit CFR Eligibility

```bash
GET /api/DeviceConfiguration/{deviceId}/state
```

Check the `isCfrEligible` property in the response.

---

## Error Handling

All endpoints return consistent error responses:

**400 Bad Request:**
```json
{
  "error": "RequiredTelemetryLevel must be between 0 and 3"
}
```

**404 Not Found:**
```json
{
  "error": "Device {deviceId} not found"
}
```

---

## Notes

- Commands are queued for device processing on next check-in
- Current implementation simulates command queuing (placeholder for future persistence)
- All timestamps are in UTC
- Device state is derived from the latest report in the database
- Batch operations process devices independently (partial success is possible)

---

## Security Considerations

- Add `[Authorize]` attribute to endpoints before production deployment
- Implement role-based access control (RBAC) for fleet-wide operations
- Consider audit logging for all configuration changes
- Validate that users have permission to modify target devices/fleets
