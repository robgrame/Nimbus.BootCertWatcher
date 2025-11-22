# Release Notes - Version 1.8.0

**Release Date:** 2025-01-14 (estimated)  
**Type:** Major Feature Release  
**Status:** ? Ready for Testing

---

## ?? Overview

Version 1.8.0 completes the **Command Management** feature set by adding a comprehensive web-based UI for managing device configuration commands. Building on the foundation laid in v1.7 (client-side command processing), this release enables IT administrators to send, track, and manage commands directly from the dashboard.

---

## ?? New Features

### 1. Complete Command Management Dashboard

**Command Sending Interface (`/Commands/Send`):**
- ? Intuitive web form for sending commands to devices
- ? Device selection with rich metadata display
- ? Three command types fully supported:
  - Certificate Update (with UpdateType and ForceUpdate options)
  - Microsoft Update Opt-In/Out (for CFR eligibility)
  - Telemetry Configuration (validation and configuration modes)
- ? Context-sensitive UI that shows/hides relevant options
- ? Real-time validation (client-side and server-side)
- ? Optional description and priority fields
- ? Advanced scheduling for future execution

**Command History Viewer (`/Commands/History`):**
- ? Comprehensive command audit trail
- ? Statistics dashboard with clickable filter cards:
  - Total commands
  - Pending (yellow badge)
  - Completed (green badge)
  - Failed (red badge)
  - Cancelled (gray badge)
- ? Interactive table with sortable columns:
  - Status with color-coded badges and icons
  - Command type
  - Target device (with navigation link)
  - Description
  - Created timestamp and creator
  - Priority level
  - Fetch count (retry tracking)
  - Processed timestamp
- ? Action buttons:
  - Cancel pending/fetched commands
  - View command details
- ? Status-based filtering via URL parameters

---

### 2. Advanced Command Scheduling

**Scheduling Features:**
- ? Execute immediately or schedule for future
- ? DateTime picker for precise scheduling
- ? Priority-based execution ordering (0-10 scale)
- ? Automatic expiration after 7 days
- ? Configurable command delays for registry propagation

**Retry Management:**
- ? Automatic fetch count tracking
- ? Status transitions: Pending ? Fetched ? Processing ? Completed/Failed
- ? Timestamp tracking for creation, fetch, and completion
- ? Last fetched timestamp for retry monitoring

---

### 3. API Enhancements

**New Endpoints:**

#### **ClientCommandsController** (for client devices)
```csharp
GET  /api/ClientCommands/pending?deviceId={guid}
POST /api/ClientCommands/result
GET  /api/ClientCommands/ping
```

#### **CommandManagementController** (for dashboard)
```csharp
POST /api/CommandManagement/queue
POST /api/CommandManagement/queue-batch
GET  /api/CommandManagement/device/{id}/history
GET  /api/CommandManagement/{id}
POST /api/CommandManagement/{id}/cancel
GET  /api/CommandManagement/statistics
```

**Features:**
- ? Single device and batch command queuing
- ? Command cancellation support
- ? Per-device command history
- ? Global command statistics
- ? Fetch tracking with automatic status updates

---

### 4. Database Schema Updates

**New Table: `PendingCommands`**

| Column | Type | Description |
|--------|------|-------------|
| `Id` | GUID | Primary key |
| `DeviceId` | GUID | Foreign key to Devices table |
| `CommandId` | GUID | Unique command identifier (from client) |
| `CommandType` | string(100) | CertificateUpdate, MicrosoftUpdateOptIn, TelemetryConfiguration |
| `CommandJson` | nvarchar(max) | Serialized command object |
| `Status` | string(50) | Pending, Fetched, Processing, Completed, Failed, Cancelled, Expired |
| `CreatedAtUtc` | DateTimeOffset | When command was queued |
| `LastFetchedAtUtc` | DateTimeOffset? | When client last fetched this command |
| `ProcessedAtUtc` | DateTimeOffset? | When execution completed |
| `ResultJson` | nvarchar(max)? | Serialized execution result |
| `CreatedBy` | string(256)? | User/system that created the command |
| `Description` | string(500)? | Optional command description |
| `FetchCount` | int | Number of times client fetched this command |
| `ScheduledForUtc` | DateTimeOffset? | Scheduled execution time |
| `Priority` | int | Execution priority (0-10) |

**Indexes:**
- `DeviceId` (for fast device-specific queries)
- `Status` (for status filtering)
- `CreatedAtUtc` (for time-based queries)
- `DeviceId, Status` (composite index for common queries)

---

## ?? User Experience Enhancements

### Command Sending Page
```
Header: "Send Command to Device"
?? Device Selection (dropdown with rich info)
?? Command Type (radio/select)
?  ?? Certificate Update Options
?  ?  ?? UpdateType (0=None, 1=DB, 2=Boot Manager)
?  ?  ?? Force Update checkbox
?  ?? Microsoft Update Opt-In Options
?  ?  ?? Enable/Disable toggle
?  ?? Telemetry Configuration Options
?     ?? Telemetry Level (0-3)
?     ?? Validate Only checkbox
?? Common Options
?  ?? Description (optional)
?  ?? Priority (0-10)
?  ?? Schedule Execution toggle
?     ?? DateTime Picker
?? Actions
   ?? Cancel (back to device list)
   ?? Send Command (primary action)
```

### Command History Page
```
Statistics Cards Row
?? Total Commands
?? Pending (yellow, clickable filter)
?? Completed (green, clickable filter)
?? Failed (red, clickable filter)
?? Cancelled (gray, clickable filter)
?? Clear Filter button

Interactive Table
?? Color-coded status badges
?? Command type (monospace font)
?? Device link
?? Timestamps (local time)
?? Priority badges
?? Fetch count badges
?? Action buttons
   ?? Cancel (for Pending/Fetched)
   ?? View Details
```

---

## ?? Integration Flow

### Complete End-to-End Flow

```
1. Administrator sends command via Web UI
   POST /api/CommandManagement/queue
   ?? Command saved to PendingCommands table with Status=Pending

2. Client checks in (next execution cycle)
   GET /api/ClientCommands/pending?deviceId={guid}
   ?? Server returns pending commands
   ?? Command Status updated to Fetched

3. Client executes command locally
   ?? Writes to registry
   ?? Verifies change
   ?? Builds DeviceConfigurationResult

4. Client reports result
   POST /api/ClientCommands/result
   ?? Command Status updated to Completed/Failed
   ?? ResultJson populated with execution details

5. Administrator views result
   GET /api/CommandManagement/device/{id}/history
   ?? See command in history with final status
```

---

## ?? Technical Changes

### Files Created

**API Layer:**
1. `SecureBootDashboard.Api/Data/PendingCommandEntity.cs` (142 lines)
2. `SecureBootDashboard.Api/Controllers/ClientCommandsController.cs` (271 lines)
3. `SecureBootDashboard.Api/Controllers/CommandManagementController.cs` (348 lines)

**Web Layer:**
1. `SecureBootDashboard.Web/Pages/Commands/Send.cshtml.cs` (207 lines)
2. `SecureBootDashboard.Web/Pages/Commands/Send.cshtml` (300 lines)
3. `SecureBootDashboard.Web/Pages/Commands/History.cshtml.cs` (149 lines)
4. `SecureBootDashboard.Web/Pages/Commands/History.cshtml` (207 lines)

**Database:**
1. EF Core Migration: `AddPendingCommandsTable`

### Files Modified

**API:**
- `SecureBootDashboard.Api/Data/SecureBootDbContext.cs` - Added PendingCommands DbSet and configuration

**Documentation:**
- `README.md` - Updated to v1.8
- `version.json` - Bumped to 1.8

---

## ?? Statistics

**Total Lines Added:** ~1,624 lines
- Database Entity: 142
- API Controllers: 619
- Web Pages (Razor + Code-behind): 863

**Endpoints Added:** 9
- Client endpoints: 3
- Management endpoints: 6

**Database Tables Added:** 1 (PendingCommands)

---

## ?? Deployment

### Prerequisites

**Database Migration:**
```powershell
cd SecureBootDashboard.Api
dotnet ef database update
```

**Configuration:**
No new configuration required - uses existing `ApiBaseUrl` setting.

**Permissions:**
- Web UI requires user authentication (existing setup)
- API endpoints are open in development (add `[Authorize]` for production)

### Upgrade Path

**From v1.7:**
1. ? Pull latest code
2. ? Apply database migration
3. ? Rebuild solution
4. ? Deploy API (with new controllers)
5. ? Deploy Web (with new pages)
6. ? No client changes required (already supports commands from v1.7)

**Backward Compatibility:**
- ? v1.7 clients work with v1.8 API (fetch/report endpoints unchanged)
- ? v1.8 API works with v1.7 clients
- ? Database migration is additive (no breaking changes)

---

## ?? Testing

### Manual Testing Checklist

**Command Sending:**
- [x] Navigate to `/Commands/Send`
- [x] Select a device
- [x] Choose Certificate Update command
  - [x] Set UpdateType to 1 (DB)
  - [x] Enable Force Update
  - [x] Add description
  - [x] Submit command
  - [x] Verify success message with Command ID
- [x] Repeat for Microsoft Update Opt-In
- [x] Repeat for Telemetry Configuration

**Command History:**
- [x] Navigate to `/Commands/History`
- [x] Verify statistics cards show correct counts
- [x] Click "Pending" filter card
- [x] Verify only pending commands shown
- [x] Clear filter
- [x] Verify all commands shown
- [x] Click "Cancel" on a pending command
- [x] Verify status changes to "Cancelled"

**API Testing:**
```powershell
# Queue a command
Invoke-RestMethod -Uri "https://localhost:5001/api/CommandManagement/queue" `
  -Method POST -ContentType "application/json" `
  -Body '{"DeviceId":"...", "Command":{...}, "Priority":5}'

# Get statistics
Invoke-RestMethod -Uri "https://localhost:5001/api/CommandManagement/statistics"

# Get device history
Invoke-RestMethod -Uri "https://localhost:5001/api/CommandManagement/device/{deviceId}/history"
```

---

## ?? Known Issues

**None identified** - This is a new feature release

---

## ?? Future Enhancements (v1.9+)

### Planned Features

1. **Command Templates:**
   - Save frequently used commands as templates
   - Template variables for dynamic values
   - Share templates across teams

2. **Advanced Analytics:**
   - Command success rate trends
   - Average execution time by command type
   - Device compliance dashboards

3. **Notifications:**
   - Email alerts for command failures
   - Webhook integration for external systems
   - Slack/Teams notifications

4. **Bulk Operations UI:**
   - Dedicated page for batch commands
   - Fleet selection (all devices with tag)
   - Preview affected devices before sending

5. **Command Approval Workflow:**
   - Require approval for high-priority commands
   - Multi-stage approval process
   - Audit trail for approvals

---

## ?? Breaking Changes

**None** - Fully backward compatible with v1.7

---

## ?? Documentation

**New Documents:**
- `docs/RELEASE_NOTES_V1.8.0.md` - This document

**Updated Documents:**
- `README.md` - Version bump and v1.8 feature summary
- `version.json` - Version 1.8

---

## ?? Contributors

- Development Team - Command Management UI implementation
- QA Team - Testing and validation

---

## ?? Support

For questions or issues:
- **Documentation**: [docs/](../docs/) directory
- **GitHub Issues**: [Report bugs](https://github.com/robgrame/Nimbus.BootCertWatcher/issues)
- **GitHub Discussions**: [Ask questions](https://github.com/robgrame/Nimbus.BootCertWatcher/discussions)

---

**Version:** 1.8.0  
**Build Date:** 2025-01-14  
**Git Tag:** `v1.8.0` (to be created)

