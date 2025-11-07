using System;

namespace SecureBootWatcher.Shared.Models
{
    /// <summary>
    /// Represents a single rule within a compliance policy.
    /// </summary>
    public sealed class PolicyRule
    {
        /// <summary>
        /// Type of rule being evaluated.
        /// </summary>
        public PolicyRuleType RuleType { get; set; }

        /// <summary>
        /// Severity level if rule fails.
        /// </summary>
        public PolicySeverity Severity { get; set; }

        /// <summary>
        /// Value or threshold for the rule (interpretation depends on RuleType).
        /// </summary>
        public string? Value { get; set; }

        /// <summary>
        /// Optional database filter (db, dbx, KEK, PK). If null, applies to all databases.
        /// </summary>
        public string? DatabaseFilter { get; set; }
    }

    /// <summary>
    /// Types of policy rules that can be evaluated.
    /// </summary>
    public enum PolicyRuleType
    {
        /// <summary>
        /// Minimum key size in bits (e.g., 2048).
        /// </summary>
        MinimumKeySize = 1,

        /// <summary>
        /// Allowed signature algorithms (comma-separated list).
        /// </summary>
        AllowedSignatureAlgorithms = 2,

        /// <summary>
        /// Disallowed signature algorithms (comma-separated list).
        /// </summary>
        DisallowedSignatureAlgorithms = 3,

        /// <summary>
        /// Maximum certificate age in days from NotBefore date.
        /// </summary>
        MaximumCertificateAge = 4,

        /// <summary>
        /// Minimum days until expiration (warn if certificate expires sooner).
        /// </summary>
        MinimumDaysUntilExpiration = 5,

        /// <summary>
        /// Require certificate to be from Microsoft.
        /// </summary>
        RequireMicrosoftCertificate = 6,

        /// <summary>
        /// Disallow expired certificates.
        /// </summary>
        DisallowExpiredCertificates = 7,

        /// <summary>
        /// Require specific subject pattern (regular expression).
        /// </summary>
        RequireSubjectPattern = 8
    }

    /// <summary>
    /// Severity levels for policy violations.
    /// </summary>
    public enum PolicySeverity
    {
        /// <summary>
        /// Informational notice.
        /// </summary>
        Info = 1,

        /// <summary>
        /// Warning that should be reviewed.
        /// </summary>
        Warning = 2,

        /// <summary>
        /// Critical compliance violation.
        /// </summary>
        Critical = 3
    }
}
