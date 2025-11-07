using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SecureBootDashboard.Api.Data;
using SecureBootWatcher.Shared.Models;

namespace SecureBootDashboard.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public sealed class RemediationWorkflowsController : ControllerBase
    {
        private readonly SecureBootDbContext _dbContext;
        private readonly ILogger<RemediationWorkflowsController> _logger;

        public RemediationWorkflowsController(
            SecureBootDbContext dbContext,
            ILogger<RemediationWorkflowsController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        /// <summary>
        /// Get all remediation workflows
        /// </summary>
        [HttpGet]
        public async Task<IReadOnlyCollection<WorkflowSummaryResponse>> GetWorkflowsAsync(CancellationToken cancellationToken)
        {
            var workflows = await _dbContext.RemediationWorkflows
                .AsNoTracking()
                .OrderBy(w => w.Priority)
                .ThenBy(w => w.Name)
                .ToListAsync(cancellationToken);

            return workflows.Select(w => new WorkflowSummaryResponse(
                w.Id,
                w.Name,
                w.Description,
                w.IsEnabled,
                w.Priority,
                w.CreatedAtUtc,
                w.UpdatedAtUtc)).ToArray();
        }

        /// <summary>
        /// Get workflow by ID
        /// </summary>
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<WorkflowDetailResponse>> GetWorkflowAsync(Guid id, CancellationToken cancellationToken)
        {
            var workflow = await _dbContext.RemediationWorkflows
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

            if (workflow == null)
            {
                return NotFound();
            }

            var trigger = JsonSerializer.Deserialize<WorkflowTrigger>(workflow.TriggerJson);
            var actions = JsonSerializer.Deserialize<List<WorkflowAction>>(workflow.ActionsJson);

            return new WorkflowDetailResponse(
                workflow.Id,
                workflow.Name,
                workflow.Description,
                workflow.IsEnabled,
                workflow.Priority,
                trigger ?? new WorkflowTrigger(),
                actions ?? new List<WorkflowAction>(),
                workflow.CreatedAtUtc,
                workflow.UpdatedAtUtc,
                workflow.CreatedBy,
                workflow.UpdatedBy);
        }

        /// <summary>
        /// Create a new workflow
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<WorkflowDetailResponse>> CreateWorkflowAsync(
            [FromBody] CreateWorkflowRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest("Workflow name is required");
            }

            var workflow = new RemediationWorkflowEntity
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Description = request.Description,
                IsEnabled = request.IsEnabled,
                Priority = request.Priority,
                TriggerJson = JsonSerializer.Serialize(request.Trigger ?? new WorkflowTrigger()),
                ActionsJson = JsonSerializer.Serialize(request.Actions ?? new List<WorkflowAction>()),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = request.CreatedBy
            };

            _dbContext.RemediationWorkflows.Add(workflow);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Created workflow {WorkflowId} with name length {NameLength}", 
                workflow.Id, workflow.Name.Length);

            return CreatedAtAction(
                nameof(GetWorkflowAsync),
                new { id = workflow.Id },
                new WorkflowDetailResponse(
                    workflow.Id,
                    workflow.Name,
                    workflow.Description,
                    workflow.IsEnabled,
                    workflow.Priority,
                    request.Trigger ?? new WorkflowTrigger(),
                    request.Actions ?? new List<WorkflowAction>(),
                    workflow.CreatedAtUtc,
                    workflow.UpdatedAtUtc,
                    workflow.CreatedBy,
                    workflow.UpdatedBy));
        }

        /// <summary>
        /// Update an existing workflow
        /// </summary>
        [HttpPut("{id:guid}")]
        public async Task<ActionResult<WorkflowDetailResponse>> UpdateWorkflowAsync(
            Guid id,
            [FromBody] UpdateWorkflowRequest request,
            CancellationToken cancellationToken)
        {
            var workflow = await _dbContext.RemediationWorkflows
                .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

            if (workflow == null)
            {
                return NotFound();
            }

            workflow.Name = request.Name ?? workflow.Name;
            workflow.Description = request.Description;
            workflow.IsEnabled = request.IsEnabled;
            workflow.Priority = request.Priority;
            workflow.TriggerJson = JsonSerializer.Serialize(request.Trigger ?? new WorkflowTrigger());
            workflow.ActionsJson = JsonSerializer.Serialize(request.Actions ?? new List<WorkflowAction>());
            workflow.UpdatedAtUtc = DateTimeOffset.UtcNow;
            workflow.UpdatedBy = request.UpdatedBy;

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Updated workflow {WorkflowId} with name length {NameLength}", 
                workflow.Id, workflow.Name.Length);

            var trigger = JsonSerializer.Deserialize<WorkflowTrigger>(workflow.TriggerJson);
            var actions = JsonSerializer.Deserialize<List<WorkflowAction>>(workflow.ActionsJson);

            return new WorkflowDetailResponse(
                workflow.Id,
                workflow.Name,
                workflow.Description,
                workflow.IsEnabled,
                workflow.Priority,
                trigger ?? new WorkflowTrigger(),
                actions ?? new List<WorkflowAction>(),
                workflow.CreatedAtUtc,
                workflow.UpdatedAtUtc,
                workflow.CreatedBy,
                workflow.UpdatedBy);
        }

        /// <summary>
        /// Delete a workflow
        /// </summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteWorkflowAsync(Guid id, CancellationToken cancellationToken)
        {
            var workflow = await _dbContext.RemediationWorkflows
                .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

            if (workflow == null)
            {
                return NotFound();
            }

            _dbContext.RemediationWorkflows.Remove(workflow);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Deleted workflow {WorkflowId}", workflow.Id);

            return NoContent();
        }

        /// <summary>
        /// Get workflow execution history
        /// </summary>
        [HttpGet("{id:guid}/executions")]
        public async Task<IReadOnlyCollection<ExecutionHistoryResponse>> GetWorkflowExecutionsAsync(
            Guid id,
            [FromQuery] int limit = 50,
            CancellationToken cancellationToken = default)
        {
            limit = Math.Clamp(limit, 1, 200);

            var executions = await _dbContext.WorkflowExecutions
                .AsNoTracking()
                .Where(e => e.WorkflowId == id)
                .Include(e => e.Device)
                .OrderByDescending(e => e.StartedAtUtc)
                .Take(limit)
                .ToListAsync(cancellationToken);

            return executions.Select(e => new ExecutionHistoryResponse(
                e.Id,
                e.DeviceId,
                e.Device?.MachineName ?? "Unknown",
                e.StartedAtUtc,
                e.CompletedAtUtc,
                (WorkflowExecutionStatus)e.Status,
                e.ResultMessage)).ToArray();
        }

        public sealed record WorkflowSummaryResponse(
            Guid Id,
            string Name,
            string? Description,
            bool IsEnabled,
            int Priority,
            DateTimeOffset CreatedAtUtc,
            DateTimeOffset UpdatedAtUtc);

        public sealed record WorkflowDetailResponse(
            Guid Id,
            string Name,
            string? Description,
            bool IsEnabled,
            int Priority,
            WorkflowTrigger Trigger,
            IList<WorkflowAction> Actions,
            DateTimeOffset CreatedAtUtc,
            DateTimeOffset UpdatedAtUtc,
            string? CreatedBy,
            string? UpdatedBy);

        public sealed record CreateWorkflowRequest(
            string Name,
            string? Description,
            bool IsEnabled,
            int Priority,
            WorkflowTrigger? Trigger,
            IList<WorkflowAction>? Actions,
            string? CreatedBy);

        public sealed record UpdateWorkflowRequest(
            string? Name,
            string? Description,
            bool IsEnabled,
            int Priority,
            WorkflowTrigger? Trigger,
            IList<WorkflowAction>? Actions,
            string? UpdatedBy);

        public sealed record ExecutionHistoryResponse(
            Guid Id,
            Guid DeviceId,
            string DeviceName,
            DateTimeOffset StartedAtUtc,
            DateTimeOffset? CompletedAtUtc,
            WorkflowExecutionStatus Status,
            string? ResultMessage);
    }
}
