using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SecureBootDashboard.Api.Data;
using SecureBootWatcher.Shared.Models;

namespace SecureBootDashboard.Api.Services
{
    /// <summary>
    /// Service for evaluating workflow triggers and executing actions.
    /// </summary>
    public sealed class WorkflowEngine
    {
        private readonly SecureBootDbContext _dbContext;
        private readonly ILogger<WorkflowEngine> _logger;

        public WorkflowEngine(
            SecureBootDbContext dbContext,
            ILogger<WorkflowEngine> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        /// <summary>
        /// Evaluates workflows for a device and report, executing matching workflows.
        /// </summary>
        public async Task<IReadOnlyList<WorkflowExecution>> EvaluateAndExecuteAsync(
            Guid deviceId,
            Guid reportId,
            CancellationToken cancellationToken = default)
        {
            var executions = new List<WorkflowExecution>();

            try
            {
                // Load device with latest report
                var device = await _dbContext.Devices
                    .Include(d => d.Reports.OrderByDescending(r => r.CreatedAtUtc).Take(1))
                    .FirstOrDefaultAsync(d => d.Id == deviceId, cancellationToken);

                if (device == null)
                {
                    _logger.LogWarning("Device {DeviceId} not found for workflow evaluation", deviceId);
                    return executions;
                }

                var report = device.Reports.FirstOrDefault();
                if (report == null)
                {
                    _logger.LogWarning("No reports found for device {DeviceId}", deviceId);
                    return executions;
                }

                // Load enabled workflows
                var workflows = await _dbContext.RemediationWorkflows
                    .Where(w => w.IsEnabled)
                    .OrderBy(w => w.Priority)
                    .ToListAsync(cancellationToken);

                _logger.LogInformation("Evaluating {Count} enabled workflows for device {DeviceId}", 
                    workflows.Count, deviceId);

                foreach (var workflowEntity in workflows)
                {
                    var workflow = MapToWorkflow(workflowEntity);
                    if (await EvaluateTriggerAsync(workflow.Trigger, device, report, cancellationToken))
                    {
                        _logger.LogInformation("Workflow {WorkflowId} ({WorkflowName}) triggered for device {DeviceId}",
                            workflow.Id, workflow.Name, deviceId);

                        var execution = await ExecuteWorkflowAsync(workflow, device, report, cancellationToken);
                        executions.Add(execution);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating workflows for device {DeviceId}", deviceId);
            }

            return executions;
        }

        private async Task<bool> EvaluateTriggerAsync(
            WorkflowTrigger trigger,
            DeviceEntity device,
            SecureBootReportEntity report,
            CancellationToken cancellationToken)
        {
            try
            {
                // Check deployment state
                if (!string.IsNullOrEmpty(trigger.DeploymentState))
                {
                    if (!string.Equals(report.DeploymentState, trigger.DeploymentState, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }

                // Check fleet ID
                if (!string.IsNullOrEmpty(trigger.FleetIdMatches))
                {
                    var fleetIds = trigger.FleetIdMatches.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(f => f.Trim());
                    if (!fleetIds.Any(f => string.Equals(device.FleetId, f, StringComparison.OrdinalIgnoreCase)))
                    {
                        return false;
                    }
                }

                // Check manufacturer
                if (!string.IsNullOrEmpty(trigger.ManufacturerMatches))
                {
                    if (!string.Equals(device.Manufacturer, trigger.ManufacturerMatches, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }

                // Check no report for days
                if (trigger.NoReportForDays.HasValue)
                {
                    var daysSinceLastReport = (DateTimeOffset.UtcNow - device.LastSeenUtc).TotalDays;
                    if (daysSinceLastReport < trigger.NoReportForDays.Value)
                    {
                        return false;
                    }
                }

                // Check alerts
                if (!string.IsNullOrEmpty(trigger.AlertContains) && !string.IsNullOrEmpty(report.AlertsJson))
                {
                    var alerts = JsonSerializer.Deserialize<List<string>>(report.AlertsJson);
                    var searchTerms = trigger.AlertContains.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(t => t.Trim());
                    
                    var hasMatch = alerts?.Any(alert => 
                        searchTerms.Any(term => alert.Contains(term, StringComparison.OrdinalIgnoreCase))) ?? false;
                    
                    if (!hasMatch)
                    {
                        return false;
                    }
                }

                // Check certificate expiration
                if (!string.IsNullOrEmpty(report.CertificatesJson))
                {
                    var certificates = JsonSerializer.Deserialize<SecureBootCertificateCollection>(report.CertificatesJson);
                    
                    if (trigger.HasExpiredCertificates.HasValue && trigger.HasExpiredCertificates.Value)
                    {
                        if (certificates?.ExpiredCertificateCount == 0)
                        {
                            return false;
                        }
                    }

                    if (trigger.CertificateExpiringWithinDays.HasValue && certificates != null)
                    {
                        var expiringCount = CountExpiringCertificates(certificates, trigger.CertificateExpiringWithinDays.Value);
                        if (expiringCount == 0)
                        {
                            return false;
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating trigger for workflow");
                return false;
            }
        }

        private int CountExpiringCertificates(SecureBootCertificateCollection certificates, int days)
        {
            var allCerts = new List<SecureBootCertificate>();
            allCerts.AddRange(certificates.SignatureDatabase);
            allCerts.AddRange(certificates.ForbiddenDatabase);
            allCerts.AddRange(certificates.KeyExchangeKeys);
            allCerts.AddRange(certificates.PlatformKeys);

            return allCerts.Count(c => 
                c.DaysUntilExpiration.HasValue && 
                c.DaysUntilExpiration.Value >= 0 && 
                c.DaysUntilExpiration.Value <= days);
        }

        private async Task<WorkflowExecution> ExecuteWorkflowAsync(
            RemediationWorkflow workflow,
            DeviceEntity device,
            SecureBootReportEntity report,
            CancellationToken cancellationToken)
        {
            var execution = new WorkflowExecution
            {
                Id = Guid.NewGuid(),
                WorkflowId = workflow.Id,
                DeviceId = device.Id,
                ReportId = report.Id,
                StartedAtUtc = DateTimeOffset.UtcNow,
                Status = WorkflowExecutionStatus.Running
            };

            var actionResults = new List<ActionResult>();

            try
            {
                foreach (var action in workflow.Actions.OrderBy(a => a.Order))
                {
                    var result = await ExecuteActionAsync(action, device, report, cancellationToken);
                    actionResults.Add(result);
                }

                execution.Status = actionResults.All(r => r.Success) 
                    ? WorkflowExecutionStatus.Completed
                    : actionResults.Any(r => r.Success)
                        ? WorkflowExecutionStatus.PartialSuccess
                        : WorkflowExecutionStatus.Failed;

                execution.ResultMessage = $"Executed {actionResults.Count} actions. " +
                    $"Success: {actionResults.Count(r => r.Success)}, Failed: {actionResults.Count(r => !r.Success)}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing workflow {WorkflowId}", workflow.Id);
                execution.Status = WorkflowExecutionStatus.Failed;
                execution.ResultMessage = $"Workflow execution failed: {ex.Message}";
            }
            finally
            {
                execution.CompletedAtUtc = DateTimeOffset.UtcNow;
                execution.ActionsResultJson = JsonSerializer.Serialize(actionResults);

                // Save execution to database
                await SaveExecutionAsync(execution, cancellationToken);
            }

            return execution;
        }

        private async Task<ActionResult> ExecuteActionAsync(
            WorkflowAction action,
            DeviceEntity device,
            SecureBootReportEntity report,
            CancellationToken cancellationToken)
        {
            var result = new ActionResult
            {
                ActionType = action.ActionType.ToString(),
                StartedAt = DateTimeOffset.UtcNow
            };

            try
            {
                switch (action.ActionType)
                {
                    case WorkflowActionType.LogEntry:
                        await ExecuteLogActionAsync(action, device, report);
                        result.Success = true;
                        result.Message = "Log entry created";
                        break;

                    case WorkflowActionType.EmailNotification:
                        result.Success = true;
                        result.Message = "Email notification simulated (not implemented)";
                        _logger.LogInformation("Email notification would be sent for device {DeviceId}", device.Id);
                        break;

                    case WorkflowActionType.Webhook:
                        result.Success = true;
                        result.Message = "Webhook call simulated (not implemented)";
                        _logger.LogInformation("Webhook would be called for device {DeviceId}", device.Id);
                        break;

                    case WorkflowActionType.UpdateDeviceTags:
                        await ExecuteUpdateTagsActionAsync(action, device, cancellationToken);
                        result.Success = true;
                        result.Message = "Device tags updated";
                        break;

                    default:
                        result.Success = false;
                        result.Message = $"Unknown action type: {action.ActionType}";
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing action {ActionType}", action.ActionType);
                result.Success = false;
                result.Message = $"Action failed: {ex.Message}";
            }
            finally
            {
                result.CompletedAt = DateTimeOffset.UtcNow;
            }

            return result;
        }

        private Task ExecuteLogActionAsync(WorkflowAction action, DeviceEntity device, SecureBootReportEntity report)
        {
            var config = JsonSerializer.Deserialize<Dictionary<string, string>>(action.ConfigurationJson);
            var message = config?.GetValueOrDefault("message", "Workflow action executed");
            
            _logger.LogInformation("Workflow log action: {Message} for device {MachineName} ({DeviceId})",
                message, device.MachineName, device.Id);
            
            return Task.CompletedTask;
        }

        private async Task ExecuteUpdateTagsActionAsync(WorkflowAction action, DeviceEntity device, CancellationToken cancellationToken)
        {
            var config = JsonSerializer.Deserialize<Dictionary<string, string>>(action.ConfigurationJson);
            if (config == null) return;

            var existingTags = string.IsNullOrEmpty(device.TagsJson)
                ? new Dictionary<string, string>()
                : JsonSerializer.Deserialize<Dictionary<string, string>>(device.TagsJson) ?? new Dictionary<string, string>();

            foreach (var (key, value) in config)
            {
                existingTags[key] = value;
            }

            device.TagsJson = JsonSerializer.Serialize(existingTags);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Updated tags for device {DeviceId}", device.Id);
        }

        private async Task SaveExecutionAsync(WorkflowExecution execution, CancellationToken cancellationToken)
        {
            var entity = new WorkflowExecutionEntity
            {
                Id = execution.Id,
                WorkflowId = execution.WorkflowId,
                DeviceId = execution.DeviceId,
                ReportId = execution.ReportId,
                StartedAtUtc = execution.StartedAtUtc,
                CompletedAtUtc = execution.CompletedAtUtc,
                Status = (int)execution.Status,
                ResultMessage = execution.ResultMessage,
                ActionsResultJson = execution.ActionsResultJson
            };

            _dbContext.WorkflowExecutions.Add(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        private RemediationWorkflow MapToWorkflow(RemediationWorkflowEntity entity)
        {
            return new RemediationWorkflow
            {
                Id = entity.Id,
                Name = entity.Name,
                Description = entity.Description,
                IsEnabled = entity.IsEnabled,
                Priority = entity.Priority,
                Trigger = JsonSerializer.Deserialize<WorkflowTrigger>(entity.TriggerJson) ?? new WorkflowTrigger(),
                Actions = JsonSerializer.Deserialize<List<WorkflowAction>>(entity.ActionsJson) ?? new List<WorkflowAction>(),
                CreatedAtUtc = entity.CreatedAtUtc,
                UpdatedAtUtc = entity.UpdatedAtUtc,
                CreatedBy = entity.CreatedBy,
                UpdatedBy = entity.UpdatedBy
            };
        }

        private sealed class ActionResult
        {
            public string ActionType { get; set; } = string.Empty;
            public bool Success { get; set; }
            public string? Message { get; set; }
            public DateTimeOffset StartedAt { get; set; }
            public DateTimeOffset? CompletedAt { get; set; }
        }
    }
}
