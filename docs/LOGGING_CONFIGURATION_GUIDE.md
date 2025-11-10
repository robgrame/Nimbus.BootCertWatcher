# Logging Configuration Guide

## Overview

The **SecureBootWatcher Client** logging is now fully configurable via `appsettings.json`. You can control:

- ? Log file path and naming
- ? Rolling interval (daily, hourly, size-based)
- ? File retention (how many old logs to keep)
- ? File size limits
- ? Log format (CMTrace or standard text)
- ? Console output (enable/disable)
- ? Log levels (per namespace)

## Configuration Schema

### Complete Logging Section

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "System": "Warning",
      "SecureBootWatcher": "Debug"
    },
    "File": {
      "Path": "logs/client-.log",
      "RollingInterval": "Day",
      "RetainedFileCountLimit": 30,
      "FileSizeLimitBytes": 10485760,
      "RollOnFileSizeLimit": false,
      "Format": "CMTrace"
    },
    "Console": {
      "Enabled": true,
      "Format": "Simple"
    }
  }
}
```

## Configuration Options

### LogLevel Section

Controls which log entries are written based on their severity level.

| Setting | Description | Values | Default |
|---------|-------------|--------|---------|
| `Default` | Default log level for all namespaces | `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`, `None` | `Information` |
| `Microsoft` | Log level for Microsoft libraries | Same as above | `Warning` |
| `System` | Log level for System libraries | Same as above | `Warning` |
| `SecureBootWatcher` | Log level for client application | Same as above | Inherits `Default` |

**Example - Debug Mode**:
```json
"LogLevel": {
  "Default": "Debug",
  "Microsoft": "Warning",
  "System": "Warning"
}
```

**Example - Production (Minimal Logs)**:
```json
"LogLevel": {
  "Default": "Warning",
  "Microsoft": "Error",
  "System": "Error"
}
```

### File Section

Controls file logging behavior.

#### Path

**Setting**: `Logging:File:Path`  
**Type**: `string`  
**Default**: `logs/client-.log`

**Description**: Path to log file. Can be absolute or relative to executable directory.

**Path Patterns**:
- `logs/client-.log` - Relative path (creates `logs` folder in executable directory)
- `C:\Logs\SecureBootWatcher\client-.log` - Absolute path
- The `-` in the filename is replaced with date/sequence based on `RollingInterval`

**Examples**:
```json
// Default - logs in executable directory
"Path": "logs/client-.log"

// Centralized logging folder
"Path": "C:\\ProgramData\\SecureBootWatcher\\logs\\client-.log"

// Network share (not recommended)
"Path": "\\\\server\\share\\logs\\client-.log"

// Include machine name in path
"Path": "logs\\{MachineName}\\client-.log"  // Note: {MachineName} substitution not currently supported
```

**Rolling Filename Examples**:
- `RollingInterval: Day` ? `client-20250110.log`, `client-20250111.log`
- `RollingInterval: Hour` ? `client-2025011016.log`, `client-2025011017.log`
- `RollingInterval: Minute` ? `client-202501101630.log`

#### RollingInterval

**Setting**: `Logging:File:RollingInterval`  
**Type**: `string` (enum)  
**Default**: `Day`

**Description**: Determines how often a new log file is created.

**Options**:

| Value | Description | Filename Pattern | Use Case |
|-------|-------------|------------------|----------|
| `Infinite` | Never create new file | `client-.log` | Single log file (not recommended) |
| `Year` | New file each year | `client-2025.log` | Long-term archival |
| `Month` | New file each month | `client-202501.log` | Monthly reporting |
| `Day` | New file each day | `client-20250110.log` | **Default - Recommended** |
| `Hour` | New file each hour | `client-2025011016.log` | High-volume logging |
| `Minute` | New file each minute | `client-202501101630.log` | Debug/troubleshooting |

**Examples**:
```json
// Daily logs (recommended for production)
"RollingInterval": "Day"

// Hourly logs for troubleshooting
"RollingInterval": "Hour"

// Single log file (not recommended - grows indefinitely)
"RollingInterval": "Infinite"
```

#### RetainedFileCountLimit

**Setting**: `Logging:File:RetainedFileCountLimit`  
**Type**: `int`  
**Default**: `30`

**Description**: Maximum number of log files to keep. Older files are automatically deleted.

**Examples**:
```json
// Keep 30 days of logs (with RollingInterval: Day)
"RetainedFileCountLimit": 30

// Keep 7 days of logs
"RetainedFileCountLimit": 7

// Keep 90 days of logs (compliance requirement)
"RetainedFileCountLimit": 90

// No limit (not recommended - disk space issues)
"RetainedFileCountLimit": null
```

**Calculation**:
- `RollingInterval: Day` + `RetainedFileCountLimit: 30` = 30 days of logs
- `RollingInterval: Hour` + `RetainedFileCountLimit: 168` = 7 days of hourly logs (168 hours)

#### FileSizeLimitBytes

**Setting**: `Logging:File:FileSizeLimitBytes`  
**Type**: `long` (bytes)  
**Default**: `null` (no limit)

**Description**: Maximum size of a single log file in bytes. When combined with `RollOnFileSizeLimit`, creates new files when size is reached.

**Common Values**:

| Size | Bytes | JSON Value | Description |
|------|-------|------------|-------------|
| 1 MB | 1,048,576 | `1048576` | Very small logs |
| 10 MB | 10,485,760 | `10485760` | **Recommended default** |
| 50 MB | 52,428,800 | `52428800` | Large logs |
| 100 MB | 104,857,600 | `104857600` | Very large logs |

**Examples**:
```json
// Limit log files to 10 MB
"FileSizeLimitBytes": 10485760

// No size limit (file size limited only by RollingInterval)
"FileSizeLimitBytes": null

// Limit to 50 MB for high-volume logging
"FileSizeLimitBytes": 52428800
```

#### RollOnFileSizeLimit

**Setting**: `Logging:File:RollOnFileSizeLimit`  
**Type**: `bool`  
**Default**: `false`

**Description**: If `true`, creates a new log file when `FileSizeLimitBytes` is reached within the same rolling interval.

**Behavior**:

| RollOnFileSizeLimit | Behavior | Filename Pattern |
|---------------------|----------|------------------|
| `false` | Stop writing when size limit reached | `client-20250110.log` (stops at 10MB) |
| `true` | Create sequenced files | `client-20250110.log`, `client-20250110_001.log`, `client-20250110_002.log` |

**Examples**:
```json
// Stop writing when file reaches size limit (data loss possible)
"RollOnFileSizeLimit": false

// Create new files when size limit reached (recommended)
"RollOnFileSizeLimit": true
```

**Recommended Configuration**:
```json
"File": {
  "FileSizeLimitBytes": 10485760,
  "RollOnFileSizeLimit": true
}
```

This prevents log files from growing too large while ensuring no log entries are lost.

#### Format

**Setting**: `Logging:File:Format`  
**Type**: `string`  
**Default**: `CMTrace`

**Description**: Log file format.

**Options**:

| Value | Description | Use Case |
|-------|-------------|----------|
| `CMTrace` | Microsoft CMTrace-compatible format | **Default** - View with CMTrace.exe |
| `Standard` | Human-readable text format | View with Notepad, text editors |

**Examples**:
```json
// CMTrace format (recommended for IT admins)
"Format": "CMTrace"

// Standard text format (easier for developers)
"Format": "Standard"
```

**Output Comparison**:

**CMTrace Format**:
```
<![LOG[SecureBootWatcher Client Starting]LOG]!><time="16:33:18.123+01:00" date="01-10-2025" component="SecureBootWatcher.Client" context="" type="1" thread="1" file="">
```

**Standard Format**:
```
2025-01-10 16:33:18.123 +01:00 [INF] SecureBootWatcher Client Starting
```

### Console Section

Controls console output (only visible when running interactively).

#### Enabled

**Setting**: `Logging:Console:Enabled`  
**Type**: `bool`  
**Default**: `true`

**Description**: Enable/disable console logging.

**Examples**:
```json
// Enable console (development/testing)
"Console": {
  "Enabled": true
}

// Disable console (production/scheduled tasks)
"Console": {
  "Enabled": false
}
```

**When to disable**:
- ? Production deployments
- ? Scheduled tasks (no console window)
- ? Windows Services
- ? Reduce overhead (minor performance gain)

**When to enable**:
- ? Development
- ? Manual testing
- ? Troubleshooting
- ? Interactive debugging

#### Format

**Setting**: `Logging:Console:Format`  
**Type**: `string`  
**Default**: `Simple`

**Description**: Console output format.

**Options**:

| Value | Description |
|-------|-------------|
| `Simple` | Human-readable, color-coded |
| `Detailed` | Includes more metadata |

*Note: Currently only `Simple` format is implemented.*

## Common Configuration Scenarios

### Scenario 1: Development Environment

**Goal**: Verbose logging, short retention, console enabled

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft": "Information",
      "System": "Information"
    },
    "File": {
      "Path": "logs/client-.log",
      "RollingInterval": "Day",
      "RetainedFileCountLimit": 7,
      "FileSizeLimitBytes": 52428800,
      "RollOnFileSizeLimit": true,
      "Format": "Standard"
    },
    "Console": {
      "Enabled": true
    }
  }
}
```

**Features**:
- Debug-level logging
- 7 days retention
- 50 MB file size limit
- Standard (readable) format
- Console enabled

### Scenario 2: Production Environment

**Goal**: Minimal logging, CMTrace format, long retention, no console

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "System": "Warning"
    },
    "File": {
      "Path": "C:\\ProgramData\\SecureBootWatcher\\logs\\client-.log",
      "RollingInterval": "Day",
      "RetainedFileCountLimit": 90,
      "FileSizeLimitBytes": 10485760,
      "RollOnFileSizeLimit": true,
      "Format": "CMTrace"
    },
    "Console": {
      "Enabled": false
    }
  }
}
```

**Features**:
- Information-level logging (errors + warnings + info)
- 90 days retention (compliance)
- 10 MB file size limit
- CMTrace format (for IT support)
- Console disabled (scheduled task)
- Centralized log path

### Scenario 3: Troubleshooting / Debug Mode

**Goal**: Maximum detail, hourly logs, CMTrace format

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Trace",
      "Microsoft": "Debug",
      "System": "Debug"
    },
    "File": {
      "Path": "logs/client-.log",
      "RollingInterval": "Hour",
      "RetainedFileCountLimit": 168,
      "FileSizeLimitBytes": 104857600,
      "RollOnFileSizeLimit": true,
      "Format": "CMTrace"
    },
    "Console": {
      "Enabled": true
    }
  }
}
```

**Features**:
- Trace-level logging (everything)
- Hourly log files
- 168 hours (7 days) retention
- 100 MB file size limit
- CMTrace format
- Console enabled

### Scenario 4: Minimal Disk Space

**Goal**: Small logs, short retention, size limits

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft": "Error",
      "System": "Error"
    },
    "File": {
      "Path": "logs/client-.log",
      "RollingInterval": "Day",
      "RetainedFileCountLimit": 7,
      "FileSizeLimitBytes": 1048576,
      "RollOnFileSizeLimit": false,
      "Format": "CMTrace"
    },
    "Console": {
      "Enabled": false
    }
  }
}
```

**Features**:
- Warning-level logging only (minimal)
- 7 days retention
- 1 MB file size limit (stops writing after)
- No console output

**Disk Usage**: Maximum ~7 MB (7 days × 1 MB)

## Environment-Specific Configuration

### Using appsettings.{Environment}.json

Override logging settings per environment:

**appsettings.json** (Base):
```json
{
  "Logging": {
    "File": {
      "Path": "logs/client-.log",
      "Format": "Standard"
    }
  }
}
```

**appsettings.production.json** (Production Override):
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    },
    "File": {
      "Path": "C:\\ProgramData\\SecureBootWatcher\\logs\\client-.log",
      "Format": "CMTrace"
    },
    "Console": {
      "Enabled": false
    }
  }
}
```

**appsettings.local.json** (Local Override - not in source control):
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug"
    }
  }
}
```

### Environment Variables

Override any setting using environment variables:

```powershell
# Set log level via environment variable
$env:SECUREBOOT_Logging__LogLevel__Default = "Debug"

# Set log path via environment variable
$env:SECUREBOOT_Logging__File__Path = "C:\Temp\logs\client-.log"

# Run client
.\SecureBootWatcher.Client.exe
```

**Format**: `SECUREBOOT_{Section}__{Subsection}__{Setting}`

**Examples**:
```powershell
$env:SECUREBOOT_Logging__LogLevel__Default = "Debug"
$env:SECUREBOOT_Logging__File__RollingInterval = "Hour"
$env:SECUREBOOT_Logging__File__RetainedFileCountLimit = "168"
$env:SECUREBOOT_Logging__Console__Enabled = "true"
```

## Monitoring Log File Size

### PowerShell Script to Monitor Logs

```powershell
# Monitor log disk usage
$logPath = "C:\Program Files\SecureBootWatcher\logs"

Get-ChildItem -Path $logPath -Filter "client-*.log" |
    Measure-Object -Property Length -Sum |
    Select-Object `
        Count,
        @{Name="TotalSizeMB"; Expression={[math]::Round($_.Sum / 1MB, 2)}},
        @{Name="AverageSizeMB"; Expression={[math]::Round($_.Sum / $_.Count / 1MB, 2)}}
```

**Output**:
```
Count TotalSizeMB AverageSizeMB
----- ----------- -------------
   30       285.5          9.52
```

### Alert if Logs Exceed Threshold

```powershell
# Alert if total logs exceed 500 MB
$logPath = "C:\Program Files\SecureBootWatcher\logs"
$maxSizeMB = 500

$totalSizeMB = (Get-ChildItem -Path $logPath -Filter "client-*.log" |
    Measure-Object -Property Length -Sum).Sum / 1MB

if ($totalSizeMB -gt $maxSizeMB) {
    Write-Warning "Log files exceed ${maxSizeMB}MB: ${totalSizeMB}MB"
    
    # Take action: reduce retention or increase FileSizeLimitBytes
}
```

## Troubleshooting

### Issue: Logs Not Being Created

**Symptoms**:
- No log files in logs directory
- Application runs but no logs

**Solutions**:

1. **Check log path exists**:
   ```powershell
   Test-Path "C:\Program Files\SecureBootWatcher\logs"
   ```

2. **Check permissions**:
   ```powershell
   icacls "C:\Program Files\SecureBootWatcher\logs"
   ```

3. **Check log level** (might be set to `None`):
   ```json
   "LogLevel": {
     "Default": "Information"  // Not "None"
   }
   ```

4. **Enable console to see errors**:
   ```json
   "Console": {
     "Enabled": true
   }
   ```

### Issue: Log Files Growing Too Large

**Symptoms**:
- Log files exceed expected size
- Disk space warnings

**Solutions**:

1. **Reduce log level**:
   ```json
   "LogLevel": {
     "Default": "Warning"  // Instead of "Debug" or "Trace"
   }
   ```

2. **Add file size limit**:
   ```json
   "File": {
     "FileSizeLimitBytes": 10485760,
     "RollOnFileSizeLimit": true
   }
   ```

3. **Reduce retention**:
   ```json
   "File": {
     "RetainedFileCountLimit": 7  // Instead of 30
   }
   ```

### Issue: Old Logs Not Being Deleted

**Symptoms**:
- More log files than `RetainedFileCountLimit`

**Solutions**:

1. **Verify RetainedFileCountLimit is set**:
   ```json
   "File": {
     "RetainedFileCountLimit": 30  // Not null
   }
   ```

2. **Check file naming pattern matches**:
   - Serilog only deletes files matching the pattern
   - Ensure `Path` setting uses `-` placeholder

3. **Manual cleanup** (if needed):
   ```powershell
   Get-ChildItem "C:\Program Files\SecureBootWatcher\logs" -Filter "client-*.log" |
       Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-30) } |
       Remove-Item -Force
   ```

### Issue: Cannot View Logs with CMTrace

**Symptoms**:
- Logs appear as plain text in CMTrace
- No color coding

**Solutions**:

1. **Verify Format setting**:
   ```json
   "File": {
     "Format": "CMTrace"
   }
   ```

2. **Rebuild application** after changing format

3. **Check log file content**:
   ```powershell
   Get-Content "logs\client-*.log" -First 5
   ```
   
   Should start with: `<![LOG[`

### Issue: Logs Missing After Application Crash

**Symptoms**:
- Last few log entries missing after crash

**Cause**: Serilog buffers log entries for performance

**Solution**: Use `Log.CloseAndFlush()` in finally block (already implemented in `Program.cs`)

## Best Practices

### ? DO

1. **Use CMTrace format in production** - Better for IT support
2. **Set appropriate retention** - Balance disk space vs. compliance
3. **Disable console in production** - Minor performance gain
4. **Use centralized log path** - Easier to collect logs
5. **Set file size limits** - Prevent runaway log growth
6. **Test configuration changes** - Verify logs are still created
7. **Monitor disk usage** - Alert if logs consume too much space

### ? DON'T

1. **Don't use Trace/Debug in production** - Generates huge logs
2. **Don't set retention to null** - Disk space issues
3. **Don't use network paths** - Performance and reliability issues
4. **Don't disable logging entirely** - Lose troubleshooting capability
5. **Don't forget file size limits** - Single file can grow indefinitely
6. **Don't change format without rebuilding** - May break log parsing

## Summary

### Key Configuration Points

| Setting | Development | Production | Troubleshooting |
|---------|-------------|------------|-----------------|
| LogLevel | `Debug` | `Information` | `Trace` |
| RollingInterval | `Day` | `Day` | `Hour` |
| RetainedFileCountLimit | `7` | `30-90` | `168` (7 days hourly) |
| FileSizeLimitBytes | `50 MB` | `10 MB` | `100 MB` |
| Format | `Standard` | `CMTrace` | `CMTrace` |
| Console | `true` | `false` | `true` |

### Quick Reference

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    },
    "File": {
      "Path": "logs/client-.log",
      "RollingInterval": "Day",
      "RetainedFileCountLimit": 30,
      "FileSizeLimitBytes": 10485760,
      "RollOnFileSizeLimit": true,
      "Format": "CMTrace"
    },
    "Console": {
      "Enabled": false
    }
  }
}
```

---

**Documentation Version**: 1.0  
**Last Updated**: 2025-01-10  
**Related Files**:
- `SecureBootWatcher.Client\appsettings.json` - Base configuration
- `SecureBootWatcher.Client\appsettings.production.json` - Production overrides
- `SecureBootWatcher.Client\Program.cs` - Logging initialization
- `docs\CMTRACE_LOGGING_GUIDE.md` - CMTrace format details
