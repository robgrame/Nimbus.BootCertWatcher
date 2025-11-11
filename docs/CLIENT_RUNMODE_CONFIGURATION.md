# Client Run Mode Configuration

## Overview

The SecureBootWatcher Client supports two execution modes to accommodate different deployment scenarios:

1. **Once** (Default) - Single-shot execution for scheduled tasks
2. **Continuous** - Long-running service mode

## Configuration

### Setting Run Mode

Add the `RunMode` property to your `appsettings.json`:

```json
{
  "SecureBootWatcher": {
    "RunMode": "Once",
    "FleetId": "your-fleet-id",
    "RegistryPollInterval": "00:30:00",
    "EventQueryInterval": "00:30:00",
    ...
  }
}
```

### Run Mode Options

#### "Once" Mode (Recommended for Scheduled Tasks)

```json
"RunMode": "Once"
```

**Behavior:**
- Executes a single report generation cycle
- Exits automatically after completion
- Ideal for Windows Task Scheduler deployments
- Prevents multiple instances from accumulating

**Use Cases:**
- Scheduled task running every N hours
- On-demand execution via scripts
- Systems with resource constraints
- Environments where persistence is managed externally

**Example Task Scheduler Configuration:**
```powershell
$action = New-ScheduledTaskAction -Execute "C:\Program Files\SecureBootWatcher\SecureBootWatcher.Client.exe"
$trigger = New-ScheduledTaskTrigger -Daily -At "2:00AM"
Register-ScheduledTask -TaskName "SecureBootWatcher" -Action $action -Trigger $trigger
```

#### "Continuous" Mode (For Services)

```json
"RunMode": "Continuous"
```

**Behavior:**
- Runs indefinitely in a loop
- Polls at intervals defined by `RegistryPollInterval` and `EventQueryInterval`
- Suitable for service deployments
- Responds to cancellation tokens (Ctrl+C, Windows Service stop)

**Use Cases:**
- Windows Service installation
- Long-running container deployments
- Development and testing scenarios
- Systems requiring immediate response to changes

**Example Service Installation:**
```powershell
New-Service -Name "SecureBootWatcher" `
    -BinaryPathName "C:\Program Files\SecureBootWatcher\SecureBootWatcher.Client.exe" `
    -DisplayName "Secure Boot Watcher" `
    -StartupType Automatic
```

## Logging

The client logs the selected run mode at startup:

**"Once" mode:**
```
[08:00:00 INF] Secure Boot watcher started in single-shot mode (will exit after one cycle).
```

**"Continuous" mode:**
```
[08:00:00 INF] Secure Boot watcher started in continuous mode.
```

## Migration from Previous Versions

If you are upgrading from a version without the `RunMode` setting:

1. The default behavior is now **"Once"** mode
2. For existing continuous deployments (services), explicitly set `"RunMode": "Continuous"`
3. Update scheduled tasks if they expect the client to run continuously (not recommended)

### Example Migration

**Before (implicit continuous mode):**
```json
{
  "SecureBootWatcher": {
    "FleetId": "production",
    "RegistryPollInterval": "00:30:00"
  }
}
```

**After (explicit mode selection):**

For scheduled tasks (recommended):
```json
{
  "SecureBootWatcher": {
    "RunMode": "Once",  // <- Add this
    "FleetId": "production",
    "RegistryPollInterval": "00:30:00"
  }
}
```

For services:
```json
{
  "SecureBootWatcher": {
    "RunMode": "Continuous",  // <- Add this
    "FleetId": "production",
    "RegistryPollInterval": "00:30:00"
  }
}
```

## Best Practices

### Scheduled Task Deployment (Recommended)

✅ **DO:**
- Use `"RunMode": "Once"`
- Schedule execution at appropriate intervals (e.g., every 4-8 hours)
- Configure task to run with highest privileges
- Set task to run whether user is logged on or not

❌ **DON'T:**
- Use `"Continuous"` mode with scheduled tasks
- Schedule too frequently (causes unnecessary load)
- Run without administrator privileges

### Service Deployment

✅ **DO:**
- Use `"RunMode": "Continuous"`
- Configure appropriate polling intervals
- Implement service recovery options
- Monitor service health

❌ **DON'T:**
- Set very short polling intervals (causes high CPU usage)
- Run multiple service instances simultaneously

## Troubleshooting

### Multiple Instances Running

**Symptom:** Task Manager shows multiple `SecureBootWatcher.Client.exe` processes

**Solution:**
1. Verify `RunMode` is set to `"Once"` in appsettings.json
2. Check scheduled task configuration
3. Ensure task is not set to run indefinitely
4. Review task history for failures that might cause restarts

### Service Stops Immediately

**Symptom:** Service starts but stops immediately when in service mode

**Solution:**
1. Verify `RunMode` is set to `"Continuous"`
2. Check service account permissions
3. Review application logs in `logs/client-.log`
4. Ensure configuration file is accessible

### Configuration Not Applied

**Symptom:** RunMode setting appears to be ignored

**Solution:**
1. Verify appsettings.json syntax is valid
2. Check for `appsettings.local.json` that might override settings
3. Ensure file is in the same directory as the executable
4. Review startup logs for configuration loading messages

## Related Documentation

- [Client Deployment Guide](CLIENT_DEPLOYMENT_GUIDE.md)
- [Client Deployment Scripts](CLIENT_DEPLOYMENT_SCRIPTS.md)
- [Logging Guide](LOGGING_GUIDE.md)

## Version History

- **v1.x** - `RunMode` configuration added
- **v1.0** - Original continuous-only mode
