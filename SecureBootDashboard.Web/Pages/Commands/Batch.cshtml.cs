using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SecureBootWatcher.Shared.Models;

namespace SecureBootDashboard.Web.Pages.Commands
{
    public class BatchModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<BatchModel> _logger;

        public BatchModel(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<BatchModel> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        [BindProperty]
        public BatchCommandInputModel Input { get; set; } = new();

        public List<DeviceInfo> AvailableDevices { get; set; } = new();
        public List<string> AvailableFleets { get; set; } = new();
        public List<string> AvailableManufacturers { get; set; } = new();

        public string? StatusMessage { get; set; }
        public bool IsSuccess { get; set; }
        public BatchCommandResponse? LastResult { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            await LoadDevicesAsync();
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

                // Determine target device IDs based on selection mode
                var targetDeviceIds = Input.SelectionMode switch
                {
                    "Manual" => Input.SelectedDeviceIds ?? new List<Guid>(),
                    "Fleet" => await GetDevicesByFleetAsync(Input.FleetFilter!),
                    "Manufacturer" => await GetDevicesByManufacturerAsync(Input.ManufacturerFilter!),
                    "DeploymentState" => await GetDevicesByDeploymentStateAsync(Input.DeploymentStateFilter!),
                    "All" => await GetAllDeviceIdsAsync(),
                    _ => new List<Guid>()
                };

                if (!targetDeviceIds.Any())
                {
                    ModelState.AddModelError("", "No devices selected");
                    await LoadDevicesAsync();
                    return Page();
                }

                // Create the appropriate command based on type
                DeviceConfigurationCommand? command = Input.CommandType switch
                {
                    "CertificateUpdate" => new CertificateUpdateCommand
                    {
                        UpdateType = Input.CertUpdateType,
                        ForceUpdate = Input.ForceUpdate,
                        Description = Input.Description
                    },
                    "MicrosoftUpdateOptIn" => new MicrosoftUpdateOptInCommand
                    {
                        OptIn = Input.OptIn,
                        Description = Input.Description
                    },
                    "TelemetryConfiguration" => new TelemetryConfigurationCommand
                    {
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

                // Queue batch command
                var batchRequest = new
                {
                    DeviceIds = targetDeviceIds,
                    Command = command,
                    Priority = Input.Priority,
                    ScheduledFor = Input.ScheduleExecution ? Input.ScheduledFor : (DateTimeOffset?)null
                };

                var response = await httpClient.PostAsJsonAsync(
                    $"{apiBaseUrl}/api/CommandManagement/queue-batch",
                    batchRequest);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<BatchCommandResponse>();
                    LastResult = result;

                    IsSuccess = true;
                    StatusMessage = $"? Batch command queued! {result?.SuccessCount} succeeded, {result?.FailureCount} failed out of {result?.TotalDevices} devices.";
                    
                    _logger.LogInformation(
                        "Batch command queued for {TotalDevices} devices. Success: {SuccessCount}, Failed: {FailureCount}",
                        result?.TotalDevices,
                        result?.SuccessCount,
                        result?.FailureCount);

                    // Clear form if all succeeded
                    if (result?.FailureCount == 0)
                    {
                        Input = new BatchCommandInputModel();
                    }
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    StatusMessage = $"? Failed to queue batch command: {error}";
                    IsSuccess = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error queuing batch command");
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

                AvailableDevices = devices ?? new List<DeviceInfo>();

                // Extract unique values for filters
                AvailableFleets = AvailableDevices
                    .Where(d => !string.IsNullOrEmpty(d.FleetId))
                    .Select(d => d.FleetId!)
                    .Distinct()
                    .OrderBy(f => f)
                    .ToList();

                AvailableManufacturers = AvailableDevices
                    .Where(d => !string.IsNullOrEmpty(d.Manufacturer))
                    .Select(d => d.Manufacturer!)
                    .Distinct()
                    .OrderBy(m => m)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load devices");
                AvailableDevices = new List<DeviceInfo>();
            }
        }

        private async Task<List<Guid>> GetDevicesByFleetAsync(string fleetId)
        {
            return AvailableDevices
                .Where(d => d.FleetId?.Equals(fleetId, StringComparison.OrdinalIgnoreCase) == true)
                .Select(d => d.Id)
                .ToList();
        }

        private async Task<List<Guid>> GetDevicesByManufacturerAsync(string manufacturer)
        {
            return AvailableDevices
                .Where(d => d.Manufacturer?.Equals(manufacturer, StringComparison.OrdinalIgnoreCase) == true)
                .Select(d => d.Id)
                .ToList();
        }

        private async Task<List<Guid>> GetDevicesByDeploymentStateAsync(string deploymentState)
        {
            return AvailableDevices
                .Where(d => d.DeploymentState?.Equals(deploymentState, StringComparison.OrdinalIgnoreCase) == true)
                .Select(d => d.Id)
                .ToList();
        }

        private async Task<List<Guid>> GetAllDeviceIdsAsync()
        {
            return AvailableDevices.Select(d => d.Id).ToList();
        }

        public class BatchCommandInputModel
        {
            [Required(ErrorMessage = "Please select a selection mode")]
            public string SelectionMode { get; set; } = "Manual";

            // Manual selection
            public List<Guid>? SelectedDeviceIds { get; set; }

            // Filter-based selection
            public string? FleetFilter { get; set; }
            public string? ManufacturerFilter { get; set; }
            public string? DeploymentStateFilter { get; set; }

            [Required(ErrorMessage = "Please select a command type")]
            public string CommandType { get; set; } = "CertificateUpdate";

            [StringLength(500)]
            public string? Description { get; set; }

            [Range(0, 10)]
            public int Priority { get; set; } = 0;

            public bool ScheduleExecution { get; set; }
            public DateTimeOffset? ScheduledFor { get; set; }

            // Certificate Update properties
            [Range(0, 2)]
            public uint CertUpdateType { get; set; } = 1;
            public bool ForceUpdate { get; set; }

            // Microsoft Update Opt-In properties
            public bool OptIn { get; set; } = true;

            // Telemetry Configuration properties
            [Range(0, 3)]
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
            public string? FleetId { get; set; }
        }

        public class BatchCommandResponse
        {
            public int TotalDevices { get; set; }
            public int SuccessCount { get; set; }
            public int FailureCount { get; set; }
            public List<Guid> FailedDeviceIds { get; set; } = new();
        }
    }
}
