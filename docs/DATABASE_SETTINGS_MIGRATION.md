# Database-Driven Application Settings - Feature Documentation

**Version**: 1.12.0  
**Date**: 2025-01-14  
**Status**: ? In Development

---

## ?? Overview

Migrated application configuration from static `appsettings.json` files to a database-driven system, enabling:

- ? **Dynamic Configuration**: Update settings without restarting the application
- ?? **Centralized Management**: Single source of truth for all environments
- ? **Category Organization**: Logical grouping of related settings
- ? **Type Safety**: Strongly-typed value serialization/deserialization
- ? **Audit Trail**: Track who changed what and when
- ? **Security**: Mark sensitive settings for special handling
- ? **Performance**: In-memory caching with auto-refresh

---

## ?? Architecture

### Database Schema

```sql
CREATE TABLE ApplicationSettings (
    Id INT PRIMARY KEY IDENTITY,
    [Key] NVARCHAR(200) NOT NULL UNIQUE,
    Value NVARCHAR(MAX) NOT NULL,
    Category NVARCHAR(100) NOT NULL,
    ValueType NVARCHAR(50) NOT NULL,
    Description NVARCHAR(500) NULL,
    IsSensitive BIT NOT NULL DEFAULT 0,
    RequiresRestart BIT NOT NULL DEFAULT 0,
    CreatedAtUtc DATETIMEOFFSET NOT NULL,
    UpdatedAtUtc DATETIMEOFFSET NOT NULL,
    UpdatedBy NVARCHAR(256) NULL
);

CREATE INDEX IX_ApplicationSettings_Key ON ApplicationSettings([Key]);
CREATE INDEX IX_ApplicationSettings_Category ON ApplicationSettings(Category);
```

### Service Architecture

```
? ApplicationSettingsService (IApplicationSettingsService)
    ?
    ??? IMemoryCache (5-minute expiration)
    ?
    ??? SecureBootDbContext
        ?
        ??? SQL Server (ApplicationSettings table)
```

---

## ?? Migrated Settings

### Settings Moved to Database

| Category | Setting Key | Type | Restart Required | Sensitive |
|----------|------------|------|------------------|-----------|
| **QueueProcessor** | `QueueProcessor:Enabled` | bool | Yes | No |
| **QueueProcessor** | `QueueProcessor:MaxMessages` | int | No | No |
| **QueueProcessor** | `QueueProcessor:ProcessingInterval` | timespan | No | No |
| **QueueProcessor** | `QueueProcessor:EmptyQueuePollInterval` | timespan | No | No |
| **ClientUpdate** | `ClientUpdate:LatestVersion` | string | No | No |
| **ClientUpdate** | `ClientUpdate:MinimumVersion` | string | No | No |
| **ClientUpdate** | `ClientUpdate:IsUpdateRequired` | bool | No | No |
| **ClientUpdate** | `ClientUpdate:DownloadUrl` | string | No | No |
| **SecureBootReadiness** | `SecureBootReadiness:CertificateExpirationWarningDays` | int | No | No |
| **SecureBootReadiness** | `SecureBootReadiness:CertificateExpirationCriticalDays` | int | No | No |
| **SecureBootReadiness** | `SecureBootReadiness:RequireWindowsUEFICA2023` | bool | No | No |
| **SecureBootReadiness** | `SecureBootReadiness:RequireOemCertificatesValid` | bool | No | No |

**Total Migrated**: 12 settings across 3 categories

---

## ?? API Endpoints

### 1. Get All Settings
```http
GET /api/Settings
```

**Response**:
```json
[
  {
    "id": 1,
    "key": "QueueProcessor:Enabled",
    "value": "true",
    "category": "QueueProcessor",
    "valueType": "bool",
    "description": "Enable or disable the queue processor background service",
    "isSensitive": false,
    "requiresRestart": true,
    "createdAtUtc": "2025-01-14T12:00:00Z",
    "updatedAtUtc": "2025-01-14T12:00:00Z",
    "updatedBy": null
  }
]
```

### 2. Get Settings by Category
```http
GET /api/Settings/category/{category}
```

**Example**:
```http
GET /api/Settings/category/QueueProcessor
```

### 3. Get Setting by Key
```http
GET /api/Settings/key/{key}
```

**Example**:
```http
GET /api/Settings/key/ClientUpdate:LatestVersion
```

### 4. Update Setting
```http
PUT /api/Settings/key/{key}
Content-Type: application/json

{
  "value": "1.6.0.0",
  "updatedBy": "admin@contoso.com"
}
```

**Response**:
```json
{
  "id": 5,
  "key": "ClientUpdate:LatestVersion",
  "value": "\"1.6.0.0\"",
  "category": "ClientUpdate",
  "valueType": "string",
  "updatedAtUtc": "2025-01-14T14:30:00Z",
  "updatedBy": "admin@contoso.com"
}
```

### 5. Refresh Cache
```http
POST /api/Settings/cache/refresh
```

### 6. Get Restart-Required Settings
```http
GET /api/Settings/restart-required
```

---

## ? Usage Examples

### C# Service Usage

**Injecting the Service**:
```csharp
public class MyService
{
    private readonly IApplicationSettingsService _settings;
    
    public MyService(IApplicationSettingsService settings)
    {
        _settings = settings;
    }
    
    public async Task DoWorkAsync()
    {
        // Get setting with default
        var maxMessages = await _settings.GetSettingAsync(
            "QueueProcessor:MaxMessages", 
            defaultValue: 10);
        
        // Get setting (nullable)
        var latestVersion = await _settings.GetSettingAsync<string>(
            "ClientUpdate:LatestVersion");
        
        // Get all settings in category
        var queueSettings = await _settings.GetCategorySettingsAsync(
            "QueueProcessor");
    }
}
```

**Updating Settings**:
```csharp
await _settings.SetSettingAsync(
    "ClientUpdate:LatestVersion",
    "1.6.0.0",
    updatedBy: "admin@contoso.com");
```

### PowerShell Examples

**Get All Settings**:
```powershell
$settings = Invoke-RestMethod -Uri "https://api.local/api/Settings"
$settings | Format-Table Key, Value, Category
```

**Update Client Version**:
```powershell
$body = @{
    value = "1.6.0.0"
    updatedBy = "admin@contoso.com"
} | ConvertTo-Json

Invoke-RestMethod `
    -Uri "https://api.local/api/Settings/key/ClientUpdate:LatestVersion" `
    -Method Put `
    -Body $body `
    -ContentType "application/json"
```

**Get Queue Processor Settings**:
```powershell
$queueSettings = Invoke-RestMethod `
    -Uri "https://api.local/api/Settings/category/QueueProcessor"
    
$queueSettings | Format-List
```

---

## ?? Caching Strategy

### Cache Configuration

- **Cache Key Format**: `AppSetting:{key}`
- **Expiration**: 5 minutes (sliding)
- **Invalidation**: On update (immediate)

### Cache Flow

```
1. GetSettingAsync()
    ?
2. Check IMemoryCache
    ?
3. ? FOUND ? Return cached value
    ?         
4. ? NOT FOUND
    ?
5. Query Database
    ?
6. Cache result (5 min)
    ?
7. Return value
```

**Update Flow**:
```
1. SetSettingAsync()
    ?
2. Update Database
    ?
3. Remove from Cache
    ?
4. Next GetSettingAsync() will re-cache
```

---

## ?? Migration Guide

### Step 1: Apply Database Migration

```powershell
cd SecureBootDashboard.Api
dotnet ef database update
```

**Verification**:
```sql
SELECT COUNT(*) FROM ApplicationSettings;
-- Expected: 12 rows
```

### Step 2: Verify Seed Data

```sql
SELECT Category, COUNT(*) AS SettingCount
FROM ApplicationSettings
GROUP BY Category
ORDER BY Category;
```

**Expected Output**:
```
Category                  SettingCount
------------------------  ------------
ClientUpdate              4
QueueProcessor            4
SecureBootReadiness       4
```

### Step 3: Test API Endpoints

```powershell
# Test GET all settings
Invoke-RestMethod -Uri "https://localhost:5001/api/Settings"

# Test GET by category
Invoke-RestMethod -Uri "https://localhost:5001/api/Settings/category/ClientUpdate"

# Test UPDATE
$body = @{ value = "11"; updatedBy = "test" } | ConvertTo-Json
Invoke-RestMethod `
    -Uri "https://localhost:5001/api/Settings/key/QueueProcessor:MaxMessages" `
    -Method Put `
    -Body $body `
    -ContentType "application/json"
```

### Step 4: Remove from appsettings.json (Optional)

**After migration is stable**, you can optionally remove the migrated settings from `appsettings.json` to avoid confusion. However, keep them as fallback during transition.

---

## ?? Benefits

### Before (Static Configuration)

**Limitations**:
- ? Requires application restart for changes
- ? Different configs per environment (dev, staging, prod)
- ? No audit trail
- ? Manual config file edits
- ? Deployment required to update

### After (Database Configuration)

**Advantages**:
- ? **Dynamic Updates**: Most settings apply immediately (no restart)
- ? **Single Source**: One database table for all environments
- ? **Audit Trail**: `UpdatedBy` and `UpdatedAtUtc` tracking
- ? **UI Management**: Can build admin UI for configuration
- ? **API Access**: RESTful API for automation
- ? **Versioning**: Can track setting history (future enhancement)
- ? **Security**: Mark sensitive settings
- ? **Performance**: 5-minute cache reduces DB queries

---

## ?? Security Considerations

### Sensitive Settings Handling

**Mark as Sensitive**:
```csharp
IsSensitive = true
```

**Behavior**:
- ? Masked in logs: `Setting updated to '***'`
- ? API returns masked values (future enhancement)
- ? Not cached (future enhancement)

**Example**:
```sql
UPDATE ApplicationSettings
SET IsSensitive = 1
WHERE [Key] LIKE '%Password%' 
   OR [Key] LIKE '%Secret%'
   OR [Key] LIKE '%ApiKey%';
```

### Access Control

**Recommended**:
1. Add `[Authorize]` attribute to SettingsController
2. Implement role-based access (Admin only)
3. Audit all setting changes
4. Encrypt sensitive values in database

---

## ?? Performance

### Cache Hit Rate

**Expected Performance**:
- **First Request**: ~5ms (database query)
- **Cached Requests**: <1ms (memory cache)
- **Cache Miss**: ~5ms (re-query database)

### Database Impact

**Queries Per Hour** (5-minute cache):
- 12 settings × (60 min / 5 min cache) = **144 queries/hour**
- With cache: ~99% reduction in DB queries

---

## ?? Future Enhancements

### Planned Features (v2.0)

1. **Setting History**
   - Track all changes to settings
   - Rollback capability
   - Diff viewer

2. **Admin UI**
   - Web-based settings editor
   - Validation rules
   - Bulk import/export

3. **Environment-Specific Overrides**
   - Per-environment values
   - Inheritance chain

4. **Setting Groups**
   - Hierarchical organization
   - Nested categories

5. **Notifications**
   - Webhook on setting change
   - Email alerts for critical settings

6. **Encryption**
   - Transparent encryption for sensitive values
   - Azure Key Vault integration

---

## ?? Troubleshooting

### Issue: Setting Not Updating

**Symptoms**: Update API succeeds but old value still used

**Solutions**:
1. Check if `RequiresRestart = true`
2. Manually refresh cache: `POST /api/Settings/cache/refresh`
3. Restart application if required

### Issue: Migration Fails

**Symptoms**: `ef database update` fails

**Solutions**:
1. Check database connectivity
2. Verify SQL Server permissions
3. Check for existing `ApplicationSettings` table

### Issue: Null Values Returned

**Symptoms**: `GetSettingAsync()` returns null

**Solutions**:
1. Verify setting exists in database:
   ```sql
   SELECT * FROM ApplicationSettings WHERE [Key] = 'YourKey';
   ```
2. Check cache: Clear and retry
3. Verify ValueType is correct

---

## ?? Migration Checklist

- [ ] Apply database migration
- [ ] Verify seed data (12 rows)
- [ ] Test API endpoints
- [ ] Update QueueProcessorService to use settings service
- [ ] Update ClientUpdateController to use settings service
- [ ] Update SecureBootReadinessService to use settings service
- [ ] Add UI for settings management (optional)
- [ ] Add authorization to SettingsController
- [ ] Document for operations team
- [ ] Remove settings from appsettings.json (optional)

---

## ?? Related Files

**Backend**:
- `Data/ApplicationSettingEntity.cs` - Entity model
- `Data/SecureBootDbContext.cs` - DbContext configuration
- `Services/IApplicationSettingsService.cs` - Service interface
- `Services/ApplicationSettingsService.cs` - Service implementation
- `Controllers/SettingsController.cs` - REST API

**Migration**:
- `Data/Migrations/*_AddApplicationSettingsTable.cs`

**Configuration**:
- `Program.cs` - Service registration

---

**Last Updated**: 2025-01-14  
**Author**: Development Team  
**Status**: ? Ready for Review

