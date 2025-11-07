using System;
using System.Collections.Generic;

namespace SecureBootWatcher.Shared.Models
{
    /// <summary>
    /// Represents an automated remediation workflow that executes actions based on device conditions.
    /// </summary>
    public sealed class RemediationWorkflow
    {
        public Guid Id { get; set; }

        /// <summary>
        /// Workflow name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Workflow description.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Indicates if this workflow is currently enabled.
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Priority of the workflow (lower numbers execute first).
        /// </summary>
        public int Priority { get; set; } = 100;

        /// <summary>
        /// Trigger conditions for this workflow.
        /// </summary>
        public WorkflowTrigger Trigger { get; set; } = new WorkflowTrigger();

        /// <summary>
        /// Actions to execute when trigger conditions are met.
        /// </summary>
        public IList<WorkflowAction> Actions { get; set; } = new List<WorkflowAction>();

        /// <summary>
        /// When this workflow was created.
        /// </summary>
        public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// When this workflow was last updated.
        /// </summary>
        public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// User or system that created this workflow.
        /// </summary>
        public string? CreatedBy { get; set; }

        /// <summary>
        /// User or system that last updated this workflow.
        /// </summary>
        public string? UpdatedBy { get; set; }
    }

    /// <summary>
    /// Defines the trigger conditions for a workflow.
    /// </summary>
    public sealed class WorkflowTrigger
    {
        /// <summary>
        /// Trigger when deployment state matches.
        /// </summary>
        public string? DeploymentState { get; set; }

        /// <summary>
        /// Trigger when certificates are expired.
        /// </summary>
        public bool? HasExpiredCertificates { get; set; }

        /// <summary>
        /// Trigger when certificates will expire within this many days.
        /// </summary>
        public int? CertificateExpiringWithinDays { get; set; }

        /// <summary>
        /// Trigger when alerts contain specific text (comma-separated).
        /// </summary>
        public string? AlertContains { get; set; }

        /// <summary>
        /// Trigger when device is in specific fleet (comma-separated).
        /// </summary>
        public string? FleetIdMatches { get; set; }

        /// <summary>
        /// Trigger when manufacturer matches.
        /// </summary>
        public string? ManufacturerMatches { get; set; }

        /// <summary>
        /// Trigger when device hasn't reported for this many days.
        /// </summary>
        public int? NoReportForDays { get; set; }
    }

    /// <summary>
    /// Defines an action to execute when workflow triggers.
    /// </summary>
    public sealed class WorkflowAction
    {
        /// <summary>
        /// Type of action to execute.
        /// </summary>
        public WorkflowActionType ActionType { get; set; }

        /// <summary>
        /// Configuration for this action (JSON).
        /// </summary>
        public string ConfigurationJson { get; set; } = "{}";

        /// <summary>
        /// Order in which this action should execute.
        /// </summary>
        public int Order { get; set; }
    }

    /// <summary>
    /// Types of actions that can be executed by workflows.
    /// </summary>
    public enum WorkflowActionType
    {
        /// <summary>
        /// Send email notification.
        /// </summary>
        EmailNotification = 1,

        /// <summary>
        /// Call webhook/HTTP endpoint.
        /// </summary>
        Webhook = 2,

        /// <summary>
        /// Log to system log.
        /// </summary>
        LogEntry = 3,

        /// <summary>
        /// Update device tags.
        /// </summary>
        UpdateDeviceTags = 4
    }
}
