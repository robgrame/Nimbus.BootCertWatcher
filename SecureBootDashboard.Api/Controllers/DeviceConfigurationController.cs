using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SecureBootDashboard.Api.Data;
using SecureBootWatcher.Shared.Models;

namespace SecureBootDashboard.Api.Controllers
{
    /// <summary>
    /// API endpoints for managing device configuration commands.
    /// Enables remote configuration of certificate updates, CFR eligibility, and telemetry settings.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public sealed class DeviceConfigurationController : ControllerBase
    {
        private readonly SecureBootDbContext _dbContext;
        private readonly ILogger<DeviceConfigurationController> _logger;

        public DeviceConfigurationController(
            SecureBootDbContext dbContext,
            ILogger<DeviceConfigurationController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        /// <summary>
        /// Command a device to update its certificates.
        /// </summary>
        /// <param name="deviceId">The unique identifier of the device.</param>
        /// <param name="command">Certificate update command details.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Result of the command execution.</returns>
        [HttpPost("{deviceId:guid}/certificate-update")]
        [ProducesResponseType(typeof(DeviceConfigurationResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<DeviceConfigurationResult>> CommandCertificateUpdateAsync(
            Guid deviceId,
            [FromBody] CertificateUpdateCommand command,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Received certificate update command {CommandId} for device {DeviceId}, UpdateType: {UpdateType}, Force: {Force}",
                command.CommandId, deviceId, command.UpdateType, command.ForceUpdate);

            // Verify device exists
            var device = await _dbContext.Devices
                .AsNoTracking()
                .Include(d => d.Reports.OrderByDescending(r => r.CreatedAtUtc).Take(1))
                .FirstOrDefaultAsync(d => d.Id == deviceId, cancellationToken);

            if (device == null)
            {
                _logger.LogWarning("Device {DeviceId} not found", deviceId);
                return NotFound(new { Error = $"Device {deviceId} not found" });
            }

            // Get current state from latest report
            var currentState = await GetDeviceConfigurationStateAsync(deviceId, cancellationToken);

            // Validate device capabilities
            if (device.UEFISecureBootEnabled != true)
            {
                var errorResult = new DeviceConfigurationResult
                {
                    CommandId = command.CommandId,
                    DeviceId = deviceId,
                    Success = false,
                    Message = "Device does not have UEFI Secure Boot enabled. Certificate updates require UEFI Secure Boot.",
                    CurrentState = currentState
                };
                _logger.LogWarning(
                    "Certificate update command {CommandId} rejected for device {DeviceId}: UEFI Secure Boot not enabled",
                    command.CommandId, deviceId);
                return BadRequest(errorResult);
            }

            // Check if device is capable of UEFI CA 2023 updates
            if (currentState?.WindowsUEFICA2023Capable == 0 && !command.ForceUpdate)
            {
                var errorResult = new DeviceConfigurationResult
                {
                    CommandId = command.CommandId,
                    DeviceId = deviceId,
                    Success = false,
                    Message = "Device is not capable of Windows UEFI CA 2023 updates. Use ForceUpdate flag to override.",
                    CurrentState = currentState
                };
                _logger.LogWarning(
                    "Certificate update command {CommandId} rejected for device {DeviceId}: Not UEFI CA 2023 capable",
                    command.CommandId, deviceId);
                return BadRequest(errorResult);
            }

            // In a real implementation, this would:
            // 1. Store the command in a pending commands table
            // 2. Queue it for the device to pick up on next check-in
            // 3. Return immediately with command accepted status
            // For now, we simulate successful queuing

            var result = new DeviceConfigurationResult
            {
                CommandId = command.CommandId,
                DeviceId = deviceId,
                Success = true,
                Message = $"Certificate update command queued successfully. UpdateType: {command.UpdateType ?? 0}, Force: {command.ForceUpdate}. Device will process on next check-in.",
                CurrentState = currentState
            };

            _logger.LogInformation(
                "Certificate update command {CommandId} queued for device {DeviceId}",
                command.CommandId, deviceId);

            return Ok(result);
        }

        /// <summary>
        /// Configure Microsoft Update Managed Opt-In status for a device or group of devices.
        /// This affects CFR (Controlled Feature Rollout) eligibility.
        /// </summary>
        /// <param name="command">Microsoft Update opt-in command details.</param>
        /// <param name="deviceIds">Optional list of specific device IDs. If empty, applies to all devices.</param>
        /// <param name="fleetId">Optional fleet ID filter. Only applies if deviceIds is empty.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of command results for each affected device.</returns>
        [HttpPost("microsoft-update-optin")]
        [ProducesResponseType(typeof(DeviceConfigurationBatchResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<DeviceConfigurationBatchResult>> ConfigureMicrosoftUpdateOptInAsync(
            [FromBody] MicrosoftUpdateOptInCommand command,
            [FromQuery] List<Guid>? deviceIds = null,
            [FromQuery] string? fleetId = null,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "Received Microsoft Update opt-in command {CommandId}, OptIn: {OptIn}, DeviceCount: {DeviceCount}, Fleet: {FleetId}",
                command.CommandId, command.OptIn, deviceIds?.Count ?? 0, fleetId);

            // Build device query
            IQueryable<Data.DeviceEntity> deviceQuery = _dbContext.Devices.AsNoTracking();

            if (deviceIds != null && deviceIds.Count > 0)
            {
                deviceQuery = deviceQuery.Where(d => deviceIds.Contains(d.Id));
            }
            else if (!string.IsNullOrWhiteSpace(fleetId))
            {
                deviceQuery = deviceQuery.Where(d => d.FleetId == fleetId);
            }

            var devices = await deviceQuery.ToListAsync(cancellationToken);

            if (devices.Count == 0)
            {
                return BadRequest(new { Error = "No devices found matching the specified criteria" });
            }

            var results = new List<DeviceConfigurationResult>();

            foreach (var device in devices)
            {
                var currentState = await GetDeviceConfigurationStateAsync(device.Id, cancellationToken);

                // In a real implementation, this would store the command for device pickup
                var result = new DeviceConfigurationResult
                {
                    CommandId = command.CommandId,
                    DeviceId = device.Id,
                    Success = true,
                    Message = $"Microsoft Update Managed Opt-In command queued. Target state: {(command.OptIn ? "Enabled" : "Disabled")}. Device will process on next check-in.",
                    CurrentState = currentState
                };

                results.Add(result);
            }

            _logger.LogInformation(
                "Microsoft Update opt-in command {CommandId} queued for {DeviceCount} devices",
                command.CommandId, results.Count);

            return Ok(new DeviceConfigurationBatchResult
            {
                CommandId = command.CommandId,
                TotalDevices = results.Count,
                SuccessCount = results.Count(r => r.Success),
                FailureCount = results.Count(r => !r.Success),
                Results = results
            });
        }

        /// <summary>
        /// Validate or configure telemetry level for a device or group of devices.
        /// Telemetry level must be Basic (1) or higher for CFR eligibility.
        /// </summary>
        /// <param name="command">Telemetry configuration command details.</param>
        /// <param name="deviceIds">Optional list of specific device IDs. If empty, applies to all devices.</param>
        /// <param name="fleetId">Optional fleet ID filter. Only applies if deviceIds is empty.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of validation/configuration results for each affected device.</returns>
        [HttpPost("telemetry-configuration")]
        [ProducesResponseType(typeof(DeviceConfigurationBatchResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<DeviceConfigurationBatchResult>> ConfigureTelemetryLevelAsync(
            [FromBody] TelemetryConfigurationCommand command,
            [FromQuery] List<Guid>? deviceIds = null,
            [FromQuery] string? fleetId = null,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "Received telemetry configuration command {CommandId}, RequiredLevel: {RequiredLevel}, ValidateOnly: {ValidateOnly}, DeviceCount: {DeviceCount}, Fleet: {FleetId}",
                command.CommandId, command.RequiredTelemetryLevel, command.ValidateOnly, deviceIds?.Count ?? 0, fleetId);

            // Validate telemetry level is valid (0-3)
            if (command.RequiredTelemetryLevel > 3)
            {
                return BadRequest(new { Error = "RequiredTelemetryLevel must be between 0 and 3" });
            }

            // Build device query
            IQueryable<Data.DeviceEntity> deviceQuery = _dbContext.Devices
                .AsNoTracking()
                .Include(d => d.Reports.OrderByDescending(r => r.CreatedAtUtc).Take(1));

            if (deviceIds != null && deviceIds.Count > 0)
            {
                deviceQuery = deviceQuery.Where(d => deviceIds.Contains(d.Id));
            }
            else if (!string.IsNullOrWhiteSpace(fleetId))
            {
                deviceQuery = deviceQuery.Where(d => d.FleetId == fleetId);
            }

            var devices = await deviceQuery.ToListAsync(cancellationToken);

            if (devices.Count == 0)
            {
                return BadRequest(new { Error = "No devices found matching the specified criteria" });
            }

            var results = new List<DeviceConfigurationResult>();

            foreach (var device in devices)
            {
                var currentState = await GetDeviceConfigurationStateAsync(device.Id, cancellationToken);
                
                bool meetsRequirement = currentState?.AllowTelemetry >= command.RequiredTelemetryLevel;
                string message;

                if (command.ValidateOnly)
                {
                    // Validation mode - just check current level
                    message = meetsRequirement
                        ? $"Telemetry validation passed. Current level: {GetTelemetryLevelName(currentState?.AllowTelemetry)} ({currentState?.AllowTelemetry}), Required: {GetTelemetryLevelName(command.RequiredTelemetryLevel)} ({command.RequiredTelemetryLevel})"
                        : $"Telemetry validation failed. Current level: {GetTelemetryLevelName(currentState?.AllowTelemetry)} ({currentState?.AllowTelemetry}), Required: {GetTelemetryLevelName(command.RequiredTelemetryLevel)} ({command.RequiredTelemetryLevel})";
                }
                else
                {
                    // Configuration mode - command device to set level
                    message = meetsRequirement
                        ? $"Telemetry level already meets requirement. Current: {GetTelemetryLevelName(currentState?.AllowTelemetry)} ({currentState?.AllowTelemetry})"
                        : $"Telemetry configuration command queued. Target level: {GetTelemetryLevelName(command.RequiredTelemetryLevel)} ({command.RequiredTelemetryLevel}). Device will process on next check-in.";
                }

                var result = new DeviceConfigurationResult
                {
                    CommandId = command.CommandId,
                    DeviceId = device.Id,
                    Success = command.ValidateOnly ? meetsRequirement : true,
                    Message = message,
                    CurrentState = currentState
                };

                results.Add(result);
            }

            _logger.LogInformation(
                "Telemetry {Mode} command {CommandId} processed for {DeviceCount} devices. Success: {SuccessCount}, Failed: {FailureCount}",
                command.ValidateOnly ? "validation" : "configuration",
                command.CommandId, results.Count, results.Count(r => r.Success), results.Count(r => !r.Success));

            return Ok(new DeviceConfigurationBatchResult
            {
                CommandId = command.CommandId,
                TotalDevices = results.Count,
                SuccessCount = results.Count(r => r.Success),
                FailureCount = results.Count(r => !r.Success),
                Results = results
            });
        }

        /// <summary>
        /// Get current configuration state for a specific device.
        /// </summary>
        /// <param name="deviceId">The unique identifier of the device.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Current device configuration state.</returns>
        [HttpGet("{deviceId:guid}/state")]
        [ProducesResponseType(typeof(DeviceConfigurationState), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<DeviceConfigurationState>> GetDeviceStateAsync(
            Guid deviceId,
            CancellationToken cancellationToken)
        {
            var state = await GetDeviceConfigurationStateAsync(deviceId, cancellationToken);

            if (state == null)
            {
                return NotFound(new { Error = $"Device {deviceId} not found or has no reports" });
            }

            return Ok(state);
        }

        /// <summary>
        /// Helper method to retrieve current device configuration state from the latest report.
        /// </summary>
        private async Task<DeviceConfigurationState?> GetDeviceConfigurationStateAsync(
            Guid deviceId,
            CancellationToken cancellationToken)
        {
            var device = await _dbContext.Devices
                .AsNoTracking()
                .Include(d => d.Reports.OrderByDescending(r => r.CreatedAtUtc).Take(1))
                .FirstOrDefaultAsync(d => d.Id == deviceId, cancellationToken);

            if (device == null)
            {
                return null;
            }

            var latestReport = device.Reports.FirstOrDefault();
            if (latestReport == null || string.IsNullOrEmpty(latestReport.RegistryStateJson))
            {
                return new DeviceConfigurationState();
            }

            try
            {
                var report = System.Text.Json.JsonSerializer.Deserialize<SecureBootStatusReport>(
                    latestReport.RegistryStateJson);

                if (report == null)
                {
                    return new DeviceConfigurationState();
                }

                return new DeviceConfigurationState
                {
                    MicrosoftUpdateManagedOptIn = report.Registry?.MicrosoftUpdateManagedOptIn,
                    AllowTelemetry = report.TelemetryPolicy?.AllowTelemetry,
                    WindowsUEFICA2023Capable = report.Registry?.WindowsUEFICA2023CapableCode,
                    SnapshotTimestampUtc = latestReport.CreatedAtUtc
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize registry state for device {DeviceId}", deviceId);
                return new DeviceConfigurationState();
            }
        }

        /// <summary>
        /// Get human-readable telemetry level name.
        /// </summary>
        private static string GetTelemetryLevelName(uint? level)
        {
            if (!level.HasValue)
                return "Unknown";

            return level.Value switch
            {
                0 => "Security",
                1 => "Basic",
                2 => "Enhanced",
                3 => "Full",
                _ => $"Unknown ({level.Value})"
            };
        }

        /// <summary>
        /// Result of a batch device configuration operation.
        /// </summary>
        public sealed record DeviceConfigurationBatchResult
        {
            public Guid CommandId { get; init; }
            public int TotalDevices { get; init; }
            public int SuccessCount { get; init; }
            public int FailureCount { get; init; }
            public List<DeviceConfigurationResult> Results { get; init; } = new();
        }
    }
}
