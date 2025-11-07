using System;
using System.Collections.Generic;

namespace SecureBootWatcher.Shared.Models
{
    /// <summary>
    /// Represents a certificate compliance policy that defines rules for certificate validation.
    /// </summary>
    public sealed class CertificateCompliancePolicy
    {
        /// <summary>
        /// Unique identifier for the policy.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Human-readable name for the policy.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Optional description of the policy.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Indicates if this policy is enabled and should be evaluated.
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Priority/order in which policies are evaluated (lower numbers = higher priority).
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// Optional fleet ID to scope policy to specific fleets. If null, applies to all fleets.
        /// </summary>
        public string? FleetId { get; set; }

        /// <summary>
        /// Policy rules that define compliance requirements.
        /// </summary>
        public List<PolicyRule> Rules { get; set; } = new List<PolicyRule>();

        /// <summary>
        /// Date when policy was created.
        /// </summary>
        public DateTimeOffset CreatedAtUtc { get; set; }

        /// <summary>
        /// Date when policy was last modified.
        /// </summary>
        public DateTimeOffset? ModifiedAtUtc { get; set; }
    }
}
