using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SecureBootWatcher.Shared.Models;

namespace SecureBootDashboard.Web.Pages.Commands
{
    public class SendModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SendModel> _logger;

        public SendModel(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<SendModel> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        [BindProperty]
        public CommandInputModel Input { get; set; } = new();

        public List<DeviceInfo> Devices { get; set; } = new();

        public string? StatusMessage { get; set; }
        public bool IsSuccess { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid? deviceId = null)
        {
            await LoadDevicesAsync();

            if (deviceId.HasValue)
            {
                Input.TargetDeviceId = deviceId.Value;
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await LoadDevicesAsync();
                return Page();
            }

            try
            {
                var httpClient = _httpClientFactory.CreateClient("SecureBootApi");
                var apiBaseUrl = _configuration["ApiBaseUrl"] ?? "https://localhost:5001";

                // Create the appropriate command based on type
                DeviceConfigurationCommand? command = Input.CommandType switch
                {
                    "CertificateUpdate" => new CertificateUpdateCommand
                    {
                        CommandId = Guid.NewGuid(),
                        UpdateType = Input.CertUpdateType,
                        ForceUpdate = Input.ForceUpdate,
                        Description = Input.Description
                    },
                    "MicrosoftUpdateOptIn" => new MicrosoftUpdateOptInCommand
                    {
                        CommandId = Guid.NewGuid(),
                        OptIn = Input.OptIn,
                        Description = Input.Description
                    },
                    "TelemetryConfiguration" => new TelemetryConfigurationCommand
                    {
                        CommandId = Guid.NewGuid(),
                        RequiredTelemetryLevel = Input.TelemetryLevel,
                        ValidateOnly = Input.ValidateOnly,
                        Description = Input.Description
                    },
                    _ => null
                };

                if (command == null)
                {
                    ModelState.AddModelError("", "Invalid command type selected");
                    await LoadDevicesAsync();
                    return Page();
                }

                // Queue the command
                var queueRequest = new
                {
                    DeviceId = Input.TargetDeviceId,
                    Command = command,
                    Priority = Input.Priority,
                    ScheduledFor = Input.ScheduleExecution ? Input.ScheduledFor : (DateTimeOffset?)null
                };

                var response = await httpClient.PostAsJsonAsync(
                    $"{apiBaseUrl}/api/CommandManagement/queue",
                    queueRequest);

                if (response.IsSuccessStatusCode)
                {
                    IsSuccess = true;
                    StatusMessage = $"? Command queued successfully! Command ID: {command.CommandId}";
                    
                    _logger.LogInformation(
                        "Command {CommandId} queued for device {DeviceId}",
                        command.CommandId,
                        Input.TargetDeviceId);

                    // Clear form
                    Input = new CommandInputModel();
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    StatusMessage = $"? Failed to queue command: {error}";
                    IsSuccess = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error queuing command");
                StatusMessage = $"? Error: {ex.Message}";
                IsSuccess = false;
            }

            await LoadDevicesAsync();
            return Page();
        }

        private async Task LoadDevicesAsync()
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient("SecureBootApi");
                var apiBaseUrl = _configuration["ApiBaseUrl"] ?? "https://localhost:5001";

                var devices = await httpClient.GetFromJsonAsync<List<DeviceInfo>>(
                    $"{apiBaseUrl}/api/Devices");

                Devices = devices ?? new List<DeviceInfo>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load devices");
                Devices = new List<DeviceInfo>();
            }
        }

        public class CommandInputModel
        {
            [Required(ErrorMessage = "Please select a device")]
            public Guid TargetDeviceId { get; set; }

            [Required(ErrorMessage = "Please select a command type")]
            public string CommandType { get; set; } = "CertificateUpdate";

            [StringLength(500)]
            public string? Description { get; set; }

            [Range(0, 10)]
            public int Priority { get; set; } = 0;

            public bool ScheduleExecution { get; set; }

            public DateTimeOffset? ScheduledFor { get; set; }

            // CertificateUpdateCommand properties
            [Range(0, 2, ErrorMessage = "Update type must be 0 (None), 1 (DB), or 2 (Boot Manager)")]
            public uint CertUpdateType { get; set; } = 1;

            public bool ForceUpdate { get; set; }

            // MicrosoftUpdateOptInCommand properties
            public bool OptIn { get; set; } = true;

            // TelemetryConfigurationCommand properties
            [Range(0, 3, ErrorMessage = "Telemetry level must be between 0 (Security) and 3 (Full)")]
            public uint TelemetryLevel { get; set; } = 1;

            public bool ValidateOnly { get; set; }
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
    }
}
