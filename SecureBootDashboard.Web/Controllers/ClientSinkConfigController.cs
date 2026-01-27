using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureBootDashboard.Api.Data;
using SecureBootWatcher.Shared.Configuration;
using System.Text.Json;

namespace SecureBootDashboard.Api.Controllers;

/// <summary>
/// API controller for managing client sink configuration.
/// Provides centralized configuration for client reporting sinks.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class ClientSinkConfigController : ControllerBase
{
    private readonly SecureBootDbContext _context;
    private readonly ILogger<ClientSinkConfigController> _logger;

    public ClientSinkConfigController(
        SecureBootDbContext context,
        ILogger<ClientSinkConfigController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get active sink configuration for clients.
    /// Returns the active configuration formatted as SecureBootWatcherOptions.SinkOptions.
    /// </summary>
    [HttpGet("active")]
    [ProducesResponseType(typeof(SinkOptions), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SinkOptions>> GetActiveConfiguration(CancellationToken cancellationToken = default)
    {
        try
        {
            var config = await _context.ClientSinkConfig
                .Where(c => c.IsActive)
                .OrderByDescending(c => c.UpdatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            if (config == null)
            {
                _logger.LogWarning("No active client sink configuration found");
                return NotFound(new { error = "No active configuration found" });
            }

            // Convert entity to SinkOptions
            var sinkOptions = MapToSinkOptions(config);

            _logger.LogInformation("Returned active sink configuration (ID: {ConfigId})", config.Id);

            return Ok(sinkOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active sink configuration");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get all sink configurations (for admin UI).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<ClientSinkConfigEntity>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ClientSinkConfigEntity>>> GetAll(CancellationToken cancellationToken = default)
    {
        try
        {
            var configs = await _context.ClientSinkConfig
                .OrderByDescending(c => c.IsActive)
                .ThenByDescending(c => c.UpdatedAtUtc)
                .ToListAsync(cancellationToken);

            return Ok(configs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving sink configurations");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get specific configuration by ID.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ClientSinkConfigEntity), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientSinkConfigEntity>> GetById(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var config = await _context.ClientSinkConfig
                .FindAsync(new object[] { id }, cancellationToken);

            if (config == null)
            {
                return NotFound();
            }

            return Ok(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving sink configuration {Id}", id);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Create new sink configuration.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ClientSinkConfigEntity), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ClientSinkConfigEntity>> Create(
        [FromBody] ClientSinkConfigEntity config,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Reset Id to 0 to ensure EF treats it as a new entity
            config.Id = 0;
            
            // Set default values for required properties if not provided
            config.ExecutionStrategy ??= "StopOnFirstSuccess";
            config.SinkPriority ??= "WebApi,AzureQueue,FileShare";
            config.FileShareExtension ??= ".json";
            config.AzureQueueName ??= "secureboot-reports";
            config.AzureQueueAuthMethod ??= "DefaultAzureCredential";
            config.AzureQueueCertStoreLocation ??= "CurrentUser";
            config.AzureQueueCertStoreName ??= "My";
            config.WebApiIngestionRoute ??= "/api/SecureBootReports";
            config.WebApiCertStoreLocation ??= "LocalMachine";
            config.WebApiCertStoreName ??= "My";
            
            config.CreatedAtUtc = DateTimeOffset.UtcNow;
            config.UpdatedAtUtc = DateTimeOffset.UtcNow;
            config.CreatedBy = User.Identity?.Name ?? "API";
            config.UpdatedBy = User.Identity?.Name ?? "API";

            _context.ClientSinkConfig.Add(config);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Created new sink configuration {Id} by {User}", config.Id, config.CreatedBy);

            return CreatedAtAction(nameof(GetById), new { id = config.Id }, config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating sink configuration");
            return StatusCode(500, new { error = "Internal server error", details = ex.Message });
        }
    }

    /// <summary>
    /// Update existing sink configuration.
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ClientSinkConfigEntity), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ClientSinkConfigEntity>> Update(
        int id,
        [FromBody] ClientSinkConfigEntity config,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (id != config.Id)
            {
                return BadRequest(new { error = "ID mismatch" });
            }

            var existing = await _context.ClientSinkConfig.FindAsync(new object[] { id }, cancellationToken);
            if (existing == null)
            {
                return NotFound();
            }

            // Update fields (preserve created info)
            existing.EnableFileShare = config.EnableFileShare;
            existing.EnableAzureQueue = config.EnableAzureQueue;
            existing.EnableWebApi = config.EnableWebApi;
            existing.ExecutionStrategy = config.ExecutionStrategy ?? "StopOnFirstSuccess";
            existing.SinkPriority = config.SinkPriority ?? "WebApi,AzureQueue,FileShare";
            existing.MaxRetryAttempts = config.MaxRetryAttempts;
            existing.RetryDelaySeconds = config.RetryDelaySeconds;
            existing.UseExponentialBackoff = config.UseExponentialBackoff;
            
            existing.FileShareRootPath = config.FileShareRootPath;
            existing.FileShareExtension = config.FileShareExtension ?? ".json";
            existing.FileShareAppendTimestamp = config.FileShareAppendTimestamp;
            
            existing.AzureQueueServiceUri = config.AzureQueueServiceUri;
            existing.AzureQueueName = config.AzureQueueName ?? "secureboot-reports";
            existing.AzureQueueAuthMethod = config.AzureQueueAuthMethod ?? "DefaultAzureCredential";
            existing.AzureQueueConnectionString = config.AzureQueueConnectionString;
            existing.AzureQueueClientId = config.AzureQueueClientId;
            existing.AzureQueueTenantId = config.AzureQueueTenantId;
            existing.AzureQueueClientSecret = config.AzureQueueClientSecret;
            existing.AzureQueueCertPath = config.AzureQueueCertPath;
            existing.AzureQueueCertPassword = config.AzureQueueCertPassword;
            existing.AzureQueueCertThumbprint = config.AzureQueueCertThumbprint;
            existing.AzureQueueCertStoreLocation = config.AzureQueueCertStoreLocation ?? "CurrentUser";
            existing.AzureQueueCertStoreName = config.AzureQueueCertStoreName ?? "My";
            existing.AzureQueueVisibilityTimeoutSeconds = config.AzureQueueVisibilityTimeoutSeconds;
            existing.AzureQueueMaxSendRetryCount = config.AzureQueueMaxSendRetryCount;
            
            existing.WebApiBaseAddress = config.WebApiBaseAddress;
            existing.WebApiIngestionRoute = config.WebApiIngestionRoute ?? "/api/SecureBootReports";
            existing.WebApiTimeoutSeconds = config.WebApiTimeoutSeconds;
            existing.WebApiUseCertAuth = config.WebApiUseCertAuth;
            existing.WebApiCertPath = config.WebApiCertPath;
            existing.WebApiCertPassword = config.WebApiCertPassword;
            existing.WebApiCertThumbprint = config.WebApiCertThumbprint;
            existing.WebApiCertStoreLocation = config.WebApiCertStoreLocation ?? "LocalMachine";
            existing.WebApiCertStoreName = config.WebApiCertStoreName ?? "My";
            
            existing.Description = config.Description;
            existing.IsActive = config.IsActive;
            existing.UpdatedAtUtc = DateTimeOffset.UtcNow;
            existing.UpdatedBy = User.Identity?.Name ?? "API";

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Updated sink configuration {Id} by {User}", id, existing.UpdatedBy);

            return Ok(existing);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating sink configuration {Id}", id);
            return StatusCode(500, new { error = "Internal server error", details = ex.Message });
        }
    }

    /// <summary>
    /// Set configuration as active (deactivates all others).
    /// </summary>
    [HttpPost("{id}/activate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activate(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var config = await _context.ClientSinkConfig.FindAsync(new object[] { id }, cancellationToken);
            if (config == null)
            {
                return NotFound();
            }

            // Deactivate all others
            var others = await _context.ClientSinkConfig
                .Where(c => c.Id != id && c.IsActive)
                .ToListAsync(cancellationToken);

            foreach (var other in others)
            {
                other.IsActive = false;
                other.UpdatedAtUtc = DateTimeOffset.UtcNow;
                other.UpdatedBy = User.Identity?.Name ?? "API";
            }

            // Activate selected
            config.IsActive = true;
            config.UpdatedAtUtc = DateTimeOffset.UtcNow;
            config.UpdatedBy = User.Identity?.Name ?? "API";

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Activated sink configuration {Id} by {User}", id, config.UpdatedBy);

            return Ok(new { message = "Configuration activated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating sink configuration {Id}", id);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Delete configuration.
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var config = await _context.ClientSinkConfig.FindAsync(new object[] { id }, cancellationToken);
            if (config == null)
            {
                return NotFound();
            }

            if (config.IsActive)
            {
                return BadRequest(new { error = "Cannot delete active configuration. Deactivate it first or activate another." });
            }

            _context.ClientSinkConfig.Remove(config);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Deleted sink configuration {Id} by {User}", id, User.Identity?.Name ?? "API");

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting sink configuration {Id}", id);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Maps database entity to SinkOptions for client consumption.
    /// </summary>
    private static SinkOptions MapToSinkOptions(ClientSinkConfigEntity config)
    {
        return new SinkOptions
        {
            EnableFileShare = config.EnableFileShare,
            EnableWebApi = config.EnableWebApi,
            EnableAzureFunction = config.EnableAzureFunction,
            ExecutionStrategy = config.ExecutionStrategy,
            SinkPriority = config.SinkPriority,
            MaxRetryAttempts = config.MaxRetryAttempts,
            RetryDelay = TimeSpan.FromSeconds(config.RetryDelaySeconds),
            UseExponentialBackoff = config.UseExponentialBackoff,
            
            FileShare = new FileShareSinkOptions
            {
                RootPath = config.FileShareRootPath,
                FileExtension = config.FileShareExtension,
                AppendTimestampToFileName = config.FileShareAppendTimestamp
            },
            
            WebApi = new WebApiSinkOptions
            {
                BaseAddress = !string.IsNullOrWhiteSpace(config.WebApiBaseAddress) 
                    ? new Uri(config.WebApiBaseAddress) 
                    : null,
                IngestionRoute = config.WebApiIngestionRoute,
                HttpTimeout = TimeSpan.FromSeconds(config.WebApiTimeoutSeconds),
                UseCertificateAuth = config.WebApiUseCertAuth,
                CertificatePath = config.WebApiCertPath,
                CertificatePassword = config.WebApiCertPassword,
                CertificateThumbprint = config.WebApiCertThumbprint,
                CertificateStoreLocation = config.WebApiCertStoreLocation,
                CertificateStoreName = config.WebApiCertStoreName,
                ValidateCertificateChain = config.WebApiValidateCertChain,
                CheckCertificateRevocation = config.WebApiCheckCertRevocation,
                ExpectedCARootName = config.WebApiExpectedCARootName,
                ExpectedCARootThumbprint = config.WebApiExpectedCARootThumbprint,
                ExpectedSubordinateCAs = ParseCertificateAuthorities(config.WebApiExpectedSubordinateCAsJson)
            },

            AzureFunction = new AzureFunctionSinkOptions
            {
                FunctionUrl = !string.IsNullOrWhiteSpace(config.AzureFunctionUrl)
                    ? new Uri(config.AzureFunctionUrl)
                    : null,
                ApiKey = config.AzureFunctionApiKey,
                HttpTimeout = TimeSpan.FromSeconds(config.AzureFunctionTimeoutSeconds),
                UseApiKeyAsQueryParameter = config.AzureFunctionUseApiKeyAsQueryParam,
                UseCertificateAuth = config.AzureFunctionUseCertAuth,
                CertificatePath = config.AzureFunctionCertPath,
                CertificatePassword = config.AzureFunctionCertPassword,
                CertificateThumbprint = config.AzureFunctionCertThumbprint,
                CertificateStoreLocation = config.AzureFunctionCertStoreLocation,
                CertificateStoreName = config.AzureFunctionCertStoreName,
                ValidateCertificateChain = config.AzureFunctionValidateCertChain,
                CheckCertificateRevocation = config.AzureFunctionCheckCertRevocation,
                ExpectedCARootName = config.AzureFunctionExpectedCARootName,
                ExpectedCARootThumbprint = config.AzureFunctionExpectedCARootThumbprint,
                ExpectedSubordinateCAs = ParseCertificateAuthorities(config.AzureFunctionExpectedSubordinateCAsJson)
            }
        };
    }

    private static List<CertificateAuthorityConfig> ParseCertificateAuthorities(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<CertificateAuthorityConfig>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<CertificateAuthorityConfig>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<CertificateAuthorityConfig>();
        }
        catch
        {
            return new List<CertificateAuthorityConfig>();
        }
    }
}
