using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SecureBootDashboard.Api.Configuration;
using SecureBootDashboard.Api.Data;

namespace SecureBootDashboard.Api.Services;

/// <summary>
/// Service for managing Windows version tracking and build security verification
/// </summary>
public class WindowsVersionService : IWindowsVersionService
{
    private readonly SecureBootDbContext _dbContext;
    private readonly WindowsSecurityOptions _securityOptions;
    private readonly ILogger<WindowsVersionService> _logger;

    public WindowsVersionService(
        SecureBootDbContext dbContext,
        IOptions<WindowsSecurityOptions> securityOptions,
        ILogger<WindowsVersionService> logger)
    {
        _dbContext = dbContext;
        _securityOptions = securityOptions.Value;
        _logger = logger;
    }

    public async Task<WindowsBuildSecurityStatus> CheckBuildSecurityAsync(
        string buildNumber, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("CheckBuildSecurityAsync: Checking build security for {BuildNumber}", buildNumber);
        var (major, minor) = ParseBuildNumber(buildNumber);
        _logger.LogTrace("CheckBuildSecurityAsync: Parsed build number - Major={Major}, Minor={Minor}", major, minor);

        // Try to determine Windows version from build number
        var windowsVersion = DetermineWindowsVersionFromBuild(major);
        _logger.LogDebug("CheckBuildSecurityAsync: Determined Windows version as {Version} from build {Major}", 
            windowsVersion ?? "Unknown", major);

        // Check against configured minimum build
        if (!string.IsNullOrEmpty(windowsVersion))
        {
            var isSecure = _securityOptions.IsBuildSecure(windowsVersion, buildNumber);
            var minimumBuild = _securityOptions.GetMinimumBuildNumber(windowsVersion);
            _logger.LogDebug("CheckBuildSecurityAsync: Build security check - IsSecure={IsSecure}, MinimumBuild={MinBuild}", 
                isSecure, minimumBuild);

            if (_securityOptions.MinimumSecureBuilds.TryGetValue(windowsVersion, out var buildInfo))
            {
                _logger.LogTrace("CheckBuildSecurityAsync: Found build info - Name={Name}, KB={KB}, ReleaseDate={Date}", 
                    buildInfo.Name, buildInfo.KBArticle, buildInfo.ReleaseDate);
                
                var result = new WindowsBuildSecurityStatus(
                    BuildNumber: buildNumber,
                    IsSecure: isSecure,
                    IsLatest: false, // We don't track "latest" in config
                    SecurityNotes: isSecure 
                        ? $"Build meets or exceeds minimum secure build {minimumBuild}"
                        : $"Build is older than minimum secure build {minimumBuild} ({buildInfo.Name}). Update to {buildInfo.KBArticle} or later.",
                    ReleaseDate: buildInfo.ReleaseDate,
                    LatestSecureBuild: isSecure ? null : minimumBuild
                );
                
                _logger.LogInformation("CheckBuildSecurityAsync: Build {BuildNumber} for {Version} is {Status}", 
                    buildNumber, windowsVersion, isSecure ? "SECURE" : "OUTDATED");
                return result;
            }
            else
            {
                _logger.LogWarning("CheckBuildSecurityAsync: No build info found in configuration for {Version}", windowsVersion);
            }
        }
        else
        {
            _logger.LogWarning("CheckBuildSecurityAsync: Could not determine Windows version from build {BuildNumber}", buildNumber);
        }

        // Build not found in configuration
        _logger.LogWarning("CheckBuildSecurityAsync: Build {BuildNumber} not found in configuration", buildNumber);
        return new WindowsBuildSecurityStatus(
            BuildNumber: buildNumber,
            IsSecure: false,
            IsLatest: false,
            SecurityNotes: "Build not found in configuration. Please verify Windows version and update configuration.",
            ReleaseDate: null,
            LatestSecureBuild: null
        );
    }

    /// <summary>
    /// Determines the Windows version configuration key from a build number
    /// </summary>
    private static string? DetermineWindowsVersionFromBuild(int majorBuild)
    {
        return majorBuild switch
        {
            19045 => "Windows10",        // Windows 10 22H2
            22000 => "Windows11_21H2",   // Windows 11 21H2
            22621 => "Windows11_22H2",   // Windows 11 22H2
            22631 => "Windows11_23H2",   // Windows 11 23H2
            26100 => "Windows11_24H2",   // Windows 11 24H2
            _ => null
        };
    }

    private static (int Major, int? Minor) ParseBuildNumber(string buildNumber)
    {
        var parts = buildNumber.Split('.');
        if (parts.Length == 0) return (0, null);

        if (!int.TryParse(parts[0], out var major))
            major = 0;

        int? minor = null;
        if (parts.Length > 1 && int.TryParse(parts[1], out var minorValue))
            minor = minorValue;

        return (major, minor);
    }

    public async Task<WindowsBuildStatistics> GetBuildStatisticsAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("GetBuildStatisticsAsync: Retrieving Windows build statistics from all devices");
        
        var devices = await _dbContext.Devices
            .Where(d => d.OSBuildNumber != null)
            .ToListAsync(cancellationToken);

        _logger.LogTrace("GetBuildStatisticsAsync: Retrieved {DeviceCount} devices with build numbers", devices.Count);

        var totalDevices = devices.Count;
        var secureCount = 0;
        var outdatedCount = 0;
        var unknownCount = 0;
        var buildDistribution = new Dictionary<string, int>();

        foreach (var device in devices)
        {
            var buildNumber = device.OSBuildNumber!;
            
            // Update distribution
            if (!buildDistribution.ContainsKey(buildNumber))
                buildDistribution[buildNumber] = 0;
            buildDistribution[buildNumber]++;

            // Check security status
            var status = await CheckBuildSecurityAsync(buildNumber, cancellationToken);
            
            if (status.IsSecure)
                secureCount++;
            else if (status.LatestSecureBuild != null)
                outdatedCount++;
            else
                unknownCount++;
        }

        var percentage = totalDevices > 0 
            ? (double)secureCount / totalDevices * 100 
            : 0;

        _logger.LogInformation("GetBuildStatisticsAsync: Statistics calculated - Total={Total}, Secure={Secure} ({Percentage:F2}%), Outdated={Outdated}, Unknown={Unknown}", 
            totalDevices, secureCount, percentage, outdatedCount, unknownCount);
        _logger.LogDebug("GetBuildStatisticsAsync: Found {UniqueBuilds} unique build numbers", buildDistribution.Count);

        return new WindowsBuildStatistics(
            TotalDevices: totalDevices,
            DevicesWithSecureBuilds: secureCount,
            DevicesWithOutdatedBuilds: outdatedCount,
            DevicesWithUnknownBuilds: unknownCount,
            SecureBuildPercentage: percentage,
            BuildDistribution: buildDistribution
        );
    }

    public async Task<IReadOnlyList<DeviceWithBuildStatus>> GetDevicesWithOutdatedBuildsAsync(
        CancellationToken cancellationToken = default)
    {
        var devices = await _dbContext.Devices
            .Where(d => d.OSBuildNumber != null)
            .OrderBy(d => d.MachineName)
            .ToListAsync(cancellationToken);

        var result = new List<DeviceWithBuildStatus>();

        foreach (var device in devices)
        {
            var status = await CheckBuildSecurityAsync(device.OSBuildNumber!, cancellationToken);
            
            // Only include devices with outdated or unknown builds
            if (!status.IsSecure)
            {
                result.Add(new DeviceWithBuildStatus(
                    DeviceId: device.Id,
                    MachineName: device.MachineName,
                    DomainName: device.DomainName,
                    OSBuildNumber: device.OSBuildNumber,
                    IsSecure: status.IsSecure,
                    IsLatest: status.IsLatest,
                    SecurityNotes: status.SecurityNotes,
                    LastSeenUtc: device.LastSeenUtc.UtcDateTime
                ));
            }
        }

        return result;
    }
}
