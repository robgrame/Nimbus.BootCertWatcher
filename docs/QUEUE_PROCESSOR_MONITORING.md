# Queue Processor Monitoring Guide

This guide explains how to verify that the SecureBootDashboard API is correctly processing messages from Azure Storage Queue.

---

## Quick Health Check

### 1. **Check API Logs**

The Queue Processor logs detailed information about its activity. Look for these key log messages:

```bash
# Windows
Get-Content "R:\Nimbus.SecureBootCert\logs\api-*.log" -Tail 100 -Wait

# Or in the configured log path
Get-Content "<YourLogPath>\api-*.log" -Tail 100 -Wait
```

**Expected log entries:**

#### ? **Successful Startup**
```
[INF] Queue processor starting. Queue: secureboot-reports, AuthMethod: Certificate
[INF] Loaded certificate from store. Thumbprint: 61FC110..., Subject: CN=SecureBoot Watcher Service
[INF] Using Certificate-based authentication with Client ID: c8034569-4990-4823-9f1d-b46223789c35
[INF] Queue processor started successfully.
```

#### ? **Active Processing**
```
[INF] Received 3 message(s) from queue secureboot-reports
[DBG] Processing report for device DESKTOP-ABC123 (MessageId: 12345...)
[INF] Saved report a1b2c3d4-... for device DESKTOP-ABC123 from queue message 12345...
[INF] Successfully processed and deleted message 12345...
```

#### ?? **Empty Queue (Normal)**
```
# No log entry - queue is empty, processor is waiting
# This is normal when no clients are sending reports
```

### 2. **Check Azure Queue Metrics**

**Via Azure Portal:**
1. Navigate to: Storage Account ? Queues ? `secureboot-reports`
2. Click "Metrics" tab
3. Add metric: "Message Count"
4. Add metric: "Transactions"

**Expected behavior:**
- **Message Count:** Should drop to 0 after processing
- **Transactions:** Should show activity when messages arrive

**Via Azure CLI:**
```powershell
# Get current message count
az storage queue metadata show `
  --name secureboot-reports `
  --account-name secbootcert `
  --auth-mode login `
  --query "{MessageCount:approximateMessageCount}" `
  --output table
```

### 3. **Check Database for New Reports**

```sql
-- Check recent reports from queue processing
SELECT TOP 10 
    Id,
    MachineName,
    CreatedAtUtc,
    DeploymentState,
    ClientVersion
FROM Devices d
INNER JOIN SecureBootReports r ON d.Id = r.DeviceId
ORDER BY r.CreatedAtUtc DESC;

-- Check if reports are being added
SELECT 
    CAST(CreatedAtUtc AS DATE) AS ReportDate,
    COUNT(*) AS ReportCount
FROM SecureBootReports
WHERE CreatedAtUtc >= DATEADD(DAY, -7, GETUTCDATE())
GROUP BY CAST(CreatedAtUtc AS DATE)
ORDER BY ReportDate DESC;
```

---

## Detailed Monitoring

### Log Messages Reference

| Log Level | Message Pattern | Meaning | Action Required |
|-----------|----------------|---------|-----------------|
| **INF** | `Queue processor starting` | Service initializing | ? Normal |
| **INF** | `Queue processor started successfully` | Ready to process | ? Normal |
| **INF** | `Received {N} message(s) from queue` | Messages found | ? Normal |
| **INF** | `Saved report {ID} for device {Name}` | Report persisted | ? Normal |
| **INF** | `Successfully processed and deleted message` | Message completed | ? Normal |
| **WRN** | `Queue processor is disabled` | Feature turned off | ?? Check config |
| **WRN** | `Queue {Name} does not exist` | Queue not found | ? Create queue |
| **WRN** | `Message {ID} exceeded max dequeue count` | Poison message | ?? Investigate |
| **ERR** | `Failed to create queue client` | Auth failure | ? Fix credentials |
| **ERR** | `Failed to receive messages from queue` | Connection issue | ? Check network |
| **ERR** | `Failed to process message {ID}` | Processing error | ? Check exception |
| **ERR** | `Failed to deserialize message` | Invalid JSON | ? Client issue |

### Configuration Checklist

#### ? **Verify QueueProcessor is Enabled**

File: `SecureBootDashboard.Api\appsettings.json`

```json
{
  "QueueProcessor": {
    "Enabled": true,  // ? Must be true
    "QueueServiceUri": "https://secbootcert.queue.core.windows.net",
    "QueueName": "secureboot-reports",
    "AuthenticationMethod": "Certificate"  // Or your chosen method
  }
}
```

#### ? **Verify Certificate Access (if using Certificate auth)**

```powershell
# Check certificate exists and is accessible
$thumbprint = "61FC110D5BABD61419B106862B304C2FFF57A262"
$cert = Get-ChildItem Cert:\LocalMachine\My | Where-Object { $_.Thumbprint -eq $thumbprint }

if ($cert) {
    Write-Host "? Certificate found: $($cert.Subject)" -ForegroundColor Green
    Write-Host "  Valid from: $($cert.NotBefore)" -ForegroundColor Gray
    Write-Host "  Valid until: $($cert.NotAfter)" -ForegroundColor Gray
    Write-Host "  Has private key: $($cert.HasPrivateKey)" -ForegroundColor Gray
} else {
    Write-Host "? Certificate NOT found!" -ForegroundColor Red
}
```

#### ? **Verify RBAC Permissions**

The service principal (Client ID) must have **Storage Queue Data Contributor** role on the storage account.

```powershell
# Check role assignment
az role assignment list `
  --assignee c8034569-4990-4823-9f1d-b46223789c35 `
  --scope "/subscriptions/<sub-id>/resourceGroups/<rg-name>/providers/Microsoft.Storage/storageAccounts/secbootcert" `
  --query "[?roleDefinitionName=='Storage Queue Data Contributor']" `
  --output table
```

If missing, assign role:
```powershell
.\scripts\Assign-StorageQueueRole.ps1
```

---

## Testing Queue Processing

### Send Test Message

#### Option 1: Use Client
```powershell
cd "C:\Program Files\SecureBootWatcher"
.\SecureBootWatcher.Client.exe
```

Check client logs for:
```
[INF] ? Successfully sent report to AzureQueue
```

#### Option 2: Manual Queue Message (PowerShell)

```powershell
# Install Azure PowerShell if needed
Install-Module -Name Az.Storage -Scope CurrentUser

# Connect
Connect-AzAccount

# Get queue
$ctx = (Get-AzStorageAccount -ResourceGroupName "<rg-name>" -Name "secbootcert").Context
$queue = Get-AzStorageQueue -Name "secureboot-reports" -Context $ctx

# Send test message
$testReport = @{
    Report = @{
        Device = @{
            MachineName = "TEST-MACHINE"
            Manufacturer = "Test"
            Model = "Test"
        }
        Registry = @{
            State = "Deployed"
        }
        Events = @()
        Certificates = $null
    }
} | ConvertTo-Json -Depth 10 -Compress

$queue.QueueClient.SendMessage([Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($testReport)))
```

#### Option 3: Azure Portal

1. Navigate to: Storage Account ? Queues ? `secureboot-reports`
2. Click "+ Add message"
3. Paste JSON payload (must be Base64 encoded)
4. Click OK

### Verify Processing

**Watch API logs in real-time:**
```powershell
Get-Content "R:\Nimbus.SecureBootCert\logs\api-*.log" -Tail 50 -Wait
```

**Expected output within 5-30 seconds:**
```
[INF] Received 1 message(s) from queue secureboot-reports
[DBG] Processing report for device TEST-MACHINE (MessageId: ...)
[INF] Saved report <guid> for device TEST-MACHINE from queue message ...
[INF] Successfully processed and deleted message ...
```

**Check database:**
```sql
SELECT TOP 1 * FROM SecureBootReports ORDER BY CreatedAtUtc DESC;
```

---

## Troubleshooting

### Problem: No log entries about queue processing

**Check 1: Is Queue Processor enabled?**
```powershell
# In appsettings.json
Get-Content "SecureBootDashboard.Api\appsettings.json" | Select-String "QueueProcessor" -Context 0,10
```

Ensure `"Enabled": true`

**Check 2: Is API running?**
```powershell
# Check if API process is running
Get-Process -Name "SecureBootDashboard.Api" -ErrorAction SilentlyContinue

# Or check port
Test-NetConnection -ComputerName localhost -Port 5001
```

**Check 3: Check Application Insights (if enabled)**
- Azure Portal ? Application Insights ? Logs
- Query: `traces | where message contains "Queue processor"`

### Problem: "Failed to create queue client"

**Likely causes:**
1. **Certificate not found**
   - Run: `.\scripts\Diagnose-QueueCertificate.ps1`
   - Check: Certificate exists in LocalMachine\My store
   - Verify: Thumbprint matches configuration

2. **Missing RBAC role**
   - Run: `.\scripts\Assign-StorageQueueRole.ps1`
   - Verify: Service principal has "Storage Queue Data Contributor"

3. **Invalid TenantId or ClientId**
   - Check Azure Portal ? Entra ID ? App registrations
   - Verify IDs match configuration

### Problem: "Queue {Name} does not exist"

**Create the queue:**
```powershell
az storage queue create `
  --name secureboot-reports `
  --account-name secbootcert `
  --auth-mode login
```

### Problem: Messages stuck in queue (not being processed)

**Check dequeue count:**
```powershell
# List messages with metadata
az storage message peek `
  --queue-name secureboot-reports `
  --account-name secbootcert `
  --num-messages 10 `
  --auth-mode login
```

If `dequeueCount >= 5`, message is poisoned.

**Clear poison messages:**
```powershell
# Delete all messages (use with caution!)
az storage message clear `
  --queue-name secureboot-reports `
  --account-name secbootcert `
  --auth-mode login
```

### Problem: "Failed to deserialize message"

**Cause:** Invalid JSON format from client

**Check client version:**
- Ensure client is up-to-date
- Check client logs for serialization errors

**Manual inspection:**
```powershell
# Peek at raw message
az storage message peek `
  --queue-name secureboot-reports `
  --account-name secbootcert `
  --num-messages 1 `
  --auth-mode login `
  --output json
```

### Problem: High dequeue count (messages failing repeatedly)

**Check API logs for exceptions:**
```powershell
Get-Content "R:\Nimbus.SecureBootCert\logs\api-*.log" | Select-String "ERR" | Select-Object -Last 20
```

Common causes:
- **Database connection issue** - Check SQL Server connectivity
- **Schema mismatch** - Apply pending EF migrations
- **Validation failure** - Check `SecureBootStatusReport` model

---

## Performance Monitoring

### Key Metrics to Track

| Metric | Query | Healthy Range |
|--------|-------|---------------|
| **Message Processing Time** | Logs: Time between "Received" and "Successfully processed" | < 5 seconds |
| **Queue Depth** | Azure Portal ? Queue Metrics | < 100 messages |
| **Poison Messages** | Logs: `exceeded max dequeue count` | 0 |
| **Processing Rate** | Reports/hour in database | Varies by fleet size |
| **Error Rate** | Logs: Count of `[ERR]` entries | < 1% |

### SQL Queries for Monitoring

```sql
-- Reports ingested in last hour (should match queue processing)
SELECT COUNT(*) AS ReportsLastHour
FROM SecureBootReports
WHERE CreatedAtUtc >= DATEADD(HOUR, -1, GETUTCDATE());

-- Average reports per day (last 7 days)
SELECT AVG(DailyCount) AS AvgReportsPerDay
FROM (
    SELECT CAST(CreatedAtUtc AS DATE) AS ReportDate, COUNT(*) AS DailyCount
    FROM SecureBootReports
    WHERE CreatedAtUtc >= DATEADD(DAY, -7, GETUTCDATE())
    GROUP BY CAST(CreatedAtUtc AS DATE)
) AS DailyCounts;

-- Devices reporting in last 24 hours
SELECT COUNT(DISTINCT DeviceId) AS ActiveDevices
FROM SecureBootReports
WHERE CreatedAtUtc >= DATEADD(HOUR, -24, GETUTCDATE());
```

### PowerShell Monitoring Script

```powershell
# Save as Monitor-QueueProcessor.ps1
param(
    [int]$RefreshSeconds = 30
)

while ($true) {
    Clear-Host
    Write-Host "=== Queue Processor Monitor ===" -ForegroundColor Cyan
    Write-Host "Time: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Gray
    Write-Host ""
    
    # Check queue depth
    $queueDepth = az storage queue metadata show `
        --name secureboot-reports `
        --account-name secbootcert `
        --auth-mode login `
        --query approximateMessageCount `
        --output tsv 2>$null
    
    if ($queueDepth) {
        Write-Host "Queue Depth: $queueDepth messages" -ForegroundColor $(if($queueDepth -gt 100){"Red"}else{"Green"})
    } else {
        Write-Host "Queue Depth: Unable to query" -ForegroundColor Yellow
    }
    
    # Check recent log entries
    Write-Host ""
    Write-Host "Recent API Log Entries:" -ForegroundColor Cyan
    Get-Content "R:\Nimbus.SecureBootCert\logs\api-*.log" -Tail 5 | ForEach-Object {
        if ($_ -match "\[ERR\]") {
            Write-Host $_ -ForegroundColor Red
        } elseif ($_ -match "\[WRN\]") {
            Write-Host $_ -ForegroundColor Yellow
        } else {
            Write-Host $_ -ForegroundColor Gray
        }
    }
    
    Write-Host ""
    Write-Host "Refreshing in $RefreshSeconds seconds... (Ctrl+C to exit)" -ForegroundColor DarkGray
    Start-Sleep -Seconds $RefreshSeconds
}
```

**Usage:**
```powershell
.\Monitor-QueueProcessor.ps1 -RefreshSeconds 30
```

---

## Integration Testing

### End-to-End Test

1. **Send report from client**
2. **Check client logs** ? ? AzureQueue success
3. **Wait 5-30 seconds**
4. **Check API logs** ? ? Message received and processed
5. **Check database** ? ? Report exists
6. **Check queue** ? ? Message count = 0

### Automated Test Script

```powershell
# Test-QueueProcessing.ps1
Write-Host "Testing Queue Processing..." -ForegroundColor Cyan

# Step 1: Get initial message count
$initialCount = az storage queue metadata show `
    --name secureboot-reports `
    --account-name secbootcert `
    --auth-mode login `
    --query approximateMessageCount `
    --output tsv

Write-Host "Initial queue depth: $initialCount"

# Step 2: Run client to send report
Write-Host "Running client to send report..."
Push-Location "C:\Program Files\SecureBootWatcher"
.\SecureBootWatcher.Client.exe
Pop-Location

# Step 3: Wait for queue to update
Start-Sleep -Seconds 10

# Step 4: Check queue depth increased
$newCount = az storage queue metadata show `
    --name secureboot-reports `
    --account-name secbootcert `
    --auth-mode login `
    --query approximateMessageCount `
    --output tsv

Write-Host "Queue depth after client: $newCount"

if ($newCount -gt $initialCount) {
    Write-Host "? Message enqueued successfully" -ForegroundColor Green
} else {
    Write-Host "? Message not enqueued!" -ForegroundColor Red
    exit 1
}

# Step 5: Wait for API to process (max 60 seconds)
Write-Host "Waiting for API to process message..."
$timeout = 60
$elapsed = 0

while ($elapsed -lt $timeout) {
    $currentCount = az storage queue metadata show `
        --name secureboot-reports `
        --account-name secbootcert `
        --auth-mode login `
        --query approximateMessageCount `
        --output tsv
    
    if ($currentCount -le $initialCount) {
        Write-Host "? Message processed successfully in $elapsed seconds" -ForegroundColor Green
        exit 0
    }
    
    Start-Sleep -Seconds 5
    $elapsed += 5
    Write-Host "  Waiting... ($elapsed/$timeout seconds)" -ForegroundColor Gray
}

Write-Host "? Message not processed within $timeout seconds!" -ForegroundColor Red
Write-Host "  Check API logs for errors" -ForegroundColor Yellow
exit 1
```

---

## Summary

**To verify Queue Processor is working:**

1. ? **Check API logs** for "Queue processor started successfully"
2. ? **Send test report** from client
3. ? **Watch for "Received N message(s) from queue"** in logs
4. ? **Verify "Successfully processed and deleted message"** in logs
5. ? **Check database** for new report entries
6. ? **Monitor queue depth** should return to 0

**Key indicators of healthy processing:**
- Queue depth stays low (< 100 messages)
- API logs show regular "Received X message(s)" entries
- Database receives new reports matching client sends
- No ERROR or WARNING logs related to queue processing

**For issues:**
1. Run `.\scripts\Diagnose-QueueCertificate.ps1`
2. Check `R:\Nimbus.SecureBootCert\logs\api-*.log` for errors
3. Verify `QueueProcessor.Enabled = true` in appsettings.json
4. Confirm RBAC role assigned to service principal
5. Test network connectivity to Azure Storage

---

**Related Documentation:**
- `docs\DEPLOYMENT_GUIDE.md` - Full deployment instructions
- `docs\TROUBLESHOOTING_PORTS.md` - Port and connectivity issues
- `scripts\Diagnose-QueueCertificate.ps1` - Certificate diagnostics
- `scripts\Assign-StorageQueueRole.ps1` - RBAC role assignment
