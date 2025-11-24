# Windows Version Tracking - API Integration

This document describes the API endpoints created for Windows version tracking and build security verification.

## Overview

The Windows Version API provides endpoints to:
- Synchronize Windows version data from WindowsVersionsCore
- Check if specific builds are secure
- Get version and build information
- View statistics and compliance status

## API Endpoints

### Base URL
```
/api/WindowsVersion
```

---

### 1. Sync Windows Versions

**Endpoint:** `POST /api/WindowsVersion/sync`

**Description:** Triggers synchronization of Windows 10 and Windows 11 version data from WindowsVersionsCore to the local database.

**Response:**
```json
{
  "success": true,
  "versionsSynced": 15,
  "buildsSynced": 45,
  "errorMessage": null,
  "lastSyncedUtc": "2025-01-22T21:30:00Z"
}
```

**Usage:**
```powershell
Invoke-RestMethod -Uri "https://api.example.com/api/WindowsVersion/sync" -Method POST
```

**When to call:**
- After initial deployment
- Weekly/monthly to refresh build data
- After Microsoft releases new Windows updates

---

### 2. Check Build Security

**Endpoint:** `GET /api/WindowsVersion/check-build/{buildNumber}`

**Description:** Checks if a specific Windows build is considered secure.

**Parameters:**
- `buildNumber` (path) - Build number to check (e.g., "19045.3803")

**Response:**
```json
{
  "buildNumber": "19045.3803",
  "isSecure": true,
  "isLatest": false,
  "securityNotes": "Current release",
  "releaseDate": "2024-01-09T00:00:00Z",
  "latestSecureBuild": "19045.3930"
}
```

**Usage:**
```powershell
$build = "19045.3803"
Invoke-RestMethod -Uri "https://api.example.com/api/WindowsVersion/check-build/$build"
```

---

### 3. Get All Versions

**Endpoint:** `GET /api/WindowsVersion/versions`

**Description:** Returns all Windows versions tracked in the database.

**Response:**
```json
[
  {
    "id": 1,
    "version": "22H2",
    "name": "Windows 10 22H2",
    "releaseDate": "2022-10-18T00:00:00Z",
    "endOfSupportDate": "2025-10-14T00:00:00Z",
    "lastSyncedUtc": "2025-01-22T21:30:00Z"
  },
  {
    "id": 2,
    "version": "23H2",
    "name": "Windows 11 23H2",
    "releaseDate": "2023-10-31T00:00:00Z",
    "endOfSupportDate": null,
    "lastSyncedUtc": "2025-01-22T21:30:00Z"
  }
]
```

---

### 4. Get Builds for Version

**Endpoint:** `GET /api/WindowsVersion/versions/{version}/builds`

**Description:** Returns all builds for a specific Windows version.

**Parameters:**
- `version` (path) - Version number (e.g., "22H2", "23H2")

**Response:**
```json
[
  {
    "id": 1,
    "buildNumber": "19045.3930",
    "majorBuild": 19045,
    "minorBuild": 3930,
    "releaseDate": "2024-01-09T00:00:00Z",
    "kbArticle": "KB5034441",
    "isSecure": true,
    "isLatest": true,
    "securityNotes": "Current release",
    "lastSyncedUtc": "2025-01-22T21:30:00Z"
  }
]
```

---

### 5. Get Latest Secure Build

**Endpoint:** `GET /api/WindowsVersion/versions/{version}/latest-secure`

**Description:** Returns the latest secure build for a specific version.

**Parameters:**
- `version` (path) - Version number (e.g., "22H2")

**Response:**
```json
{
  "id": 1,
  "buildNumber": "19045.3930",
  "majorBuild": 19045,
  "minorBuild": 3930,
  "releaseDate": "2024-01-09T00:00:00Z",
  "kbArticle": "KB5034441",
  "isSecure": true,
  "isLatest": true,
  "securityNotes": "Current release",
  "lastSyncedUtc": "2025-01-22T21:30:00Z"
}
```

---

### 6. Get Build Statistics

**Endpoint:** `GET /api/WindowsVersion/statistics`

**Description:** Returns aggregated statistics about Windows builds across all devices.

**Response:**
```json
{
  "totalDevices": 150,
  "devicesWithSecureBuilds": 120,
  "devicesWithOutdatedBuilds": 25,
  "devicesWithUnknownBuilds": 5,
  "secureBuildPercentage": 80.0,
  "buildDistribution": {
    "19045.3930": 85,
    "22631.2861": 35,
    "19045.3803": 20,
    "22621.1000": 10
  }
}
```

**Dashboard Visualization:**
```
Fleet Security Status:
??????????????????????????????????????
Total Devices:        150
  ? Secure Builds:   120 (80.0%)
  ??  Outdated:       25  (16.7%)
  ? Unknown:         5   (3.3%)
```

---

### 7. Get Devices with Outdated Builds

**Endpoint:** `GET /api/WindowsVersion/devices/outdated`

**Description:** Returns devices with insecure or outdated Windows builds.

**Response:**
```json
[
  {
    "deviceId": "550e8400-e29b-41d4-a716-446655440000",
    "machineName": "DESKTOP-ABC123",
    "domainName": "CONTOSO",
    "osBuildNumber": "19045.3803",
    "isSecure": false,
    "isLatest": false,
    "securityNotes": "Build 19045.3803 not found. Latest known build: 19045.3930",
    "lastSeenUtc": "2025-01-22T20:00:00Z"
  }
]
```

**Use Cases:**
- Compliance reporting
- Patch management prioritization
- Security audits
- Update deployment targeting

---

## Testing

Use the provided PowerShell script to test all endpoints:

```powershell
# Test against local development API
.\scripts\Test-WindowsVersionApi.ps1 -ApiBaseUrl "https://localhost:5001" -SkipCertificateCheck -Verbose

# Test against production API
.\scripts\Test-WindowsVersionApi.ps1 -ApiBaseUrl "https://app-secureboot-api-prod.azurewebsites.net"
```

**Expected Output:**
```
========================================
Windows Version API Test Suite
========================================
API Base URL: https://localhost:5001

?? Test: Sync Windows Versions
   URL: https://localhost:5001/api/WindowsVersion/sync
   Method: POST
   ? Request successful
   ? Response validation passed

?? Test: Get All Windows Versions
   URL: https://localhost:5001/api/WindowsVersion/versions
   ? Request successful
   Found 12 Windows versions

...

========================================
Test Summary
========================================
? Passed:  8
? Failed:  0
??  Skipped: 0

Success Rate: 100.0%

? All tests passed successfully!
```

---

## Integration Examples

### Check Device Build on Report Submission

```csharp
[HttpPost]
public async Task<IActionResult> SubmitReport([FromBody] SecureBootStatusReport report)
{
    // Existing validation...
    
    // Check Windows build security
    if (!string.IsNullOrEmpty(report.Device.OSBuildNumber))
    {
        var buildStatus = await _windowsVersionService.CheckBuildSecurityAsync(
            report.Device.OSBuildNumber, 
            cancellationToken);
        
        if (!buildStatus.IsSecure)
        {
            _logger.LogWarning(
                "Device {MachineName} has insecure build {BuildNumber}. Latest: {LatestBuild}",
                report.Device.MachineName,
                buildStatus.BuildNumber,
                buildStatus.LatestSecureBuild);
            
            // Optionally: Flag device for patching
            // Optionally: Send alert/notification
        }
    }
    
    // Continue with report processing...
}
```

### Dashboard Widget

```razor
@inject IWindowsVersionService WindowsVersionService

<div class="card">
    <div class="card-header">
        <h5><i class="fas fa-shield-alt"></i> Windows Build Security</h5>
    </div>
    <div class="card-body">
        @{
            var stats = await WindowsVersionService.GetBuildStatisticsAsync();
        }
        
        <div class="row">
            <div class="col-md-6">
                <h3 class="text-success">@stats.SecureBuildPercentage.ToString("F1")%</h3>
                <p class="text-muted">Devices with secure builds</p>
            </div>
            <div class="col-md-6">
                <h3 class="text-warning">@stats.DevicesWithOutdatedBuilds</h3>
                <p class="text-muted">Devices needing updates</p>
            </div>
        </div>
        
        <a asp-page="/Windows/OutdatedDevices" class="btn btn-warning">
            <i class="fas fa-exclamation-triangle"></i> View Outdated Devices
        </a>
    </div>
</div>
```

### Automated Sync Task

```csharp
public class WindowsVersionSyncHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WindowsVersionSyncHostedService> _logger;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var versionService = scope.ServiceProvider.GetRequiredService<IWindowsVersionService>();
                
                _logger.LogInformation("Starting Windows version sync");
                var result = await versionService.SyncWindowsVersionsAsync(stoppingToken);
                
                if (result.Success)
                {
                    _logger.LogInformation(
                        "Sync completed. Versions: {Versions}, Builds: {Builds}",
                        result.VersionsSynced,
                        result.BuildsSynced);
                }
                else
                {
                    _logger.LogError("Sync failed: {Error}", result.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Windows version sync");
            }
            
            // Wait 7 days before next sync
            await Task.Delay(TimeSpan.FromDays(7), stoppingToken);
        }
    }
}
```

---

## Security Considerations

### Authentication

Currently the API endpoints are **unauthenticated**. For production deployments, consider:

1. **API Keys**: Add `[Authorize]` attribute and require API key in headers
2. **Azure AD**: Use Azure AD authentication for admin-only endpoints
3. **Rate Limiting**: Implement rate limiting to prevent abuse

**Example with Authorization:**
```csharp
[Authorize(Roles = "Administrator")]
[HttpPost("sync")]
public async Task<ActionResult<WindowsVersionSyncResult>> SyncWindowsVersions(...)
{
    // Only administrators can trigger sync
}

[AllowAnonymous] // Available to all authenticated users
[HttpGet("check-build/{buildNumber}")]
public async Task<ActionResult<WindowsBuildSecurityStatus>> CheckBuildSecurity(...)
{
    // Read-only, safe to expose
}
```

### Data Privacy

Windows version data is **not PII** (Personally Identifiable Information), but device associations should be handled carefully:
- Build numbers: ? Safe to expose
- Version names: ? Safe to expose
- Device names with build info: ?? Consider access controls

---

## Performance

### Caching

Consider caching frequently accessed data:

```csharp
[ResponseCache(Duration = 3600)] // Cache for 1 hour
[HttpGet("versions")]
public async Task<ActionResult<IReadOnlyList<WindowsVersionDto>>> GetAllVersions(...)
{
    // Version data changes infrequently
}

[ResponseCache(Duration = 300)] // Cache for 5 minutes
[HttpGet("statistics")]
public async Task<ActionResult<WindowsBuildStatistics>> GetBuildStatistics(...)
{
    // Statistics can be slightly stale
}

[ResponseCache(NoStore = true)] // Never cache
[HttpGet("devices/outdated")]
public async Task<ActionResult<IReadOnlyList<DeviceWithBuildStatus>>> GetDevicesWithOutdatedBuilds(...)
{
    // Device list must be fresh
}
```

### Database Indexing

Ensure indexes exist on:
- `WindowsBuilds.BuildNumber` ? (already indexed)
- `WindowsBuilds.IsLatest` ? (already indexed)
- `Devices.OSBuildNumber` ?? (should add index if not present)

---

## Monitoring

Track these metrics:
- Sync success/failure rate
- API endpoint response times
- Number of insecure builds detected
- Percentage of devices with outdated builds

**Application Insights Query:**
```kql
requests
| where name contains "WindowsVersion"
| summarize 
    count(),
    avg(duration),
    percentiles(duration, 50, 95, 99)
  by name
| order by count_ desc
```

---

## Next Steps

1. ? **Database Setup** - Completed
2. ? **Service Layer** - Completed
3. ? **API Endpoints** - Completed
4. ?? **Dashboard Pages** - Create Razor Pages for visualization
5. ?? **Client Integration** - Add build detection to SecureBootWatcher.Client
6. ?? **Alerting** - Notify admins of insecure builds

---

## References

- WindowsVersionsCore: https://github.com/robgrame/WindowsVersionsCore
- Microsoft Windows Release Health: https://learn.microsoft.com/en-us/windows/release-health/
- Windows Update History: https://support.microsoft.com/en-us/topic/windows-10-update-history

---

**Status**: ? API Integration Complete  
**Version**: 1.11.0  
**Last Updated**: 2025-01-22
