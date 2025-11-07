using System;
using System.Collections.Generic;

namespace SecureBootWatcher.Shared.Models
{
    /// <summary>
    /// Represents the result of evaluating a device against compliance policies.
    /// </summary>
    public sealed class ComplianceResult
    {
        /// <summary>
        /// Device ID being evaluated.
        /// </summary>
        public Guid DeviceId { get; set; }

        /// <summary>
        /// Overall compliance status.
        /// </summary>
        public ComplianceStatus Status { get; set; }

        /// <summary>
        /// Individual policy violations detected.
        /// </summary>
        public List<PolicyViolation> Violations { get; set; } = new List<PolicyViolation>();

        /// <summary>
        /// Timestamp when evaluation was performed.
        /// </summary>
        public DateTimeOffset EvaluatedAtUtc { get; set; }

        /// <summary>
        /// Report ID used for evaluation.
        /// </summary>
        public Guid? ReportId { get; set; }
    }

    /// <summary>
    /// Overall compliance status for a device.
    /// </summary>
    public enum ComplianceStatus
    {
        /// <summary>
        /// Compliance status could not be determined.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Device is fully compliant with all policies.
        /// </summary>
        Compliant = 1,

        /// <summary>
        /// Device has warnings but no critical violations.
        /// </summary>
        Warning = 2,

        /// <summary>
        /// Device has critical policy violations.
        /// </summary>
        NonCompliant = 3
    }

    /// <summary>
    /// Represents a single policy violation.
    /// </summary>
    public sealed class PolicyViolation
    {
        /// <summary>
        /// Policy that was violated.
        /// </summary>
        public Guid PolicyId { get; set; }

        /// <summary>
        /// Name of the policy.
        /// </summary>
        public string PolicyName { get; set; } = string.Empty;

        /// <summary>
        /// Rule that was violated.
        /// </summary>
        public PolicyRule Rule { get; set; } = new PolicyRule();

        /// <summary>
        /// Description of the violation.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Certificate thumbprint related to the violation (if applicable).
        /// </summary>
        public string? CertificateThumbprint { get; set; }

        /// <summary>
        /// Certificate database where violation occurred (if applicable).
        /// </summary>
        public string? Database { get; set; }
    }
}
