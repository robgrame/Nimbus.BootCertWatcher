# ?? Q1 2025 Features Implementation Plan

## ?? Overview

This document outlines the implementation plan for Q1 2025 features as defined in the roadmap:

1. ? Real-time dashboard updates (SignalR)
2. ? Export reports to Excel/CSV
3. ? Dark mode theme support
4. ? Custom alert thresholds per fleet

---

## ?? Feature 1: Real-time Dashboard Updates (SignalR)

### Objective
Enable real-time updates of device status and compliance metrics without page refresh.

### Implementation Steps

#### 1.1 Backend (API)

**Add NuGet Package:**
```bash
cd SecureBootDashboard.Api
dotnet add package Microsoft.AspNetCore.SignalR
```

**Create SignalR Hub:**
- File: `SecureBootDashboard.Api/Hubs/DashboardHub.cs`
- Methods:
  - `SubscribeToDeviceUpdates()`
  - `UnsubscribeFromDeviceUpdates()`
  - `SendDeviceUpdate(deviceId, status)`
  - `SendComplianceUpdate(totalDevices, compliant, pending, error)`

**Integrate with Report Ingestion:**
- Update `SecureBootReportsController.cs`
- Broadcast updates when new report is received
- Notify clients about device status changes

**Configure SignalR in `Program.cs`:**
```csharp
builder.Services.AddSignalR();
app.MapHub<DashboardHub>("/dashboardHub");
```

#### 1.2 Frontend (Web)

**Add SignalR Client Library:**
- Add `@microsoft/signalr` via CDN or npm

**Create JavaScript Client:**
- File: `wwwroot/js/dashboard-realtime.js`
- Connect to `/dashboardHub`
- Subscribe to updates
- Update DOM when notifications received

**Update Dashboard Pages:**
- `Index.cshtml` - Real-time device counts
- `Devices/List.cshtml` - Live device status updates
- `Devices/Details.cshtml` - Live report updates

#### 1.3 Testing
- [ ] Test connection establishment
- [ ] Test device update broadcasting
- [ ] Test compliance metrics updates
- [ ] Test reconnection on connection loss
- [ ] Test performance with 100+ connected clients

### Estimated Time
- Backend: 8 hours
- Frontend: 6 hours
- Testing: 4 hours
- **Total: 18 hours (~2-3 days)**

---

## ?? Feature 2: Export Reports to Excel/CSV

### Objective
Allow users to export device lists, reports, and compliance data to Excel/CSV formats.

### Implementation Steps

#### 2.1 Backend (API)

**Add NuGet Packages:**
```bash
cd SecureBootDashboard.Api
dotnet add package ClosedXML
dotnet add package CsvHelper
```

**Create Export Service:**
- File: `Services/ExportService.cs`
- Interface: `IExportService`
- Methods:
  - `ExportDevicesToExcel(devices)`
  - `ExportDevicesToCsv(devices)`
  - `ExportReportsToExcel(reports)`
  - `ExportReportsToCsv(reports)`
  - `ExportComplianceHistoryToExcel(history)`

**Add Export Endpoints:**
- `GET /api/Devices/export/excel`
- `GET /api/Devices/export/csv`
- `GET /api/SecureBootReports/export/excel?deviceId={guid}`
- `GET /api/SecureBootReports/export/csv?deviceId={guid}`

**Excel Template Features:**
- Auto-sized columns
- Header row with bold text
- Color-coded status (green/yellow/red)
- Frozen header row
- Filter dropdowns

**CSV Format:**
- UTF-8 encoding with BOM
- Comma delimiter
- Quoted strings
- Header row

#### 2.2 Frontend (Web)

**Add Export Buttons:**
- Device list page: "Export to Excel" / "Export to CSV"
- Report history page: "Export to Excel" / "Export to CSV"
- Dashboard page: "Export Summary to Excel"

**Create Export UI:**
- File: `wwwroot/js/export.js`
- Show export modal with options:
  - Format (Excel/CSV)
  - Date range filter
  - Columns to include
- Progress indicator during export
- Auto-download file

**Update Pages:**
- `Devices/List.cshtml` - Add export buttons
- `Devices/Reports.cshtml` - Add export buttons
- `Index.cshtml` - Add summary export button

#### 2.3 Testing
- [ ] Test Excel export with 100+ devices
- [ ] Test CSV export with special characters
- [ ] Test file download in different browsers
- [ ] Test export with filters applied
- [ ] Test memory usage with large exports

### Estimated Time
- Backend: 10 hours
- Frontend: 6 hours
- Testing: 4 hours
- **Total: 20 hours (~2-3 days)**

---

## ?? Feature 3: Dark Mode Theme Support

### Objective
Implement dark mode theme with user preference persistence.

### Implementation Steps

#### 3.1 CSS Theme Variables

**Create Theme Files:**
- `wwwroot/css/theme-light.css` - Light theme variables
- `wwwroot/css/theme-dark.css` - Dark theme variables
- `wwwroot/css/theme-switcher.css` - Theme toggle styles

**Define CSS Variables:**
```css
/* Light Theme */
:root[data-theme="light"] {
  --bg-primary: #ffffff;
  --bg-secondary: #f8f9fa;
  --text-primary: #212529;
  --text-secondary: #6c757d;
  --border-color: #dee2e6;
  /* ... more variables */
}

/* Dark Theme */
:root[data-theme="dark"] {
  --bg-primary: #1a1a1a;
  --bg-secondary: #2d2d2d;
  --text-primary: #e9ecef;
  --text-secondary: #adb5bd;
  --border-color: #495057;
  /* ... more variables */
}
```

**Update Existing CSS:**
- Replace hardcoded colors with CSS variables
- Test all components in both themes

#### 3.2 Theme Switcher

**Create Theme Switcher Component:**
- File: `wwwroot/js/theme-switcher.js`
- Functions:
  - `getCurrentTheme()`
  - `setTheme(theme)`
  - `toggleTheme()`
  - `saveThemePreference(theme)`
  - `loadThemePreference()`

**Add Theme Toggle Button:**
- Location: Navbar (top-right)
- Icon: Sun/Moon toggle
- Smooth transition animation

**Persist Theme Preference:**
- Use `localStorage` for client-side persistence
- Optional: Save to user profile (database)

#### 3.3 Chart.js Dark Mode

**Update Chart Configurations:**
- Detect current theme
- Apply appropriate colors to charts
- Update on theme change

```javascript
function getChartColors() {
  const isDark = document.documentElement.getAttribute('data-theme') === 'dark';
  return {
    background: isDark ? 'rgba(255, 255, 255, 0.1)' : 'rgba(0, 0, 0, 0.1)',
    text: isDark ? '#e9ecef' : '#212529',
    // ... more colors
  };
}
```

#### 3.4 Splash Screen Dark Mode

**Update Splash Screen:**
- Detect theme preference
- Apply dark gradient for dark mode
- Update logo if needed (light logo variant)

#### 3.5 Testing
- [ ] Test theme switcher in all pages
- [ ] Test theme persistence across sessions
- [ ] Test charts in dark mode
- [ ] Test splash screen in dark mode
- [ ] Test accessibility (contrast ratios)
- [ ] Test all UI components (cards, tables, modals)

### Estimated Time
- CSS Theme Variables: 8 hours
- Theme Switcher: 4 hours
- Chart.js Integration: 4 hours
- Testing: 6 hours
- **Total: 22 hours (~3 days)**

---

## ?? Feature 4: Custom Alert Thresholds per Fleet

### Objective
Allow administrators to configure custom alert thresholds for different fleets (e.g., critical fleets require 95% compliance, others 80%).

### Implementation Steps

#### 4.1 Database Schema

**Add FleetSettings Table:**
```sql
CREATE TABLE FleetSettings (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    FleetId NVARCHAR(100) NOT NULL UNIQUE,
    DisplayName NVARCHAR(200) NOT NULL,
    ComplianceThreshold INT NOT NULL DEFAULT 80,
    PendingWarningDays INT NOT NULL DEFAULT 7,
    InactiveDeviceThresholdDays INT NOT NULL DEFAULT 30,
    CertificateExpiryWarningDays INT NOT NULL DEFAULT 90,
    AlertEnabled BIT NOT NULL DEFAULT 1,
    AlertEmailRecipients NVARCHAR(MAX),
    CreatedAtUtc DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAtUtc DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);
```

**Add EF Migration:**
```bash
cd SecureBootDashboard.Api
dotnet ef migrations add AddFleetSettings
dotnet ef database update
```

#### 4.2 Backend (API)

**Create FleetSettings Entity:**
- File: `SecureBootDashboard.Api/Data/FleetSettingsEntity.cs`

**Add FleetSettings DbSet:**
```csharp
public DbSet<FleetSettingsEntity> FleetSettings { get; set; }
```

**Create FleetSettings Service:**
- File: `Services/FleetSettingsService.cs`
- Interface: `IFleetSettingsService`
- Methods:
  - `GetFleetSettingsAsync(fleetId)`
  - `GetAllFleetSettingsAsync()`
  - `CreateFleetSettingsAsync(settings)`
  - `UpdateFleetSettingsAsync(settings)`
  - `DeleteFleetSettingsAsync(fleetId)`

**Create FleetSettings Controller:**
- File: `Controllers/FleetSettingsController.cs`
- Endpoints:
  - `GET /api/FleetSettings`
  - `GET /api/FleetSettings/{fleetId}`
  - `POST /api/FleetSettings`
  - `PUT /api/FleetSettings/{fleetId}`
  - `DELETE /api/FleetSettings/{fleetId}`

**Update Alert Logic:**
- Use fleet-specific thresholds in compliance calculations
- Generate alerts based on fleet settings

#### 4.3 Frontend (Web)

**Create Fleet Settings Page:**
- File: `Pages/Admin/FleetSettings.cshtml`
- File: `Pages/Admin/FleetSettings.cshtml.cs`

**Fleet Settings UI:**
- List of all fleets with current settings
- Edit modal for each fleet
- Form fields:
  - Fleet ID (readonly)
  - Display Name
  - Compliance Threshold (%)
  - Pending Warning Days
  - Inactive Device Threshold Days
  - Certificate Expiry Warning Days
  - Alert Enabled (checkbox)
  - Alert Email Recipients (textarea)
- Save/Cancel buttons
- Validation

**Update Dashboard:**
- Show fleet-specific compliance status
- Color-code based on fleet thresholds
- Fleet filter dropdown

**Add Admin Navigation:**
- New menu item: "Fleet Settings"
- Restrict to admin users (future RBAC)

#### 4.4 Alert System Integration

**Create AlertService:**
- File: `Services/AlertService.cs`
- Methods:
  - `GenerateComplianceAlerts(fleetId)`
  - `SendAlertEmail(alert)`
  - `CheckThresholdViolations(fleetId)`

**Scheduled Alert Check:**
- Background service to check thresholds
- Run hourly or daily
- Send email notifications

#### 4.5 Testing
- [ ] Test CRUD operations on FleetSettings
- [ ] Test threshold calculations
- [ ] Test alert generation
- [ ] Test email notifications
- [ ] Test UI with multiple fleets
- [ ] Test validation rules

### Estimated Time
- Database Schema: 4 hours
- Backend Service: 10 hours
- Frontend UI: 12 hours
- Alert Integration: 8 hours
- Testing: 6 hours
- **Total: 40 hours (~5 days)**

---

## ?? Implementation Timeline

### Week 1-2: Real-time Dashboard (SignalR)
- Days 1-2: Backend implementation
- Days 3-4: Frontend integration
- Day 5: Testing and bug fixes

### Week 3-4: Export to Excel/CSV
- Days 1-2: Backend export service
- Days 3-4: Frontend UI and integration
- Day 5: Testing and optimization

### Week 5-6: Dark Mode Theme
- Days 1-2: CSS theme variables
- Days 3-4: Theme switcher and charts
- Day 5: Testing and polishing

### Week 7-8: Custom Alert Thresholds
- Days 1-2: Database and backend
- Days 3-5: Frontend UI
- Days 6-7: Alert integration
- Day 8: Testing

**Total Duration: ~8 weeks (Q1 2025)**

---

## ?? Testing Strategy

### Unit Tests
- [ ] Backend services (FleetSettings, Export, SignalR)
- [ ] Theme switcher logic
- [ ] Alert threshold calculations

### Integration Tests
- [ ] SignalR hub communication
- [ ] Export file generation
- [ ] Fleet settings CRUD operations

### E2E Tests
- [ ] Real-time dashboard updates
- [ ] Export workflow (click ? download)
- [ ] Theme switching across pages
- [ ] Fleet settings management

### Performance Tests
- [ ] SignalR with 100+ clients
- [ ] Export with 1000+ records
- [ ] Theme switching performance

---

## ?? Success Metrics

### Real-time Updates
- ? Updates appear within 2 seconds of event
- ? No UI freezing during updates
- ? Reconnection within 5 seconds on disconnect

### Export
- ? Export 1000 records in < 5 seconds
- ? Excel file < 5 MB for 1000 records
- ? All data exported correctly

### Dark Mode
- ? All components visible in dark mode
- ? Contrast ratios meet WCAG AA standards
- ? Theme preference persists across sessions

### Fleet Thresholds
- ? Alerts generated based on fleet settings
- ? UI updates to show fleet-specific compliance
- ? Settings saved and applied correctly

---

## ?? Security Considerations

### SignalR
- ? Authenticate connections
- ? Authorize hub method calls
- ? Rate limit broadcasts
- ? Validate client inputs

### Export
- ? Authorize export requests
- ? Limit export size (max records)
- ? Sanitize data before export
- ? Log export operations

### Fleet Settings
- ? Admin-only access (RBAC)
- ? Validate input ranges
- ? Audit log for changes
- ? Prevent fleet ID conflicts

---

## ?? Documentation Updates

### User Documentation
- [ ] Real-time updates user guide
- [ ] Export feature guide
- [ ] Dark mode user guide
- [ ] Fleet settings admin guide

### Developer Documentation
- [ ] SignalR hub documentation
- [ ] Export service API docs
- [ ] Theme switcher technical guide
- [ ] Fleet settings API reference

### README Updates
- [ ] Update roadmap (mark Q1 2025 as complete)
- [ ] Add screenshots of new features
- [ ] Update feature list

---

## ?? Deliverables

### Code
- ? SignalR hub and client integration
- ? Export service with Excel/CSV support
- ? Dark mode theme with switcher
- ? Fleet settings management system

### Documentation
- ? Feature implementation guides
- ? API documentation updates
- ? User guides for new features

### Tests
- ? Unit tests (80% coverage)
- ? Integration tests
- ? E2E test scenarios

### Deployment
- ? Migration scripts
- ? Configuration updates
- ? Release notes

---

## ?? Rollout Plan

### Phase 1: Development (Weeks 1-6)
- Implement features
- Unit and integration testing
- Code reviews

### Phase 2: Testing (Week 7)
- E2E testing
- Performance testing
- Bug fixes

### Phase 3: Staging (Week 8)
- Deploy to staging environment
- User acceptance testing
- Documentation finalization

### Phase 4: Production (Week 9)
- Deploy to production
- Monitor for issues
- Collect user feedback

---

## ?? Notes

### Dependencies
- SignalR: Requires WebSocket support
- Export: ClosedXML and CsvHelper libraries
- Dark Mode: Modern browser with CSS variable support
- Fleet Settings: Database migration required

### Browser Support
- Chrome/Edge: ? Full support
- Firefox: ? Full support
- Safari: ? Full support (iOS 13+)
- IE11: ? Not supported (deprecated)

### Known Limitations
- SignalR: May require sticky sessions in load-balanced environments
- Export: Large exports (>10,000 records) may timeout
- Dark Mode: Some third-party components may need manual styling
- Fleet Settings: Requires admin privileges to configure

---

## ?? Related Documents

- [README.md](../README.md) - Project overview and roadmap
- [DEPLOYMENT_GUIDE.md](DEPLOYMENT_GUIDE.md) - Deployment instructions
- [API Documentation](../SecureBootDashboard.Api/swagger) - API reference

---

**Feature Branch**: `feature/q1-2025-enhancements`  
**Target Release**: Q1 2025  
**Status**: ?? In Progress

---

*Last Updated: 2025-01-20*
