using System;
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
    public class DetailsModel : PageModel
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

        [BindProperty(SupportsGet = true)]
        public Guid Id { get; set; }

        public CommandDetails? Command { get; set; }
        public DeviceInfo? Device { get; set; }
        public CommandResult? Result { get; set; }
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient("SecureBootApi");
                var apiBaseUrl = _configuration["ApiBaseUrl"] ?? "https://localhost:5001";

                // Load command details
                var command = await httpClient.GetFromJsonAsync<CommandDetails>(
                    $"{apiBaseUrl}/api/CommandManagement/{Id}");

                if (command == null)
                {
                    return NotFound();
                }

                Command = command;

                // Load device info
                if (Command.DeviceId != Guid.Empty)
                {
                    try
                    {
                        Device = await httpClient.GetFromJsonAsync<DeviceInfo>(
                            $"{apiBaseUrl}/api/Devices/{Command.DeviceId}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load device info for {DeviceId}", Command.DeviceId);
                    }
                }

                // Parse result if available
                if (!string.IsNullOrEmpty(Command.ResultJson))
                {
                    try
                    {
                        Result = JsonSerializer.Deserialize<CommandResult>(Command.ResultJson);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse command result JSON");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load command details for {CommandId}", Id);
                ErrorMessage = $"Failed to load command details: {ex.Message}";
            }

            return Page();
        }

        public async Task<IActionResult> OnPostCancelAsync()
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient("SecureBootApi");
                var apiBaseUrl = _configuration["ApiBaseUrl"] ?? "https://localhost:5001";

                var response = await httpClient.PostAsync(
                    $"{apiBaseUrl}/api/CommandManagement/{Id}/cancel",
                    null);

                if (response.IsSuccessStatusCode)
                {
                    return RedirectToPage();
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    ErrorMessage = $"Failed to cancel command: {error}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling command {CommandId}", Id);
                ErrorMessage = $"Error cancelling command: {ex.Message}";
            }

            return RedirectToPage();
        }

        public class CommandDetails
        {
            public Guid Id { get; set; }
            public Guid DeviceId { get; set; }
            public Guid CommandId { get; set; }
            public string CommandType { get; set; } = string.Empty;
            public string CommandJson { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public DateTimeOffset CreatedAtUtc { get; set; }
            public DateTimeOffset? LastFetchedAtUtc { get; set; }
            public DateTimeOffset? ProcessedAtUtc { get; set; }
            public string? ResultJson { get; set; }
            public string? CreatedBy { get; set; }
            public string? Description { get; set; }
            public int FetchCount { get; set; }
            public DateTimeOffset? ScheduledForUtc { get; set; }
            public int Priority { get; set; }
        }

        public class DeviceInfo
        {
            public Guid Id { get; set; }
            public string MachineName { get; set; } = string.Empty;
            public string? DomainName { get; set; }
            public string? Manufacturer { get; set; }
            public string? Model { get; set; }
            public string DeploymentState { get; set; } = string.Empty;
        }

        public class CommandResult
        {
            public Guid CommandId { get; set; }
            public Guid DeviceId { get; set; }
            public bool Success { get; set; }
            public string? Message { get; set; }
            public string? ErrorDetails { get; set; }
            public DateTimeOffset ExecutedAtUtc { get; set; }
            public bool VerificationSucceeded { get; set; }
            public string? VerificationDetails { get; set; }
        }
    }
}
