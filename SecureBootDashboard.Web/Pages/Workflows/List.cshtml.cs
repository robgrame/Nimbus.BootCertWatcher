using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SecureBootDashboard.Web.Pages.Workflows
{
    public sealed class ListModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ListModel> _logger;

        public ListModel(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<ListModel> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        public IReadOnlyList<WorkflowSummary> Workflows { get; set; } = Array.Empty<WorkflowSummary>();

        public async Task OnGetAsync()
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                var apiBaseUrl = _configuration.GetValue<string>("ApiBaseUrl") ?? "https://localhost:5001";
                var response = await httpClient.GetAsync($"{apiBaseUrl}/api/RemediationWorkflows");

                if (response.IsSuccessStatusCode)
                {
                    var workflows = await response.Content.ReadFromJsonAsync<List<WorkflowSummary>>();
                    Workflows = workflows ?? new List<WorkflowSummary>();
                }
                else
                {
                    _logger.LogWarning("Failed to fetch workflows: {StatusCode}", response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching workflows");
            }
        }

        public sealed record WorkflowSummary(
            Guid Id,
            string Name,
            string? Description,
            bool IsEnabled,
            int Priority,
            DateTimeOffset CreatedAtUtc,
            DateTimeOffset UpdatedAtUtc);
    }
}
