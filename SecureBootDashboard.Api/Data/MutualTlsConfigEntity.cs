using System.ComponentModel.DataAnnotations;

namespace SecureBootDashboard.Api.Data;

/// <summary>
/// Entity representing the mutual TLS configuration stored in the database.
/// Provides centralized, database-driven configuration for client certificate validation.
/// </summary>
public class MutualTlsConfigEntity
{
    /// <summary>
    /// Gets or sets the primary key. Only one configuration record should exist.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether mutual TLS authentication is enabled.
    /// When disabled, client certificates are not required or validated.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to allow self-signed certificates.
    /// Should be false in production environments.
    /// </summary>
    public bool AllowSelfSignedCertificates { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to check certificate revocation via CRL/OCSP.
    /// Requires network access to CRL distribution points or OCSP responders.
    /// </summary>
    public bool CheckCertificateRevocation { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to validate the complete certificate chain.
    /// Ensures all certificates in the chain are valid and trusted.
    /// </summary>
    public bool ValidateCertificateChain { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to require extended key usage for client authentication.
    /// Verifies the certificate has EKU OID 1.3.6.1.5.5.7.3.2 (clientAuth).
    /// </summary>
    public bool RequireClientAuthEku { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to validate certificate validity period.
    /// Checks that the current time is within NotBefore and NotAfter dates.
    /// </summary>
    public bool ValidateCertificateValidity { get; set; } = true;

    /// <summary>
    /// Gets or sets the grace period in days before certificate expiration to start rejecting certificates.
    /// Allows time for certificate renewal. 0 means reject only expired certificates.
    /// </summary>
    public int ExpirationGracePeriodDays { get; set; } = 0;

    /// <summary>
    /// Gets or sets a value indicating whether to use thumbprint allowlisting.
    /// When enabled, only certificates with thumbprints in AllowedThumbprints are accepted.
    /// </summary>
    public bool EnableThumbprintAllowlist { get; set; }

    /// <summary>
    /// Gets or sets the comma-separated list of allowed certificate thumbprints (SHA-1).
    /// Only used when EnableThumbprintAllowlist is true.
    /// </summary>
    [MaxLength(4000)]
    public string? AllowedThumbprints { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to use issuer allowlisting.
    /// When enabled, only certificates issued by CAs in the TrustedCertificateAuthorities table are accepted.
    /// </summary>
    public bool EnableIssuerAllowlist { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to log certificate validation details.
    /// Useful for debugging but may generate significant log volume.
    /// </summary>
    public bool EnableDetailedLogging { get; set; }

    /// <summary>
    /// Gets or sets the timeout in seconds for certificate revocation checks.
    /// Prevents long delays if CRL/OCSP endpoints are slow or unavailable.
    /// </summary>
    public int RevocationCheckTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Gets or sets optional validation rules or notes.
    /// Can document custom validation logic or special requirements.
    /// </summary>
    [MaxLength(2000)]
    public string? ValidationNotes { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when this configuration was created.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the username of the administrator who created this configuration.
    /// </summary>
    [MaxLength(256)]
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when this configuration was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the username of the administrator who last updated this configuration.
    /// </summary>
    [MaxLength(256)]
    public string? UpdatedBy { get; set; }
}
