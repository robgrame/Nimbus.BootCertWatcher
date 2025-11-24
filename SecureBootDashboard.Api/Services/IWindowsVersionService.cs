using SecureBootDashboard.Api.Data;

namespace SecureBootDashboard.Api.Services;

/// <summary>
/// Service for managing Windows version tracking and build security verification
/// </summary>
public interface IWindowsVersionService
{
    /// <summary>
    /// Synchronizes Windows version data from WindowsVersionsCore to local database
    /// </summary>
    Task<WindowsVersionSyncResult> SyncWindowsVersionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a specific build number is considered secure
    /// </summary>
    Task<WindowsBuildSecurityStatus> CheckBuildSecurityAsync(string buildNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all Windows versions from database
    /// </summary>
    Task<IReadOnlyList<WindowsVersionEntity>> GetAllVersionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all builds for a specific Windows version
    /// </summary>
    Task<IReadOnlyList<WindowsBuildEntity>> GetBuildsForVersionAsync(string version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the latest secure build for a Windows version
    /// </summary>
    Task<WindowsBuildEntity?> GetLatestSecureBuildAsync(string version, CancellationToken cancellationToken = default);

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
/// Result of Windows version synchronization
/// </summary>
public record WindowsVersionSyncResult(
    bool Success,
    int VersionsSynced,
    int BuildsSynced,
    string? ErrorMessage = null,
    DateTime? LastSyncedUtc = null
);

/// <summary>
/// Security status of a Windows build
/// </summary>
public record WindowsBuildSecurityStatus(
    string BuildNumber,
    bool IsSecure,
    bool IsLatest,
    string? SecurityNotes = null,
    DateTime? ReleaseDate = null,
    string? LatestSecureBuild = null
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
