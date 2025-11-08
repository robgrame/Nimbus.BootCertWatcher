# Statistical Anomaly Detection

## Overview

The Secure Boot Certificate Watcher now includes a statistical anomaly detection system that automatically identifies unusual patterns in device behavior, certificate management, and reporting patterns across your fleet.

## Features

### Anomaly Detection Algorithms

The system uses statistical analysis and pattern recognition to detect the following anomaly types:

#### 1. Reporting Frequency Anomalies
Detects devices that report too frequently or infrequently compared to fleet averages.

- **Method**: Statistical analysis using mean and standard deviation
- **Threshold**: 2.5 standard deviations from mean
- **Use Case**: Identify devices with malfunctioning clients or connectivity issues

#### 2. Deployment State Anomalies
Identifies devices stuck in non-deployed states.

- **Pending State**: Flags devices in "Pending" for 3+ consecutive reports
- **Error State**: Flags devices in "Error" for 2+ reports
- **Use Case**: Identify devices requiring manual intervention

#### 3. Device Inactivity Anomalies
Detects devices that have stopped reporting.

- **Threshold**: No reports for 14+ days
- **Severity**: Increases based on days since last report (normalized to 30 days)
- **Use Case**: Identify offline or decommissioned devices

### Automatic Detection

The anomaly detection service runs automatically in the background:

- **Frequency**: Every 60 minutes
- **Startup Delay**: 5 minutes after API starts
- **Processing**: Asynchronous to avoid impacting API performance

### Manual Scanning

Administrators can trigger manual scans at any time:

1. Navigate to **Anomalies** page
2. Click **Run Scan** button
3. View results immediately

## API Endpoints

### Get Active Anomalies
```
GET /api/Anomalies
```
Returns all active (unresolved) anomalies, ordered by severity.

**Response:**
```json
[
  {
    "id": "guid",
    "deviceId": "guid",
    "deviceName": "LAPTOP-01",
    "type": "ReportingFrequency",
    "description": "Excessive reporting detected...",
    "severity": 0.85,
    "detectedAtUtc": "2025-11-07T10:30:00Z",
    "status": "Active"
  }
]
```

### Get Specific Anomaly
```
GET /api/Anomalies/{id}
```
Returns details for a specific anomaly.

### Trigger Manual Scan
```
POST /api/Anomalies/scan
```
Triggers an immediate anomaly detection scan. Returns newly detected anomalies.

### Resolve Anomaly
```
POST /api/Anomalies/{id}/resolve
Content-Type: application/json

{
  "resolvedBy": "admin@example.com"
}
```
Marks an anomaly as resolved.

## Web Dashboard

### Anomalies Page

Access the anomalies dashboard at: `/Anomalies/List`

**Features:**
- Statistics cards showing anomaly counts by severity
- Sortable table of active anomalies
- Direct links to affected devices
- One-click resolution
- Information panel explaining anomaly types

**Severity Levels:**
- **High** (≥70%): Red badge - Critical issues requiring immediate attention
- **Medium** (40-69%): Yellow badge - Important issues to investigate
- **Low** (<40%): Blue badge - Minor issues for monitoring

### Dashboard Integration

The main dashboard displays an alert banner when anomalies are detected:

```
⚠️ Anomalies Detected
5 active anomalies detected. View details →
```

## Database Schema

### Anomalies Table

```sql
CREATE TABLE [Anomalies] (
    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
    [DeviceId] uniqueidentifier NOT NULL,
    [AnomalyType] nvarchar(64) NOT NULL,
    [Description] nvarchar(1024) NOT NULL,
    [Severity] float NOT NULL,
    [DetectedAtUtc] datetimeoffset NOT NULL,
    [Status] nvarchar(64) NOT NULL,
    [ResolvedBy] nvarchar(256) NULL,
    [ResolvedAtUtc] datetimeoffset NULL,
    [Metadata] nvarchar(max) NULL,
    CONSTRAINT [FK_Anomalies_Devices] FOREIGN KEY ([DeviceId]) 
        REFERENCES [Devices]([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_Anomalies_DetectedAtUtc] ON [Anomalies] ([DetectedAtUtc]);
CREATE INDEX [IX_Anomalies_Status] ON [Anomalies] ([Status]);
```

## Configuration

### Background Service Settings

The anomaly detection service can be configured in `appsettings.json`:

```json
{
  "AnomalyDetection": {
    "Enabled": true,
    "ScanIntervalMinutes": 60,
    "StartupDelayMinutes": 5
  }
}
```

**Note**: Currently, the service is always enabled. Configuration support can be added if needed.

## Monitoring

### Logging

The service logs anomaly detection activity:

```
[INF] Running anomaly detection scan
[INF] Detected 3 new anomalies
[INF] Anomaly detection completed. Found 3 new anomalies
```

### Health Checks

Monitor the background service status through:
- Application logs
- Health check endpoint: `/health`

## Best Practices

### Resolution Workflow

1. **Review**: Click on device name to view full device details
2. **Investigate**: Check recent reports and certificate status
3. **Action**: Take corrective action (restart client, fix configuration, etc.)
4. **Resolve**: Mark anomaly as resolved after verification

### False Positives

The statistical thresholds are designed to minimize false positives:
- 2.5σ threshold catches ~99% of normal behavior
- Consecutive report checks prevent transient issues
- Adjustable severity scoring for prioritization

### Fleet Management

For large fleets:
- Focus on high-severity anomalies first
- Set up alerts for critical anomalies (future enhancement)
- Review anomaly trends weekly
- Adjust monitoring thresholds as needed

## Architecture

### Components

```
┌─────────────────────────────────────────────────┐
│  AnomalyDetectionBackgroundService              │
│  • Runs every hour                              │
│  • Triggers detection scans                     │
└────────────────┬────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────┐
│  AnomalyDetectionService                        │
│  • Reporting frequency detection                │
│  • Deployment state detection                   │
│  • Inactivity detection                         │
│  • Statistical analysis                         │
└────────────────┬────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────┐
│  SecureBootDbContext                            │
│  • Anomalies table                              │
│  • Device correlation                           │
└─────────────────────────────────────────────────┘
```

### Data Flow

1. Background service triggers scan
2. Service queries device/report data from database
3. Statistical analysis performed on aggregated data
4. Anomalies detected and stored
5. Web UI displays active anomalies
6. Admin resolves anomalies through UI
7. Resolved anomalies archived with resolution info

## Future Enhancements

Potential improvements for future versions:

- **ML Model Training**: Implement ML.NET models to enhance the current statistical analysis
- **Time Series Analysis**: Detect trends and seasonal patterns
- **Predictive Alerts**: Forecast potential issues before they occur
- **Custom Thresholds**: Per-fleet configuration of detection parameters
- **Alert Integration**: Email/SMS notifications for critical anomalies
- **Certificate Anomalies**: Detect unexpected certificate changes
- **Fleet-wide Anomalies**: Identify patterns affecting multiple devices
- **Historical Analysis**: Trend reports and anomaly history

## Troubleshooting

### No Anomalies Detected

If the system isn't detecting expected anomalies:

1. **Verify background service is running**:
   - Check logs for "Running anomaly detection scan"
   - Confirm 5-minute startup delay has passed

2. **Check device data**:
   - Ensure devices are reporting
   - Verify sufficient historical data (7+ days recommended)
   - Confirm deployment states are being set

3. **Review thresholds**:
   - Statistical thresholds may need adjustment
   - Small fleets (<5 devices) may not trigger statistical anomalies

### High False Positive Rate

If too many false positives are detected:

1. Increase statistical threshold (2.5σ → 3.0σ)
2. Adjust consecutive report thresholds
3. Add device/fleet exclusion rules (future enhancement)

### Performance Issues

For large fleets (>1000 devices):

1. Consider increasing scan interval
2. Monitor database query performance
3. Add database indexes if needed
4. Review EF Core query efficiency

## Security Considerations

- Anomaly data contains device information - ensure proper access controls
- Resolution actions are logged with user attribution
- API endpoints should be protected with authentication
- Consider RBAC for anomaly management permissions

## Support

For issues or questions:
- Review logs in `logs/api-{date}.log`
- Check GitHub Issues
- See main [README.md](../README.md) for contact information
