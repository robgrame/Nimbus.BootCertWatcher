# Retry and Resilience Configuration Guide

This document explains the retry and failover mechanisms in the SecureBootWatcher client sink system.

---

## Overview

The client implements a **robust retry mechanism** with configurable attempts, delays, and exponential backoff for each sink before moving to the next one in the priority order.

### Key Features

- ? **Configurable retry attempts** per sink (default: 3)
- ? **Configurable delay between retries** (default: 5 minutes)
- ? **Optional exponential backoff** (doubles delay each retry)
- ? **Priority-based sink ordering** with failover
- ? **Detailed logging** of each retry attempt
- ? **Cancellation support** for graceful shutdown

---

## Configuration Parameters

### SinkOptions

```json
{
  "Sinks": {
    "ExecutionStrategy": "StopOnFirstSuccess",
    "SinkPriority": "WebApi,AzureQueue,FileShare",
    "MaxRetryAttempts": 3,
    "RetryDelay": "00:05:00",
    "UseExponentialBackoff": false
  }
}
```

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `MaxRetryAttempts` | `int` | `3` | Number of retry attempts per sink before moving to next |
| `RetryDelay` | `TimeSpan` | `00:05:00` | Delay between retry attempts |
| `UseExponentialBackoff` | `bool` | `false` | Whether to double delay on each retry |

---

## Retry Behavior

### Example Flow (MaxRetryAttempts = 3, RetryDelay = 5 min)

```
???????????????????????????????????????????????????????????
? Report Ready to Send                                     ?
???????????????????????????????????????????????????????????
                          ?
                          ?
???????????????????????????????????????????????????????????
? Sink 1 (WebApi) - Priority 1                            ?
???????????????????????????????????????????????????????????
? Attempt 1: FAIL ? Wait 5 min                            ?
? Attempt 2: FAIL ? Wait 5 min                            ?
? Attempt 3: FAIL ? Wait 5 min                            ?
? Attempt 4 (final): FAIL ? Move to next sink             ?
???????????????????????????????????????????????????????????
                          ?
                          ?
???????????????????????????????????????????????????????????
? Sink 2 (AzureQueue) - Priority 2 (Fallback)            ?
???????????????????????????????????????????????????????????
? Attempt 1: SUCCESS ?                                    ?
? ? Stop (StopOnFirstSuccess strategy)                    ?
???????????????????????????????????????????????????????????
```

**Total time for this example:** ~15 minutes (3 retries × 5 min delay)

---

## Configuration Examples

### 1. **Aggressive Retry (Fast Failover)**

For environments where quick failover is more important than persistence:

```json
{
  "Sinks": {
    "MaxRetryAttempts": 1,
    "RetryDelay": "00:01:00",
    "UseExponentialBackoff": false
  }
}
```

- **1 retry** per sink
- **1 minute** between attempts
- **Total failover time:** ~2 minutes per sink

### 2. **Conservative Retry (Maximum Persistence)**

For critical environments where report delivery is paramount:

```json
{
  "Sinks": {
    "MaxRetryAttempts": 5,
    "RetryDelay": "00:10:00",
    "UseExponentialBackoff": false
  }
}
```

- **5 retries** per sink
- **10 minutes** between attempts
- **Total failover time:** ~50 minutes per sink

### 3. **Exponential Backoff (Smart Retry)**

For balancing speed and persistence with intelligent delay scaling:

```json
{
  "Sinks": {
    "MaxRetryAttempts": 4,
    "RetryDelay": "00:02:00",
    "UseExponentialBackoff": true
  }
}
```

**Delay progression:**
- Attempt 1: Immediate
- Attempt 2: 2 minutes (base delay)
- Attempt 3: 4 minutes (2 × base)
- Attempt 4: 8 minutes (4 × base)
- Attempt 5: 16 minutes (8 × base)

**Total failover time:** ~30 minutes per sink

**Note:** Max delay capped at 30 minutes to prevent excessive waits.

### 4. **Production Recommended**

Balanced configuration for production environments:

```json
{
  "Sinks": {
    "ExecutionStrategy": "StopOnFirstSuccess",
    "SinkPriority": "WebApi,AzureQueue,FileShare",
    "MaxRetryAttempts": 3,
    "RetryDelay": "00:05:00",
    "UseExponentialBackoff": false,
    "EnableWebApi": true,
    "EnableAzureQueue": true,
    "EnableFileShare": false
  }
}
```

**Why this configuration:**
- **3 retries** gives reasonable persistence without excessive delay
- **5 minute delay** allows transient issues to resolve
- **Fixed delay** (no backoff) for predictable timing
- **WebApi primary** for real-time ingestion
- **AzureQueue fallback** for guaranteed delivery if API is down

---

## Logging Output

### Successful After Retry

```
[16:50:20 INF] Sending report using strategy: StopOnFirstSuccess. Enabled sinks: WebApiReportSink, AzureQueueReportSink. Retry config: 3 attempts, 00:05:00 delay
[16:50:20 DBG] Attempting to send report to WebApi...
[16:50:23 WRN] Attempt 1/4 failed for WebApi: Unable to connect to the remote server. Retrying in 00:05:00...
[16:55:23 INF] Retry attempt 2/4 for WebApi after 00:05:00...
[16:55:26 INF] ? Successfully sent report to WebApi (after 2 attempts)
[16:55:26 INF] StopOnFirstSuccess strategy: stopping after first successful sink.
[16:55:26 INF] Report delivery summary: 1 succeeded, 0 failed (total attempts: 2).
```

### Failover to Next Sink

```
[16:50:20 INF] Sending report using strategy: StopOnFirstSuccess. Enabled sinks: WebApiReportSink, AzureQueueReportSink. Retry config: 3 attempts, 00:05:00 delay
[16:50:20 DBG] Attempting to send report to WebApi...
[16:50:23 WRN] Attempt 1/4 failed for WebApi: Unable to connect to the remote server. Retrying in 00:05:00...
[16:55:23 INF] Retry attempt 2/4 for WebApi after 00:05:00...
[16:55:26 WRN] Attempt 2/4 failed for WebApi: Unable to connect to the remote server. Retrying in 00:05:00...
[17:00:26 INF] Retry attempt 3/4 for WebApi after 00:05:00...
[17:00:29 WRN] Attempt 3/4 failed for WebApi: Unable to connect to the remote server. Retrying in 00:05:00...
[17:05:29 INF] Retry attempt 4/4 for WebApi after 00:05:00...
[17:05:32 ERR] ? All 4 attempts failed for WebApi. Moving to next sink.
[17:05:32 DBG] Attempting to send report to AzureQueue...
[17:05:35 INF] ? Successfully sent report to AzureQueue
[17:05:35 INF] StopOnFirstSuccess strategy: stopping after first successful sink.
[17:05:35 INF] Report delivery summary: 1 succeeded, 1 failed (total attempts: 5).
```

### Exponential Backoff

```
[16:50:20 INF] Sending report using strategy: StopOnFirstSuccess. Enabled sinks: WebApiReportSink. Retry config: 3 attempts, 00:02:00 delay (exponential backoff)
[16:50:20 DBG] Attempting to send report to WebApi...
[16:50:23 WRN] Attempt 1/4 failed for WebApi: Connection timeout. Retrying in 00:02:00...
[16:52:23 INF] Retry attempt 2/4 for WebApi after 00:02:00...
[16:52:26 WRN] Attempt 2/4 failed for WebApi: Connection timeout. Retrying in 00:04:00...
[16:56:26 INF] Retry attempt 3/4 for WebApi after 00:04:00...
[16:56:29 WRN] Attempt 3/4 failed for WebApi: Connection timeout. Retrying in 00:08:00...
[17:04:29 INF] Retry attempt 4/4 for WebApi after 00:08:00...
[17:04:32 INF] ? Successfully sent report to WebApi (after 4 attempts)
```

---

## Best Practices

### 1. **Choose Appropriate Retry Count**

- **Low retry count (1-2):** For non-critical environments or when fast failover is preferred
- **Medium retry count (3-4):** Recommended for most production scenarios
- **High retry count (5+):** Only for critical systems where report loss is unacceptable

### 2. **Configure Delay Based on Expected Recovery Time**

- **Network transients:** 1-2 minutes usually sufficient
- **Service restarts:** 5-10 minutes recommended
- **Planned maintenance:** Consider longer delays (15-30 min) or disable retries temporarily

### 3. **Use Exponential Backoff for:**

- **Rate-limited APIs** (reduces pressure on recovering services)
- **Cloud services** with throttling
- **Distributed systems** with cascading failures

### 4. **Avoid Exponential Backoff for:**

- **Local network issues** (fixed delay more predictable)
- **Time-sensitive reports** (exponential delay too slow)
- **Simple failover scenarios** (complexity not needed)

### 5. **Sink Priority Recommendations**

**Development/Testing:**
```json
"SinkPriority": "WebApi,FileShare,AzureQueue"
```
- Direct API for immediate feedback
- Local file backup for debugging
- Azure Queue as last resort

**Production (Cloud-Hosted API):**
```json
"SinkPriority": "WebApi,AzureQueue,FileShare"
```
- API for real-time processing
- Azure Queue for guaranteed delivery
- File share for disaster recovery

**Production (On-Prem API):**
```json
"SinkPriority": "FileShare,WebApi,AzureQueue"
```
- File share most reliable in on-prem
- API for real-time when available
- Azure Queue for cloud backup

**Air-Gapped Environments:**
```json
"SinkPriority": "FileShare"
```
- Only enable FileShare sink
- Disable retries (file writes usually succeed immediately)

---

## Troubleshooting

### Retries Taking Too Long

**Problem:** Client taking 15+ minutes to deliver report

**Solutions:**
1. Reduce `MaxRetryAttempts`
2. Reduce `RetryDelay`
3. Reorder `SinkPriority` to put most reliable sink first
4. Disable slow/unreliable sinks

### Reports Not Delivered Despite Retries

**Problem:** All retries exhausted for all sinks

**Check:**
1. Network connectivity to all sinks
2. API/Storage credentials and authentication
3. Firewall rules
4. Service health of destination endpoints

**Temporary Fix:**
```json
{
  "Sinks": {
    "MaxRetryAttempts": 10,
    "RetryDelay": "00:30:00"
  }
}
```

### Client Hanging on Retry

**Problem:** Client appears frozen during retry wait

**This is normal!** The client is sleeping between retry attempts. Check logs to confirm:

```
[WRN] Attempt 2/4 failed for WebApi. Retrying in 00:05:00...
```

To reduce perceived "hang":
- Lower `RetryDelay`
- Enable multiple sinks for faster failover
- Use shorter `HttpTimeout` for WebApi

### Cancellation During Retry

**Problem:** Client doesn't stop immediately when cancelled

**Expected behavior:** Client waits for current retry delay to complete before cancelling.

**To improve:**
```json
{
  "Sinks": {
    "RetryDelay": "00:01:00"
  }
}
```

Shorter delays = faster cancellation response.

---

## Performance Considerations

### Impact on Report Delivery Time

| Scenario | Config | Time to Success |
|----------|--------|-----------------|
| **Immediate Success** | Any | < 5 seconds |
| **Success After 1 Retry** | `RetryDelay: 00:05:00` | ~5 minutes |
| **Success After 3 Retries** | `RetryDelay: 00:05:00` | ~15 minutes |
| **Failover to 2nd Sink** | `MaxRetries: 3, RetryDelay: 00:05:00` | ~15-20 minutes |
| **All Sinks Exhausted** | `MaxRetries: 3, RetryDelay: 00:05:00`, 2 sinks | ~30-35 minutes |

### Memory and CPU Usage

- **Retry logic:** Negligible CPU/memory overhead
- **Delay mechanism:** Uses `Task.Delay` (async, non-blocking)
- **Logs:** Serilog buffering may use ~1-5 MB during retries

### Network Traffic

- **Retry attempts:** Same payload size as initial attempt
- **Exponential backoff:** Reduces overall network pressure
- **Multiple sinks:** May double/triple traffic if using "TryAll" strategy

---

## Related Configuration

### Complementary Settings

```json
{
  "SecureBootWatcher": {
    "RegistryPollInterval": "00:30:00",
    "EventQueryInterval": "00:30:00",
    "Sinks": {
      "MaxRetryAttempts": 3,
      "RetryDelay": "00:05:00",
      "WebApi": {
        "HttpTimeout": "00:00:30"
      },
      "AzureQueue": {
        "MaxSendRetryCount": 5
      }
    }
  }
}
```

**Relationships:**
- `HttpTimeout` should be < `RetryDelay` to detect failures quickly
- `AzureQueue.MaxSendRetryCount` is **internal Azure SDK retries** (separate from sink-level retries)
- Poll intervals should be >= total retry time to avoid overlapping runs

---

## Summary

The retry mechanism provides **robust, configurable resilience** for report delivery:

? **Configurable retry attempts and delays**  
? **Exponential backoff for intelligent retries**  
? **Priority-based sink ordering with failover**  
? **Detailed logging for troubleshooting**  
? **Graceful cancellation support**  

**Recommended Production Config:**
- **3 retry attempts** (balance of persistence and speed)
- **5 minute delay** (allows transient issues to resolve)
- **Fixed delay** (predictable timing)
- **WebApi ? AzureQueue** priority (real-time + guaranteed delivery)

This ensures reports are delivered even during temporary outages while maintaining reasonable performance.
