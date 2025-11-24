using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SecureBootDashboard.Api.Data;
using System.Text.Json;
using WindowsVersionsCore.Models;
using WindowsVersionsCore.Services;

namespace SecureBootDashboard.Api.Services;

/// <summary>
/// Service for managing Windows version tracking and build security verification
/// </summary>
public class WindowsVersionService : IWindowsVersionService
{
    private readonly SecureBootDbContext _dbContext;
    private readonly IWindowsService _windowsService;
    private readonly ILogger<WindowsVersionService> _logger;

    public WindowsVersionService(
        SecureBootDbContext dbContext,
        IWindowsService windowsService,
        ILogger<WindowsVersionService> logger)
    {
        _dbContext = dbContext;
        _windowsService = windowsService;
        _logger = logger;
    }

    public async Task<WindowsVersionSyncResult> SyncWindowsVersionsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting Windows version synchronization");

            int versionsSynced = 0;
            int buildsSynced = 0;

            // Sync Windows 10
            var win10Result = await SyncWindowsEditionAsync(WindowsEdition.Windows10, cancellationToken);
            versionsSynced += win10Result.VersionsSynced;
            buildsSynced += win10Result.BuildsSynced;

            // Sync Windows 11
            var win11Result = await SyncWindowsEditionAsync(WindowsEdition.Windows11, cancellationToken);
            versionsSynced += win11Result.VersionsSynced;
            buildsSynced += win11Result.BuildsSynced;

            var now = DateTime.UtcNow;

            _logger.LogInformation(
                "Windows version sync completed. Versions: {VersionsSynced}, Builds: {BuildsSynced}",
                versionsSynced,
                buildsSynced);

            return new WindowsVersionSyncResult(
                Success: true,
                VersionsSynced: versionsSynced,
                BuildsSynced: buildsSynced,
                LastSyncedUtc: now
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing Windows versions");
            return new WindowsVersionSyncResult(
                Success: false,
                VersionsSynced: 0,
                BuildsSynced: 0,
                ErrorMessage: ex.Message
            );
        }
    }

    private async Task<(int VersionsSynced, int BuildsSynced)> SyncWindowsEditionAsync(
        WindowsEdition edition,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Syncing {Edition} versions", edition);

        // Get version summary from WindowsVersionsCore
        var summaryResponse = await _windowsService.GetReleaseSummaryAsync(edition);
        
        if (!summaryResponse.Success || summaryResponse.Data == null)
        {
            _logger.LogWarning("Failed to get release summary for {Edition}: {Message}", 
                edition, summaryResponse.Message);
            return (0, 0);
        }

        var summary = summaryResponse.Data;
        int versionsSynced = 0;
        int buildsSynced = 0;

        // Process Regular servicing versions
        foreach (var regularVersion in summary.RegularVersions)
        {
            var (vs, bs) = await SyncVersionWithBuildsAsync(
                edition.ToString(),
                regularVersion.Version,
                regularVersion.LatestBuild,
                regularVersion.ReleaseDate,
                regularVersion.EndOfServicingEnterprise,
                regularVersion.IsCurrentVersion,
                cancellationToken);
            
            versionsSynced += vs;
            buildsSynced += bs;
        }

        // Process LTSC servicing versions
        foreach (var ltscVersion in summary.LtscVersions)
        {
            var (vs, bs) = await SyncVersionWithBuildsAsync(
                edition.ToString(),
                ltscVersion.Version,
                ltscVersion.LatestBuild,
                ltscVersion.ReleaseDate,
                ltscVersion.ExtendedSupportEndDate,
                ltscVersion.IsCurrentVersion,
                cancellationToken);
            
            versionsSynced += vs;
            buildsSynced += bs;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return (versionsSynced, buildsSynced);
    }

    private async Task<(int Versions, int Builds)> SyncVersionWithBuildsAsync(
        string editionName,
        string versionNumber,
        string latestBuild,
        DateTime? releaseDate,
        string? endOfSupport,
        bool isCurrentVersion,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        // Find or create Windows version
        var version = await _dbContext.WindowsVersions
            .FirstOrDefaultAsync(v => v.Version == versionNumber, cancellationToken);

        if (version == null)
        {
            version = new WindowsVersionEntity
            {
                Version = versionNumber,
                Name = $"Windows {editionName} {versionNumber}",
                ReleaseDate = releaseDate,
                EndOfSupportDate = ParseEndOfSupportDate(endOfSupport),
                LastSyncedUtc = now
            };
            _dbContext.WindowsVersions.Add(version);
            await _dbContext.SaveChangesAsync(cancellationToken); // Save to get ID
        }
        else
        {
            version.LastSyncedUtc = now;
            version.EndOfSupportDate = ParseEndOfSupportDate(endOfSupport) ?? version.EndOfSupportDate;
        }

        int buildsAdded = 0;

        // Sync the latest build
        if (!string.IsNullOrEmpty(latestBuild))
        {
            var (major, minor) = ParseBuildNumber(latestBuild);
            
            var existingBuild = await _dbContext.WindowsBuilds
                .FirstOrDefaultAsync(b => 
                    b.WindowsVersionId == version.Id && 
                    b.BuildNumber == latestBuild, 
                    cancellationToken);

            if (existingBuild == null)
            {
                var build = new WindowsBuildEntity
                {
                    WindowsVersionId = version.Id,
                    BuildNumber = latestBuild,
                    MajorBuild = major,
                    MinorBuild = minor,
                    ReleaseDate = releaseDate,
                    IsSecure = isCurrentVersion, // Latest build for current version is considered secure
                    IsLatest = true,
                    SecurityNotes = isCurrentVersion ? "Current release" : "Older release",
                    LastSyncedUtc = now
                };
                _dbContext.WindowsBuilds.Add(build);
                buildsAdded++;
            }
            else
            {
                // Update existing build
                existingBuild.IsLatest = true;
                existingBuild.IsSecure = isCurrentVersion;
                existingBuild.LastSyncedUtc = now;
            }

            // Mark other builds as not latest
            await _dbContext.WindowsBuilds
                .Where(b => b.WindowsVersionId == version.Id && b.BuildNumber != latestBuild)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(b => b.IsLatest, false),
                    cancellationToken);
        }

        return (version.Id > 0 ? 1 : 0, buildsAdded);
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

    private static DateTime? ParseEndOfSupportDate(string? endOfSupport)
    {
        if (string.IsNullOrWhiteSpace(endOfSupport))
            return null;

        if (DateTime.TryParse(endOfSupport, out var date))
            return date;

        return null;
    }

    public async Task<WindowsBuildSecurityStatus> CheckBuildSecurityAsync(
        string buildNumber, 
        CancellationToken cancellationToken = default)
    {
        var (major, minor) = ParseBuildNumber(buildNumber);

        var build = await _dbContext.WindowsBuilds
            .Include(b => b.Version)
            .FirstOrDefaultAsync(b => b.BuildNumber == buildNumber, cancellationToken);

        if (build == null)
        {
            // Build not in database - try to find by major build number
            build = await _dbContext.WindowsBuilds
                .Include(b => b.Version)
                .FirstOrDefaultAsync(b => b.MajorBuild == major, cancellationToken);

            if (build != null)
            {
                return new WindowsBuildSecurityStatus(
                    BuildNumber: buildNumber,
                    IsSecure: false, // Exact build not found, consider insecure
                    IsLatest: false,
                    SecurityNotes: $"Build {buildNumber} not found. Latest known build for this version: {build.BuildNumber}",
                    ReleaseDate: build.ReleaseDate,
                    LatestSecureBuild: build.BuildNumber
                );
            }

            // No match at all
            return new WindowsBuildSecurityStatus(
                BuildNumber: buildNumber,
                IsSecure: false,
                IsLatest: false,
                SecurityNotes: "Build not found in database. Please sync Windows versions.",
                ReleaseDate: null,
                LatestSecureBuild: null
            );
        }

        // Found exact build
        return new WindowsBuildSecurityStatus(
            BuildNumber: buildNumber,
            IsSecure: build.IsSecure,
            IsLatest: build.IsLatest,
            SecurityNotes: build.SecurityNotes,
            ReleaseDate: build.ReleaseDate,
            LatestSecureBuild: build.IsLatest ? null : 
                (await GetLatestSecureBuildAsync(build.Version?.Version ?? "", cancellationToken))?.BuildNumber
        );
    }

    public async Task<IReadOnlyList<WindowsVersionEntity>> GetAllVersionsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.WindowsVersions
            .OrderByDescending(v => v.ReleaseDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WindowsBuildEntity>> GetBuildsForVersionAsync(
        string version, 
        CancellationToken cancellationToken = default)
    {
        var versionEntity = await _dbContext.WindowsVersions
            .FirstOrDefaultAsync(v => v.Version == version, cancellationToken);

        if (versionEntity == null)
            return Array.Empty<WindowsBuildEntity>();

        return await _dbContext.WindowsBuilds
            .Where(b => b.WindowsVersionId == versionEntity.Id)
            .OrderByDescending(b => b.ReleaseDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<WindowsBuildEntity?> GetLatestSecureBuildAsync(
        string version, 
        CancellationToken cancellationToken = default)
    {
        var versionEntity = await _dbContext.WindowsVersions
            .FirstOrDefaultAsync(v => v.Version == version, cancellationToken);

        if (versionEntity == null)
            return null;

        return await _dbContext.WindowsBuilds
            .Where(b => b.WindowsVersionId == versionEntity.Id && b.IsSecure)
            .OrderByDescending(b => b.IsLatest)
            .ThenByDescending(b => b.ReleaseDate)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<WindowsBuildStatistics> GetBuildStatisticsAsync(
        CancellationToken cancellationToken = default)
    {
        var devices = await _dbContext.Devices
            .Where(d => d.OSBuildNumber != null)
            .ToListAsync(cancellationToken);

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
