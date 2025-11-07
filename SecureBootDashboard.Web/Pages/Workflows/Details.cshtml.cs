using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SecureBootWatcher.Shared.Models;

namespace SecureBootDashboard.Web.Pages.Workflows
{
    public sealed class DetailsModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DetailsModel> _logger;

        public DetailsModel(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<DetailsModel> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        public WorkflowDetail? Workflow { get; set; }
        public IReadOnlyList<ExecutionHistory> Executions { get; set; } = Array.Empty<ExecutionHistory>();

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                var apiBaseUrl = _configuration.GetValue<string>("ApiBaseUrl") ?? "https://localhost:5001";

                // Fetch workflow details
                var workflowResponse = await httpClient.GetAsync($"{apiBaseUrl}/api/RemediationWorkflows/{id}");
                if (!workflowResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Workflow {WorkflowId} not found: {StatusCode}", id, workflowResponse.StatusCode);
                    return NotFound();
                }

                Workflow = await workflowResponse.Content.ReadFromJsonAsync<WorkflowDetail>();

                // Fetch execution history
                var executionsResponse = await httpClient.GetAsync($"{apiBaseUrl}/api/RemediationWorkflows/{id}/executions?limit=50");
                if (executionsResponse.IsSuccessStatusCode)
                {
                    var executions = await executionsResponse.Content.ReadFromJsonAsync<List<ExecutionHistory>>();
                    Executions = executions ?? new List<ExecutionHistory>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching workflow details for {WorkflowId}", id);
            }

            return Page();
        }

        public sealed record WorkflowDetail(
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

        public sealed record ExecutionHistory(
            Guid Id,
            Guid DeviceId,
            string DeviceName,
            DateTimeOffset StartedAtUtc,
            DateTimeOffset? CompletedAtUtc,
            WorkflowExecutionStatus Status,
            string? ResultMessage);
    }
}
