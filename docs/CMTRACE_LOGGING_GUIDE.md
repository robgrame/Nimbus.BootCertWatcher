# CMTrace Logging Format Guide

## Overview

The **SecureBootWatcher Client** now uses **CMTrace-compatible log format** for file logging. This allows IT administrators to use the popular **CMTrace.exe** tool (part of Microsoft System Center Configuration Manager) to view and analyze client logs with advanced features like:

- ? Color-coded log levels
- ? Real-time log monitoring
- ? Advanced filtering
- ? Component-based filtering
- ? Thread tracking
- ? Highlighting and searching

## CMTrace Format Specification

### Log Entry Format

```
<![LOG[Message]LOG]!><time="HH:mm:ss.fff+000" date="MM-dd-yyyy" component="Component" context="" type="1" thread="0" file="">
```

### Field Descriptions

| Field | Description | Example |
|-------|-------------|---------|
| `Message` | Log message text | `SecureBootWatcher Client Starting` |
| `time` | Time with timezone offset | `16:33:18.123+01:00` |
| `date` | Date in MM-dd-yyyy format | `01-10-2025` |
| `component` | Component name | `SecureBootWatcher.Client` |
| `context` | Context (unused) | `""` |
| `type` | Log level (1=Info, 2=Warning, 3=Error) | `1` |
| `thread` | Thread ID | `1234` |
| `file` | Source file (unused) | `""` |

### Log Level Mapping

| Serilog Level | CMTrace Type | Color in CMTrace |
|---------------|--------------|------------------|
| Information   | 1            | White/Gray       |
| Warning       | 2            | Yellow           |
| Error         | 3            | Red              |
| Fatal         | 3            | Red              |

## Example Log Output

### CMTrace Format (File)

```
<![LOG[========================================]LOG]!><time="16:33:18.123+01:00" date="01-10-2025" component="SecureBootWatcher.Client" context="" type="1" thread="1" file="">
<![LOG[SecureBootWatcher Client Starting]LOG]!><time="16:33:18.124+01:00" date="01-10-2025" component="SecureBootWatcher.Client" context="" type="1" thread="1" file="">
<![LOG[========================================]LOG]!><time="16:33:18.125+01:00" date="01-10-2025" component="SecureBootWatcher.Client" context="" type="1" thread="1" file="">
<![LOG[Version: 1.1.1.48182]LOG]!><time="16:33:18.126+01:00" date="01-10-2025" component="SecureBootWatcher.Client" context="" type="1" thread="1" file="">
<![LOG[Machine Name: DESKTOP-ABC123]LOG]!><time="16:33:18.127+01:00" date="01-10-2025" component="SecureBootWatcher.Client" context="" type="1" thread="1" file="">
<![LOG[Active Sinks: WebApi, AzureQueue]LOG]!><time="16:33:18.200+01:00" date="01-10-2025" component="SecureBootWatcher.Client" context="" type="1" thread="1" file="">
<![LOG[Building secure boot status report...]LOG]!><time="16:33:18.300+01:00" date="01-10-2025" component="SecureBootWatcher.Client" context="" type="1" thread="1" file="">
<![LOG[Successfully sent report to API]LOG]!><time="16:33:19.500+01:00" date="01-10-2025" component="SecureBootWatcher.Client" context="" type="1" thread="1" file="">
<![LOG[?? WARNING: Failed to connect to Azure Queue]LOG]!><time="16:33:19.600+01:00" date="01-10-2025" component="SecureBootWatcher.Client" context="" type="2" thread="1" file="">
```

### Console Format (Human-Readable)

Console output remains human-readable for manual testing:

```
[16:33:18 INF] ========================================
[16:33:18 INF] SecureBootWatcher Client Starting
[16:33:18 INF] ========================================
[16:33:18 INF] Version: 1.1.1.48182
[16:33:18 INF] Machine Name: DESKTOP-ABC123
[16:33:18 WRN] ?? WARNING: Failed to connect to Azure Queue
```

## Using CMTrace

### Download CMTrace

**Source**: Microsoft System Center Configuration Manager Toolkit

**Download Options**:
1. **Configuration Manager Console** - Included with SCCM installation
   ```
   C:\Program Files (x86)\Microsoft Endpoint Manager\AdminConsole\bin\CMTrace.exe
   ```

2. **Standalone Download**
   - Download from Microsoft official sources
   - Often bundled with SCCM client tools

3. **Alternative**: OneTrace (newer version)
   ```
   C:\Program Files (x86)\Microsoft Endpoint Manager\AdminConsole\bin\OneTrace.exe
   ```

### Opening Logs with CMTrace

#### Method 1: Direct Open

```powershell
# Open log file with CMTrace
& "C:\Program Files (x86)\Microsoft Endpoint Manager\AdminConsole\bin\CMTrace.exe" "C:\Program Files\SecureBootWatcher\logs\client-20250110.log"
```

#### Method 2: Set as Default Viewer

1. Right-click a `.log` file
2. **Open with** ? **Choose another app**
3. Browse to `CMTrace.exe`
4. Check **Always use this app to open .log files**

#### Method 3: Command Line Association

```powershell
# Associate .log files with CMTrace
$cmtracePath = "C:\Program Files (x86)\Microsoft Endpoint Manager\AdminConsole\bin\CMTrace.exe"
cmd /c assoc .log=CMTraceLog
cmd /c ftype CMTraceLog="$cmtracePath" "%1"
```

### CMTrace Features

#### 1. Real-Time Monitoring

**Enable**: Tools ? Start Monitoring

- Automatically shows new log entries as they're written
- Perfect for watching scheduled tasks or manual runs
- Refreshes automatically (no need to reload file)

#### 2. Filtering

**Filter by log level**:
- View ? Show only Errors (red entries)
- View ? Show only Warnings (yellow entries)
- View ? Show all entries

**Custom filters**:
- Tools ? Find ? Enter search term
- Highlights matching entries
- F3 to find next occurrence

**Example searches**:
```
"ERROR"           - Find all errors
"Certificate"     - Find certificate-related entries
"API"             - Find API operations
"Version:"        - Find version information
```

#### 3. Highlighting

**Configure highlights**:
- Tools ? Highlight ? Add...
- Enter text to highlight
- Choose highlight color
- Case-sensitive option

**Example highlights**:
| Text | Color | Purpose |
|------|-------|---------|
| `ERROR` | Red background | Errors |
| `WARNING` | Yellow background | Warnings |
| `Certificate` | Green text | Certificate operations |
| `Successfully sent` | Blue text | Successful operations |

#### 4. Column Customization

**Available columns**:
- Time
- Date
- Component
- Type (log level)
- Thread
- Message

**Customize**:
- Right-click column headers
- Show/Hide columns
- Resize columns
- Sort by clicking column header

#### 5. Multi-Log View

Open multiple log files simultaneously:
- File ? Open ? Select multiple `.log` files
- OR drag-and-drop multiple files onto CMTrace
- Each log opens in a separate tab

**Use case**: Compare logs from different devices or dates

## Log File Location

### Production Deployment

**Scheduled Task** (runs as SYSTEM):
```
C:\Program Files\SecureBootWatcher\logs\client-YYYYMMDD.log
```

**Example**:
```
C:\Program Files\SecureBootWatcher\logs\client-20250110.log
```

### Manual Run

**Current user context**:
```
{ExecutableDirectory}\logs\client-YYYYMMDD.log
```

### Log Retention

- **Rolling Interval**: Daily (new log file each day)
- **Retention**: 30 days (older logs automatically deleted)
- **Naming Pattern**: `client-YYYYMMDD.log`

**Example log files**:
```
C:\Program Files\SecureBootWatcher\logs\
??? client-20250110.log  (today)
??? client-20250109.log
??? client-20250108.log
??? ...
??? client-20241212.log  (30 days ago - will be deleted tomorrow)
```

## Collecting Logs from Remote Devices

### Method 1: PowerShell Remoting

```powershell
# Collect logs from remote device
$deviceName = "DESKTOP-ABC123"
$logPath = "\\$deviceName\C$\Program Files\SecureBootWatcher\logs\client-*.log"

# Copy to local folder
$localPath = "C:\Temp\SecureBootWatcher-Logs\$deviceName"
New-Item -ItemType Directory -Path $localPath -Force
Copy-Item -Path $logPath -Destination $localPath
```

### Method 2: Intune Log Collection (Proactive Remediation)

**Detection Script**:
```powershell
# Detect if logs need collection
$logPath = "C:\Program Files\SecureBootWatcher\logs"
$latestLog = Get-ChildItem -Path $logPath -Filter "client-*.log" | 
             Sort-Object LastWriteTime -Descending | 
             Select-Object -First 1

if ($latestLog) {
    # Check for errors in last 100 lines
    $errors = Get-Content $latestLog.FullName -Tail 100 | 
              Select-String 'type="3"'  # CMTrace type=3 is Error
    
    if ($errors) {
        Write-Output "Errors found in logs"
        exit 1  # Needs remediation
    }
}

Write-Output "No errors"
exit 0
```

**Remediation Script**:
```powershell
# Copy logs to shared location or upload to Azure Blob
$logPath = "C:\Program Files\SecureBootWatcher\logs"
$destination = "\\server\share\SecureBootWatcher-Logs\$env:COMPUTERNAME"

New-Item -ItemType Directory -Path $destination -Force -ErrorAction SilentlyContinue
Copy-Item -Path "$logPath\client-*.log" -Destination $destination -Force

Write-Output "Logs copied successfully"
exit 0
```

### Method 3: Azure Monitor Agent (Advanced)

Configure Azure Monitor Agent to collect custom logs:

**Custom Log Configuration**:
```json
{
  "customLogs": [
    {
      "name": "SecureBootWatcher_CL",
      "path": "C:\\Program Files\\SecureBootWatcher\\logs\\client-*.log",
      "parseFormat": "CMTrace"
    }
  ]
}
```

**Kusto Query** (Log Analytics):
```kusto
SecureBootWatcher_CL
| where TimeGenerated > ago(24h)
| where Type_d == 3  // Errors only
| project TimeGenerated, Computer, Message_s
| order by TimeGenerated desc
```

## Troubleshooting Common Issues

### Issue: CMTrace Shows Garbled Text

**Cause**: File encoding mismatch

**Solution**: CMTrace expects UTF-8 or ANSI encoding. The client now uses UTF-8 by default.

**Verify encoding**:
```powershell
Get-Content "C:\Program Files\SecureBootWatcher\logs\client-20250110.log" -Encoding UTF8 | Select-Object -First 5
```

### Issue: Logs Not Updating in CMTrace

**Cause**: File monitoring not enabled

**Solution**:
1. In CMTrace: Tools ? Start Monitoring
2. OR close and reopen the log file

### Issue: Cannot Find CMTrace.exe

**Solution**:

**Option 1**: Download SCCM Client Tools

**Option 2**: Use alternative log viewers:
- **OneTrace** (newer Microsoft tool)
- **BareTail** (free log viewer with highlighting)
- **Log Expert** (open-source alternative)

**Option 3**: PowerShell-based viewer:
```powershell
# Simple PowerShell log viewer
function Watch-CMTraceLog {
    param([string]$Path)
    
    Get-Content $Path -Wait | ForEach-Object {
        if ($_ -match 'type="3"') {
            Write-Host $_ -ForegroundColor Red      # Errors
        } elseif ($_ -match 'type="2"') {
            Write-Host $_ -ForegroundColor Yellow   # Warnings
        } else {
            Write-Host $_ -ForegroundColor White    # Info
        }
    }
}

# Usage
Watch-CMTraceLog -Path "C:\Program Files\SecureBootWatcher\logs\client-20250110.log"
```

### Issue: Log Files Too Large

**Cause**: High-frequency operations or debug logging

**Solution**:

**1. Change log level in appsettings.json**:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",  // Change to "Warning" or "Error"
      "Microsoft": "Warning"
    }
  }
}
```

**2. Reduce retention**:
```csharp
// In Program.cs (rebuild required)
.WriteTo.File(
    path: logPath,
    rollingInterval: RollingInterval.Day,
    retainedFileCountLimit: 7,  // Reduce from 30 to 7 days
    outputTemplate: cmTraceOutputTemplate)
```

## Best Practices

### ? DO

1. **Use CMTrace for troubleshooting** - Much better than Notepad
2. **Configure highlights** - Makes finding issues faster
3. **Enable real-time monitoring** - When watching scheduled tasks
4. **Collect logs proactively** - Before users report issues
5. **Archive logs** - For compliance or long-term troubleshooting

### ? DON'T

1. **Don't open huge logs in Notepad** - Use CMTrace instead
2. **Don't delete logs manually** - Automatic cleanup after 30 days
3. **Don't set Debug logging in production** - Generates huge logs
4. **Don't ignore warnings** - They often indicate configuration issues

## Integration with Monitoring Systems

### SCCM Integration

**Client Logs Collection**:
- SCCM can collect custom logs via Client Settings
- Configure: Administration ? Client Settings ? Client Policy ? Client Log Files
- Add: `C:\Program Files\SecureBootWatcher\logs\client-*.log`

### Splunk Integration

**Universal Forwarder Configuration**:
```ini
[monitor://C:\Program Files\SecureBootWatcher\logs\client-*.log]
disabled = false
sourcetype = cmtrace
index = secureboot
```

**Field Extraction** (props.conf):
```ini
[cmtrace]
SHOULD_LINEMERGE = false
LINE_BREAKER = (<!\[LOG\[)
REGEX = <!\[LOG\[(?P<message>.*?)\]LOG\]!><time="(?P<time>.*?)" date="(?P<date>.*?)" component="(?P<component>.*?)" context="(?P<context>.*?)" type="(?P<type>.*?)" thread="(?P<thread>.*?)" file="(?P<file>.*?)">
```

### Elastic/Logstash Integration

**Logstash Grok Pattern**:
```ruby
filter {
  grok {
    match => { 
      "message" => "<!\[LOG\[%{DATA:log_message}\]LOG\]!><time=\"%{DATA:log_time}\" date=\"%{DATA:log_date}\" component=\"%{DATA:component}\" context=\"%{DATA:context}\" type=\"%{DATA:log_type}\" thread=\"%{DATA:thread}\" file=\"%{DATA:file}\">" 
    }
  }
}
```

## Summary

### Key Benefits of CMTrace Logging

? **Better Troubleshooting** - Color-coded levels, filtering, real-time monitoring  
? **Industry Standard** - Familiar to SCCM/Intune administrators  
? **Advanced Features** - Highlighting, searching, multi-log view  
? **Integration Ready** - Works with SCCM, Splunk, Elastic, Azure Monitor  
? **Backward Compatible** - Console output remains human-readable  

### Quick Reference Card

| Task | Command |
|------|---------|
| Open log with CMTrace | `CMTrace.exe "C:\Program Files\SecureBootWatcher\logs\client-20250110.log"` |
| View latest log | `CMTrace.exe (Get-ChildItem "C:\Program Files\SecureBootWatcher\logs" | Sort LastWriteTime -Desc | Select -First 1).FullName` |
| Monitor real-time | Tools ? Start Monitoring |
| Show only errors | View ? Show only Errors |
| Search | Tools ? Find |
| Highlight keyword | Tools ? Highlight ? Add |
| Open multiple logs | Drag-and-drop multiple files |

---

**Documentation Version**: 1.0  
**Last Updated**: 2025-01-10  
**Related Files**:
- `SecureBootWatcher.Client\Program.cs` - CMTrace format implementation
- `SecureBootWatcher.Client\appsettings.json` - Log level configuration
- `docs\CLIENT_LOGGING_GUIDE.md` - General logging documentation
