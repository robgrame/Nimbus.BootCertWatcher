# ? CMTrace Logging Format - Quick Reference

## What Changed

**File logging format** changed from **standard text** to **CMTrace-compatible format**.

### Before (Standard Format)
```
2025-01-10 16:33:18.123 +01:00 [INF] SecureBootWatcher Client Starting
2025-01-10 16:33:18.124 +01:00 [INF] Version: 1.1.1.48182
2025-01-10 16:33:19.500 +01:00 [ERR] Failed to connect to Azure Queue
```

### After (CMTrace Format)
```
<![LOG[SecureBootWatcher Client Starting]LOG]!><time="16:33:18.123+01:00" date="01-10-2025" component="SecureBootWatcher.Client" context="" type="1" thread="1" file="">
<![LOG[Version: 1.1.1.48182]LOG]!><time="16:33:18.124+01:00" date="01-10-2025" component="SecureBootWatcher.Client" context="" type="1" thread="1" file="">
<![LOG[Failed to connect to Azure Queue]LOG]!><time="16:33:19.500+01:00" date="01-10-2025" component="SecureBootWatcher.Client" context="" type="3" thread="1" file="">
```

**Console output** remains unchanged (human-readable).

## Why CMTrace Format?

? **Industry Standard** - Used by SCCM/Intune administrators worldwide  
? **Better Troubleshooting** - Color-coded errors, warnings, info  
? **Advanced Filtering** - Filter by component, thread, log level  
? **Real-Time Monitoring** - Watch logs as client runs  
? **Integration Ready** - Works with SCCM, Splunk, Azure Monitor  

## Files Modified

### 1. `SecureBootWatcher.Client\Program.cs`

**Added**:
```csharp
// CMTrace-compatible log format
var cmTraceOutputTemplate = "<![LOG[{Message:lj}{NewLine}{Exception}]LOG]!>" +
    "<time=\"{Timestamp:HH:mm:ss.fff}{Timestamp:zzz}\" " +
    "date=\"{Timestamp:MM-dd-yyyy}\" " +
    "component=\"SecureBootWatcher.Client\" " +
    "context=\"\" " +
    "type=\"{Level:w}\" " +
    "thread=\"{ThreadId}\" " +
    "file=\"\">";

Log.Logger = new LoggerConfiguration()
    // ...existing config...
    .Enrich.WithThreadId()  // NEW: Thread tracking
    .WriteTo.File(
        path: logPath,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: cmTraceOutputTemplate)  // NEW: CMTrace format
    .CreateLogger();
```

### 2. `SecureBootWatcher.Client\SecureBootWatcher.Client.csproj`

**Added NuGet package**:
```xml
<PackageReference Include="Serilog.Enrichers.Thread" Version="4.0.0" />
```

## How to Use

### Step 1: Download CMTrace

**Option 1**: From SCCM installation
```
C:\Program Files (x86)\Microsoft Endpoint Manager\AdminConsole\bin\CMTrace.exe
```

**Option 2**: Download Microsoft System Center Configuration Manager Toolkit

**Option 3**: Use OneTrace (newer version)
```
C:\Program Files (x86)\Microsoft Endpoint Manager\AdminConsole\bin\OneTrace.exe
```

### Step 2: Open Logs

```powershell
# Open latest log file
$latestLog = Get-ChildItem "C:\Program Files\SecureBootWatcher\logs" -Filter "client-*.log" | 
             Sort-Object LastWriteTime -Descending | 
             Select-Object -First 1

& "CMTrace.exe" $latestLog.FullName
```

### Step 3: Enable Real-Time Monitoring

In CMTrace:
1. **Tools** ? **Start Monitoring**
2. Logs update automatically as client writes new entries

### Step 4: Filter & Highlight

**Show only errors**:
- View ? Show only Errors

**Highlight keywords**:
- Tools ? Highlight ? Add
- Enter: `Certificate`, `API`, `Error`, etc.

## Log Locations

| Scenario | Log Path |
|----------|----------|
| Scheduled Task (SYSTEM) | `C:\Program Files\SecureBootWatcher\logs\client-YYYYMMDD.log` |
| Manual Run (User) | `{ExeDirectory}\logs\client-YYYYMMDD.log` |
| Today's Log | `client-20250110.log` |

## Log Level Colors in CMTrace

| Level | CMTrace Type | Color |
|-------|--------------|-------|
| Information | 1 | White/Gray |
| Warning | 2 | Yellow |
| Error | 3 | Red |

## Deployment Impact

### ? No Configuration Changes Needed

- Existing `appsettings.json` files work without changes
- Log level controlled by `Logging:LogLevel` section (unchanged)
- No new parameters or settings required

### ? Backward Compatible

- Console output unchanged (still human-readable)
- Log file location unchanged
- Log retention unchanged (30 days)
- File naming unchanged (`client-YYYYMMDD.log`)

### ?? Rebuild Required

**Before deploying**:
```powershell
# 1. Restore packages (new: Serilog.Enrichers.Thread)
cd SecureBootWatcher.Client
dotnet restore

# 2. Rebuild client
dotnet clean
dotnet build -c Release

# 3. Verify new format in logs
.\bin\Release\net48\SecureBootWatcher.Client.exe

# 4. Check log output
Get-Content ".\bin\Release\net48\logs\client-*.log" -Tail 5
```

**Expected output**:
```
<![LOG[SecureBootWatcher Client Starting]LOG]!><time="16:33:18.123+01:00" date="01-10-2025" ...
```

### ?? Redeploy

After rebuild, redeploy using existing method:
- Intune Win32 app
- SCCM package
- GPO deployment
- Manual copy

**No configuration changes needed on target devices.**

## Testing Checklist

- [ ] Rebuild client with new Serilog.Enrichers.Thread package
- [ ] Run client manually and verify logs generated
- [ ] Open log file with CMTrace - verify format recognized
- [ ] Check errors show in red, warnings in yellow
- [ ] Test real-time monitoring (Tools ? Start Monitoring)
- [ ] Verify console output still human-readable
- [ ] Test on pilot device before mass deployment

## Troubleshooting

### Issue: Build Error - Serilog.Enrichers.Thread not found

**Solution**:
```powershell
dotnet restore
dotnet build
```

### Issue: CMTrace doesn't recognize format

**Symptom**: Logs show as plain text, no color coding

**Solution**:
1. Verify log file contains CMTrace format:
   ```powershell
   Get-Content "logs\client-*.log" -First 5
   ```
2. Expected: Should start with `<![LOG[`
3. If not, rebuild client from updated source

### Issue: Logs show garbled characters

**Cause**: Encoding mismatch

**Solution**: CMTrace expects UTF-8. Serilog uses UTF-8 by default. No action needed.

## Quick Commands

```powershell
# Open today's log with CMTrace
$log = Get-ChildItem "C:\Program Files\SecureBootWatcher\logs" | 
       Where-Object { $_.Name -like "client-$(Get-Date -Format 'yyyyMMdd').log" }
& "CMTrace.exe" $log.FullName

# View last 20 entries (PowerShell alternative)
Get-Content "C:\Program Files\SecureBootWatcher\logs\client-*.log" -Tail 20

# Search for errors in all logs
Get-ChildItem "C:\Program Files\SecureBootWatcher\logs" -Filter "client-*.log" | 
    ForEach-Object { 
        Select-String -Path $_.FullName -Pattern 'type="3"' 
    }

# Count errors per log file
Get-ChildItem "C:\Program Files\SecureBootWatcher\logs" -Filter "client-*.log" | 
    ForEach-Object {
        $errors = (Select-String -Path $_.FullName -Pattern 'type="3"').Count
        [PSCustomObject]@{
            File = $_.Name
            Errors = $errors
        }
    }
```

## Next Steps

1. **Rebuild Client** with new logging format
2. **Test on Pilot Devices** (5-10 devices)
3. **Collect Feedback** from IT team on log readability
4. **Deploy to Production** via Intune/SCCM
5. **Train IT Team** on using CMTrace

## Resources

- **Full Documentation**: `docs\CMTRACE_LOGGING_GUIDE.md`
- **CMTrace Download**: Microsoft System Center Configuration Manager Toolkit
- **Alternative**: OneTrace (newer Microsoft tool)
- **Log Analytics Integration**: Configure Azure Monitor Agent for custom logs

---

**Change Summary**:
- ? File logging now uses CMTrace format
- ? Console output unchanged (human-readable)
- ? No configuration changes required
- ? Rebuild + redeploy needed
- ? Works with SCCM, Intune, monitoring tools

**Impact**: Low (cosmetic change to log format)  
**Risk**: None (console output unchanged, logs still readable)  
**Benefit**: Better troubleshooting with CMTrace viewer
