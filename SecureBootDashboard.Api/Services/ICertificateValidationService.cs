using System.Security.Cryptography.X509Certificates;

namespace SecureBootDashboard.Api.Services;

/// <summary>
/// Service for validating client certificates against database-driven configuration.
/// </summary>
public interface ICertificateValidationService
{
    /// <summary>
    /// Validates a client certificate against the current mutual TLS configuration.
    /// </summary>
    /// <param name="certificate">The client certificate to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A validation result indicating success or failure with detailed reasons.</returns>
    Task<CertificateValidationResult> ValidateClientCertificateAsync(
        X509Certificate2 certificate, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current mutual TLS configuration from the database.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The current mTLS configuration, or null if not configured.</returns>
    Task<Data.MutualTlsConfigEntity?> GetConfigurationAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all enabled trusted Certificate Authorities from the database.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of enabled CA certificates.</returns>
    Task<IReadOnlyList<Data.TrustedCertificateAuthorityEntity>> GetTrustedCAsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new trusted Certificate Authority from a certificate file.
    /// </summary>
    /// <param name="certificateData">The certificate data (DER or PEM encoded).</param>
    /// <param name="description">Optional description for the CA.</param>
    /// <param name="createdBy">Username of the administrator adding the CA.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created CA entity.</returns>
    Task<Data.TrustedCertificateAuthorityEntity> AddTrustedCAAsync(
        byte[] certificateData,
        string? description,
        string createdBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a trusted Certificate Authority by ID.
    /// </summary>
    /// <param name="caId">The ID of the CA to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if removed, false if not found.</returns>
    Task<bool> RemoveTrustedCAAsync(int caId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enables or disables a trusted Certificate Authority.
    /// </summary>
    /// <param name="caId">The ID of the CA to update.</param>
    /// <param name="enabled">True to enable, false to disable.</param>
    /// <param name="updatedBy">Username of the administrator updating the CA.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if updated, false if not found.</returns>
    Task<bool> SetCAEnabledAsync(
        int caId, 
        bool enabled, 
        string updatedBy, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the mutual TLS configuration.
    /// </summary>
    /// <param name="config">The updated configuration.</param>
    /// <param name="updatedBy">Username of the administrator updating the configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated configuration entity.</returns>
    Task<Data.MutualTlsConfigEntity> UpdateConfigurationAsync(
        Data.MutualTlsConfigEntity config,
        string updatedBy,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of certificate validation.
/// </summary>
public class CertificateValidationResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the certificate is valid.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets the list of validation errors (if any).
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of validation warnings (if any).
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Gets or sets detailed validation information for logging.
    /// </summary>
    public Dictionary<string, object> ValidationDetails { get; set; } = new();

    /// <summary>
    /// Gets or sets the matched CA (if issuer validation was used).
    /// </summary>
    public Data.TrustedCertificateAuthorityEntity? MatchedCA { get; set; }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static CertificateValidationResult Success(Data.TrustedCertificateAuthorityEntity? matchedCA = null)
    {
        return new CertificateValidationResult
        {
            IsValid = true,
            MatchedCA = matchedCA
        };
    }

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    public static CertificateValidationResult Failure(params string[] errors)
    {
        return new CertificateValidationResult
        {
            IsValid = false,
            Errors = errors.ToList()
        };
    }
}
