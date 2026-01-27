using Microsoft.AspNetCore.Mvc;
using SecureBootDashboard.Api.Data;
using SecureBootDashboard.Api.Services;

namespace SecureBootDashboard.Api.Controllers;

/// <summary>
/// API controller for managing mutual TLS configuration.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class MutualTlsConfigController : ControllerBase
{
    private readonly ICertificateValidationService _certValidationService;
    private readonly ILogger<MutualTlsConfigController> _logger;

    public MutualTlsConfigController(
        ICertificateValidationService certValidationService,
        ILogger<MutualTlsConfigController> logger)
    {
        _certValidationService = certValidationService;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current mutual TLS configuration.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The current mTLS configuration.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(MutualTlsConfigDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MutualTlsConfigDto>> GetConfigurationAsync(
        CancellationToken cancellationToken = default)
    {
        var config = await _certValidationService.GetConfigurationAsync(cancellationToken);

        if (config == null)
        {
            return NotFound("Mutual TLS configuration not found");
        }

        var dto = MapToDto(config);
        return Ok(dto);
    }

    /// <summary>
    /// Updates the mutual TLS configuration.
    /// </summary>
    /// <param name="request">The updated configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated configuration.</returns>
    [HttpPut]
    [ProducesResponseType(typeof(MutualTlsConfigDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MutualTlsConfigDto>> UpdateConfigurationAsync(
        [FromBody] UpdateMutualTlsConfigRequest request,
        CancellationToken cancellationToken = default)
    {
        // Validate request
        if (request.ExpirationGracePeriodDays < 0)
        {
            return BadRequest("Expiration grace period cannot be negative");
        }

        if (request.RevocationCheckTimeoutSeconds < 1 || request.RevocationCheckTimeoutSeconds > 300)
        {
            return BadRequest("Revocation check timeout must be between 1 and 300 seconds");
        }

        // Get existing configuration
        var existing = await _certValidationService.GetConfigurationAsync(cancellationToken);
        
        // Map request to entity
        var config = existing ?? new MutualTlsConfigEntity { Id = 1 };
        
        config.Enabled = request.Enabled;
        config.AllowSelfSignedCertificates = request.AllowSelfSignedCertificates;
        config.CheckCertificateRevocation = request.CheckCertificateRevocation;
        config.ValidateCertificateChain = request.ValidateCertificateChain;
        config.RequireClientAuthEku = request.RequireClientAuthEku;
        config.ValidateCertificateValidity = request.ValidateCertificateValidity;
        config.ExpirationGracePeriodDays = request.ExpirationGracePeriodDays;
        config.EnableThumbprintAllowlist = request.EnableThumbprintAllowlist;
        config.AllowedThumbprints = request.AllowedThumbprints;
        config.EnableIssuerAllowlist = request.EnableIssuerAllowlist;
        config.EnableDetailedLogging = request.EnableDetailedLogging;
        config.RevocationCheckTimeoutSeconds = request.RevocationCheckTimeoutSeconds;
        config.ValidationNotes = request.ValidationNotes;

        var username = User.Identity?.Name ?? "Anonymous";

        var updated = await _certValidationService.UpdateConfigurationAsync(config, username, cancellationToken);

        _logger.LogInformation("Updated mutual TLS configuration. Enabled: {Enabled}, User: {Username}",
            updated.Enabled, username);

        var dto = MapToDto(updated);
        return Ok(dto);
    }

    /// <summary>
    /// Enables or disables mutual TLS authentication.
    /// </summary>
    /// <param name="request">The enable/disable request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    [HttpPatch("enabled")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetEnabledAsync(
        [FromBody] SetMtlsEnabledRequest request,
        CancellationToken cancellationToken = default)
    {
        var config = await _certValidationService.GetConfigurationAsync(cancellationToken);

        if (config == null)
        {
            return NotFound("Mutual TLS configuration not found");
        }

        config.Enabled = request.Enabled;

        var username = User.Identity?.Name ?? "Anonymous";
        await _certValidationService.UpdateConfigurationAsync(config, username, cancellationToken);

        _logger.LogInformation("Set mutual TLS enabled: {Enabled}, User: {Username}", request.Enabled, username);

        return NoContent();
    }

    /// <summary>
    /// Gets validation statistics and status.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation status information.</returns>
    [HttpGet("status")]
    [ProducesResponseType(typeof(MutualTlsStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MutualTlsStatusDto>> GetStatusAsync(
        CancellationToken cancellationToken = default)
    {
        var config = await _certValidationService.GetConfigurationAsync(cancellationToken);
        var trustedCAs = await _certValidationService.GetTrustedCAsAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var expiringSoonCAs = trustedCAs
            .Where(ca => ca.IsEnabled && ca.NotAfter > now && ca.NotAfter < now.AddDays(90))
            .Count();

        var expiredCAs = trustedCAs
            .Where(ca => ca.IsEnabled && ca.NotAfter < now)
            .Count();

        var status = new MutualTlsStatusDto
        {
            IsEnabled = config?.Enabled ?? false,
            TotalTrustedCAs = trustedCAs.Count,
            EnabledTrustedCAs = trustedCAs.Count(ca => ca.IsEnabled),
            ExpiredCAs = expiredCAs,
            ExpiringSoonCAs = expiringSoonCAs,
            IssuerAllowlistEnabled = config?.EnableIssuerAllowlist ?? false,
            ThumbprintAllowlistEnabled = config?.EnableThumbprintAllowlist ?? false,
            RevocationCheckEnabled = config?.CheckCertificateRevocation ?? false,
            ChainValidationEnabled = config?.ValidateCertificateChain ?? false,
            ConfigurationLastUpdated = config?.UpdatedAtUtc,
            ConfigurationUpdatedBy = config?.UpdatedBy
        };

        return Ok(status);
    }

    private static MutualTlsConfigDto MapToDto(MutualTlsConfigEntity entity)
    {
        return new MutualTlsConfigDto
        {
            Id = entity.Id,
            Enabled = entity.Enabled,
            AllowSelfSignedCertificates = entity.AllowSelfSignedCertificates,
            CheckCertificateRevocation = entity.CheckCertificateRevocation,
            ValidateCertificateChain = entity.ValidateCertificateChain,
            RequireClientAuthEku = entity.RequireClientAuthEku,
            ValidateCertificateValidity = entity.ValidateCertificateValidity,
            ExpirationGracePeriodDays = entity.ExpirationGracePeriodDays,
            EnableThumbprintAllowlist = entity.EnableThumbprintAllowlist,
            AllowedThumbprints = entity.AllowedThumbprints,
            EnableIssuerAllowlist = entity.EnableIssuerAllowlist,
            EnableDetailedLogging = entity.EnableDetailedLogging,
            RevocationCheckTimeoutSeconds = entity.RevocationCheckTimeoutSeconds,
            ValidationNotes = entity.ValidationNotes,
            CreatedAtUtc = entity.CreatedAtUtc,
            CreatedBy = entity.CreatedBy,
            UpdatedAtUtc = entity.UpdatedAtUtc,
            UpdatedBy = entity.UpdatedBy
        };
    }
}

/// <summary>
/// DTO for mutual TLS configuration.
/// </summary>
public class MutualTlsConfigDto
{
    public int Id { get; set; }
    public bool Enabled { get; set; }
    public bool AllowSelfSignedCertificates { get; set; }
    public bool CheckCertificateRevocation { get; set; }
    public bool ValidateCertificateChain { get; set; }
    public bool RequireClientAuthEku { get; set; }
    public bool ValidateCertificateValidity { get; set; }
    public int ExpirationGracePeriodDays { get; set; }
    public bool EnableThumbprintAllowlist { get; set; }
    public string? AllowedThumbprints { get; set; }
    public bool EnableIssuerAllowlist { get; set; }
    public bool EnableDetailedLogging { get; set; }
    public int RevocationCheckTimeoutSeconds { get; set; }
    public string? ValidationNotes { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// Request to update mutual TLS configuration.
/// </summary>
public class UpdateMutualTlsConfigRequest
{
    public bool Enabled { get; set; }
    public bool AllowSelfSignedCertificates { get; set; }
    public bool CheckCertificateRevocation { get; set; }
    public bool ValidateCertificateChain { get; set; }
    public bool RequireClientAuthEku { get; set; }
    public bool ValidateCertificateValidity { get; set; }
    public int ExpirationGracePeriodDays { get; set; }
    public bool EnableThumbprintAllowlist { get; set; }
    public string? AllowedThumbprints { get; set; }
    public bool EnableIssuerAllowlist { get; set; }
    public bool EnableDetailedLogging { get; set; }
    public int RevocationCheckTimeoutSeconds { get; set; }
    public string? ValidationNotes { get; set; }
}

/// <summary>
/// Request to enable/disable mutual TLS.
/// </summary>
public class SetMtlsEnabledRequest
{
    public bool Enabled { get; set; }
}

/// <summary>
/// Status information for mutual TLS.
/// </summary>
public class MutualTlsStatusDto
{
    public bool IsEnabled { get; set; }
    public int TotalTrustedCAs { get; set; }
    public int EnabledTrustedCAs { get; set; }
    public int ExpiredCAs { get; set; }
    public int ExpiringSoonCAs { get; set; }
    public bool IssuerAllowlistEnabled { get; set; }
    public bool ThumbprintAllowlistEnabled { get; set; }
    public bool RevocationCheckEnabled { get; set; }
    public bool ChainValidationEnabled { get; set; }
    public DateTimeOffset? ConfigurationLastUpdated { get; set; }
    public string? ConfigurationUpdatedBy { get; set; }
}
