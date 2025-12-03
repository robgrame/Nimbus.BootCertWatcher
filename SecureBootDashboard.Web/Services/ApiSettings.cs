namespace SecureBootDashboard.Web.Services;

public sealed class ApiSettings
{
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Enable client certificate authentication for API calls.
    /// Default: false
    /// </summary>
    public bool UseClientCertificate { get; set; } = false;

    /// <summary>
    /// Certificate thumbprint for client certificate authentication from certificate store.
    /// Format: "ABC123DEF456..." (SHA-1 thumbprint, no spaces or colons)
    /// </summary>
    public string? ClientCertificateThumbprint { get; set; }

    /// <summary>
    /// Certificate store location when using ClientCertificateThumbprint.
    /// Values: "CurrentUser" or "LocalMachine"
    /// Default: "LocalMachine"
    /// </summary>
    public string ClientCertificateStoreLocation { get; set; } = "LocalMachine";

    /// <summary>
    /// Certificate store name when using ClientCertificateThumbprint.
    /// Values: "My" (Personal), "Root", "CA", etc.
    /// Default: "My" (Personal certificates)
    /// </summary>
    public string ClientCertificateStoreName { get; set; } = "My";

    /// <summary>
    /// Path to certificate file (.pfx) for client certificate authentication.
    /// Alternative to using certificate store.
    /// </summary>
    public string? ClientCertificatePath { get; set; }

    /// <summary>
    /// Password for the certificate file (if the .pfx is password-protected).
    /// Should be stored securely (Azure Key Vault, environment variables, etc.)
    /// </summary>
    public string? ClientCertificatePassword { get; set; }
}
