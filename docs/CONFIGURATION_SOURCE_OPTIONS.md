# Configuration Source Options

## Overview

The **Configuration Source** feature provides flexibility in how the SecureBootDashboard API loads its configuration. You can choose to load configuration from:
- **appsettings.json** (default, recommended for most deployments)
- **Database** (for advanced scenarios requiring runtime configuration changes)

This feature addresses deployments where database configuration may not be available or desired.

---

## Configuration

### appsettings.json

Add the `ConfigurationSource` section to your `appsettings.json`:

```json
{
  "ConfigurationSource": {
    "Provider": "AppSettings"
  }
}
```

### Available Providers

| Provider | Description | Use Case |
|----------|-------------|----------|
| `AppSettings` | Loads all configuration from appsettings.json files (default) | Standard deployments, containerized environments, immutable infrastructure |
| `Database` | Loads dynamic configuration from database tables | Advanced scenarios requiring runtime configuration updates without redeployment |

**Default:** If not specified, the system defaults to `AppSettings`.

---

## Affected Configuration Areas

When `Provider` is set to `AppSettings`, the following configuration is read exclusively from appsettings.json:

### 1. Queue Processor Configuration
- Queue service URI and name
- Authentication method and credentials
- Processing intervals and batch sizes
- Message visibility timeout

**Location:** `appsettings.json` → `QueueProcessor` section

### 2. Mutual TLS Configuration
- Client certificate validation rules
- Allowed thumbprints and issuers
- Certificate chain validation options
- Revocation checking settings

**Location:** `appsettings.json` → `MutualTls` section

### 3. Application Settings
- General application configuration
- Feature flags
- Service endpoints

**Location:** `appsettings.json` → Various sections

---

## Usage Examples

### Standard Deployment (Default)

Most deployments should use the default `AppSettings` provider:

```json
{
  "ConfigurationSource": {
    "Provider": "AppSettings"
  },
  "QueueProcessor": {
    "Enabled": true,
    "QueueServiceUri": "https://myaccount.queue.core.windows.net",
    "QueueName": "secureboot-reports",
    "AuthenticationMethod": "ManagedIdentity"
  },
  "MutualTls": {
    "Enabled": false
  }
}
```

### Database-Driven Configuration

For advanced scenarios where configuration must change at runtime without redeployment:

```json
{
  "ConfigurationSource": {
    "Provider": "Database"
  }
}
```

**Requirements for Database provider:**
- Database must be accessible and configured
- Required tables must exist: `ApiConfiguration`, `MutualTlsConfig`, `ApplicationSettings`
- Configuration must be populated in these tables

---

## Behavior Details

### AppSettings Provider
1. **Startup**: Configuration is loaded from appsettings.json files
2. **Runtime**: Configuration is cached and does not change without application restart
3. **Fallback**: Not applicable (primary configuration source)
4. **Errors**: Application will fail to start if required configuration is missing from appsettings.json

### Database Provider
1. **Startup**: Configuration is loaded from database tables
2. **Runtime**: Configuration is cached with 5-minute expiration (configurable)
3. **Fallback**: If database is unavailable, falls back to appsettings.json with warnings logged
4. **Errors**: Database connection failures are logged but do not prevent startup if fallback succeeds

---

## Migration Guide

### From Database to AppSettings

If you're currently using database configuration and want to switch to appsettings.json:

1. **Extract current configuration** from database tables:
   - `ApiConfiguration` → `QueueProcessor` section
   - `MutualTlsConfig` → `MutualTls` section
   - `ApplicationSettings` → Various sections

2. **Update appsettings.json** with extracted values

3. **Change ConfigurationSource**:
   ```json
   {
     "ConfigurationSource": {
       "Provider": "AppSettings"
     }
   }
   ```

4. **Restart the application** and verify logs show:
   ```
   Configuration Source: AppSettings
   Use Database Configuration: False
   Use AppSettings Configuration: True
   ```

### From AppSettings to Database

If you need runtime configuration changes without redeployment:

1. **Ensure database tables exist** (run migrations if needed):
   - `ApiConfiguration`
   - `MutualTlsConfig`
   - `ApplicationSettings`
   - `TrustedCertificateAuthorities`

2. **Populate configuration** in database tables

3. **Change ConfigurationSource**:
   ```json
   {
     "ConfigurationSource": {
       "Provider": "Database"
     }
   }
   ```

4. **Restart the application** and verify logs show:
   ```
   Configuration Source: Database
   Use Database Configuration: True
   Use AppSettings Configuration: False
   ```

---

## Logging

The application logs the configuration source at startup:

```
Configuration Source: AppSettings
  Use Database Configuration: False
  Use AppSettings Configuration: True
```

When using Database provider, you'll see additional logs:
```
Loading Queue Processor configuration from DATABASE
✓ Queue Processor configured from DATABASE: Enabled=True, Queue=secureboot-reports, ...
```

If database configuration fails:
```
✗ Failed to load Queue Processor configuration from database. Falling back to appsettings.json configuration.
```

---

## Troubleshooting

### Application fails to start with "Configuration not found"

**Cause:** ConfigurationSource is set to `AppSettings` but required configuration is missing from appsettings.json

**Solution:** Add the missing configuration sections to appsettings.json (QueueProcessor, MutualTls, etc.)

### Database configuration not loading

**Cause:** ConfigurationSource is set to `Database` but database is unavailable or tables don't exist

**Solution:** 
1. Check database connection string
2. Verify database tables exist
3. Check logs for specific database errors
4. Consider falling back to `AppSettings` provider temporarily

### Configuration changes not taking effect

**Cause (AppSettings):** Changes to appsettings.json require application restart

**Solution:** Restart the application

**Cause (Database):** Configuration is cached for 5 minutes

**Solution:** Wait for cache expiration or restart application for immediate effect

---

## Best Practices

1. **Use AppSettings for production**: More reliable, easier to version control, works in containerized environments
2. **Use Database for edge cases**: Only when runtime configuration changes are absolutely required
3. **Version control appsettings.json**: Keep configuration in source control for auditability
4. **Document configuration**: Comment important settings or use a separate documentation file
5. **Test configuration changes**: Always test in non-production before deploying configuration changes
6. **Monitor logs**: Watch startup logs to confirm correct configuration source is used

---

## API Impact

The configuration source setting **does not affect** API endpoints. The API continues to function normally regardless of the configuration source chosen. The setting only affects where configuration is loaded from during application startup.

---

## Security Considerations

### AppSettings Provider
- Configuration stored in files on disk
- Protect appsettings.json with appropriate file permissions
- Use Azure Key Vault or environment variables for secrets
- Never commit secrets to source control

### Database Provider
- Configuration stored in database
- Protect database with appropriate access controls
- Encrypt connection strings
- Audit configuration changes in database
- Consider encrypting sensitive configuration fields

---

## Version History

- **v1.13**: Configuration Source Options feature introduced
  - Added `ConfigurationSourceOptions` class
  - Updated appsettings.json with `ConfigurationSource` section
  - Modified services to respect configuration source setting
  - Added comprehensive tests for configuration source behavior
