namespace SecureBootDashboard.Web.Services;

public sealed class ApiSettings
{
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Enable client certificate authentication for mutual TLS.
    /// Default: false
    /// </summary>
    public bool UseCertificateAuth { get; set; }

    /// <summary>
    /// Path to client certificate file (.pfx or .p12) for certificate-based authentication.
    /// </summary>
    public string? CertificatePath { get; set; }

    /// <summary>
    /// Password for the certificate file (if the .pfx is password-protected).
    /// Should be stored securely, not in config files.
    /// </summary>
    public string? CertificatePassword { get; set; }

    /// <summary>
    /// Certificate thumbprint for certificate-based authentication from certificate store.
    /// Alternative to CertificatePath - looks for certificate in Windows Certificate Store.
    /// Format: "ABC123DEF456..." (SHA-1 thumbprint, no spaces or colons)
    /// </summary>
    public string? CertificateThumbprint { get; set; }

    /// <summary>
    /// Certificate store location when using CertificateThumbprint.
    /// Values: "CurrentUser" or "LocalMachine"
    /// Default: "LocalMachine"
    /// </summary>
    public string CertificateStoreLocation { get; set; } = "LocalMachine";

    /// <summary>
    /// Certificate store name when using CertificateThumbprint.
    /// Values: "My" (Personal), "Root", "CA", etc.
    /// Default: "My" (Personal certificates)
    /// </summary>
    public string CertificateStoreName { get; set; } = "My";
}
