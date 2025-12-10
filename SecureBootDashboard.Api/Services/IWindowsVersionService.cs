using SecureBootDashboard.Api.Data;

namespace SecureBootDashboard.Api.Services;

/// <summary>
/// Service for managing Windows build security verification (configuration-based)
/// </summary>
public interface IWindowsVersionService
{
    /// <summary>
    /// Checks if a specific build number is considered secure based on configuration
    /// </summary>
    Task<WindowsBuildSecurityStatus> CheckBuildSecurityAsync(string buildNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets statistics about build security status across all devices
    /// </summary>
    Task<WindowsBuildStatistics> GetBuildStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets devices with outdated/insecure builds
    /// </summary>
    Task<IReadOnlyList<DeviceWithBuildStatus>> GetDevicesWithOutdatedBuildsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Security status of a Windows build
/// </summary>
public record WindowsBuildSecurityStatus(
    string BuildNumber,
    bool IsSecure,
    bool IsLatest,
    string? SecurityNotes = null,
    DateTime? ReleaseDate = null,
    string? LatestSecureBuild = null,
    string? KbArticle = null
);

/// <summary>
/// Statistics about Windows builds across devices
/// </summary>
public record WindowsBuildStatistics(
    int TotalDevices,
    int DevicesWithSecureBuilds,
    int DevicesWithOutdatedBuilds,
    int DevicesWithUnknownBuilds,
    double SecureBuildPercentage,
    Dictionary<string, int> BuildDistribution
);

/// <summary>
/// Device with its build security status
/// </summary>
public record DeviceWithBuildStatus(
    Guid DeviceId,
    string MachineName,
    string? DomainName,
    string? OSBuildNumber,
    bool IsSecure,
    bool IsLatest,
    string? SecurityNotes,
    DateTime? LastSeenUtc
);
