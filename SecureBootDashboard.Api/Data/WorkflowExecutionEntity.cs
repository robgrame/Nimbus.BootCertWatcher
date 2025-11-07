using System;

namespace SecureBootDashboard.Api.Data
{
    /// <summary>
    /// Database entity for workflow execution history.
    /// </summary>
    public sealed class WorkflowExecutionEntity
    {
        public Guid Id { get; set; }

        public Guid WorkflowId { get; set; }

        public RemediationWorkflowEntity? Workflow { get; set; }

        public Guid DeviceId { get; set; }

        public DeviceEntity? Device { get; set; }

        public Guid? ReportId { get; set; }

        public SecureBootReportEntity? Report { get; set; }

        public DateTimeOffset StartedAtUtc { get; set; }

        public DateTimeOffset? CompletedAtUtc { get; set; }

        public int Status { get; set; }

        public string? ResultMessage { get; set; }

        public string? ActionsResultJson { get; set; }
    }
}
