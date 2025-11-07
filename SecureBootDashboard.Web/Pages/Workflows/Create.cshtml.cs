using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SecureBootDashboard.Web.Pages.Workflows
{
    public sealed class CreateModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<CreateModel> _logger;

        public CreateModel(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<CreateModel> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        [BindProperty]
        [Required]
        public string Name { get; set; } = string.Empty;

        [BindProperty]
        public string? Description { get; set; }

        [BindProperty]
        public bool IsEnabled { get; set; } = true;

        [BindProperty]
        public int Priority { get; set; } = 100;

        [BindProperty]
        public string? DeploymentState { get; set; }

        [BindProperty]
        public string? FleetIdMatches { get; set; }

        [BindProperty]
        public string? ManufacturerMatches { get; set; }

        [BindProperty]
        public int? NoReportForDays { get; set; }

        [BindProperty]
        public string? AlertContains { get; set; }

        [BindProperty]
        public bool? HasExpiredCertificates { get; set; }

        [BindProperty]
        public int? CertificateExpiringWithinDays { get; set; }

        [BindProperty]
        public string ActionType { get; set; } = "LogEntry";

        [BindProperty]
        public string ActionConfiguration { get; set; } = "{\"message\": \"Workflow triggered\"}";

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            try
            {
                var trigger = new
                {
                    DeploymentState,
                    FleetIdMatches,
                    ManufacturerMatches,
                    NoReportForDays,
                    AlertContains,
                    HasExpiredCertificates,
                    CertificateExpiringWithinDays
                };

                var actionTypeEnum = Enum.Parse<WorkflowActionType>(ActionType);
                var actions = new[]
                {
                    new
                    {
                        ActionType = (int)actionTypeEnum,
                        ConfigurationJson = ActionConfiguration,
                        Order = 1
                    }
                };

                var request = new
                {
                    Name,
                    Description,
                    IsEnabled,
                    Priority,
                    Trigger = trigger,
                    Actions = actions,
                    CreatedBy = "web-user"
                };

                var httpClient = _httpClientFactory.CreateClient();
                var apiBaseUrl = _configuration.GetValue<string>("ApiBaseUrl") ?? "https://localhost:5001";
                var response = await httpClient.PostAsJsonAsync($"{apiBaseUrl}/api/RemediationWorkflows", request);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Created workflow {WorkflowName}", Name);
                    return RedirectToPage("./List");
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Failed to create workflow: {StatusCode} - {Error}", response.StatusCode, error);
                    ModelState.AddModelError(string.Empty, $"Failed to create workflow: {response.StatusCode}");
                    return Page();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating workflow");
                ModelState.AddModelError(string.Empty, "An error occurred while creating the workflow");
                return Page();
            }
        }

        private enum WorkflowActionType
        {
            EmailNotification = 1,
            Webhook = 2,
            LogEntry = 3,
            UpdateDeviceTags = 4
        }
    }
}
