using SecureBootWatcher.Shared.Storage;
using SecureBootWatcher.Shared.Models;

namespace SecureBootDashboard.Web.Services;

public interface ISecureBootApiClient
{
    Task<IReadOnlyList<ReportSummary>> GetRecentReportsAsync(int limit = 50, CancellationToken cancellationToken = default);
    Task<ReportDetail?> GetReportDetailAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a complete report with deserialized certificate data
    /// </summary>
    Task<SecureBootStatusReport?> GetReportAsync(Guid id, CancellationToken cancellationToken = default);
    
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
    
    // New methods for device management
    Task<IReadOnlyList<DeviceSummary>> GetDevicesAsync(CancellationToken cancellationToken = default);
    Task<DeviceDetail?> GetDeviceAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReportHistoryItem>> GetDeviceReportsAsync(Guid deviceId, int limit = 50, CancellationToken cancellationToken = default);

    // Device Cleanup methods
    Task<CleanupConfigResponse?> GetCleanupConfigAsync(CancellationToken cancellationToken = default);
    Task<CleanupPreviewResponse?> GetCleanupPreviewAsync(int? daysThreshold = null, CancellationToken cancellationToken = default);
    Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default);
    
    // Generic HTTP methods for any API endpoint
    Task<T?> GetAsync<T>(string requestUri, CancellationToken cancellationToken = default);
    Task<T?> PostAsync<T>(string requestUri, object? content, CancellationToken cancellationToken = default);
    Task<T?> PutAsync<T>(string requestUri, object content, CancellationToken cancellationToken = default);
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
    DateTimeOffset? LatestReportDate,
    bool? UEFISecureBootEnabled,
    string? ClientVersion,
    string? OperatingSystem,
    string? OSVersion,
    string? OSBuildNumber,
    int? OSProductType,
    string? ChassisTypesJson,
    bool? IsVirtualMachine,
    string? VirtualizationPlatform,
    DateTime? FirmwareReleaseDate,
    uint? AllowTelemetry,
    bool? MicrosoftUpdateManagedOptIn,
    uint? WindowsUEFICA2023Capable)
{
    /// <summary>
    /// Overall readiness status - calculated by SecureBootReadinessService
    /// </summary>
    public bool IsReadyToUpdate { get; init; }

    /// <summary>
    /// OS version meets minimum requirements
    /// </summary>
    public bool IsOSReady { get; init; }

    /// <summary>
    /// OEM certificates are valid (not expired and not expiring soon)
    /// </summary>
    public bool AreOemCertificatesValid { get; init; }

    /// <summary>
    /// Windows UEFI CA 2023 is present in db
    /// </summary>
    public bool HasWindowsUEFICA2023 { get; init; }

    /// <summary>
    /// Indicates if no OEM certificates were found (VM, consumer device, or read error)
    /// </summary>
    public bool HasNoOemCertificates { get; init; }

    /// <summary>
    /// Number of expired OEM certificates
    /// </summary>
    public int ExpiredOemCertificateCount { get; init; }

    /// <summary>
    /// Number of OEM certificates expiring within critical threshold
    /// </summary>
    public int CriticalOemCertificateCount { get; init; }

    /// <summary>
    /// Number of OEM certificates expiring within warning threshold
    /// </summary>
    public int WarningOemCertificateCount { get; init; }

    /// <summary>
    /// Number of valid OEM certificates
    /// </summary>
    public int ValidOemCertificateCount { get; init; }

    /// <summary>
    /// Detailed OS evaluation message
    /// </summary>
    public string OSEvaluationDetails { get; init; } = string.Empty;

    /// <summary>
    /// Detailed certificate evaluation message
    /// </summary>
    public string CertificateEvaluationDetails { get; init; } = string.Empty;
}

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
    bool? UEFISecureBootEnabled,
    string? LatestRegistryStateJson,
    string? LatestCertificatesJson,
    IReadOnlyCollection<ReportHistoryItem> RecentReports)
{
    /// <summary>
    /// Overall readiness status
    /// </summary>
    public bool IsReadyToUpdate { get; init; }

    /// <summary>
    /// OS version meets minimum requirements
    /// </summary>
    public bool IsOSReady { get; init; }

    /// <summary>
    /// OEM certificates are valid (not expired and not expiring soon)
    /// </summary>
    public bool AreOemCertificatesValid { get; init; }

    /// <summary>
    /// Windows UEFI CA 2023 is present in db
    /// </summary>
    public bool HasWindowsUEFICA2023 { get; init; }

    /// <summary>
    /// Indicates if no OEM certificates were found (VM, consumer device, or read error)
    /// </summary>
    public bool HasNoOemCertificates { get; init; }

    /// <summary>
    /// Number of expired OEM certificates
    /// </summary>
    public int ExpiredOemCertificateCount { get; init; }

    /// <summary>
    /// Number of OEM certificates expiring within critical threshold
    /// </summary>
    public int CriticalOemCertificateCount { get; init; }

    /// <summary>
    /// Number of OEM certificates expiring within warning threshold
    /// </summary>
    public int WarningOemCertificateCount { get; init; }

    /// <summary>
    /// Number of valid OEM certificates
    /// </summary>
    public int ValidOemCertificateCount { get; init; }

    /// <summary>
    /// Detailed OS evaluation message
    /// </summary>
    public string OSEvaluationDetails { get; init; } = string.Empty;

    /// <summary>
    /// Detailed certificate evaluation message
    /// </summary>
    public string CertificateEvaluationDetails { get; init; } = string.Empty;
}

public sealed record ReportHistoryItem(
    Guid ReportId,
    DateTimeOffset CreatedAtUtc,
    string? DeploymentState,
    string? ClientVersion);

// Device Cleanup response models
public sealed record CleanupConfigResponse
{
    public int Id { get; init; }
    public bool Enabled { get; init; }
    public int InactiveDaysThreshold { get; init; }
    public string? CleanupSchedule { get; init; }
    public bool DeleteAssociatedData { get; init; }
    public bool NotifyOnCleanup { get; init; }
    public string? NotificationEmail { get; init; }
    public DateTimeOffset? LastCleanupRunUtc { get; init; }
    public int LastCleanupDeviceCount { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

public sealed record CleanupPreviewResponse
{
    public int Threshold { get; init; }
    public DateTimeOffset CutoffDate { get; init; }
    public int DeviceCount { get; init; }
    public List<InactiveDeviceInfo> Devices { get; init; } = new();
}

public sealed record InactiveDeviceInfo
{
    public Guid Id { get; init; }
    public string MachineName { get; init; } = string.Empty;
    public string? DomainName { get; init; }
    public DateTimeOffset LastSeenUtc { get; init; }
    public int DaysInactive { get; init; }
    public int ReportCount { get; init; }
}
