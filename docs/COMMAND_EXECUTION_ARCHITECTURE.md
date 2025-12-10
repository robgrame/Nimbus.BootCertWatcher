# ?? Command Execution Architecture

## ?? Overview

This document explains how the **Command Management** system works, focusing on the **polling-based architecture** that enables remote command execution on client devices.

## ??? Architecture Type

**Pull-Based Polling** (not Push-based real-time)

- ? **Simple**: No persistent connections required
- ? **Firewall-friendly**: Only outbound HTTPS from clients
- ? **Resilient**: Tolerates network disconnections
- ?? **Delayed**: Command execution depends on polling interval (5-30 minutes)

## ?? Complete Execution Flow

### High-Level Sequence Diagram

```
????????????         ???????????????         ????????????         ??????????????
?   Admin  ?         ?  Dashboard  ?         ?  API     ?         ?   Client   ?
?  (User)  ?         ?   Web UI    ?         ?  Server  ?         ?  (Device)  ?
????????????         ???????????????         ????????????         ??????????????
     ?                      ?                     ?                      ?
     ? 1. Navigate to       ?                     ?                      ?
     ?    /Commands/Send    ?                     ?                      ?
     ??????????????????????>?                     ?                      ?
     ?                      ?                     ?                      ?
     ? 2. Select device     ?                     ?                      ?
     ?    + Configure cmd   ?                     ?                      ?
     ?                      ?                     ?                      ?
     ? 3. Click "Send       ?                     ?                      ?
     ?    Command"          ?                     ?                      ?
     ??????????????????????>?                     ?                      ?
     ?                      ?                     ?                      ?
     ?                      ? 4. POST /api/       ?                      ?
     ?                      ?    CommandManagement?                      ?
     ?                      ?    /queue           ?                      ?
     ?                      ?????????????????????>?                      ?
     ?                      ?                     ?                      ?
     ?                      ?                     ? 5. Insert into       ?
     ?                      ?                     ?    PendingCommands   ?
     ?                      ?                     ?    table             ?
     ?                      ?                     ?    Status: Pending   ?
     ?                      ?                     ?                      ?
     ?                      ?<?????????????????????                      ?
     ?                      ?  201 Created        ?                      ?
     ?<??????????????????????                     ?                      ?
     ?  "Command queued!"   ?                     ?                      ?
     ?                      ?                     ?                      ?
     ?                      ?                     ?                      ?
     ?                 ? WAIT (0-30 minutes) ?                          ?
     ?                      ?                     ?                      ?
     ?                      ?                     ?  6. Scheduled Task   ?
     ?                      ?                     ?     triggers         ?
     ?                      ?                     ?     (every 30 min)   ?
     ?                      ?                     ?                      ?
     ?                      ?                     ?  7. Client.exe       ?
     ?                      ?                     ?     starts           ?
     ?                      ?                     ?                      ?
     ?                      ?                     ?   8. GET /api/       ?
     ?                      ?                     ?      ClientCommands/ ?
     ?                      ?                     ?      pending?deviceId?
     ?                      ?                     ?<??????????????????????
     ?                      ?                     ?                      ?
     ?                      ?                     ? 9. Query Pending     ?
     ?                      ?                     ?    Commands for      ?
     ?                      ?                     ?    this device       ?
     ?                      ?                     ?    Status? Fetched   ?
     ?                      ?                     ?                      ?
     ?                      ?                     ?  10. Return commands ?
     ?                      ?                     ?      [Command1,...]  ?
     ?                      ?                     ??????????????????????>?
     ?                      ?                     ?                      ?
     ?                      ?                     ?  11. Execute command ?
     ?                      ?                     ?      (modify registry?
     ?                      ?                     ?      verify result)  ?
     ?                      ?                     ?                      ?
     ?                      ?                     ?  12. POST /api/      ?
     ?                      ?                     ?      ClientCommands/ ?
     ?                      ?                     ?      result          ?
     ?                      ?                     ?<??????????????????????
     ?                      ?                     ?                      ?
     ?                      ?                     ? 13. Update command   ?
     ?                      ?                     ?     Status?Completed ?
     ?                      ?                     ?     or Failed        ?
     ?                      ?                     ?                      ?
     ?                      ?                     ??????????????????????>?
     ?                      ?                     ?  200 OK              ?
     ?                      ?                     ?                      ?
     ?                      ?                     ?  14. Build inventory ?
     ?                      ?                     ?      report          ?
     ?                      ?                     ?                      ?
     ?                      ?                     ?  15. POST /api/      ?
     ?                      ?                     ?      SecureBootReports?
     ?                      ?                     ?<??????????????????????
     ?                      ?                     ?                      ?
     ?                      ?                     ??????????????????????>?
     ?                      ?                     ?  200 OK              ?
     ?                      ?                     ?                      ?
     ?                      ?                     ?  16. Client.exe exits?
     ?                      ?                     ?                      ?
     ?                      ?                     ?                      ?
     ? 17. Refresh          ?                     ?                      ?
     ?     /Commands/History?                     ?                      ?
     ??????????????????????>?                     ?                      ?
     ?                      ? 18. GET /api/       ?                      ?
     ?                      ?     CommandManagement?                     ?
     ?                      ?     /statistics      ?                      ?
     ?                      ?????????????????????>?                      ?
     ?                      ?<?????????????????????                      ?
     ?<??????????????????????  Statistics         ?                      ?
     ?  Shows "Completed: 1"?                     ?                      ?
     ?                      ?                     ?                      ?
```

## ?? Detailed Component Flow

### 1. Command Queuing (Dashboard ? API)

**Endpoint**: `POST /api/CommandManagement/queue`

**Request**:
```json
{
  "DeviceId": "abc123-...-xyz",
  "Command": {
    "CommandId": "def456-...-uvw",
    "ConfigurationType": "CertificateUpdate",
    "UpdateType": 1,
    "ForceUpdate": true,
    "Description": "UEFI CA 2023 rollout"
  },
  "Priority": 5,
  "ScheduledFor": null
}
```

**API Action** (`CommandManagementController.cs`):
```csharp
// 1. Verify device exists
var deviceExists = await _dbContext.Devices
    .AnyAsync(d => d.Id == request.DeviceId, cancellationToken);

// 2. Create pending command entity
var commandEntity = new PendingCommandEntity
{
    DeviceId = request.DeviceId,
    CommandId = request.Command.CommandId,
    CommandType = request.Command.GetType().Name,
    CommandJson = JsonSerializer.Serialize(request.Command),
    Status = CommandStatus.Pending, // ? Initial state
    CreatedAtUtc = DateTimeOffset.UtcNow,
    Priority = request.Priority ?? 0
};

// 3. Save to database
_dbContext.PendingCommands.Add(commandEntity);
await _dbContext.SaveChangesAsync(cancellationToken);
```

**Database State**:
```sql
INSERT INTO PendingCommands (
    Id, DeviceId, CommandId, CommandType, CommandJson,
    Status, CreatedAtUtc, Priority
) VALUES (
    NEWID(), 'abc123-...', 'def456-...',
    'CertificateUpdateCommand', '{...}',
    'Pending', GETUTCDATE(), 5
)
```

### 2. Client Scheduled Execution

**Windows Task Scheduler**:
```xml
<Task>
  <Triggers>
    <TimeTrigger>
      <Repetition>
        <Interval>PT30M</Interval>  <!-- Every 30 minutes -->
      </Repetition>
    </TimeTrigger>
  </Triggers>
  <Actions>
    <Exec>
      <Command>C:\Program Files\SecureBootWatcher\SecureBootWatcher.Client.exe</Command>
      <WorkingDirectory>C:\Program Files\SecureBootWatcher</WorkingDirectory>
    </Exec>
  </Actions>
  <Settings>
    <RunOnlyIfNetworkAvailable>true</RunOnlyIfNetworkAvailable>
    <ExecutionTimeLimit>PT10M</ExecutionTimeLimit>
  </Settings>
</Task>
```

**Client Configuration** (`appsettings.json`):
```json
{
  "SecureBootWatcher": {
    "RunMode": "Once",  // ? Runs and exits
    "RegistryPollInterval": "00:30:00",  // ? Task scheduler interval
    "Commands": {
      "EnableCommandProcessing": true,  // ? MUST BE ENABLED!
      "ProcessBeforeInventory": true,   // ? Execute commands first
      "MaxCommandsPerCycle": 10,
      "CommandExecutionDelay": "00:00:02"
    }
  }
}
```

### 3. Command Fetching (Client ? API)

**Client Execution** (`SecureBootWatcherService.cs`):
```csharp
public async Task RunAsync(CancellationToken cancellationToken)
{
    do
    {
        // === PHASE 1: PROCESS COMMANDS FIRST ===
        if (options.Commands.EnableCommandProcessing && 
            options.Commands.ProcessBeforeInventory && 
            _commandProcessor != null)
        {
            await ProcessCommandsAsync(cancellationToken);
        }

        // === PHASE 2: BUILD AND SEND INVENTORY REPORT ===
        var report = await _reportBuilder.BuildAsync(cancellationToken);
        await _reportSink.EmitAsync(report, cancellationToken);

        // Exit if running in single-shot mode
        if (runOnce) break;
        
        await Task.Delay(CalculateDelay(), cancellationToken);
    }
    while (!cancellationToken.IsCancellationRequested);
}
```

**Command Fetching** (`CommandProcessor.cs`):
```csharp
public async Task<IReadOnlyList<DeviceConfigurationCommand>> FetchPendingCommandsAsync(
    CancellationToken cancellationToken)
{
    var deviceId = GetDeviceIdentifier();  // MD5 hash of MachineName+Domain
    var requestUri = $"{apiBaseUrl}/api/ClientCommands/pending?deviceId={deviceId}";
    
    var response = await httpClient.GetAsync(requestUri, cancellationToken);
    
    if (response.IsSuccessStatusCode)
    {
        var commands = await response.Content
            .ReadFromJsonAsync<List<DeviceConfigurationCommand>>();
        
        return commands ?? new List<DeviceConfigurationCommand>();
    }
    
    return Array.Empty<DeviceConfigurationCommand>();
}
```

**API Endpoint** (`ClientCommandsController.cs`):
```csharp
[HttpGet("pending")]
public async Task<ActionResult<List<DeviceConfigurationCommand>>> GetPendingCommandsAsync(
    [FromQuery] string deviceId,
    CancellationToken cancellationToken)
{
    // 1. Parse and validate device ID
    if (!Guid.TryParse(deviceId, out var deviceGuid))
        return BadRequest(new { Error = "Invalid device ID" });

    // 2. Query pending commands
    var pendingCommands = await _dbContext.PendingCommands
        .Where(c => c.DeviceId == deviceGuid && c.IsReadyForExecution)
        .OrderBy(c => c.Priority)      // Higher priority first
        .ThenBy(c => c.CreatedAtUtc)   // Older commands first
        .Take(10)                       // Limit to 10 per cycle
        .ToListAsync(cancellationToken);

    // 3. Deserialize commands and update tracking
    var commands = new List<DeviceConfigurationCommand>();
    
    foreach (var cmdEntity in pendingCommands)
    {
        // Deserialize based on type
        DeviceConfigurationCommand? command = cmdEntity.CommandType switch
        {
            nameof(CertificateUpdateCommand) => 
                JsonSerializer.Deserialize<CertificateUpdateCommand>(cmdEntity.CommandJson),
            nameof(MicrosoftUpdateOptInCommand) => 
                JsonSerializer.Deserialize<MicrosoftUpdateOptInCommand>(cmdEntity.CommandJson),
            nameof(TelemetryConfigurationCommand) => 
                JsonSerializer.Deserialize<TelemetryConfigurationCommand>(cmdEntity.CommandJson),
            _ => null
        };

        if (command != null)
        {
            commands.Add(command);
            
            // Update fetch tracking
            cmdEntity.LastFetchedAtUtc = DateTimeOffset.UtcNow;
            cmdEntity.FetchCount++;
            cmdEntity.Status = CommandStatus.Fetched;  // ? Status update
        }
    }

    // 4. Save tracking updates
    await _dbContext.SaveChangesAsync(cancellationToken);
    
    return Ok(commands);
}
```

**Database State After Fetch**:
```sql
UPDATE PendingCommands
SET Status = 'Fetched',
    LastFetchedAtUtc = GETUTCDATE(),
    FetchCount = FetchCount + 1
WHERE Id = 'abc123-...'
```

### 4. Command Execution (Client)

**Execution Loop** (`CommandProcessor.cs`):
```csharp
private async Task ProcessCommandsAsync(CancellationToken cancellationToken)
{
    // 1. Fetch pending commands
    var commands = await _commandProcessor.FetchPendingCommandsAsync(cancellationToken);
    
    if (commands.Count == 0)
    {
        _logger.LogInformation("No pending commands to process");
        return;
    }

    // 2. Execute each command
    foreach (var command in commandsToProcess)
    {
        try
        {
            // Execute command
            var result = await _commandProcessor.ExecuteCommandAsync(
                command, 
                cancellationToken);

            // Verify result locally
            if (result.Success)
            {
                var currentState = await _commandProcessor
                    .VerifyCommandResultAsync(command, cancellationToken);
                result.CurrentState = currentState;
            }

            // Report result back to API
            var reported = await _commandProcessor.ReportResultAsync(
                result, 
                cancellationToken);

            // Delay between commands to allow registry propagation
            await Task.Delay(options.CommandExecutionDelay, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process command {CommandId}", 
                command.CommandId);
        }
    }
}
```

**Command Execution** (Example: Certificate Update):
```csharp
private async Task<DeviceConfigurationResult> ExecuteCertificateUpdateAsync(
    CertificateUpdateCommand command, 
    CancellationToken cancellationToken)
{
    const string registryPath = @"SYSTEM\CurrentControlSet\Control\SecureBoot";

    // 1. Open registry key (requires Administrator privileges)
    using var key = Registry.LocalMachine.OpenSubKey(registryPath, writable: true);
    if (key == null)
        return FailureResult(command.CommandId, deviceId, 
            "Cannot open SecureBoot registry key (requires admin privileges)");

    // 2. Set UpdateType registry value
    var updateTypeValue = command.UpdateType ?? 1; // Default to DB update (1)
    key.SetValue("UpdateType", updateTypeValue, RegistryValueKind.DWord);

    _logger.LogInformation("Set UpdateType to {Value} in registry", updateTypeValue);

    // 3. Verify the change
    var verifiedValue = key.GetValue("UpdateType");
    if (verifiedValue == null || (uint)verifiedValue != updateTypeValue)
        return FailureResult(command.CommandId, deviceId, 
            "UpdateType registry value not set correctly");

    // 4. Capture post-execution state
    var currentState = await VerifyCommandResultAsync(command, cancellationToken);

    // 5. Return success result
    return new DeviceConfigurationResult
    {
        CommandId = command.CommandId,
        DeviceId = deviceId,
        Success = true,
        Message = $"UpdateType set to {updateTypeValue}. Change verified.",
        CurrentState = currentState
    };
}
```

**Registry Modification**:
```
HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\SecureBoot
?? UpdateType (DWORD) = 1
```

### 5. Result Reporting (Client ? API)

**Client Submission** (`CommandProcessor.cs`):
```csharp
public async Task<bool> ReportResultAsync(
    DeviceConfigurationResult result, 
    CancellationToken cancellationToken)
{
    var requestUri = $"{apiBaseUrl}/api/ClientCommands/result";
    
    var json = JsonSerializer.Serialize(result);
    var content = new StringContent(json, Encoding.UTF8, "application/json");
    
    var response = await httpClient.PostAsync(requestUri, content, cancellationToken);
    
    return response.IsSuccessStatusCode;
}
```

**Request Payload**:
```json
{
  "CommandId": "def456-...-uvw",
  "DeviceId": "abc123-...-xyz",
  "Success": true,
  "Message": "UpdateType set to 1. Change verified.",
  "CurrentState": {
    "MicrosoftUpdateManagedOptIn": 1,
    "AllowTelemetry": 1,
    "WindowsUEFICA2023Capable": 1,
    "SnapshotTimestampUtc": "2025-01-23T14:30:00Z"
  }
}
```

**API Endpoint** (`ClientCommandsController.cs`):
```csharp
[HttpPost("result")]
public async Task<IActionResult> SubmitCommandResultAsync(
    [FromBody] DeviceConfigurationResult result,
    CancellationToken cancellationToken)
{
    // 1. Find the pending command
    var pendingCommand = await _dbContext.PendingCommands
        .FirstOrDefaultAsync(
            c => c.CommandId == result.CommandId && 
                 c.DeviceId == result.DeviceId,
            cancellationToken);

    if (pendingCommand == null)
        return NotFound(new { Error = $"Command {result.CommandId} not found" });

    // 2. Update command status
    pendingCommand.Status = result.Success 
        ? CommandStatus.Completed 
        : CommandStatus.Failed;
    pendingCommand.ProcessedAtUtc = DateTimeOffset.UtcNow;
    pendingCommand.ResultJson = JsonSerializer.Serialize(result);

    // 3. Save changes
    await _dbContext.SaveChangesAsync(cancellationToken);

    return Ok(new 
    { 
        Message = "Command result recorded successfully",
        CommandId = result.CommandId,
        Status = pendingCommand.Status
    });
}
```

**Database State After Result**:
```sql
UPDATE PendingCommands
SET Status = 'Completed',
    ProcessedAtUtc = GETUTCDATE(),
    ResultJson = '{...}'
WHERE CommandId = 'def456-...'
```

## ?? Timing and Latency

### Typical Execution Timeline

| Event | Time (T+) | Status | Notes |
|-------|-----------|--------|-------|
| **Admin queues command** | T+0 min | Pending | Command inserted in database |
| Wait for next client check-in | T+0-30 min | Pending | Depends on when last check-in was |
| **Client check-in** | T+15 min | Pending ? Fetched | Client polls API |
| Client fetches command | T+15 min | Fetched | Command downloaded to client |
| Client executes command | T+15 min | Fetched | Registry modified |
| Client reports result | T+15 min | Fetched ? Completed | Result sent to API |
| **Total end-to-end time** | **0-30 minutes** | Completed | Variable delay |

### Latency Factors

1. **Polling Interval** (Primary Factor)
   - Default: 30 minutes
   - Configurable: `RegistryPollInterval` in `appsettings.json`
   - Minimum recommended: 5 minutes
   - Impact: 70% of total latency

2. **Task Scheduler Alignment**
   - If command queued 1 minute after check-in ? Wait 29 minutes
   - If command queued 1 minute before check-in ? Wait 1 minute
   - Average wait: 15 minutes

3. **Client Execution Time**
   - Command execution: 1-5 seconds
   - Registry verification: < 1 second
   - Result reporting: 1-2 seconds
   - Total: < 10 seconds (negligible)

4. **Client Availability**
   - Device must be powered on
   - Network connectivity required
   - If offline: Command stays "Pending" until next online check-in

### Latency Optimization Strategies

#### Strategy 1: Reduce Polling Interval

**Configuration**:
```json
{
  "RegistryPollInterval": "00:05:00"  // 5 minutes instead of 30
}
```

**Impact**:
- ? Max latency reduced from 30 min ? 5 min
- ? 6x more API calls
- ? Increased network/server load

**When to use**: Urgent deployments, small fleet (<50 devices)

#### Strategy 2: Use Command Priority

**Configuration**:
```json
{
  "Priority": 10  // Highest priority
}
```

**Impact**:
- ? Commands executed first in queue
- ? Doesn't reduce polling latency
- ? Ensures execution order within same check-in

**When to use**: Critical hotfixes, security patches

#### Strategy 3: Schedule Commands

**Configuration**:
```json
{
  "ScheduledFor": "2025-01-24T02:00:00Z"  // Maintenance window
}
```

**Impact**:
- ? Predictable execution time
- ? Aligns with maintenance windows
- ? No reduction in overall latency

**When to use**: Planned rollouts, batch updates

## ?? Security Considerations

### Client Requirements

1. **Administrator Privileges**
   - Registry write access requires Admin/SYSTEM
   - Task Scheduler runs as SYSTEM by default
   - Client must be installed with elevation

2. **Network Access**
   - Outbound HTTPS to API (port 443/5001)
   - Firewall rules must allow outbound
   - No inbound connections required

3. **Certificate Authentication** (Optional)
   - Mutual TLS can be enabled
   - Client certificate in Machine store
   - API validates client certificate

### Data Protection

1. **Command Storage**
   - Commands stored in SQL Server
   - Encrypted at rest (TDE recommended)
   - Access controlled via API authentication

2. **Transport Security**
   - All communication over HTTPS/TLS 1.2+
   - Certificate validation enforced
   - Optional certificate pinning

3. **Result Confidentiality**
   - Results include device state snapshot
   - May contain sensitive registry values
   - Logged to database (retention policy applies)

## ?? Monitoring and Diagnostics

### Key Metrics

1. **Command Latency**
   ```sql
   SELECT 
       CommandId,
       DATEDIFF(MINUTE, CreatedAtUtc, ProcessedAtUtc) AS LatencyMinutes,
       FetchCount,
       Status
   FROM PendingCommands
   WHERE ProcessedAtUtc IS NOT NULL
   ORDER BY CreatedAtUtc DESC
   ```

2. **Success Rate**
   ```sql
   SELECT 
       Status,
       COUNT(*) AS Count,
       CAST(COUNT(*) * 100.0 / SUM(COUNT(*)) OVER() AS DECIMAL(5,2)) AS Percentage
   FROM PendingCommands
   GROUP BY Status
   ```

3. **Device Participation**
   ```sql
   SELECT 
       d.MachineName,
       COUNT(pc.Id) AS CommandsProcessed,
       MAX(pc.ProcessedAtUtc) AS LastCommandProcessed
   FROM Devices d
   LEFT JOIN PendingCommands pc ON d.Id = pc.DeviceId
   GROUP BY d.MachineName
   ORDER BY CommandsProcessed DESC
   ```

### Troubleshooting

#### Command Stays "Pending"

**Symptoms**:
- Command never changes from "Pending" status
- `FetchCount` remains 0
- `LastFetchedAtUtc` is NULL

**Possible Causes**:

1. **Client not running**
   ```powershell
   # Check Scheduled Task
   Get-ScheduledTask -TaskName "SecureBootWatcher"
   
   # Check last run
   Get-ScheduledTaskInfo -TaskName "SecureBootWatcher"
   ```

2. **Command processing disabled**
   ```json
   // Check appsettings.json
   {
     "Commands": {
       "EnableCommandProcessing": false  // ? Should be true!
     }
   }
   ```

3. **Device offline/powered off**
   ```sql
   SELECT LastSeenUtc 
   FROM Devices 
   WHERE Id = 'device-guid'
   ```

4. **Network/firewall issues**
   ```powershell
   # Test API connectivity
   Test-NetConnection -ComputerName api-server -Port 5001
   ```

#### Command "Fetched" but Never Completes

**Symptoms**:
- Status stuck at "Fetched"
- `FetchCount` > 0
- `ProcessedAtUtc` is NULL

**Possible Causes**:

1. **Client crashes during execution**
   ```
   Check client logs: C:\Program Files\SecureBootWatcher\logs\client-*.log
   ```

2. **Permission denied (not running as Admin)**
   ```
   ERROR: UnauthorizedAccessException: Access denied writing to registry
   ```

3. **Registry key not found (wrong Windows version)**
   ```
   ERROR: Cannot open SecureBoot registry key
   ```

#### Command "Failed" Status

**Symptoms**:
- Status = "Failed"
- Error message in `ResultJson`

**Analysis**:
```sql
SELECT 
    CommandId,
    CommandType,
    JSON_VALUE(ResultJson, '$.Message') AS ErrorMessage,
    ProcessedAtUtc
FROM PendingCommands
WHERE Status = 'Failed'
ORDER BY ProcessedAtUtc DESC
```

**Common Errors**:

| Error Message | Cause | Resolution |
|---------------|-------|------------|
| "Access denied" | Client not running as Admin | Run Scheduled Task as SYSTEM |
| "Registry key not found" | Unsupported OS version | Check Windows compatibility |
| "UpdateType registry value not set correctly" | Registry write failed | Check permissions, disk space |

### Logging

**Client Logs** (`C:\Program Files\SecureBootWatcher\logs\client-*.log`):
```
2025-01-23 14:25:00.123 [INF] Fetched 1 pending command(s)
2025-01-23 14:25:00.456 [INF] Processing command def456-... of type CertificateUpdate
2025-01-23 14:25:01.789 [INF] Set UpdateType to 1 in registry
2025-01-23 14:25:02.012 [INF] Command def456-... executed successfully
2025-01-23 14:25:02.345 [INF] Command def456-... result reported to API
```

**API Logs** (`R:\Nimbus.SecureBootCert\logs\api-*.log`):
```
2025-01-23 14:20:00.123 [INF] Queued command def456-... for device abc123-...
2025-01-23 14:25:00.456 [INF] Fetching pending commands for device abc123-...
2025-01-23 14:25:00.789 [INF] Found 1 pending command(s) for device abc123-...
2025-01-23 14:25:02.345 [INF] Received command result for CommandId def456-..., Success: True
2025-01-23 14:25:02.678 [INF] Command def456-... marked as Completed
```

## ?? Alternative Architectures (Future Enhancements)

### Option 1: Continuous Mode (Already Supported)

**Current**: RunMode = "Once" (runs and exits)
**Alternative**: RunMode = "Continuous" (persistent background service)

**Configuration**:
```json
{
  "RunMode": "Continuous",
  "RegistryPollInterval": "00:05:00"
}
```

**Deployment**:
```powershell
# Install as Windows Service instead of Scheduled Task
New-Service -Name "SecureBootWatcher" `
            -BinaryPathName "C:\Program Files\SecureBootWatcher\SecureBootWatcher.Client.exe" `
            -StartupType Automatic
```

**Pros**:
- ? Reduced latency (polls every 5 min)
- ? No task scheduler overhead
- ? Persistent process

**Cons**:
- ? Higher memory footprint
- ? More complex lifecycle management
- ? Requires service installation

### Option 2: SignalR Push (Not Yet Implemented)

**Architecture**: Real-time push via WebSocket

**Flow**:
```
Admin queues command ? API ? SignalR Hub ? WebSocket ? Client ? Execute ? Report
                                    ?
                            Connected clients only
```

**Pros**:
- ? Near-instant execution (~seconds)
- ? Real-time feedback
- ? No polling overhead

**Cons**:
- ? Requires persistent WebSocket connection
- ? More complex client implementation
- ? Firewall may block WebSockets
- ? Doesn't work for offline devices

**Implementation Scope**:
- New SignalR Hub in API project
- WebSocket client in SecureBootWatcher.Client
- Connection state management
- Reconnection logic
- Fallback to polling if WebSocket fails

## ?? Related Documentation

- **User Guide**: `docs/COMMAND_MANAGEMENT_USER_GUIDE.md`
- **Client Deployment**: `docs/CLIENT_DEPLOYMENT.md`
- **API Configuration**: Dashboard ? Admin ? API Configuration
- **Troubleshooting**: Dashboard ? Commands ? History

## ?? Key Components

| Component | File | Responsibility |
|-----------|------|----------------|
| **Dashboard UI** | `SecureBootDashboard.Web/Pages/Commands/Send.cshtml.cs` | Command queuing UI |
| **API Controller (Admin)** | `SecureBootDashboard.Api/Controllers/CommandManagementController.cs` | Queue commands for devices |
| **API Controller (Client)** | `SecureBootDashboard.Api/Controllers/ClientCommandsController.cs` | Serve commands to clients |
| **Client Service** | `SecureBootWatcher.Client/Services/SecureBootWatcherService.cs` | Main execution loop |
| **Command Processor** | `SecureBootWatcher.Client/Services/CommandProcessor.cs` | Fetch, execute, report |
| **Database Entity** | `SecureBootDashboard.Api/Data/PendingCommandEntity.cs` | Command storage model |

## ?? Performance Characteristics

### Scalability Limits

| Metric | Value | Notes |
|--------|-------|-------|
| **Max devices** | ~10,000 | With 30-min polling |
| **Commands/hour** | ~20,000 | At 10 commands/device/hour |
| **API load** | Low | Bursty (every 30 min) |
| **Database growth** | ~1 GB/year | Per 1000 devices |

### Resource Requirements

**Client**:
- CPU: < 1% during execution
- Memory: ~50 MB
- Disk: ~100 MB (app + logs)
- Network: ~10 KB per check-in

**API**:
- CPU: ~5% average
- Memory: ~500 MB
- Database: ~1 GB/year/1000 devices

**SQL Server**:
- Connections: 1 per concurrent client check-in
- IOPS: Low (mostly reads)
- Storage: ~1 GB/year/1000 devices

---

**Document Version**: 1.0  
**Last Updated**: 2025-01-23  
**Status**: ? Production  
**Architecture**: Pull-based HTTP Polling
