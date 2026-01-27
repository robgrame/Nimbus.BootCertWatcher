using System.ComponentModel.DataAnnotations;

namespace SecureBootDashboard.Api.Data;

/// <summary>
/// Entity representing a trusted Certificate Authority for client certificate validation.
/// Stores CA certificates uploaded by administrators to validate client certificate chains.
/// </summary>
public class TrustedCertificateAuthorityEntity
{
    /// <summary>
    /// Gets or sets the primary key.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the common name (CN) of the Certificate Authority.
    /// Extracted from the certificate subject.
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string CommonName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the SHA-1 thumbprint of the CA certificate.
    /// Used for exact certificate matching during validation.
    /// </summary>
    [Required]
    [MaxLength(40)]
    public string Thumbprint { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the SHA-256 thumbprint of the CA certificate.
    /// Provides stronger cryptographic hash for certificate identification.
    /// </summary>
    [MaxLength(64)]
    public string? Thumbprint256 { get; set; }

    /// <summary>
    /// Gets or sets the certificate subject distinguished name.
    /// Full subject DN from the X.509 certificate.
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the certificate issuer distinguished name.
    /// For root CAs, this is the same as Subject. For intermediate CAs, it's the parent CA.
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the certificate not-before date.
    /// Certificate is not valid before this date.
    /// </summary>
    public DateTimeOffset NotBefore { get; set; }

    /// <summary>
    /// Gets or sets the certificate not-after (expiration) date.
    /// Certificate is not valid after this date.
    /// </summary>
    public DateTimeOffset NotAfter { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this is a root CA certificate.
    /// Root CAs are self-signed (Subject == Issuer).
    /// </summary>
    public bool IsRootCa { get; set; }

    /// <summary>
    /// Gets or sets the serial number of the certificate.
    /// </summary>
    [MaxLength(100)]
    public string? SerialNumber { get; set; }

    /// <summary>
    /// Gets or sets the certificate data in Base64-encoded DER format.
    /// Stores the complete certificate for chain validation.
    /// </summary>
    [Required]
    public string CertificateDataBase64 { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this CA is enabled for validation.
    /// Disabled CAs are not used for client certificate validation.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets an optional description or notes about this CA.
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when this CA was added.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the username of the administrator who added this CA.
    /// </summary>
    [MaxLength(256)]
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when this CA was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the username of the administrator who last updated this CA.
    /// </summary>
    [MaxLength(256)]
    public string? UpdatedBy { get; set; }
}
