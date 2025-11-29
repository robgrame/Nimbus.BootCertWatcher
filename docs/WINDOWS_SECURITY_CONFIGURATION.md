# Windows Security Configuration

## Overview

The Windows Security configuration in `appsettings.json` defines minimum secure build requirements for Windows 10 and Windows 11 versions. This configuration is used by the dashboard to:

1. **Identify outdated devices** that need Windows updates
2. **Calculate security compliance** across the fleet
3. **Provide actionable recommendations** with KB article numbers
4. **Track firmware readiness** based on release dates

---

## Configuration Structure

### appsettings.json Section

```json
{
  "WindowsSecurity": {
    "MinimumSecureBuilds": {
      "Windows10": { ... },
      "Windows11_21H2": { ... },
      "Windows11_22H2": { ... },
      "Windows11_23H2": { ... },
      "Windows11_24H2": { ... }
    },
    "FirmwareSecurityDate": "2024-01-01",
    "FirmwareSecurityReason": "Firmware released after this date supports UEFI CA 2023"
  }
}
```

### Build Information Properties

Each build entry contains:

| Property | Type | Description | Example |
|----------|------|-------------|---------|
| `BuildNumber` | string | Full build number | `"19045.5131"` |
| `Name` | string | Friendly name | `"Windows 10 22H2 - November 2024"` |
| `KBArticle` | string | KB article number | `"KB5046613"` |
| `ReleaseDate` | DateTime | Release date | `"2024-11-12"` |
| `Reason` | string | Why this is minimum | `"First build with UEFI CA 2023 support"` |

---

## Default Configuration

### Windows 10 (22H2)

```json
"Windows10": {
  "BuildNumber": "19045.5131",
  "Name": "Windows 10 22H2 - November 2024",
  "KBArticle": "KB5046613",
  "ReleaseDate": "2024-11-12",
  "Reason": "First build with UEFI CA 2023 support"
}
```

**Rationale**: Windows 10 22H2 build 19045.5131 is the first build that includes support for the UEFI CA 2023 certificate update.

### Windows 11 21H2

```json
"Windows11_21H2": {
  "BuildNumber": "22000.3079",
  "Name": "Windows 11 21H2 - August 2024",
  "KBArticle": "KB5041592",
  "ReleaseDate": "2024-08-13",
  "Reason": "UEFI CA 2023 support for 21H2"
}
```

**Rationale**: Windows 11 21H2 is out of support for most editions, but this build provides UEFI CA 2023 support.

### Windows 11 22H2

```json
"Windows11_22H2": {
  "BuildNumber": "22621.4317",
  "Name": "Windows 11 22H2 - November 2024",
  "KBArticle": "KB5046617",
  "ReleaseDate": "2024-11-12",
  "Reason": "Latest secure build for 22H2"
}
```

**Rationale**: Latest cumulative update for Windows 11 22H2 with all security patches.

### Windows 11 23H2

```json
"Windows11_23H2": {
  "BuildNumber": "22631.4317",
  "Name": "Windows 11 23H2 - November 2024",
  "KBArticle": "KB5046617",
  "ReleaseDate": "2024-11-12",
  "Reason": "Latest secure build for 23H2"
}
```

**Rationale**: Latest cumulative update for Windows 11 23H2.

### Windows 11 24H2

```json
"Windows11_24H2": {
  "BuildNumber": "26100.2314",
  "Name": "Windows 11 24H2 - November 2024",
  "KBArticle": "KB5046617",
  "ReleaseDate": "2024-11-12",
  "Reason": "Latest secure build for 24H2"
}
```

**Rationale**: Latest cumulative update for Windows 11 24H2, the newest Windows 11 version.

### Firmware Security Date

```json
"FirmwareSecurityDate": "2024-01-01",
"FirmwareSecurityReason": "Firmware released after this date supports UEFI CA 2023"
```

**Rationale**: Firmware released after January 1, 2024 is expected to include UEFI CA 2023 support.

---

## How It Works

### Build Version Detection

The service automatically detects the Windows version from the build number:

| Build Number | Windows Version | Config Key |
|--------------|----------------|------------|
| 19045.xxxx | Windows 10 22H2 | `Windows10` |
| 22000.xxxx | Windows 11 21H2 | `Windows11_21H2` |
| 22621.xxxx | Windows 11 22H2 | `Windows11_22H2` |
| 22631.xxxx | Windows 11 23H2 | `Windows11_23H2` |
| 26100.xxxx | Windows 11 24H2 | `Windows11_24H2` |

### Security Check Logic

```csharp
// 1. Detect Windows version from build
var windowsVersion = DetermineWindowsVersionFromBuild(majorBuild);

// 2. Check against configured minimum
var isSecure = _securityOptions.IsBuildSecure(windowsVersion, buildNumber);

// 3. Return status with recommendations
return new WindowsBuildSecurityStatus(
    BuildNumber: buildNumber,
    IsSecure: isSecure,
    SecurityNotes: isSecure 
        ? $"Build meets or exceeds minimum secure build"
        : $"Update to {buildInfo.KBArticle} or later",
    LatestSecureBuild: isSecure ? null : minimumBuild
);
```

### Build Number Comparison

Build numbers are compared component by component:

```
19045.5131 vs 19045.5000
  ?       ?
 Same   Greater ? 19045.5131 is newer
```

---

## Updating Configuration

### When to Update

Update the configuration when:

1. **New security updates are released** (Patch Tuesday - 2nd Tuesday of each month)
2. **New Windows versions are released** (e.g., Windows 11 25H2)
3. **UEFI CA 2023 requirements change**
4. **You want to enforce higher security standards**

### How to Update

#### 1. Via appsettings.json

Edit the configuration file directly:

```json
"Windows11_22H2": {
  "BuildNumber": "22621.XXXX",  // Update build number
  "Name": "Windows 11 22H2 - December 2024",  // Update name
  "KBArticle": "KBXXXXXXX",  // Update KB article
  "ReleaseDate": "2024-12-10",  // Update date
  "Reason": "Latest secure build for 22H2"
}
```

#### 2. Via Environment Variables

Override specific values using environment variables:

```bash
# Windows
set WindowsSecurity__MinimumSecureBuilds__Windows11_22H2__BuildNumber=22621.XXXX

# Linux/Mac
export WindowsSecurity__MinimumSecureBuilds__Windows11_22H2__BuildNumber=22621.XXXX
```

#### 3. Via Azure App Service Configuration

Add application settings in Azure Portal:

```
Name: WindowsSecurity__MinimumSecureBuilds__Windows11_22H2__BuildNumber
Value: 22621.XXXX
```

### Example Update Workflow

```powershell
# 1. Get latest Windows 11 22H2 build from Microsoft
$latestBuild = "22621.4460"  # Example from December 2024

# 2. Update appsettings.json
$configPath = "SecureBootDashboard.Api\appsettings.json"
$config = Get-Content $configPath | ConvertFrom-Json
$config.WindowsSecurity.MinimumSecureBuilds.Windows11_22H2.BuildNumber = $latestBuild
$config.WindowsSecurity.MinimumSecureBuilds.Windows11_22H2.Name = "Windows 11 22H2 - December 2024"
$config.WindowsSecurity.MinimumSecureBuilds.Windows11_22H2.KBArticle = "KB5048667"
$config.WindowsSecurity.MinimumSecureBuilds.Windows11_22H2.ReleaseDate = "2024-12-10"
$config | ConvertTo-Json -Depth 10 | Set-Content $configPath

# 3. Restart API to apply changes
dotnet run --project SecureBootDashboard.Api
```

---

## Adding New Windows Versions

When a new Windows version is released (e.g., Windows 11 25H2):

### 1. Add Configuration Entry

```json
"Windows11_25H2": {
  "BuildNumber": "27000.1000",
  "Name": "Windows 11 25H2 - RTM",
  "KBArticle": "N/A",
  "ReleaseDate": "2025-XX-XX",
  "Reason": "Initial release of 25H2"
}
```

### 2. Update Detection Logic

Edit `WindowsVersionService.cs`:

```csharp
private static string? DetermineWindowsVersionFromBuild(int majorBuild)
{
    return majorBuild switch
    {
        19045 => "Windows10",
        22000 => "Windows11_21H2",
        22621 => "Windows11_22H2",
        22631 => "Windows11_23H2",
        26100 => "Windows11_24H2",
        27000 => "Windows11_25H2",  // Add new version
        _ => null
    };
}
```

### 3. Test Configuration

```powershell
# Test the new configuration
curl https://localhost:5001/api/WindowsVersion/check-build/27000.1000
```

---

## API Integration

### Check Build Security

```http
GET /api/WindowsVersion/check-build/{buildNumber}
```

**Example Request**:
```bash
curl https://localhost:5001/api/WindowsVersion/check-build/19045.5000
```

**Example Response**:
```json
{
  "buildNumber": "19045.5000",
  "isSecure": false,
  "isLatest": false,
  "securityNotes": "Build is older than minimum secure build 19045.5131 (Windows 10 22H2 - November 2024). Update to KB5046613 or later.",
  "releaseDate": "2024-11-12T00:00:00Z",
  "latestSecureBuild": "19045.5131"
}
```

### Get Statistics

```http
GET /api/WindowsVersion/statistics
```

**Example Response**:
```json
{
  "totalDevices": 150,
  "devicesWithSecureBuilds": 120,
  "devicesWithOutdatedBuilds": 25,
  "devicesWithUnknownBuilds": 5,
  "secureBuildPercentage": 80.0,
  "buildDistribution": {
    "19045.5131": 50,
    "22621.4317": 70,
    "19045.5000": 25,
    "unknown": 5
  }
}
```

---

## Dashboard Integration

### Device List

The configuration is used to display:
- ? **Green** - Device has secure build
- ?? **Yellow** - Device has outdated build (with KB article recommendation)
- ? **Red** - Device has unknown/very old build

### Outdated Devices Page

Shows devices that need updates with:
- Current build number
- Required minimum build
- KB article to install
- Days since last update

### Windows Build Widget

Displays:
- Total devices tracked
- Percentage with secure builds
- Count of outdated builds
- Count of unknown builds

---

## Best Practices

### Regular Updates

1. **Monthly**: Update configuration after Patch Tuesday (2nd Tuesday)
2. **Quarterly**: Review firmware security date if needed
3. **Annually**: Review and update all Windows version entries

### Configuration Management

1. **Version Control**: Commit configuration changes with meaningful messages
2. **Testing**: Test configuration in dev environment first
3. **Documentation**: Document why specific builds are chosen
4. **Monitoring**: Monitor dashboard after configuration changes

### Security Considerations

1. **Don't set builds too old**: Balance security with compatibility
2. **Consider support lifecycle**: Don't require builds for unsupported versions
3. **Document exceptions**: If certain devices can't update, document why
4. **Regular reviews**: Review configuration quarterly for updates

---

## Troubleshooting

### Device Shows as Outdated but Has Latest Build

**Cause**: Configuration not updated after latest Patch Tuesday.

**Solution**: Update the `BuildNumber` for the appropriate Windows version.

### All Devices Show as Unknown

**Cause**: Build number format mismatch or missing configuration.

**Solution**: Check build number format and ensure all Windows versions are configured.

### Firmware Date Not Working

**Cause**: Device not reporting firmware release date.

**Solution**: Verify firmware date is captured in registry snapshot.

### Configuration Not Applied

**Cause**: API not restarted after configuration change.

**Solution**: Restart the API service:

```powershell
# Development
dotnet run --project SecureBootDashboard.Api

# Production (Windows Service)
Restart-Service "SecureBootDashboard.Api"

# Production (IIS)
iisreset
```

---

## References

### Microsoft Resources

- [Windows 10 Update History](https://support.microsoft.com/en-us/topic/windows-10-update-history)
- [Windows 11 Update History](https://support.microsoft.com/en-us/topic/windows-11-version-22h2-update-history)
- [Windows Release Information](https://learn.microsoft.com/en-us/windows/release-health/)
- [Windows Lifecycle](https://learn.microsoft.com/en-us/lifecycle/products/windows-11-home-and-pro)

### Related Documentation

- [Windows Version API](WINDOWS_VERSION_API.md) - Complete API reference
- [Release Notes v1.11](RELEASE_NOTES_V1.11.0.md) - Windows version tracking feature
- [Ready to Update Status](RELEASE_NOTES_V1.10.0.md) - Device readiness indicators

---

## Example: Updating for December 2024 Patch Tuesday

```json
{
  "WindowsSecurity": {
    "MinimumSecureBuilds": {
      "Windows10": {
        "BuildNumber": "19045.5247",
        "Name": "Windows 10 22H2 - December 2024",
        "KBArticle": "KB5048667",
        "ReleaseDate": "2024-12-10",
        "Reason": "Latest cumulative update with security fixes"
      },
      "Windows11_22H2": {
        "BuildNumber": "22621.4460",
        "Name": "Windows 11 22H2 - December 2024",
        "KBArticle": "KB5048685",
        "ReleaseDate": "2024-12-10",
        "Reason": "Latest cumulative update with security fixes"
      },
      "Windows11_23H2": {
        "BuildNumber": "22631.4460",
        "Name": "Windows 11 23H2 - December 2024",
        "KBArticle": "KB5048685",
        "ReleaseDate": "2024-12-10",
        "Reason": "Latest cumulative update with security fixes"
      },
      "Windows11_24H2": {
        "BuildNumber": "26100.2454",
        "Name": "Windows 11 24H2 - December 2024",
        "KBArticle": "KB5048685",
        "ReleaseDate": "2024-12-10",
        "Reason": "Latest cumulative update with security fixes"
      }
    }
  }
}
```

---

**Last Updated**: November 24, 2025  
**Version**: v1.11.1  
**Configuration File**: `SecureBootDashboard.Api/appsettings.json`
