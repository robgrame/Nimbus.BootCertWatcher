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
    /// <summary>
    /// API endpoints for clients to fetch pending commands and submit execution results.
    /// These endpoints are called by SecureBootWatcher.Client instances.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public sealed class ClientCommandsController : ControllerBase
    {
        private readonly SecureBootDbContext _dbContext;
        private readonly ILogger<ClientCommandsController> _logger;

        public ClientCommandsController(
            SecureBootDbContext dbContext,
            ILogger<ClientCommandsController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        /// <summary>
        /// Get pending commands for a specific device.
        /// Called by clients on each execution cycle to check for new commands.
        /// </summary>
        /// <param name="deviceId">The unique identifier of the device requesting commands.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of pending commands ready for execution.</returns>
        [HttpGet("pending")]
        [ProducesResponseType(typeof(List<DeviceConfigurationCommand>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<List<DeviceConfigurationCommand>>> GetPendingCommandsAsync(
            [FromQuery] string deviceId,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                return BadRequest(new { Error = "DeviceId is required" });
            }

            if (!Guid.TryParse(deviceId, out var deviceGuid))
            {
                return BadRequest(new { Error = "DeviceId must be a valid GUID" });
            }

            _logger.LogInformation("Fetching pending commands for device {DeviceId}", deviceGuid);

            try
            {
                // Get all pending commands for this device that are ready for execution
                var pendingCommands = await _dbContext.PendingCommands
                    .Where(c => c.DeviceId == deviceGuid && c.IsReadyForExecution)
                    .OrderBy(c => c.Priority) // Higher priority first
                    .ThenBy(c => c.CreatedAtUtc) // Older commands first
                    .Take(10) // Limit to avoid overwhelming the client
                    .ToListAsync(cancellationToken);

                _logger.LogInformation("Found {Count} pending command(s) for device {DeviceId}", 
                    pendingCommands.Count, deviceGuid);

                // Deserialize commands from JSON
                var commands = new List<DeviceConfigurationCommand>();

                foreach (var cmdEntity in pendingCommands)
                {
                    try
                    {
                        // Deserialize based on command type
                        DeviceConfigurationCommand? command = cmdEntity.CommandType switch
                        {
                            nameof(CertificateUpdateCommand) => 
                                JsonSerializer.Deserialize<CertificateUpdateCommand>(cmdEntity.CommandJson),
                            nameof(MicrosoftUpdateOptInCommand) => 
                                JsonSerializer.Deserialize<MicrosoftUpdateOptInCommand>(cmdEntity.CommandJson),
                            nameof(TelemetryConfigurationCommand) => 
                                JsonSerializer.Deserialize<TelemetryConfigurationCommand>(cmdEntity.CommandJson),
                            _ => null
                        };

                        if (command != null)
                        {
                            commands.Add(command);

                            // Update fetch tracking
                            cmdEntity.LastFetchedAtUtc = DateTimeOffset.UtcNow;
                            cmdEntity.FetchCount++;
                            cmdEntity.Status = CommandStatus.Fetched;
                        }
                        else
                        {
                            _logger.LogWarning(
                                "Unknown command type '{Type}' for command {CommandId}", 
                                cmdEntity.CommandType, 
                                cmdEntity.CommandId);
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError(ex, 
                            "Failed to deserialize command {CommandId} of type {Type}", 
                            cmdEntity.CommandId, 
                            cmdEntity.CommandType);
                    }
                }

                // Save fetch tracking updates
                if (pendingCommands.Any())
                {
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }

                return Ok(commands);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch pending commands for device {DeviceId}", deviceGuid);
                return StatusCode(500, new { Error = "Internal server error while fetching commands" });
            }
        }

        /// <summary>
        /// Submit command execution result from a client.
        /// Called by clients after executing a command to report success/failure.
        /// </summary>
        /// <param name="result">The command execution result.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Acknowledgment of result receipt.</returns>
        [HttpPost("result")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SubmitCommandResultAsync(
            [FromBody] DeviceConfigurationResult result,
            CancellationToken cancellationToken)
        {
            if (result == null)
            {
                return BadRequest(new { Error = "Result is required" });
            }

            _logger.LogInformation(
                "Received command result for CommandId {CommandId}, DeviceId {DeviceId}, Success: {Success}",
                result.CommandId,
                result.DeviceId,
                result.Success);

            try
            {
                // Find the pending command
                var pendingCommand = await _dbContext.PendingCommands
                    .FirstOrDefaultAsync(
                        c => c.CommandId == result.CommandId && c.DeviceId == result.DeviceId,
                        cancellationToken);

                if (pendingCommand == null)
                {
                    _logger.LogWarning(
                        "Command {CommandId} not found for device {DeviceId}",
                        result.CommandId,
                        result.DeviceId);
                    
                    return NotFound(new { Error = $"Command {result.CommandId} not found" });
                }

                // Update command status
                pendingCommand.Status = result.Success ? CommandStatus.Completed : CommandStatus.Failed;
                pendingCommand.ProcessedAtUtc = DateTimeOffset.UtcNow;
                pendingCommand.ResultJson = JsonSerializer.Serialize(result);

                await _dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Command {CommandId} marked as {Status}",
                    result.CommandId,
                    pendingCommand.Status);

                return Ok(new 
                { 
                    Message = $"Command result recorded successfully",
                    CommandId = result.CommandId,
                    Status = pendingCommand.Status
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Failed to save command result for CommandId {CommandId}", 
                    result.CommandId);
                
                return StatusCode(500, new { Error = "Internal server error while saving result" });
            }
        }

        /// <summary>
        /// Health check endpoint for client connectivity testing.
        /// </summary>
        [HttpGet("ping")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult Ping()
        {
            return Ok(new 
            { 
                Status = "OK", 
                Timestamp = DateTimeOffset.UtcNow,
                Service = "ClientCommands"
            });
        }
    }
}
