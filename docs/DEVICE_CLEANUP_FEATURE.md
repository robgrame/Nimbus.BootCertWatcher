# Device Cleanup Feature - Implementation Guide

**Date:** 2025-01-14  
**Version:** 1.12.0  
**Feature:** Automatic Device Cleanup & Manual Deletion

---

## Overview

The Device Cleanup feature provides automated and manual management of inactive devices in the SecureBootDashboard database. This helps maintain database hygiene by removing devices that have been inactive for a specified period.

---

## Features

### 1. **Automatic Cleanup Service**
- Background service running every hour
- Configurable inactivity threshold (default: 90 days)
- Scheduled daily execution at 2:00 AM UTC
- Tracks last cleanup run and device count
- Cascade deletion of reports, events, and commands

### 2. **Manual Cleanup Controls**
- Web-based management interface
- Preview devices before deletion
- Selective device deletion
- Bulk delete operations
- Configuration management

### 3. **Database Configuration**
- Stored in `DeviceCleanupConfig` table
- Single configuration record (ID = 1)
- Configurable via API and Web UI
- Default values seeded on migration

---

## Database Schema

### DeviceCleanupConfig Table

```sql
CREATE TABLE [DeviceCleanupConfig] (
    [Id] int NOT NULL IDENTITY,
    [Enabled] bit NOT NULL,
    [InactiveDaysThreshold] int NOT NULL,
    [CleanupSchedule] nvarchar(100) NULL,
    [DeleteAssociatedData] bit NOT NULL,
    [NotifyOnCleanup] bit NOT NULL,
    [NotificationEmail] nvarchar(256) NULL,
    [LastCleanupRunUtc] datetimeoffset NULL,
    [LastCleanupDeviceCount] int NOT NULL,
    [CreatedAtUtc] datetimeoffset NOT NULL,
    [UpdatedAtUtc] datetimeoffset NOT NULL,
    CONSTRAINT [PK_DeviceCleanupConfig] PRIMARY KEY ([Id])
);
```

### Default Configuration

```csharp
{
    Id = 1,
    Enabled = false,                     // Disabled by default
    InactiveDaysThreshold = 90,          // 90 days
    CleanupSchedule = "0 2 * * *",       // Daily at 2 AM UTC
    DeleteAssociatedData = true,         // Cascade delete
    NotifyOnCleanup = false,             // No notifications
    NotificationEmail = null,
    LastCleanupRunUtc = null,
    LastCleanupDeviceCount = 0,
    CreatedAtUtc = DateTimeOffset.UtcNow,
    UpdatedAtUtc = DateTimeOffset.UtcNow
}
```

---

## API Endpoints

### 1. Get Cleanup Configuration

**GET** `/api/DeviceCleanup/config`

**Response:**
```json
{
  "id": 1,
  "enabled": false,
  "inactiveDaysThreshold": 90,
  "cleanupSchedule": "0 2 * * *",
  "deleteAssociatedData": true,
  "notifyOnCleanup": false,
  "notificationEmail": null,
  "lastCleanupRunUtc": null,
  "lastCleanupDeviceCount": 0,
  "createdAtUtc": "2025-01-14T00:00:00Z",
  "updatedAtUtc": "2025-01-14T00:00:00Z"
}
```

---

### 2. Update Cleanup Configuration

**PUT** `/api/DeviceCleanup/config`

**Request Body:**
```json
{
  "enabled": true,
  "inactiveDaysThreshold": 180,
  "cleanupSchedule": "0 2 * * *",
  "deleteAssociatedData": true,
  "notifyOnCleanup": true,
  "notificationEmail": "admin@example.com"
}
```

**Response:** Updated configuration object

---

### 3. Preview Cleanup (Eligible Devices)

**GET** `/api/DeviceCleanup/preview?daysThreshold=90`

**Response:**
```json
{
  "threshold": 90,
  "cutoffDate": "2024-10-16T00:00:00Z",
  "deviceCount": 15,
  "devices": [
    {
      "id": "guid-1",
      "machineName": "DESKTOP-01",
      "domainName": "contoso.com",
      "lastSeenUtc": "2024-08-01T10:30:00Z",
      "daysInactive": 165,
      "reportCount": 25
    }
  ]
}
```

---

### 4. Delete Specific Devices

**POST** `/api/DeviceCleanup/delete`

**Request Body:**
```json
{
  "deviceIds": [
    "guid-1",
    "guid-2",
    "guid-3"
  ]
}
```

**Response:**
```json
{
  "deletedCount": 3,
  "deletedDevices": [
    {
      "id": "guid-1",
      "machineName": "DESKTOP-01",
      "lastSeenUtc": "2024-08-01T10:30:00Z"
    }
  ]
}
```

---

### 5. Run Cleanup Now (Manual Trigger)

**POST** `/api/DeviceCleanup/run`

**Response:**
```json
{
  "deletedCount": 15,
  "deletedDevices": [...]
}
```

---

## Web Interface

### Access Path
`/Devices/Cleanup`

### Features

1. **Configuration Panel**
   - Enable/Disable automatic cleanup
   - Set inactivity threshold (1-365 days)
   - Configure email notifications
   - View cleanup schedule
   - Toggle cascade deletion

2. **Statistics Cards**
   - Devices eligible for cleanup
   - Current threshold
   - Cutoff date display

3. **Device Preview Table**
   - List of eligible devices
   - Last seen date
   - Days inactive (color-coded)
   - Report count
   - Checkbox selection

4. **Manual Actions**
   - Preview cleanup results
   - Run cleanup immediately
   - Delete selected devices
   - Bulk operations

---

## Background Service

### DeviceCleanupService

**Location:** `SecureBootDashboard.Api/Services/DeviceCleanupService.cs`

**Execution Flow:**
```
1. Check every hour
2. Load cleanup configuration from DB
3. If enabled:
   a. Calculate cutoff date (UtcNow - Threshold)
   b. Query devices where LastSeenUtc < cutoff
   c. Delete matching devices (cascade)
   d. Update cleanup stats in config
   e. Send notification email (if configured)
```

**Registration:** `Program.cs`
```csharp
builder.Services.AddHostedService<DeviceCleanupService>();
```

---

## Migration

### Migration Name
`AddDeviceCleanupConfiguration`

### To Apply
```powershell
cd SecureBootDashboard.Api
dotnet ef database update
```

### Expected Output
```
Applying migration '20250114XXXXXX_AddDeviceCleanupConfiguration'.
Done.
```

### Rollback
```powershell
dotnet ef database update <previous-migration-name>
```

---

## Usage Examples

### Example 1: Enable Automatic Cleanup

**Scenario:** Clean up devices inactive for 180 days

**Steps:**
1. Navigate to `/Devices/Cleanup`
2. Enable "Enable Automatic Cleanup"
3. Set threshold to `180` days
4. Click "Save Configuration"

**Result:**
- Service will run daily at 2 AM UTC
- Devices not seen in 180+ days will be deleted
- Cascade deletes reports, events, commands

---

### Example 2: Manual Selective Cleanup

**Scenario:** Delete specific test devices

**Steps:**
1. Navigate to `/Devices/Cleanup`
2. Review eligible devices list
3. Check boxes for target devices
4. Click "Delete Selected Devices"
5. Confirm deletion

**Result:**
- Selected devices deleted immediately
- Associated data removed
- Stats updated

---

### Example 3: Preview Before Cleanup

**Scenario:** Check what will be deleted with 60-day threshold

**Steps:**
1. Navigate to `/Devices/Cleanup`
2. Change threshold to `60` days
3. Click "Preview Devices to be Deleted"

**Result:**
- Page refreshes with updated preview
- Shows devices inactive for 60+ days
- No data deleted (preview only)

---

## Configuration Best Practices

### Recommended Thresholds

| Environment | Threshold | Rationale |
|-------------|-----------|-----------|
| **Production** | 180 days | Conservative, avoid accidental deletion |
| **Test** | 90 days | Balance between cleanup and retention |
| **Development** | 30 days | Aggressive cleanup for test data |

### Email Notifications

**When to Enable:**
- Production environments
- Compliance requirements
- Audit trail needed

**Notification includes:**
- Number of devices deleted
- Timestamp of cleanup
- Threshold used

---

## Safety Features

### 1. **Cascade Protection**
- Foreign key constraints ensure referential integrity
- Deletes cascade to:
  - SecureBootReports
  - SecureBootEvents
  - PendingCommands

### 2. **Preview Before Delete**
- Always preview eligible devices
- Review before enabling automatic cleanup
- Test with higher thresholds first

### 3. **Confirmation Prompts**
- Manual deletions require confirmation
- Selected device list shown before delete
- Non-reversible operation warning

### 4. **Disabled by Default**
- Automatic cleanup disabled on install
- Manual opt-in required
- Administrator must configure

---

## Monitoring

### Check Last Cleanup Run

**API:** `GET /api/DeviceCleanup/config`

**Look for:**
```json
{
  "lastCleanupRunUtc": "2025-01-14T02:00:00Z",
  "lastCleanupDeviceCount": 15
}
```

### Service Health

**Logs:** `logs/api-*.log`

**Search for:**
```
[DeviceCleanup] Starting cleanup run. Threshold: 90 days
[DeviceCleanup] Found 15 inactive devices to delete
[DeviceCleanup] Successfully deleted 15 inactive devices
```

---

## Troubleshooting

### Issue: Cleanup Not Running

**Symptoms:**
- `LastCleanupRunUtc` never updates
- Eligible devices not being deleted

**Checks:**
1. Verify `Enabled = true` in config
2. Check service logs for errors
3. Confirm `DeviceCleanupService` registered in `Program.cs`
4. Check database connectivity

---

### Issue: Too Many Devices Deleted

**Symptoms:**
- More devices deleted than expected
- Important devices removed

**Recovery:**
1. Disable automatic cleanup immediately
2. Check SQL backups
3. Restore from backup if critical
4. Increase threshold to prevent recurrence

---

### Issue: Preview Shows Wrong Count

**Symptoms:**
- Preview count doesn't match actual devices

**Solution:**
1. Refresh preview with current threshold
2. Check `LastSeenUtc` values in database
3. Verify timezone (UTC) consistency
4. Re-calculate cutoff date

---

## Security Considerations

### 1. **Access Control**
- Cleanup page accessible to all authenticated users
- **TODO:** Add role-based access control
- Restrict deletion to administrators

### 2. **Audit Trail**
- Log all cleanup operations
- Track manual deletions
- Record configuration changes

### 3. **Data Loss Prevention**
- Confirm before delete
- Preview before bulk operations
- Test in non-production first

---

## Future Enhancements

### Planned Features

1. **Role-Based Access Control**
   - Restrict cleanup to admin role
   - Separate view/delete permissions

2. **Email Notifications**
   - Implement SMTP sending
   - Customizable email templates
   - Digest reports

3. **Advanced Scheduling**
   - Parse cron expressions properly
   - Multiple schedules
   - Maintenance windows

4. **Soft Delete**
   - Mark deleted instead of hard delete
   - Retention period before permanent delete
   - Recovery option

5. **Cleanup History**
   - Track all cleanup operations
   - View historical stats
   - Audit reports

---

## Summary

? **Implemented:**
- Automatic cleanup service
- Database configuration
- API endpoints
- Web UI for management
- Manual deletion controls
- Preview functionality

?? **Known Limitations:**
- No email notification sending (TODO)
- Basic cron schedule parsing
- No role-based access control
- Hard delete only (no recovery)

?? **Impact:**
- Improved database performance
- Reduced storage usage
- Automated maintenance
- Better data hygiene

---

**Next Steps:**
1. Apply database migration
2. Configure cleanup threshold
3. Test in non-production
4. Enable automatic cleanup
5. Monitor cleanup runs

---

**Support:**
- GitHub Issues: [Report bugs](https://github.com/robgrame/Nimbus.BootCertWatcher/issues)
- Documentation: `docs/` directory

**Version:** 1.12.0  
**Last Updated:** 2025-01-14
