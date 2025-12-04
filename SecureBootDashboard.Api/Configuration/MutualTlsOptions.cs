namespace SecureBootDashboard.Api.Configuration;

/// <summary>
/// Configuration options for mutual TLS authentication.
/// </summary>
public class MutualTlsOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether mutual TLS authentication is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to allow self-signed certificates.
    /// Default is false for production security.
    /// </summary>
    public bool AllowSelfSignedCertificates { get; set; }

    /// <summary>
    /// Gets or sets the allowed certificate thumbprints.
    /// If specified, only certificates with these thumbprints are allowed.
    /// </summary>
    public List<string> AllowedThumbprints { get; set; } = new();

    /// <summary>
    /// Gets or sets the allowed certificate issuers (Certificate Authority CNs).
    /// If specified, only certificates issued by these CAs are allowed.
    /// </summary>
    public List<string> AllowedIssuers { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether to check certificate revocation.
    /// Default is true for production security.
    /// </summary>
    public bool CheckCertificateRevocation { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to validate certificate chain.
    /// Default is true for production security.
    /// </summary>
    public bool ValidateCertificateChain { get; set; } = true;
}
