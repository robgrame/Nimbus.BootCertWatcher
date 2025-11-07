using System;
using System.Collections.Generic;

namespace SecureBootDashboard.Api.Data
{
    /// <summary>
    /// Database entity for remediation workflows.
    /// </summary>
    public sealed class RemediationWorkflowEntity
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public bool IsEnabled { get; set; } = true;

        public int Priority { get; set; } = 100;

        public string TriggerJson { get; set; } = "{}";

        public string ActionsJson { get; set; } = "[]";

        public DateTimeOffset CreatedAtUtc { get; set; }

        public DateTimeOffset UpdatedAtUtc { get; set; }

        public string? CreatedBy { get; set; }

        public string? UpdatedBy { get; set; }

        public ICollection<WorkflowExecutionEntity> Executions { get; set; } = new List<WorkflowExecutionEntity>();
    }
}
