using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SecureBootDashboard.Web.Pages.Commands
{
    public class HistoryModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<HistoryModel> _logger;

        public HistoryModel(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<HistoryModel> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        public List<CommandHistoryItem> Commands { get; set; } = new();
        public CommandStatistics Statistics { get; set; } = new();
        
        [BindProperty(SupportsGet = true)]
        public Guid? DeviceId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Status { get; set; }

        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient("SecureBootApi");
                var apiBaseUrl = _configuration["ApiBaseUrl"] ?? "https://localhost:5001";

                // Load statistics
                var stats = await httpClient.GetFromJsonAsync<CommandStatistics>(
                    $"{apiBaseUrl}/api/CommandManagement/statistics");
                Statistics = stats ?? new CommandStatistics();

                // Load commands
                string endpoint;
                if (DeviceId.HasValue)
                {
                    endpoint = $"{apiBaseUrl}/api/CommandManagement/device/{DeviceId}/history";
                }
                else
                {
                    // TODO: Implement global command history endpoint
                    endpoint = $"{apiBaseUrl}/api/CommandManagement/device/{Guid.Empty}/history";
                }

                var commands = await httpClient.GetFromJsonAsync<List<CommandHistoryItem>>(endpoint);
                Commands = commands ?? new List<CommandHistoryItem>();

                // Apply status filter if specified
                if (!string.IsNullOrEmpty(Status))
                {
                    Commands = Commands.Where(c => c.Status.Equals(Status, StringComparison.OrdinalIgnoreCase)).ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load command history");
                ErrorMessage = $"Failed to load command history: {ex.Message}";
            }

            return Page();
        }

        public async Task<IActionResult> OnPostCancelAsync(Guid commandId)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient("SecureBootApi");
                var apiBaseUrl = _configuration["ApiBaseUrl"] ?? "https://localhost:5001";

                var response = await httpClient.PostAsync(
                    $"{apiBaseUrl}/api/CommandManagement/{commandId}/cancel",
                    null);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    ErrorMessage = $"Failed to cancel command: {error}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling command {CommandId}", commandId);
                ErrorMessage = $"Error cancelling command: {ex.Message}";
            }

            return RedirectToPage();
        }

        public class CommandHistoryItem
        {
            public Guid Id { get; set; }
            public Guid DeviceId { get; set; }
            public Guid CommandId { get; set; }
            public string CommandType { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public DateTimeOffset CreatedAtUtc { get; set; }
            public DateTimeOffset? ProcessedAtUtc { get; set; }
            public string? Description { get; set; }
            public string? CreatedBy { get; set; }
            public int Priority { get; set; }
            public int FetchCount { get; set; }
            
            public string StatusBadgeClass => Status switch
            {
                "Pending" => "bg-warning text-dark",
                "Fetched" => "bg-info",
                "Processing" => "bg-primary",
                "Completed" => "bg-success",
                "Failed" => "bg-danger",
                "Cancelled" => "bg-secondary",
                "Expired" => "bg-dark",
                _ => "bg-light text-dark"
            };

            public string StatusIcon => Status switch
            {
                "Pending" => "fa-clock",
                "Fetched" => "fa-download",
                "Processing" => "fa-spinner fa-spin",
                "Completed" => "fa-check-circle",
                "Failed" => "fa-times-circle",
                "Cancelled" => "fa-ban",
                "Expired" => "fa-hourglass-end",
                _ => "fa-question-circle"
            };
        }

        public class CommandStatistics
        {
            public int TotalCommands { get; set; }
            public int PendingCount { get; set; }
            public int CompletedCount { get; set; }
            public int FailedCount { get; set; }
            public int CancelledCount { get; set; }
        }
    }
}
