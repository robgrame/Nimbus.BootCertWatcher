using Microsoft.AspNetCore.Mvc;
using SecureBootDashboard.Api.Services;

namespace SecureBootDashboard.Api.Controllers;

/// <summary>
/// API endpoints for Windows version tracking and build security verification
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class WindowsVersionController : ControllerBase
{
    private readonly IWindowsVersionService _windowsVersionService;
    private readonly ILogger<WindowsVersionController> _logger;

    public WindowsVersionController(
        IWindowsVersionService windowsVersionService,
        ILogger<WindowsVersionController> logger)
    {
        _windowsVersionService = windowsVersionService;
        _logger = logger;
    }

    /// <summary>
    /// Synchronizes Windows version data from WindowsVersionsCore to local database
    /// </summary>
    /// <remarks>
    /// This endpoint triggers a full synchronization of Windows 10 and Windows 11 version data.
    /// Should be called periodically (e.g., weekly) to keep build information up-to-date.
    /// </remarks>
    /// <response code="200">Synchronization completed successfully</response>
    /// <response code="500">Synchronization failed</response>
    [HttpPost("sync")]
    [ProducesResponseType(typeof(WindowsVersionSyncResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<WindowsVersionSyncResult>> SyncWindowsVersions(
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Sync Windows versions requested");

        try
        {
            var result = await _windowsVersionService.SyncWindowsVersionsAsync(cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation(
                    "Windows version sync completed. Versions: {VersionsSynced}, Builds: {BuildsSynced}",
                    result.VersionsSynced,
                    result.BuildsSynced);

                return Ok(result);
            }
            else
            {
                _logger.LogError("Windows version sync failed: {ErrorMessage}", result.ErrorMessage);
                return StatusCode(500, result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during Windows version sync");
            return StatusCode(500, new WindowsVersionSyncResult(
                Success: false,
                VersionsSynced: 0,
                BuildsSynced: 0,
                ErrorMessage: ex.Message
            ));
        }
    }

    /// <summary>
    /// Checks if a specific Windows build number is considered secure
    /// </summary>
    /// <param name="buildNumber">Build number to check (e.g., "19045.3803", "22631.2861")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <remarks>
    /// Returns detailed security status including:
    /// - Whether the build is secure
    /// - Whether it's the latest available
    /// - Security notes and recommendations
    /// - Latest secure build if current one is outdated
    /// </remarks>
    /// <response code="200">Build security status retrieved successfully</response>
    /// <response code="400">Invalid build number format</response>
    [HttpGet("check-build/{buildNumber}")]
    [ProducesResponseType(typeof(WindowsBuildSecurityStatus), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WindowsBuildSecurityStatus>> CheckBuildSecurity(
        string buildNumber,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(buildNumber))
        {
            return BadRequest(new { error = "Build number is required" });
        }

        _logger.LogInformation("Checking build security for: {BuildNumber}", buildNumber);

        var status = await _windowsVersionService.CheckBuildSecurityAsync(buildNumber, cancellationToken);

        return Ok(status);
    }

    /// <summary>
    /// Gets all Windows versions tracked in the database
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <remarks>
    /// Returns list of Windows versions (e.g., "22H2", "23H2") with metadata
    /// </remarks>
    /// <response code="200">Windows versions retrieved successfully</response>
    [HttpGet("versions")]
    [ProducesResponseType(typeof(IReadOnlyList<WindowsVersionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<WindowsVersionDto>>> GetAllVersions(
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting all Windows versions");

        var versions = await _windowsVersionService.GetAllVersionsAsync(cancellationToken);

        var dtos = versions.Select(v => new WindowsVersionDto
        {
            Id = v.Id,
            Version = v.Version,
            Name = v.Name,
            ReleaseDate = v.ReleaseDate,
            EndOfSupportDate = v.EndOfSupportDate,
            LastSyncedUtc = v.LastSyncedUtc
        }).ToList();

        return Ok(dtos);
    }

    /// <summary>
    /// Gets all builds for a specific Windows version
    /// </summary>
    /// <param name="version">Version number (e.g., "22H2", "23H2")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <remarks>
    /// Returns all known builds for the specified version, ordered by release date
    /// </remarks>
    /// <response code="200">Builds retrieved successfully</response>
    /// <response code="404">Version not found</response>
    [HttpGet("versions/{version}/builds")]
    [ProducesResponseType(typeof(IReadOnlyList<WindowsBuildDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<WindowsBuildDto>>> GetBuildsForVersion(
        string version,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting builds for Windows version: {Version}", version);

        var builds = await _windowsVersionService.GetBuildsForVersionAsync(version, cancellationToken);

        if (!builds.Any())
        {
            return NotFound(new { error = $"No builds found for version {version}" });
        }

        var dtos = builds.Select(b => new WindowsBuildDto
        {
            Id = b.Id,
            BuildNumber = b.BuildNumber,
            MajorBuild = b.MajorBuild,
            MinorBuild = b.MinorBuild,
            ReleaseDate = b.ReleaseDate,
            KbArticle = b.KbArticle,
            IsSecure = b.IsSecure,
            IsLatest = b.IsLatest,
            SecurityNotes = b.SecurityNotes,
            LastSyncedUtc = b.LastSyncedUtc
        }).ToList();

        return Ok(dtos);
    }

    /// <summary>
    /// Gets the latest secure build for a specific Windows version
    /// </summary>
    /// <param name="version">Version number (e.g., "22H2", "23H2")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <remarks>
    /// Returns the most recent build marked as secure for the specified version
    /// </remarks>
    /// <response code="200">Latest secure build retrieved successfully</response>
    /// <response code="404">Version not found or no secure builds available</response>
    [HttpGet("versions/{version}/latest-secure")]
    [ProducesResponseType(typeof(WindowsBuildDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WindowsBuildDto>> GetLatestSecureBuild(
        string version,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting latest secure build for Windows version: {Version}", version);

        var build = await _windowsVersionService.GetLatestSecureBuildAsync(version, cancellationToken);

        if (build == null)
        {
            return NotFound(new { error = $"No secure builds found for version {version}" });
        }

        var dto = new WindowsBuildDto
        {
            Id = build.Id,
            BuildNumber = build.BuildNumber,
            MajorBuild = build.MajorBuild,
            MinorBuild = build.MinorBuild,
            ReleaseDate = build.ReleaseDate,
            KbArticle = build.KbArticle,
            IsSecure = build.IsSecure,
            IsLatest = build.IsLatest,
            SecurityNotes = build.SecurityNotes,
            LastSyncedUtc = build.LastSyncedUtc
        };

        return Ok(dto);
    }

    /// <summary>
    /// Gets statistics about Windows build security across all devices
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <remarks>
    /// Returns aggregated statistics including:
    /// - Total devices tracked
    /// - Devices with secure builds
    /// - Devices with outdated builds
    /// - Devices with unknown builds
    /// - Build distribution across the fleet
    /// </remarks>
    /// <response code="200">Statistics retrieved successfully</response>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(WindowsBuildStatistics), StatusCodes.Status200OK)]
    public async Task<ActionResult<WindowsBuildStatistics>> GetBuildStatistics(
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting Windows build statistics");

        var statistics = await _windowsVersionService.GetBuildStatisticsAsync(cancellationToken);

        return Ok(statistics);
    }

    /// <summary>
    /// Gets list of devices with outdated or insecure Windows builds
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <remarks>
    /// Returns devices that have builds marked as not secure, along with:
    /// - Device identification
    /// - Current build number
    /// - Security status and notes
    /// - Last seen timestamp
    /// </remarks>
    /// <response code="200">Devices retrieved successfully</response>
    [HttpGet("devices/outdated")]
    [ProducesResponseType(typeof(IReadOnlyList<DeviceWithBuildStatus>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DeviceWithBuildStatus>>> GetDevicesWithOutdatedBuilds(
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting devices with outdated builds");

        var devices = await _windowsVersionService.GetDevicesWithOutdatedBuildsAsync(cancellationToken);

        _logger.LogInformation("Found {Count} devices with outdated builds", devices.Count);

        return Ok(devices);
    }
}

#region DTOs

/// <summary>
/// Data transfer object for Windows version
/// </summary>
public record WindowsVersionDto
{
    public int Id { get; init; }
    public required string Version { get; init; }
    public required string Name { get; init; }
    public DateTime? ReleaseDate { get; init; }
    public DateTime? EndOfSupportDate { get; init; }
    public DateTime LastSyncedUtc { get; init; }
}

/// <summary>
/// Data transfer object for Windows build
/// </summary>
public record WindowsBuildDto
{
    public int Id { get; init; }
    public required string BuildNumber { get; init; }
    public int MajorBuild { get; init; }
    public int? MinorBuild { get; init; }
    public DateTime? ReleaseDate { get; init; }
    public string? KbArticle { get; init; }
    public bool IsSecure { get; init; }
    public bool IsLatest { get; init; }
    public string? SecurityNotes { get; init; }
    public DateTime LastSyncedUtc { get; init; }
}

#endregion
