using SecureBootWatcher.Shared.Models;
using SecureBootWatcher.Shared.Storage;

namespace SecureBootDashboard.Web.Services;

public interface ISecureBootApiClient
{
    Task<IReadOnlyList<ReportSummary>> GetRecentReportsAsync(int limit = 50, CancellationToken cancellationToken = default);
    Task<ReportDetail?> GetReportDetailAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
    
    // New methods for device management
    Task<IReadOnlyList<DeviceSummary>> GetDevicesAsync(CancellationToken cancellationToken = default);
    Task<DeviceDetail?> GetDeviceAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReportHistoryItem>> GetDeviceReportsAsync(Guid deviceId, int limit = 50, CancellationToken cancellationToken = default);
    
    // Anomaly detection methods
    Task<IReadOnlyList<AnomalyDetectionResult>> GetActiveAnomaliesAsync(CancellationToken cancellationToken = default);
    Task<AnomalyDetectionResult?> GetAnomalyAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AnomalyDetectionResult>> TriggerAnomalyScanAsync(CancellationToken cancellationToken = default);
    Task ResolveAnomalyAsync(Guid id, string resolvedBy, CancellationToken cancellationToken = default);
}

// DTOs for device endpoints
public sealed record DeviceSummary(
    Guid Id,
    string MachineName,
    string? DomainName,
    string? FleetId,
    string? Manufacturer,
    string? Model,
    DateTimeOffset FirstSeenUtc,
    DateTimeOffset LastSeenUtc,
    int ReportCount,
    string? LatestDeploymentState,
    DateTimeOffset? LatestReportDate);

public sealed record DeviceDetail(
    Guid Id,
    string MachineName,
    string? DomainName,
    string? UserPrincipalName,
    string? FleetId,
    string? Manufacturer,
    string? Model,
    string? FirmwareVersion,
    string? TagsJson,
    DateTimeOffset FirstSeenUtc,
    DateTimeOffset LastSeenUtc,
    IReadOnlyCollection<ReportHistoryItem> RecentReports);

public sealed record ReportHistoryItem(
    Guid ReportId,
    DateTimeOffset CreatedAtUtc,
    string? DeploymentState,
    string? ClientVersion);
