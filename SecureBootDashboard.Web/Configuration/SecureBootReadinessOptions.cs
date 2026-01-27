using System.Collections.Generic;

namespace SecureBootDashboard.Api.Configuration
{
    /// <summary>
    /// Configuration options for determining Secure Boot readiness criteria.
    /// </summary>
    public sealed class SecureBootReadinessOptions
    {
        public const string SectionName = "SecureBootReadiness";

        /// <summary>
        /// Number of days before certificate expiration to show warning (default: 180 days / 6 months).
        /// </summary>
        public int CertificateExpirationWarningDays { get; set; } = 180;

        /// <summary>
        /// Number of days before certificate expiration to mark as critical (default: 90 days / 3 months).
        /// </summary>
        public int CertificateExpirationCriticalDays { get; set; } = 90;

        /// <summary>
        /// Require Windows UEFI CA 2023 to be present in db database.
        /// </summary>
        public bool RequireWindowsUEFICA2023 { get; set; } = true;

        /// <summary>
        /// SHA-1 thumbprint of Windows UEFI CA 2023 certificate.
        /// Default: 45A0FA32604773C82433C3B7D59E7466B3AC0C67
        /// </summary>
        public string WindowsUEFICA2023Thumbprint { get; set; } = "45A0FA32604773C82433C3B7D59E7466B3AC0C67";

        /// <summary>
        /// Require OEM certificates in db to be valid (not expired and not expiring soon).
        /// </summary>
        public bool RequireOemCertificatesValid { get; set; } = true;

        /// <summary>
        /// Minimum OS build versions required for Secure Boot certificate updates.
        /// Key format: "Windows10_22H2", "Windows11_22H2", etc.
        /// Value format: "10.0.19045.5131" (Major.Minor.Build.UBR)
        /// </summary>
        public Dictionary<string, string> MinimumOSBuildVersions { get; set; } = new()
        {
            { "Windows10_22H2", "10.0.19045.5131" },
            { "Windows11_21H2", "10.0.22000.3079" },
            { "Windows11_22H2", "10.0.22621.4317" },
            { "Windows11_23H2", "10.0.22631.4317" },
            { "Windows11_24H2", "10.0.26100.2314" }
        };
    }
}
