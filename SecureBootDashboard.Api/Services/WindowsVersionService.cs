using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SecureBootDashboard.Api.Configuration;
using SecureBootDashboard.Api.Data;

namespace SecureBootDashboard.Api.Services;

/// <summary>
/// Service for managing Windows version tracking and build security verification
/// Uses Office Versions API as primary source with configuration fallback
/// </summary>
public class WindowsVersionService : IWindowsVersionService
{
    private readonly SecureBootDbContext _dbContext;
    private readonly WindowsSecurityOptions _securityOptions;
    private readonly IOfficeVersionsApiClient _officeVersionsClient;
    private readonly ILogger<WindowsVersionService> _logger;

    public WindowsVersionService(
        SecureBootDbContext dbContext,
        IOptions<WindowsSecurityOptions> securityOptions,
        IOfficeVersionsApiClient officeVersionsClient,
        ILogger<WindowsVersionService> logger)
    {
        _dbContext = dbContext;
        _securityOptions = securityOptions.Value;
        _officeVersionsClient = officeVersionsClient;
        _logger = logger;
    }

    public async Task<WindowsBuildSecurityStatus> CheckBuildSecurityAsync(
        string buildNumber, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("CheckBuildSecurityAsync: Checking build security for {BuildNumber}", buildNumber);
        
        // STRATEGY 1: Try Office Versions API first (primary source)
        var apiStatus = await CheckBuildSecurityFromApiAsync(buildNumber, cancellationToken);
        if (apiStatus != null)
        {
            _logger.LogInformation("CheckBuildSecurityAsync: Used Office Versions API for build {BuildNumber}", buildNumber);
            return apiStatus;
        }

        _logger.LogWarning("CheckBuildSecurityAsync: Office Versions API unavailable, falling back to configuration");

        // STRATEGY 2: Fallback to local configuration
        return CheckBuildSecurityFromConfiguration(buildNumber);
    }

    private async Task<WindowsBuildSecurityStatus?> CheckBuildSecurityFromApiAsync(
        string buildNumber,
        CancellationToken cancellationToken)
    {
        try
        {
            var (major, _) = ParseBuildNumber(buildNumber);
            
            // Determine which Windows version to query
            var versionsResponse = major switch
            {
                19045 or 19044 or 19043 or 19042 or 19041 => 
                    await _officeVersionsClient.GetWindows10VersionsAsync(cancellationToken),
                22000 or 22621 or 22631 or 26100 => 
                    await _officeVersionsClient.GetWindows11VersionsAsync(cancellationToken),
                _ => null
            };

            if (versionsResponse == null || !versionsResponse.IsSuccess || versionsResponse.Data == null)
            {
                _logger.LogDebug("CheckBuildSecurityFromApiAsync: No data from API for build {BuildNumber}", buildNumber);
                return null;
            }

            // Find the build in the versions
            WindowsBuildInfo? matchedBuild = null;
            WindowsVersionInfo? matchedVersion = null;

            foreach (var version in versionsResponse.Data)
            {
                matchedBuild = version.Builds?.FirstOrDefault(b => b.BuildNumber == buildNumber);
                if (matchedBuild != null)
                {
                    matchedVersion = version;
                    break;
                }
            }

            if (matchedBuild == null || matchedVersion == null)
            {
                _logger.LogDebug("CheckBuildSecurityFromApiAsync: Build {BuildNumber} not found in API data", buildNumber);
                return null;
            }

            // Find latest build for comparison
            var latestBuild = matchedVersion.Builds?
                .Where(b => b.IsLatest)
                .OrderByDescending(b => b.ReleaseDate)
                .FirstOrDefault();

            var isSecure = matchedBuild.IsLatest || matchedBuild.SecurityUpdate;
            var securityNotes = matchedBuild.IsLatest
                ? "? This is the latest build for this Windows version"
                : $"? Latest build available: {latestBuild?.BuildNumber ?? "Unknown"} (released {latestBuild?.ReleaseDate:yyyy-MM-dd})";

            if (matchedBuild.SecurityUpdate)
            {
                securityNotes = "? Security update applied. " + securityNotes;
            }

            if (matchedBuild.Notes != null)
            {
                securityNotes += $"\nNotes: {matchedBuild.Notes}";
            }

            _logger.LogInformation(
                "CheckBuildSecurityFromApiAsync: Build {BuildNumber} security status - IsSecure={IsSecure}, IsLatest={IsLatest}",
                buildNumber, isSecure, matchedBuild.IsLatest);

            return new WindowsBuildSecurityStatus(
                BuildNumber: buildNumber,
                IsSecure: isSecure,
                IsLatest: matchedBuild.IsLatest,
                SecurityNotes: securityNotes,
                ReleaseDate: matchedBuild.ReleaseDate,
                LatestSecureBuild: matchedBuild.IsLatest ? null : latestBuild?.BuildNumber,
                KbArticle: matchedBuild.KbArticle
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CheckBuildSecurityFromApiAsync: Error checking build {BuildNumber} via API", buildNumber);
            return null;
        }
    }

    private WindowsBuildSecurityStatus CheckBuildSecurityFromConfiguration(string buildNumber)
    {
        var (major, minor) = ParseBuildNumber(buildNumber);
        _logger.LogTrace("CheckBuildSecurityFromConfiguration: Parsed build number - Major={Major}, Minor={Minor}", major, minor);

        // Try to determine Windows version from build number
        var windowsVersion = DetermineWindowsVersionFromBuild(major);
        _logger.LogDebug("CheckBuildSecurityFromConfiguration: Determined Windows version as {Version} from build {Major}", 
            windowsVersion ?? "Unknown", major);

        // Check against configured minimum build
        if (!string.IsNullOrEmpty(windowsVersion))
        {
            var isSecure = _securityOptions.IsBuildSecure(windowsVersion, buildNumber);
            var minimumBuild = _securityOptions.GetMinimumBuildNumber(windowsVersion);
            _logger.LogDebug("CheckBuildSecurityFromConfiguration: Build security check - IsSecure={IsSecure}, MinimumBuild={MinBuild}", 
                isSecure, minimumBuild);

            if (_securityOptions.MinimumSecureBuilds.TryGetValue(windowsVersion, out var buildInfo))
            {
                _logger.LogTrace("CheckBuildSecurityFromConfiguration: Found build info - Name={Name}, KB={KB}, ReleaseDate={Date}", 
                    buildInfo.Name, buildInfo.KBArticle, buildInfo.ReleaseDate);
                
                var result = new WindowsBuildSecurityStatus(
                    BuildNumber: buildNumber,
                    IsSecure: isSecure,
                    IsLatest: false, // We don't track "latest" in config
                    SecurityNotes: isSecure 
                        ? $"? Build meets or exceeds minimum secure build {minimumBuild} (from local configuration)"
                        : $"? Build is older than minimum secure build {minimumBuild} ({buildInfo.Name}). Update to {buildInfo.KBArticle} or later.",
                    ReleaseDate: buildInfo.ReleaseDate,
                    LatestSecureBuild: isSecure ? null : minimumBuild,
                    KbArticle: buildInfo.KBArticle
                );
                
                _logger.LogInformation("CheckBuildSecurityFromConfiguration: Build {BuildNumber} for {Version} is {Status} (configuration-based)", 
                    buildNumber, windowsVersion, isSecure ? "SECURE" : "OUTDATED");
                return result;
            }
            else
            {
                _logger.LogWarning("CheckBuildSecurityFromConfiguration: No build info found in configuration for {Version}", windowsVersion);
            }
        }
        else
        {
            _logger.LogWarning("CheckBuildSecurityFromConfiguration: Could not determine Windows version from build {BuildNumber}", buildNumber);
        }

        // Build not found in configuration
        _logger.LogWarning("CheckBuildSecurityFromConfiguration: Build {BuildNumber} not found in configuration", buildNumber);
        return new WindowsBuildSecurityStatus(
            BuildNumber: buildNumber,
            IsSecure: false,
            IsLatest: false,
            SecurityNotes: "? Build not found in API or configuration. Please verify Windows version and update sources.",
            ReleaseDate: null,
            LatestSecureBuild: null,
            KbArticle: null
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
