using Microsoft.AspNetCore.Mvc;
using SecureBootDashboard.Api.Services;

namespace SecureBootDashboard.Api.Controllers;

/// <summary>
/// API endpoints for Windows build security verification (configuration-based)
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
    /// Checks if a specific Windows build number is considered secure
    /// </summary>
    /// <param name="buildNumber">Build number to check (e.g., "19045.3803", "22631.2861")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <remarks>
    /// Returns detailed security status including:
    /// - Whether the build meets minimum security requirements (configured in appsettings.json)
    /// - Security notes and recommendations
    /// - Minimum secure build if current one is outdated
    /// - KB article to install
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
    /// Gets statistics about Windows build security across all devices
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <remarks>
    /// Returns aggregated statistics including:
    /// - Total devices tracked
    /// - Devices with secure builds (meeting minimum requirements)
    /// - Devices with outdated builds
    /// - Devices with unknown builds
    /// - Build distribution across the fleet
    /// 
    /// Based on minimum secure builds configured in appsettings.json
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
    /// Returns devices that have builds below minimum security requirements, along with:
    /// - Device identification
    /// - Current build number
    /// - Security status and notes
    /// - KB article recommendation
    /// - Last seen timestamp
    /// 
    /// Based on minimum secure builds configured in appsettings.json
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
/// Data transfer object for Windows build security status
/// </summary>
public record WindowsBuildSecurityStatus
{
    public required string BuildNumber { get; init; }
    public bool IsSecure { get; init; }
    public bool IsLatest { get; init; }
    public string? SecurityNotes { get; init; }
    public string? MinimumSecureBuild { get; init; }
    public string? KbArticle { get; init; }
}

/// <summary>
/// Data transfer object for Windows build statistics
/// </summary>
public record WindowsBuildStatistics
{
    public int TotalDevices { get; init; }
    public int SecureBuilds { get; init; }
    public int OutdatedBuilds { get; init; }
    public int UnknownBuilds { get; init; }
    public required Dictionary<string, int> BuildDistribution { get; init; }
}

/// <summary>
/// Data transfer object for device with build status
/// </summary>
public record DeviceWithBuildStatus
{
    public required string DeviceId { get; init; }
    public required string BuildNumber { get; init; }
    public bool IsSecure { get; init; }
    public string? SecurityNotes { get; init; }
    public string? KbArticle { get; init; }
    public DateTime LastSeen { get; init; }
}

#endregion
