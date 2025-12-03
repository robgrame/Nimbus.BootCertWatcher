namespace SecureBootDashboard.Api.Configuration
{
    /// <summary>
    /// Configuration options for client certificate authentication.
    /// </summary>
    public class ClientCertificateAuthenticationOptions
    {
        /// <summary>
        /// Enable client certificate authentication.
        /// When enabled, API endpoints will require and validate client certificates.
        /// Default: false
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// List of allowed certificate thumbprints (SHA-1).
        /// If empty, any valid certificate is accepted.
        /// Format: "ABC123DEF456..." (no spaces or colons)
        /// </summary>
        public List<string> AllowedCertificateThumbprints { get; set; } = new List<string>();

        /// <summary>
        /// Validate certificate validity period (NotBefore and NotAfter).
        /// Default: true
        /// </summary>
        public bool ValidateValidityPeriod { get; set; } = true;

        /// <summary>
        /// Validate certificate chain (issuer and trust chain).
        /// Default: true
        /// </summary>
        public bool ValidateCertificateChain { get; set; } = true;

        /// <summary>
        /// Check certificate revocation status (requires network access to CRL/OCSP).
        /// When true, validates if certificates have been revoked.
        /// When false, revocation status is not checked (faster but less secure).
        /// Default: false (for compatibility with self-signed certificates and air-gapped environments)
        /// </summary>
        public bool CheckCertificateRevocation { get; set; } = false;

        /// <summary>
        /// Require client certificate for all API requests.
        /// If false, certificate authentication is optional (401 if invalid, but allowed if missing).
        /// If true, requests without certificates are rejected (401).
        /// Default: false
        /// </summary>
        public bool RequireClientCertificate { get; set; } = false;
    }
}
