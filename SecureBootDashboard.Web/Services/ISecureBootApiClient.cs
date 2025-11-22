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
    /// Indicates if the device is ready to update based on:
    /// - Firmware release date newer than 2024 (>= Jan 1, 2024)
    /// - OS build number indicating updates from November 2024 or later
    /// </summary>
    public bool ReadyToUpdate => IsFirmwareReady && IsOSUpdateReady;

    /// <summary>
    /// Firmware is ready if release date is >= January 1, 2024
    /// </summary>
    public bool IsFirmwareReady => FirmwareReleaseDate.HasValue && 
                                  FirmwareReleaseDate.Value >= new DateTime(2024, 1, 1);

    /// <summary>
    /// OS is ready if build number indicates November 2024 updates or later.
    /// - Windows 11 24H2+: Build >= 26100 (October 2024 release)
    /// - Windows Server 2022: Build >= 20349 with recent updates
    /// - Windows 10 22H2: Build >= 19046 with recent updates
    /// Note: Without UBR (Update Build Revision), we use major build numbers only.
    /// </summary>
    public bool IsOSUpdateReady
    {
        get
        {
            if (string.IsNullOrEmpty(OSBuildNumber) || !int.TryParse(OSBuildNumber, out var buildNumber))
                return false;

            // Windows 11 24H2 or later (October 2024 release)
            if (buildNumber >= 26100)
                return true;

            // Windows Server 2022 - Build 20348.x - check if recent enough
            // Need build 20349 or higher (since we can't check UBR)
            if (buildNumber >= 20349 && buildNumber < 26000)
                return true;

            // Windows 10 22H2 - Build 19045.x - check if recent enough
            // Need build 19046 or higher (since we can't check UBR)
            if (buildNumber >= 19046 && buildNumber < 20000)
                return true;

            // For exact build matching (e.g., 19045.5011), we'd need UBR (Update Build Revision)
            // Since we only have major build number, be conservative
            return false;
        }
    }
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
    IReadOnlyCollection<ReportHistoryItem> RecentReports);

public sealed record ReportHistoryItem(
    Guid ReportId,
    DateTimeOffset CreatedAtUtc,
    string? DeploymentState,
    string? ClientVersion);
