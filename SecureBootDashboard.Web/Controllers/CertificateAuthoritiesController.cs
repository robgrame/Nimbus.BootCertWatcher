using Microsoft.AspNetCore.Mvc;
using SecureBootDashboard.Api.Data;
using SecureBootDashboard.Api.Services;

namespace SecureBootDashboard.Api.Controllers;

/// <summary>
/// API controller for managing trusted Certificate Authorities for mutual TLS authentication.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CertificateAuthoritiesController : ControllerBase
{
    private readonly ICertificateValidationService _certValidationService;
    private readonly ILogger<CertificateAuthoritiesController> _logger;

    public CertificateAuthoritiesController(
        ICertificateValidationService certValidationService,
        ILogger<CertificateAuthoritiesController> logger)
    {
        _certValidationService = certValidationService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all trusted Certificate Authorities.
    /// </summary>
    /// <param name="includeDisabled">Include disabled CAs in the response.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of trusted CAs.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TrustedCADto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TrustedCADto>>> GetAllAsync(
        [FromQuery] bool includeDisabled = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting all trusted CAs (includeDisabled: {IncludeDisabled})", includeDisabled);

        var cas = await _certValidationService.GetTrustedCAsAsync(cancellationToken);
        
        var dtos = cas
            .Where(ca => includeDisabled || ca.IsEnabled)
            .Select(ca => new TrustedCADto
            {
                Id = ca.Id,
                CommonName = ca.CommonName,
                Thumbprint = ca.Thumbprint,
                Thumbprint256 = ca.Thumbprint256,
                Subject = ca.Subject,
                Issuer = ca.Issuer,
                NotBefore = ca.NotBefore,
                NotAfter = ca.NotAfter,
                IsRootCa = ca.IsRootCa,
                SerialNumber = ca.SerialNumber,
                IsEnabled = ca.IsEnabled,
                Description = ca.Description,
                CreatedAtUtc = ca.CreatedAtUtc,
                CreatedBy = ca.CreatedBy,
                UpdatedAtUtc = ca.UpdatedAtUtc,
                UpdatedBy = ca.UpdatedBy,
                IsExpired = ca.NotAfter < DateTimeOffset.UtcNow,
                DaysUntilExpiration = (ca.NotAfter - DateTimeOffset.UtcNow).Days
            })
            .OrderBy(ca => ca.CommonName)
            .ToList();

        return Ok(dtos);
    }

    /// <summary>
    /// Gets a specific trusted Certificate Authority by ID.
    /// </summary>
    /// <param name="id">The CA ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The trusted CA.</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(TrustedCADto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TrustedCADto>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var cas = await _certValidationService.GetTrustedCAsAsync(cancellationToken);
        var ca = cas.FirstOrDefault(c => c.Id == id);

        if (ca == null)
        {
            return NotFound($"Trusted CA with ID {id} not found");
        }

        var dto = new TrustedCADto
        {
            Id = ca.Id,
            CommonName = ca.CommonName,
            Thumbprint = ca.Thumbprint,
            Thumbprint256 = ca.Thumbprint256,
            Subject = ca.Subject,
            Issuer = ca.Issuer,
            NotBefore = ca.NotBefore,
            NotAfter = ca.NotAfter,
            IsRootCa = ca.IsRootCa,
            SerialNumber = ca.SerialNumber,
            IsEnabled = ca.IsEnabled,
            Description = ca.Description,
            CreatedAtUtc = ca.CreatedAtUtc,
            CreatedBy = ca.CreatedBy,
            UpdatedAtUtc = ca.UpdatedAtUtc,
            UpdatedBy = ca.UpdatedBy,
            IsExpired = ca.NotAfter < DateTimeOffset.UtcNow,
            DaysUntilExpiration = (ca.NotAfter - DateTimeOffset.UtcNow).Days
        };

        return Ok(dto);
    }

    /// <summary>
    /// Uploads and adds a new trusted Certificate Authority.
    /// </summary>
    /// <param name="request">The upload request with certificate file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created trusted CA.</returns>
    [HttpPost("upload")]
    [ProducesResponseType(typeof(TrustedCADto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TrustedCADto>> UploadCertificateAsync(
        [FromForm] UploadCARequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.CertificateFile == null || request.CertificateFile.Length == 0)
        {
            return BadRequest("Certificate file is required");
        }

        // Validate file extension
        var allowedExtensions = new[] { ".cer", ".crt", ".pem", ".der" };
        var fileExtension = Path.GetExtension(request.CertificateFile.FileName).ToLowerInvariant();
        
        if (!allowedExtensions.Contains(fileExtension))
        {
            return BadRequest($"Invalid file extension. Allowed: {string.Join(", ", allowedExtensions)}");
        }

        // Read certificate data
        byte[] certificateData;
        using (var memoryStream = new MemoryStream())
        {
            await request.CertificateFile.CopyToAsync(memoryStream, cancellationToken);
            certificateData = memoryStream.ToArray();
        }

        // Get username from claims (or use a default)
        var username = User.Identity?.Name ?? "Anonymous";

        try
        {
            var ca = await _certValidationService.AddTrustedCAAsync(
                certificateData,
                request.Description,
                username,
                cancellationToken);

            _logger.LogInformation("Uploaded new trusted CA: {CommonName} by {Username}", ca.CommonName, username);

            var dto = new TrustedCADto
            {
                Id = ca.Id,
                CommonName = ca.CommonName,
                Thumbprint = ca.Thumbprint,
                Thumbprint256 = ca.Thumbprint256,
                Subject = ca.Subject,
                Issuer = ca.Issuer,
                NotBefore = ca.NotBefore,
                NotAfter = ca.NotAfter,
                IsRootCa = ca.IsRootCa,
                SerialNumber = ca.SerialNumber,
                IsEnabled = ca.IsEnabled,
                Description = ca.Description,
                CreatedAtUtc = ca.CreatedAtUtc,
                CreatedBy = ca.CreatedBy,
                UpdatedAtUtc = ca.UpdatedAtUtc,
                UpdatedBy = ca.UpdatedBy,
                IsExpired = ca.NotAfter < DateTimeOffset.UtcNow,
                DaysUntilExpiration = (ca.NotAfter - DateTimeOffset.UtcNow).Days
            };

            return CreatedAtAction(nameof(GetByIdAsync), new { id = ca.Id }, dto);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to upload certificate");
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Enables or disables a trusted Certificate Authority.
    /// </summary>
    /// <param name="id">The CA ID.</param>
    /// <param name="request">The enable/disable request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    [HttpPatch("{id}/enabled")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetEnabledAsync(
        int id,
        [FromBody] SetEnabledRequest request,
        CancellationToken cancellationToken = default)
    {
        var username = User.Identity?.Name ?? "Anonymous";

        var success = await _certValidationService.SetCAEnabledAsync(
            id,
            request.Enabled,
            username,
            cancellationToken);

        if (!success)
        {
            return NotFound($"Trusted CA with ID {id} not found");
        }

        _logger.LogInformation("Updated CA enabled status: ID={Id}, Enabled={Enabled}, User={Username}",
            id, request.Enabled, username);

        return NoContent();
    }

    /// <summary>
    /// Deletes a trusted Certificate Authority.
    /// </summary>
    /// <param name="id">The CA ID to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var success = await _certValidationService.RemoveTrustedCAAsync(id, cancellationToken);

        if (!success)
        {
            return NotFound($"Trusted CA with ID {id} not found");
        }

        _logger.LogInformation("Deleted trusted CA: ID={Id}, User={Username}",
            id, User.Identity?.Name ?? "Anonymous");

        return NoContent();
    }
}

/// <summary>
/// DTO for trusted Certificate Authority.
/// </summary>
public class TrustedCADto
{
    public int Id { get; set; }
    public string CommonName { get; set; } = string.Empty;
    public string Thumbprint { get; set; } = string.Empty;
    public string? Thumbprint256 { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public DateTimeOffset NotBefore { get; set; }
    public DateTimeOffset NotAfter { get; set; }
    public bool IsRootCa { get; set; }
    public string? SerialNumber { get; set; }
    public bool IsEnabled { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsExpired { get; set; }
    public int DaysUntilExpiration { get; set; }
}

/// <summary>
/// Request to upload a CA certificate.
/// </summary>
public class UploadCARequest
{
    /// <summary>
    /// The certificate file (.cer, .crt, .pem, .der).
    /// </summary>
    public IFormFile CertificateFile { get; set; } = null!;

    /// <summary>
    /// Optional description for the CA.
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Request to enable/disable a CA.
/// </summary>
public class SetEnabledRequest
{
    /// <summary>
    /// True to enable, false to disable.
    /// </summary>
    public bool Enabled { get; set; }
}
