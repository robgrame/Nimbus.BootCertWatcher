namespace SecureBootReportProxy.Functions.Configuration;

/// <summary>
/// Configuration options for the Secure Boot Report Proxy Azure Function.
/// Maps environment variables to strongly-typed configuration.
/// </summary>
public sealed class ProxyFunctionOptions
{
    /// <summary>
    /// API Key for authenticating incoming requests.
    /// Clients must provide this key via X-API-Key header or ?code= query parameter.
    /// Required.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Target Azure Queue Storage URI where reports will be forwarded.
    /// Example: https://mystorageaccount.queue.core.windows.net
    /// Required.
    /// </summary>
    public string QueueStorageUri { get; set; } = string.Empty;

    /// <summary>
    /// Target queue name within the storage account.
    /// Default: "secureboot-reports"
    /// </summary>
    public string QueueName { get; set; } = "secureboot-reports";

    /// <summary>
    /// Require client certificate authentication (mutual TLS) in addition to API key.
    /// When enabled, clients must present a valid certificate matching the allowlist.
    /// Default: false
    /// </summary>
    public bool RequireCertificateAuthentication { get; set; }

    /// <summary>
    /// Certificate authentication options when RequireCertificateAuthentication is enabled.
    /// </summary>
    public CertificateAuthenticationOptions CertificateAuthentication { get; set; } = new();
}

/// <summary>
/// Certificate authentication configuration for the proxy function.
/// </summary>
public sealed class CertificateAuthenticationOptions
{
    /// <summary>
    /// Comma-separated list of allowed certificate thumbprints (SHA-1).
    /// Format: "ABC123,DEF456,..." (no spaces or colons in thumbprints)
    /// Only certificates with these thumbprints will be accepted.
    /// If empty, any valid certificate is accepted (not recommended for production).
    /// </summary>
    public string AllowedThumbprints { get; set; } = string.Empty;

    /// <summary>
    /// Validate certificate expiration dates.
    /// Default: true
    /// </summary>
    public bool ValidateExpiration { get; set; } = true;

    /// <summary>
    /// Validate certificate chain (ensure it chains to a trusted root CA).
    /// Default: true (recommended for production)
    /// </summary>
    public bool ValidateCertificateChain { get; set; } = true;

    /// <summary>
    /// Check Certificate Revocation List (CRL) for revoked certificates.
    /// Default: false (can cause delays if CRL server is unavailable)
    /// </summary>
    public bool CheckCertificateRevocation { get; set; }

    /// <summary>
    /// Expected Root CA certificate Subject name.
    /// If specified, validates that the certificate chain's root CA matches this subject.
    /// Example: "CN=Contoso Root CA, O=Contoso, C=US"
    /// Optional: Leave empty to skip Root CA subject validation.
    /// </summary>
    public string? ExpectedCARootName { get; set; }

    /// <summary>
    /// Expected Root CA certificate thumbprint (SHA-1).
    /// If specified, validates that the certificate chain's root CA has this thumbprint.
    /// Format: "ABC123DEF456..." (no spaces or colons)
    /// Optional: Leave empty to skip Root CA thumbprint validation.
    /// Recommended: Use this in combination with ExpectedCARootName for stronger validation.
    /// </summary>
    public string? ExpectedCARootThumbprint { get; set; }

    /// <summary>
    /// Expected Subordinate (Intermediate) CA certificates as JSON array.
    /// Format: [{"name":"CN=Contoso Issuing CA 01","thumbprint":"ABC123"},{"name":"CN=Contoso Issuing CA 02","thumbprint":"DEF456"}]
    /// If specified, validates that these intermediate CAs are present in the certificate chain.
    /// Optional: Leave empty to skip Subordinate CA validation.
    /// </summary>
    public string? ExpectedSubordinateCAsJson { get; set; }

    /// <summary>
    /// Parsed array of allowed thumbprints for efficient lookup.
    /// Automatically populated from AllowedThumbprints.
    /// </summary>
    public string[] AllowedThumbprintsArray => 
        AllowedThumbprints
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.Replace(":", "").Replace(" ", "").ToUpperInvariant())
            .ToArray();

    /// <summary>
    /// Parsed list of expected Subordinate CAs from JSON.
    /// Automatically populated from ExpectedSubordinateCAsJson.
    /// </summary>
    public List<CertificateAuthorityInfo> ExpectedSubordinateCAs
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ExpectedSubordinateCAsJson))
            {
                return new List<CertificateAuthorityInfo>();
            }

            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<List<CertificateAuthorityInfo>>(
                    ExpectedSubordinateCAsJson,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? new List<CertificateAuthorityInfo>();
            }
            catch
            {
                return new List<CertificateAuthorityInfo>();
            }
        }
    }
}

/// <summary>
/// Certificate Authority information for validation.
/// </summary>
public sealed class CertificateAuthorityInfo
{
    /// <summary>
    /// CA certificate Subject name (e.g., "CN=Contoso Issuing CA 01, O=Contoso, C=US")
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// CA certificate thumbprint (SHA-1, no spaces or colons)
    /// </summary>
    public string? Thumbprint { get; set; }
}
