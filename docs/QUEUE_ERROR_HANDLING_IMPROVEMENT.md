# Queue Processor Error Handling Improvements

## Problem

The `QueueProcessorService` was generating excessive error logs when it couldn't connect to Azure Queue Storage due to authorization/authentication failures. This resulted in:

- **Log flooding**: Error logged every 10 seconds indefinitely
- **Alert fatigue**: Operations teams overwhelmed with repeated alerts
- **Difficult troubleshooting**: Hard to identify when the issue started or was resolved
- **Resource waste**: Unnecessary API calls to Azure Storage

## Solution

Implemented intelligent error handling with:

### 1. **Exponential Backoff**

Progressive retry delays to reduce load on Azure Storage and avoid rate limiting:

| Attempt | Delay |
|---------|-------|
| 1 | 10 seconds |
| 2 | 20 seconds |
| 3 | 40 seconds |
| 4 | 80 seconds |
| 5 | 160 seconds |
| 6+ | 300 seconds (5 minutes max) |

### 2. **Intelligent Logging Throttling**

Authorization/Authentication errors are logged:
- **First occurrence**: Full error details logged
- **Subsequent errors**: Logged only every **15 minutes**
- **Between intervals**: Logged as `Debug` level (won't appear in production logs)

This reduces log volume while maintaining visibility into the issue.

### 3. **Error Recovery Detection**

When connection is restored:
- Logs an informational message: `"Queue connection restored after X consecutive errors"`
- Resets error counters and backoff timers
- Marks service as healthy

### 4. **Health Status Tracking**

New public properties for monitoring:

```csharp
public bool IsHealthy { get; }                      // Current health status
public DateTime LastSuccessfulOperation { get; }    // Last successful queue operation
public int ConsecutiveErrors { get; }               // Count of consecutive failures
```

### 5. **Health Check Endpoint**

New API endpoint: `GET /api/QueueHealth/status`

**Response when healthy:**
```json
{
  "enabled": true,
  "isHealthy": true,
  "lastSuccessfulOperation": "2025-01-11T10:30:00Z",
  "consecutiveErrors": 0,
  "timeSinceLastSuccess": "00:00:15",
  "status": "Healthy"
}
```

**Response when degraded:**
```json
{
  "enabled": true,
  "isHealthy": false,
  "lastSuccessfulOperation": "2025-01-11T08:00:00Z",
  "consecutiveErrors": 45,
  "timeSinceLastSuccess": "02:30:00",
  "status": "Degraded"
}
```

**Response when disabled:**
```json
{
  "enabled": false,
  "message": "Queue processor is not enabled or not registered"
}
```

## Error Handling by Type

### Authorization Failure (403)

**First error log:**
```
[ERR] Authorization failed for queue secureboot-reports. 
Check that the service principal or managed identity has 'Storage Queue Data Contributor' role. 
Consecutive errors: 1. This error will be logged again in 15 minutes if it persists.
```

**Subsequent errors (within 15 min):**
```
[DBG] Authorization failed for queue secureboot-reports (error #10, suppressing detailed log)
```

**After 15 minutes:**
```
[ERR] Authorization failed for queue secureboot-reports. 
Check that the service principal or managed identity has 'Storage Queue Data Contributor' role. 
Consecutive errors: 90. This error will be logged again in 15 minutes if it persists.
```

### Authentication Failure

Similar pattern, but with different diagnostic message:

```
[ERR] Authentication failed for queue secureboot-reports. 
Check authentication configuration (TenantId, ClientId, Certificate/Secret). 
Consecutive errors: 1. This error will be logged again in 15 minutes if it persists.
```

### Queue Not Found (404)

```
[WRN] Queue secureboot-reports does not exist. Will retry periodically.
```

Only logged on first occurrence, then suppressed.

### Other Errors

Logged on first occurrence, then every 10th error:

```
[ERR] Failed to receive messages from queue secureboot-reports (consecutive errors: 1)
[ERR] Failed to receive messages from queue secureboot-reports (consecutive errors: 10)
[ERR] Failed to receive messages from queue secureboot-reports (consecutive errors: 20)
```

## Monitoring & Alerting

### Recommended Azure Monitor Alerts

1. **Queue Processor Unhealthy**
   - **Condition**: `GET /api/QueueHealth/status` returns `isHealthy: false`
   - **For**: More than 30 minutes
   - **Action**: Alert operations team

2. **No Successful Operations**
   - **Condition**: `timeSinceLastSuccess > 1 hour`
   - **Action**: Alert operations team + check Azure RBAC

3. **High Consecutive Errors**
   - **Condition**: `consecutiveErrors > 50`
   - **Action**: Investigate Azure Storage configuration

### Log Queries

**Check for authorization issues:**
```kusto
traces
| where message contains "Authorization failed"
| where severityLevel >= 3  // Error level
| summarize count() by bin(timestamp, 15m)
```

**Monitor recovery events:**
```kusto
traces
| where message contains "Queue connection restored"
| project timestamp, message
```

## Configuration

No configuration changes required. The service automatically:
- Detects error types
- Applies appropriate backoff
- Throttles logging
- Tracks health status

## Benefits

| Before | After |
|--------|-------|
| Log entry every 10 seconds | Log entry every 15 minutes (during outage) |
| ~8,640 error logs/day | ~96 error logs/day |
| Constant API calls | Exponential backoff (max 5 min) |
| No health visibility | Health check endpoint |
| Manual monitoring | Automated alerting possible |

## Troubleshooting

### Queue Processor Shows Degraded

1. **Check Health Endpoint**:
   ```bash
   curl https://your-api.azurewebsites.net/api/QueueHealth/status
   ```

2. **Check Consecutive Errors**:
   - High count (>50) = Persistent issue, needs investigation
   - Low count (<10) = Transient issue, may self-resolve

3. **Check Time Since Last Success**:
   - < 1 hour = Temporary blip
   - > 1 hour = Configuration issue likely

4. **Check Logs**:
   ```bash
   # Recent authorization errors
   az webapp log tail --name your-api-name --resource-group your-rg | grep "Authorization failed"
   ```

### Common Root Causes

**Authorization Failure (403)**:
- Missing role assignment: `Storage Queue Data Contributor`
- Role assignment not yet propagated (wait 5-10 minutes)
- Managed Identity not enabled on App Service
- Wrong Client ID specified for User-Assigned Managed Identity

**Authentication Failure**:
- Expired certificate
- Wrong TenantId/ClientId
- Certificate not accessible (permissions)
- Client Secret expired (App Registration)

**Queue Not Found (404)**:
- Queue name typo in configuration
- Queue doesn't exist in storage account
- Using wrong storage account

## Deployment

1. **Redeploy API** with updated `QueueProcessorService.cs`
2. **Verify health endpoint** works: `GET /api/QueueHealth/status`
3. **Monitor logs** for reduced error volume
4. **Set up alerts** based on health endpoint

No client changes required.

## Related Documentation

- [Queue Processor Monitoring](QUEUE_PROCESSOR_MONITORING.md)
- [Azure Storage Authentication](https://learn.microsoft.com/en-us/azure/storage/common/authorize-data-access)
- [Managed Identity](https://learn.microsoft.com/en-us/entra/identity/managed-identities-azure-resources/overview)

---

**Last Updated**: 2025-01-11  
**Version**: 1.4.0
