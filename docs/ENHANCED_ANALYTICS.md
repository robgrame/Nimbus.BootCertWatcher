# Enhanced Analytics (30/60/90 Day Trends) - Implementation Guide

## Overview

This document describes the enhanced analytics feature that provides 30/60/90 day trend analysis for compliance and device enrollment metrics.

## Features

### 1. Historical Compliance Trends
The dashboard now supports viewing compliance trends over multiple time periods:
- **7 days** - Short-term compliance tracking
- **30 days** - Monthly compliance analysis  
- **60 days** - Bi-monthly compliance trends
- **90 days** - Long-term compliance patterns

### 2. Period Selection
The dashboard home page includes period selector buttons in the "Compliance Trend" chart header:
```
[7d] [30d] [60d] [90d]
```
Clicking any button reloads the page with the selected period, and the active period is highlighted.

### 3. Real Historical Data
The trend chart displays actual historical compliance data calculated from the reports table:
- For each day in the selected period, the system determines each device's deployment state based on the most recent report as of that date
- Calculates daily snapshots of:
  - Total devices
  - Deployed devices (compliant)
  - Pending devices
  - Error devices
  - Unknown devices
  - Compliance percentage

## API Endpoints

### GET /api/Analytics/compliance-trend

Retrieves historical compliance trend data.

**Query Parameters:**
- `days` (int, required): Number of days to retrieve (1-365)

**Response:**
```json
{
  "days": 30,
  "snapshots": [
    {
      "date": "2025-10-08T00:00:00Z",
      "totalDevices": 100,
      "deployedDevices": 85,
      "pendingDevices": 10,
      "errorDevices": 3,
      "unknownDevices": 2,
      "compliancePercentage": 85.0
    },
    // ... more daily snapshots
  ]
}
```

### GET /api/Analytics/enrollment-trend

Retrieves device enrollment trend showing new devices over time.

**Query Parameters:**
- `days` (int, required): Number of days to retrieve (1-365)

**Response:**
```json
{
  "days": 30,
  "dataPoints": [
    {
      "date": "2025-10-08T00:00:00Z",
      "newDevices": 5
    },
    // ... more daily data points
  ]
}
```

## Implementation Details

### Backend (API)

#### AnalyticsController
New controller at `/api/Analytics` with two main endpoints:

1. **GetComplianceTrendAsync**: Calculates daily compliance snapshots
   - Queries all reports within the date range
   - For each day, determines device states based on most recent report
   - Returns aggregated metrics per day

2. **GetEnrollmentTrendAsync**: Tracks new device enrollments
   - Groups devices by creation date
   - Returns count of new devices per day

### Frontend (Web Dashboard)

#### UI Components
- **Period Selector**: Button group in chart header with 7d/30d/60d/90d options
- **Active State**: Highlights currently selected period with `btn-light` class
- **Chart Update**: Chart.js line chart displays historical data

#### Page Model
- **TrendDays Property**: Binds to query parameter `?trendDays=30`
- **API Integration**: Calls `GetComplianceTrendAsync` with selected period
- **Fallback**: If API call fails, displays simulated data for graceful degradation

## Usage Examples

### Viewing 30-Day Trends
1. Navigate to dashboard home page
2. Click the "30d" button in the Compliance Trend chart header
3. Page reloads with URL: `/?trendDays=30`
4. Chart displays 30 days of historical compliance data

### Comparing Different Periods
1. Start with 7-day view to see recent changes
2. Switch to 30-day view to identify weekly patterns
3. Switch to 90-day view to observe long-term trends

### API Integration Example

```csharp
// C# API Client
var response = await httpClient.GetFromJsonAsync<ComplianceTrendResponse>(
    "/api/Analytics/compliance-trend?days=60");

if (response != null)
{
    foreach (var snapshot in response.Snapshots)
    {
        Console.WriteLine($"{snapshot.Date:yyyy-MM-dd}: {snapshot.CompliancePercentage:F1}% compliant");
    }
}
```

## Testing

### Unit Tests
Comprehensive test suite in `AnalyticsControllerTests.cs`:
- ✅ Empty database scenarios
- ✅ Device state transitions over time
- ✅ Multiple time periods (7/30/60/90 days)
- ✅ Invalid input validation
- ✅ Enrollment trend calculations

All 10 tests pass successfully.

### Manual Testing
1. Ensure devices and reports exist in the database
2. Navigate to dashboard home page
3. Test each period selector button
4. Verify chart updates with correct data
5. Check browser console for JavaScript errors

## Performance Considerations

### Database Queries
- The analytics endpoint queries the Reports table with date range filters
- For large datasets, consider adding indexes on:
  - `Reports.CreatedAtUtc`
  - `Reports.DeviceId`
  - `Devices.CreatedAtUtc`

### Caching
Consider implementing caching for analytics data:
```csharp
[ResponseCache(Duration = 300, VaryByQueryKeys = new[] { "days" })]
public async Task<ActionResult<ComplianceTrendResponse>> GetComplianceTrendAsync(...)
```

### Optimization Tips
- For 90-day queries on large datasets, consider pre-calculating daily snapshots
- Use database views or materialized views for frequently accessed metrics
- Implement pagination for very large result sets

## Future Enhancements

Potential improvements for v1.3+:
- [ ] Export trend data to Excel/CSV
- [ ] Custom date range selection (e.g., "Last 45 days")
- [ ] Compare multiple time periods side-by-side
- [ ] Trend forecasting with machine learning
- [ ] Fleet-specific trend analysis
- [ ] Email reports with weekly/monthly trends

## Troubleshooting

### Chart Not Updating
**Issue**: Period selector doesn't update the chart
**Solution**: Check browser console for JavaScript errors; verify API endpoint returns data

### API Returns 500 Error
**Issue**: Database connection error
**Solution**: Verify SQL Server connection string in `appsettings.json`

### Empty Chart
**Issue**: No data displayed even with reports in database
**Solution**: Check that reports have valid `CreatedAtUtc` timestamps

### Performance Issues with 90-Day Query
**Issue**: Slow response for 90-day analytics
**Solution**: Add database indexes, implement caching, or pre-calculate snapshots

## Related Documentation

- [Dashboard Charts Implementation](DASHBOARD_CHARTS_SPLASH_IMPLEMENTATION.md)
- [API Architecture](WEB_IMPLEMENTATION_SUMMARY.md)
- [Database Schema](COMPLETE_IMPLEMENTATION.md)

## Conclusion

The enhanced analytics feature provides powerful insights into compliance trends over multiple time periods, enabling IT teams to:
- Track compliance progress over time
- Identify seasonal patterns or anomalies
- Make data-driven decisions about certificate deployments
- Monitor fleet health with historical context
