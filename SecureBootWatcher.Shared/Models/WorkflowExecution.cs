using System;

namespace SecureBootWatcher.Shared.Models
{
    /// <summary>
    /// Represents an execution instance of a remediation workflow.
    /// </summary>
    public sealed class WorkflowExecution
    {
        public Guid Id { get; set; }

        /// <summary>
        /// The workflow that was executed.
        /// </summary>
        public Guid WorkflowId { get; set; }

        /// <summary>
        /// The device that triggered this execution.
        /// </summary>
        public Guid DeviceId { get; set; }

        /// <summary>
        /// The report that triggered this execution.
        /// </summary>
        public Guid? ReportId { get; set; }

        /// <summary>
        /// When this workflow execution started.
        /// </summary>
        public DateTimeOffset StartedAtUtc { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// When this workflow execution completed.
        /// </summary>
        public DateTimeOffset? CompletedAtUtc { get; set; }

        /// <summary>
        /// Status of the workflow execution.
        /// </summary>
        public WorkflowExecutionStatus Status { get; set; } = WorkflowExecutionStatus.Running;

        /// <summary>
        /// Outcome message or error details.
        /// </summary>
        public string? ResultMessage { get; set; }

        /// <summary>
        /// JSON with detailed action results.
        /// </summary>
        public string? ActionsResultJson { get; set; }
    }

    /// <summary>
    /// Status of a workflow execution.
    /// </summary>
    public enum WorkflowExecutionStatus
    {
        Running = 1,
        Completed = 2,
        Failed = 3,
        PartialSuccess = 4
    }
}
