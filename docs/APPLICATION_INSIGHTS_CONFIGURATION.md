# Application Insights Configuration Guide

This guide explains how to configure Application Insights for **SecureBootDashboard** to monitor performance, track usage, and diagnose issues - even when deployed **outside Azure** (on-premises or other cloud providers).

## Overview

Application Insights provides:
- **Performance Monitoring**: Track response times, throughput, and dependencies
- **Usage Analytics**: Monitor user behavior, page views, and custom events
- **Error Tracking**: Capture exceptions and failed requests
- **Live Metrics**: Real-time monitoring dashboard
- **Custom Telemetry**: Track business-specific metrics

## Architecture

```
???????????????????????????????????????????????????????????????
?  SecureBootDashboard Components                              ?
???????????????????????????????????????????????????????????????
?  ???????????????  ???????????????  ????????????????????   ?
?  ?  Web (UI)   ?  ?  Api (REST) ?  ?  Client (Agent)  ?   ?
?  ?   Razor     ?  ?   ASP.NET   ?  ?  .NET Fx 4.8     ?   ?
?  ?   Pages     ?  ?   Core 10   ?  ?                  ?   ?
?  ???????????????  ???????????????  ????????????????????   ?
?         ?                ?                    ?             ?
?         ???????????????????????????????????????             ?
?                          ?                                   ?
?                          ?                                   ?
?              ?????????????????????????                       ?
?              ? Application Insights  ?                       ?
?              ?    Telemetry SDK      ?                       ?
?              ?????????????????????????                       ?
?                          ?                                   ?
????????????????????????????????????????????????????????????????
                           ?
                           ?
        ????????????????????????????????????????
        ?  Application Insights Service         ?
        ?  (Azure or On-Premises Collector)    ?
        ????????????????????????????????????????
                           ?
                           ?
        ????????????????????????????????????????
        ?  Azure Portal / Custom Dashboard      ?
        ?  - Performance metrics                ?
        ?  - Usage analytics                    ?
        ?  - Error tracking                     ?
        ?  - Custom queries (KQL)               ?
        ????????????????????????????????????????
```

---

## Configuration Options

### Option 1: Azure Application Insights (Recommended)

**Best for:**
- Azure-hosted deployments
- Cloud-first scenarios
- No infrastructure management

#### Step 1: Create Application Insights Resource

```bash
# Using Azure CLI
az monitor app-insights component create \
  --app secureboot-dashboard-insights \
  --location eastus \
  --resource-group rg-secureboot-prod \
  --application-type web

# Get connection string
az monitor app-insights component show \
  --app secureboot-dashboard-insights \
  --resource-group rg-secureboot-prod \
  --query connectionString --output tsv
```

**Output:**
```
InstrumentationKey=12345678-1234-1234-1234-123456789012;IngestionEndpoint=https://eastus-1.in.applicationinsights.azure.com/;LiveEndpoint=https://eastus.livediagnostics.monitor.azure.com/
```

#### Step 2: Configure Connection String

**Environment Variable (Recommended):**
```bash
# Linux/macOS
export APPLICATIONINSIGHTS_CONNECTION_STRING="InstrumentationKey=...;IngestionEndpoint=..."

# Windows PowerShell
$env:APPLICATIONINSIGHTS_CONNECTION_STRING="InstrumentationKey=...;IngestionEndpoint=..."

# Windows CMD
set APPLICATIONINSIGHTS_CONNECTION_STRING=InstrumentationKey=...;IngestionEndpoint=...
```

**Or in `appsettings.Production.json`:**
```json
{
  "ApplicationInsights": {
    "ConnectionString": "InstrumentationKey=12345678-1234-1234-1234-123456789012;IngestionEndpoint=https://eastus-1.in.applicationinsights.azure.com/;LiveEndpoint=https://eastus.livediagnostics.monitor.azure.com/"
  }
}
```

---

### Option 2: On-Premises Application Insights Gateway

**Best for:**
- Air-gapped environments
- Data sovereignty requirements
- Self-hosted infrastructure

#### Step 1: Deploy Application Insights Collector

Use **OpenTelemetry Collector** or **Application Insights Local Forwarder**:

```bash
# Docker deployment example
docker run -d \
  --name appinsights-collector \
  -p 55678:55678 \
  -p 4318:4318 \
  -e APPLICATIONINSIGHTS_CONNECTION_STRING="InstrumentationKey=local;IngestionEndpoint=http://localhost:4318/" \
  otel/opentelemetry-collector-contrib:latest
```

#### Step 2: Configure Custom Endpoint

```json
{
  "ApplicationInsights": {
    "ConnectionString": "InstrumentationKey=local;IngestionEndpoint=http://your-collector:4318/",
    "EndpointAddress": "http://your-collector:4318/v2/track"
  }
}
```

---

### Option 3: Hybrid (Development: Disabled, Production: Enabled)

**Development (`appsettings.Development.json`):**
```json
{
  "ApplicationInsights": {
    "ConnectionString": ""
  }
}
```

**Production (`appsettings.Production.json`):**
```json
{
  "ApplicationInsights": {
    "ConnectionString": "InstrumentationKey=...;IngestionEndpoint=..."
  }
}
```

---

## Component-Specific Configuration

### SecureBootDashboard.Web (Frontend)

**appsettings.json:**
```json
{
  "ApplicationInsights": {
    "ConnectionString": "",
    "EnableAdaptiveSampling": true,
    "EnablePerformanceCounterCollectionModule": true,
    "EnableQuickPulseMetricStream": true,
    "CloudRoleName": "SecureBootDashboard.Web"
  }
}
```

**What gets tracked:**
- Page view duration
- Server response times
- Failed requests (4xx, 5xx)
- Dependency calls to API
- Browser timing (if JavaScript SDK added)

---

### SecureBootDashboard.Api (Backend)

**appsettings.json:**
```json
{
  "ApplicationInsights": {
    "ConnectionString": "",
    "EnableAdaptiveSampling": true,
    "EnablePerformanceCounterCollectionModule": true,
    "EnableQuickPulseMetricStream": true,
    "CloudRoleName": "SecureBootDashboard.Api"
  }
}
```

**What gets tracked:**
- HTTP request duration
- SQL query performance (via EF Core)
- Azure Queue operations
- SignalR connection events
- Background service performance
- Custom events (e.g., "Device Cleanup Started")

---

### SecureBootWatcher.Client (Agent)

**Not yet implemented** - Consider adding for:
- Report submission success/failure
- Certificate enumeration performance
- Client errors and exceptions

**Future implementation:**
```csharp
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;

var config = TelemetryConfiguration.CreateDefault();
config.ConnectionString = "InstrumentationKey=...;IngestionEndpoint=...";
var telemetryClient = new TelemetryClient(config);

telemetryClient.TrackEvent("ReportSubmitted", 
    properties: new Dictionary<string, string> 
    { 
        { "DeviceName", Environment.MachineName },
        { "Version", clientVersion }
    });
```

---

## Key Metrics to Monitor

### Performance Metrics

| Metric | Query (Kusto/KQL) | Alert Threshold |
|--------|-------------------|-----------------|
| API Response Time (P95) | `requests \| where cloud_RoleName == "SecureBootDashboard.Api" \| summarize percentile(duration, 95) by bin(timestamp, 5m)` | > 500ms |
| Web Page Load Time | `pageViews \| summarize avg(duration) by name` | > 2s |
| Database Query Duration | `dependencies \| where type == "SQL" \| summarize avg(duration) by name` | > 100ms |
| Failed Requests | `requests \| where success == false \| summarize count() by resultCode` | > 10/min |

### Usage Metrics

| Metric | Query | Business Value |
|--------|-------|----------------|
| Active Devices | `customEvents \| where name == "ReportSubmitted" \| summarize dcount(tostring(customDimensions.DeviceName)) by bin(timestamp, 1d)` | Fleet coverage |
| Dashboard Users | `pageViews \| summarize dcount(user_Id) by bin(timestamp, 1d)` | Adoption rate |
| API Calls/Hour | `requests \| where cloud_RoleName == "SecureBootDashboard.Api" \| summarize count() by bin(timestamp, 1h)` | System load |

### Error Tracking

| Metric | Query | Action |
|--------|-------|--------|
| Exception Rate | `exceptions \| summarize count() by type` | Investigate top 3 |
| Queue Failures | `customEvents \| where name == "QueueProcessingFailed" \| summarize count()` | Check queue health |
| Certificate Validation Errors | `traces \| where message contains "Certificate validation failed" \| summarize count()` | Review mTLS config |

---

## Custom Telemetry Examples

### Track Device Cleanup Operation

**In `DeviceCleanupService.cs`:**
```csharp
using Microsoft.ApplicationInsights;

public class DeviceCleanupService
{
    private readonly TelemetryClient _telemetryClient;
    
    public DeviceCleanupService(TelemetryClient telemetryClient)
    {
        _telemetryClient = telemetryClient;
    }
    
    public async Task<int> CleanupInactiveDevicesAsync(int daysInactive)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            var deletedCount = await PerformCleanup(daysInactive);
            
            // Track success
            _telemetryClient.TrackEvent("DeviceCleanupCompleted",
                properties: new Dictionary<string, string>
                {
                    { "DaysInactive", daysInactive.ToString() },
                    { "DevicesDeleted", deletedCount.ToString() }
                },
                metrics: new Dictionary<string, double>
                {
                    { "Duration", (DateTime.UtcNow - startTime).TotalMilliseconds }
                });
            
            return deletedCount;
        }
        catch (Exception ex)
        {
            _telemetryClient.TrackException(ex,
                properties: new Dictionary<string, string>
                {
                    { "Operation", "DeviceCleanup" },
                    { "DaysInactive", daysInactive.ToString() }
                });
            
            throw;
        }
    }
}
```

### Track Secure Boot Compliance Status

**In `SecureBootReportsController.cs`:**
```csharp
[HttpPost]
public async Task<IActionResult> SubmitReport([FromBody] SecureBootStatusReport report)
{
    // ...existing validation...
    
    // Track deployment state distribution
    _telemetryClient.TrackMetric("SecureBootDeploymentState",
        value: (int)report.DeploymentState,
        properties: new Dictionary<string, string>
        {
            { "State", report.DeploymentState.ToString() },
            { "FleetId", report.Device.FleetId ?? "Unknown" }
        });
    
    // Track certificate issues
    if (report.DeploymentState != DeploymentState.Compliant)
    {
        _telemetryClient.TrackEvent("NonCompliantDeviceReported",
            properties: new Dictionary<string, string>
            {
                { "DeviceName", report.Device.MachineName },
                { "State", report.DeploymentState.ToString() },
                { "MissingCerts", string.Join(",", GetMissingCertificates(report)) }
            });
    }
    
    return Ok();
}
```

---

## Dashboard Queries (Kusto/KQL)

### 1. **Real-Time Dashboard Overview**

```kql
// Live metrics for last 5 minutes
requests
| where timestamp > ago(5m)
| where cloud_RoleName in ("SecureBootDashboard.Web", "SecureBootDashboard.Api")
| summarize 
    TotalRequests = count(),
    AvgDuration = avg(duration),
    P95Duration = percentile(duration, 95),
    FailedRequests = countif(success == false)
  by cloud_RoleName, bin(timestamp, 1m)
| render timechart
```

### 2. **Slowest API Endpoints**

```kql
requests
| where cloud_RoleName == "SecureBootDashboard.Api"
| where timestamp > ago(1d)
| summarize 
    Count = count(),
    AvgDuration = avg(duration),
    P95Duration = percentile(duration, 95),
    MaxDuration = max(duration)
  by name
| where P95Duration > 500 // Endpoints slower than 500ms
| order by P95Duration desc
| take 10
```

### 3. **Device Report Submission Trends**

```kql
customEvents
| where name == "ReportSubmitted"
| where timestamp > ago(7d)
| extend DeviceName = tostring(customDimensions.DeviceName)
| summarize ReportCount = count() by bin(timestamp, 1h)
| render timechart
```

### 4. **Exception Heatmap**

```kql
exceptions
| where timestamp > ago(24h)
| summarize Count = count() by type, bin(timestamp, 1h)
| render columnchart
```

---

## Alerts Configuration

### Critical Alerts

**1. High Error Rate**
```kql
requests
| where timestamp > ago(5m)
| where success == false
| summarize FailureRate = (count() * 100.0) / toscalar(requests | where timestamp > ago(5m) | count())
| where FailureRate > 5 // Alert if >5% failures
```

**Notification:** Email + SMS to on-call engineer

---

**2. API Latency Spike**
```kql
requests
| where cloud_RoleName == "SecureBootDashboard.Api"
| where timestamp > ago(10m)
| summarize P95Duration = percentile(duration, 95)
| where P95Duration > 1000 // Alert if P95 > 1s
```

**Notification:** Slack channel #alerts

---

**3. Database Connection Failures**
```kql
dependencies
| where type == "SQL"
| where success == false
| where timestamp > ago(5m)
| summarize FailureCount = count()
| where FailureCount > 10
```

**Notification:** PagerDuty incident

---

### Warning Alerts

**1. Queue Processing Delay**
```kql
customEvents
| where name == "QueueMessageProcessed"
| extend QueueDelay = todouble(customDimensions.DelayMs)
| where timestamp > ago(15m)
| summarize AvgDelay = avg(QueueDelay)
| where AvgDelay > 30000 // Warn if avg delay > 30s
```

---

**2. Low Device Report Volume**
```kql
customEvents
| where name == "ReportSubmitted"
| where timestamp > ago(1h)
| summarize ReportCount = count()
| where ReportCount < 10 // Warn if <10 reports/hour
```

---

## Data Retention & Cost Optimization

### Sampling Strategy

**Adaptive Sampling (Default):**
- Automatically adjusts sampling rate based on volume
- Keeps all errors and exceptions
- Reduces telemetry cost by 60-80%

**Fixed Sampling:**
```json
{
  "ApplicationInsights": {
    "EnableAdaptiveSampling": false,
    "SamplingSettings": {
      "MaxTelemetryItemsPerSecond": 5
    }
  }
}
```

### Data Retention

| Data Type | Default Retention | Extended Retention |
|-----------|-------------------|-------------------|
| Raw Telemetry | 90 days | Up to 730 days (paid) |
| Aggregated Metrics | 90 days | 90 days |
| Log Analytics | Configurable | Up to 2 years |

**Cost Estimate:**
- 1 GB ingested: ~$2.30/month
- Typical deployment: 5-10 GB/month = ~$12-25/month

---

## Privacy & Compliance

### Disable Personal Data Collection

**appsettings.json:**
```json
{
  "ApplicationInsights": {
    "EnableAuthenticationTrackingJavaScript": false,
    "DisableTelemetry": false,
    "RequestCollectionOptions": {
      "InjectResponseHeaders": false
    }
  }
}
```

### Exclude Sensitive Data

**Program.cs:**
```csharp
builder.Services.AddApplicationInsightsTelemetry(options =>
{
    // Exclude IP addresses
    options.EnableAdaptiveSampling = true;
});

builder.Services.AddApplicationInsightsTelemetryProcessor<SensitiveDataFilterTelemetryProcessor>();

// Custom processor
public class SensitiveDataFilterTelemetryProcessor : ITelemetryProcessor
{
    public void Process(ITelemetry item)
    {
        if (item is RequestTelemetry request)
        {
            // Remove IP address
            request.Context.Location.Ip = "0.0.0.0";
            
            // Redact query strings with passwords
            if (request.Url?.Query?.Contains("password") == true)
            {
                request.Url = new Uri(request.Url.GetLeftPart(UriPartial.Path));
            }
        }
    }
}
```

---

## Troubleshooting

### Issue: No Data in Application Insights

**Symptoms:**
- No telemetry visible in Azure Portal
- Logs show "Application Insights: Disabled"

**Solutions:**
1. Verify connection string is set:
   ```bash
   echo $APPLICATIONINSIGHTS_CONNECTION_STRING
   ```
2. Check firewall rules allow outbound HTTPS to:
   - `*.in.applicationinsights.azure.com`
   - `*.livediagnostics.monitor.azure.com`
3. Enable verbose logging:
   ```json
   {
     "Logging": {
       "ApplicationInsights": {
         "LogLevel": {
           "Default": "Debug"
         }
       }
     }
   }
   ```

---

### Issue: High Telemetry Volume

**Symptoms:**
- Unexpected costs
- Sampling rate very low (<10%)

**Solutions:**
1. Review top telemetry contributors:
   ```kql
   union *
   | where timestamp > ago(1d)
   | summarize Count = count() by itemType, cloud_RoleName
   | order by Count desc
   ```
2. Exclude noisy endpoints:
   ```csharp
   builder.Services.Configure<TelemetryConfiguration>(config =>
   {
       config.TelemetryProcessorChainBuilder
           .UseAdaptiveSampling(excludedTypes: "Event;Exception")
           .Build();
   });
   ```

---

## Testing Application Insights

### Local Testing

**PowerShell Script:**
```powershell
# Test-ApplicationInsights.ps1

$connectionString = $env:APPLICATIONINSIGHTS_CONNECTION_STRING

if ([string]::IsNullOrEmpty($connectionString)) {
    Write-Error "APPLICATIONINSIGHTS_CONNECTION_STRING not set"
    exit 1
}

Write-Host "Testing Application Insights connectivity..." -ForegroundColor Cyan

# Start application
Start-Process "dotnet" -ArgumentList "run --project SecureBootDashboard.Api" -NoNewWindow

Start-Sleep 10

# Generate test traffic
for ($i = 1; $i -le 20; $i++) {
    Invoke-RestMethod -Uri "https://localhost:5001/api/Devices" -SkipCertificateCheck
    Start-Sleep 1
}

Write-Host "? Test traffic generated" -ForegroundColor Green
Write-Host "  Check Azure Portal in 2-3 minutes for telemetry" -ForegroundColor Yellow
```

---

## Next Steps

1. ? **NuGet Packages Installed** - Application Insights SDK added
2. ? **Configuration Added** - appsettings.json updated
3. ? **Serilog Integration** - Logs forwarded to App Insights
4. ? **Set Connection String** - Configure for your environment
5. ? **Create Alerts** - Set up critical monitoring
6. ? **Build Dashboard** - Custom KQL queries in Azure Portal

---

**Status**: ? Configuration Complete  
**Version**: 1.12.0  
**Last Updated**: 2025-01-23
