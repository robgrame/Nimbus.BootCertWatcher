using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureBootDashboard.Api.Data;
using SecureBootDashboard.Api.Services;

namespace SecureBootDashboard.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ApiConfigController : ControllerBase
{
    private readonly SecureBootDbContext _context;
    private readonly IApiConfigurationService _configService;
    private readonly ILogger<ApiConfigController> _logger;

    public ApiConfigController(
        SecureBootDbContext context,
        IApiConfigurationService configService,
        ILogger<ApiConfigController> logger)
    {
        _context = context;
        _configService = configService;
        _logger = logger;
    }

    /// <summary>
    /// Get all API configurations.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ApiConfigurationEntity>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ApiConfigurationEntity>>> GetAll(CancellationToken cancellationToken = default)
    {
        var configs = await _context.ApiConfiguration
            .OrderByDescending(c => c.IsActive)
            .ThenByDescending(c => c.UpdatedAtUtc)
            .ToListAsync(cancellationToken);

        return Ok(configs);
    }

    /// <summary>
    /// Get API configuration by ID.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiConfigurationEntity), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiConfigurationEntity>> GetById(int id, CancellationToken cancellationToken = default)
    {
        var config = await _context.ApiConfiguration.FindAsync(new object[] { id }, cancellationToken);

        if (config == null)
        {
            return NotFound();
        }

        return Ok(config);
    }

    /// <summary>
    /// Get the currently active API configuration.
    /// </summary>
    [HttpGet("active")]
    [ProducesResponseType(typeof(ApiConfigurationEntity), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiConfigurationEntity>> GetActive(CancellationToken cancellationToken = default)
    {
        var config = await _configService.GetActiveConfigurationAsync(cancellationToken);

        if (config == null)
        {
            return NotFound(new { error = "No active API configuration found" });
        }

        return Ok(config);
    }

    /// <summary>
    /// Create new API configuration.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiConfigurationEntity), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiConfigurationEntity>> Create(
        [FromBody] ApiConfigurationEntity config,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Reset Id to 0 to ensure EF treats it as a new entity
            config.Id = 0;

            // Set default values for required properties if not provided
            config.QueueName ??= "secureboot-reports";
            config.QueueAuthenticationMethod ??= "ManagedIdentity";
            config.QueueCertificateStoreLocation ??= "LocalMachine";
            config.QueueCertificateStoreName ??= "My";
            config.FileReportStoreExtension ??= ".json";
            config.DeviceCleanupSchedule ??= "0 2 * * 0";

            config.CreatedAtUtc = DateTimeOffset.UtcNow;
            config.UpdatedAtUtc = DateTimeOffset.UtcNow;
            config.CreatedBy = User.Identity?.Name ?? "API";
            config.UpdatedBy = User.Identity?.Name ?? "API";

            _context.ApiConfiguration.Add(config);
            await _context.SaveChangesAsync(cancellationToken);

            // Invalidate cache so new config is picked up
            _configService.InvalidateCache();

            _logger.LogInformation("Created new API configuration {Id} by {User}", config.Id, config.CreatedBy);

            return CreatedAtAction(nameof(GetById), new { id = config.Id }, config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating API configuration");
            return StatusCode(500, new { error = "Internal server error", details = ex.Message });
        }
    }

    /// <summary>
    /// Update existing API configuration.
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ApiConfigurationEntity), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiConfigurationEntity>> Update(
        int id,
        [FromBody] ApiConfigurationEntity config,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (id != config.Id)
            {
                return BadRequest(new { error = "ID mismatch" });
            }

            var existing = await _context.ApiConfiguration.FindAsync(new object[] { id }, cancellationToken);
            if (existing == null)
            {
                return NotFound();
            }

            // Update fields (preserve created info)
            existing.QueueProcessorEnabled = config.QueueProcessorEnabled;
            existing.QueueServiceUri = config.QueueServiceUri;
            existing.QueueName = config.QueueName ?? "secureboot-reports";
            existing.QueueAuthenticationMethod = config.QueueAuthenticationMethod ?? "ManagedIdentity";
            existing.QueueConnectionString = config.QueueConnectionString;
            existing.QueueClientId = config.QueueClientId;
            existing.QueueTenantId = config.QueueTenantId;
            existing.QueueClientSecret = config.QueueClientSecret;
            existing.QueueCertificatePath = config.QueueCertificatePath;
            existing.QueueCertificatePassword = config.QueueCertificatePassword;
            existing.QueueCertificateThumbprint = config.QueueCertificateThumbprint;
            existing.QueueCertificateStoreLocation = config.QueueCertificateStoreLocation ?? "LocalMachine";
            existing.QueueCertificateStoreName = config.QueueCertificateStoreName ?? "My";
            existing.QueueMaxMessages = config.QueueMaxMessages;
            existing.QueueProcessingIntervalSeconds = config.QueueProcessingIntervalSeconds;
            existing.QueueEmptyQueuePollIntervalSeconds = config.QueueEmptyQueuePollIntervalSeconds;
            existing.QueueVisibilityTimeoutSeconds = config.QueueVisibilityTimeoutSeconds;
            existing.QueueMaxDequeueCount = config.QueueMaxDequeueCount;

            existing.FileReportStoreEnabled = config.FileReportStoreEnabled;
            existing.FileReportStoreBasePath = config.FileReportStoreBasePath;
            existing.FileReportStoreExtension = config.FileReportStoreExtension ?? ".json";
            existing.FileReportStoreAppendTimestamp = config.FileReportStoreAppendTimestamp;

            existing.DeviceCleanupEnabled = config.DeviceCleanupEnabled;
            existing.DeviceCleanupSchedule = config.DeviceCleanupSchedule ?? "0 2 * * 0";
            existing.DeviceCleanupDaysThreshold = config.DeviceCleanupDaysThreshold;

            existing.Description = config.Description;
            existing.IsActive = config.IsActive;
            existing.UpdatedAtUtc = DateTimeOffset.UtcNow;
            existing.UpdatedBy = User.Identity?.Name ?? "API";

            await _context.SaveChangesAsync(cancellationToken);

            // Invalidate cache so updated config is picked up
            _configService.InvalidateCache();

            _logger.LogInformation("Updated API configuration {Id} by {User}", id, existing.UpdatedBy);

            return Ok(existing);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating API configuration {Id}", id);
            return StatusCode(500, new { error = "Internal server error", details = ex.Message });
        }
    }

    /// <summary>
    /// Delete API configuration.
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var config = await _context.ApiConfiguration.FindAsync(new object[] { id }, cancellationToken);

            if (config == null)
            {
                return NotFound();
            }

            _context.ApiConfiguration.Remove(config);
            await _context.SaveChangesAsync(cancellationToken);

            // Invalidate cache
            _configService.InvalidateCache();

            _logger.LogInformation("Deleted API configuration {Id}", id);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting API configuration {Id}", id);
            return StatusCode(500, new { error = "Internal server error", details = ex.Message });
        }
    }

    /// <summary>
    /// Invalidate configuration cache.
    /// </summary>
    [HttpPost("invalidate-cache")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult InvalidateCache()
    {
        _configService.InvalidateCache();
        _logger.LogInformation("API configuration cache invalidated by {User}", User.Identity?.Name ?? "API");
        return Ok(new { message = "Cache invalidated successfully" });
    }
}
