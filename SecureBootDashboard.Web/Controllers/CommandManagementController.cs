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
    /// API endpoints for dashboard administrators to manage device commands.
    /// Used by the web dashboard UI to send commands and view command history.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public sealed class CommandManagementController : ControllerBase
    {
        private readonly SecureBootDbContext _dbContext;
        private readonly ILogger<CommandManagementController> _logger;

        public CommandManagementController(
            SecureBootDbContext dbContext,
            ILogger<CommandManagementController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        /// <summary>
        /// Queue a command for execution on a specific device.
        /// </summary>
        [HttpPost("queue")]
        [ProducesResponseType(typeof(PendingCommandEntity), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PendingCommandEntity>> QueueCommandAsync(
            [FromBody] QueueCommandRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null || request.Command == null)
            {
                return BadRequest(new { Error = "Command is required" });
            }

            // Verify device exists
            var deviceExists = await _dbContext.Devices
                .AnyAsync(d => d.Id == request.DeviceId, cancellationToken);

            if (!deviceExists)
            {
                return NotFound(new { Error = $"Device {request.DeviceId} not found" });
            }

            try
            {
                var commandEntity = new PendingCommandEntity
                {
                    DeviceId = request.DeviceId,
                    CommandId = request.Command.CommandId,
                    CommandType = request.Command.GetType().Name,
                    CommandJson = JsonSerializer.Serialize(request.Command, request.Command.GetType()),
                    Status = CommandStatus.Pending,
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    CreatedBy = User.Identity?.Name ?? "System",
                    Description = request.Command.Description,
                    Priority = request.Priority ?? 0,
                    ScheduledForUtc = request.ScheduledFor
                };

                _dbContext.PendingCommands.Add(commandEntity);
                await _dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Queued command {CommandId} of type {Type} for device {DeviceId}",
                    commandEntity.CommandId,
                    commandEntity.CommandType,
                    commandEntity.DeviceId);

                return CreatedAtAction(
                    nameof(GetCommandByIdAsync),
                    new { id = commandEntity.Id },
                    commandEntity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to queue command for device {DeviceId}", request.DeviceId);
                return StatusCode(500, new { Error = "Failed to queue command" });
            }
        }

        /// <summary>
        /// Queue commands for multiple devices (batch operation).
        /// </summary>
        [HttpPost("queue-batch")]
        [ProducesResponseType(typeof(BatchCommandResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<BatchCommandResponse>> QueueBatchCommandAsync(
            [FromBody] BatchCommandRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null || request.Command == null || request.DeviceIds == null || !request.DeviceIds.Any())
            {
                return BadRequest(new { Error = "Command and DeviceIds are required" });
            }

            var response = new BatchCommandResponse
            {
                TotalDevices = request.DeviceIds.Count,
                QueuedCommands = new List<PendingCommandEntity>(),
                FailedDeviceIds = new List<Guid>()
            };

            try
            {
                foreach (var deviceId in request.DeviceIds)
                {
                    try
                    {
                        var deviceExists = await _dbContext.Devices
                            .AnyAsync(d => d.Id == deviceId, cancellationToken);

                        if (!deviceExists)
                        {
                            response.FailedDeviceIds.Add(deviceId);
                            continue;
                        }

                        var commandEntity = new PendingCommandEntity
                        {
                            DeviceId = deviceId,
                            CommandId = Guid.NewGuid(), // New CommandId for each device
                            CommandType = request.Command.GetType().Name,
                            CommandJson = JsonSerializer.Serialize(request.Command, request.Command.GetType()),
                            Status = CommandStatus.Pending,
                            CreatedAtUtc = DateTimeOffset.UtcNow,
                            CreatedBy = User.Identity?.Name ?? "System",
                            Description = request.Command.Description,
                            Priority = request.Priority ?? 0,
                            ScheduledForUtc = request.ScheduledFor
                        };

                        _dbContext.PendingCommands.Add(commandEntity);
                        response.QueuedCommands.Add(commandEntity);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to queue command for device {DeviceId}", deviceId);
                        response.FailedDeviceIds.Add(deviceId);
                    }
                }

                await _dbContext.SaveChangesAsync(cancellationToken);

                response.SuccessCount = response.QueuedCommands.Count;
                response.FailureCount = response.FailedDeviceIds.Count;

                _logger.LogInformation(
                    "Batch command queued: {Success} succeeded, {Failed} failed out of {Total}",
                    response.SuccessCount,
                    response.FailureCount,
                    response.TotalDevices);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process batch command");
                return StatusCode(500, new { Error = "Failed to process batch command" });
            }
        }

        /// <summary>
        /// Get command history for a specific device.
        /// </summary>
        [HttpGet("device/{deviceId}/history")]
        [ProducesResponseType(typeof(List<PendingCommandEntity>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<List<PendingCommandEntity>>> GetDeviceCommandHistoryAsync(
            Guid deviceId,
            [FromQuery] int skip = 0,
            [FromQuery] int take = 50,
            CancellationToken cancellationToken = default)
        {
            var deviceExists = await _dbContext.Devices.AnyAsync(d => d.Id == deviceId, cancellationToken);
            if (!deviceExists)
            {
                return NotFound(new { Error = $"Device {deviceId} not found" });
            }

            var commands = await _dbContext.PendingCommands
                .Where(c => c.DeviceId == deviceId)
                .OrderByDescending(c => c.CreatedAtUtc)
                .Skip(skip)
                .Take(take)
                .ToListAsync(cancellationToken);

            return Ok(commands);
        }

        /// <summary>
        /// Get command by ID.
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(PendingCommandEntity), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PendingCommandEntity>> GetCommandByIdAsync(
            Guid id,
            CancellationToken cancellationToken)
        {
            var command = await _dbContext.PendingCommands
                .Include(c => c.Device)
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

            if (command == null)
            {
                return NotFound(new { Error = $"Command {id} not found" });
            }

            return Ok(command);
        }

        /// <summary>
        /// Cancel a pending command.
        /// </summary>
        [HttpPost("{id}/cancel")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CancelCommandAsync(
            Guid id,
            CancellationToken cancellationToken)
        {
            var command = await _dbContext.PendingCommands.FindAsync(new object[] { id }, cancellationToken);
            
            if (command == null)
            {
                return NotFound(new { Error = $"Command {id} not found" });
            }

            if (command.Status != CommandStatus.Pending && command.Status != CommandStatus.Fetched)
            {
                return BadRequest(new { Error = $"Command cannot be cancelled in status {command.Status}" });
            }

            command.Status = CommandStatus.Cancelled;
            command.ProcessedAtUtc = DateTimeOffset.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Cancelled command {CommandId}", id);

            return Ok(new { Message = "Command cancelled successfully", CommandId = id });
        }

        /// <summary>
        /// Get command statistics.
        /// </summary>
        [HttpGet("statistics")]
        [ProducesResponseType(typeof(CommandStatistics), StatusCodes.Status200OK)]
        public async Task<ActionResult<CommandStatistics>> GetStatisticsAsync(
            CancellationToken cancellationToken)
        {
            var stats = new CommandStatistics
            {
                TotalCommands = await _dbContext.PendingCommands.CountAsync(cancellationToken),
                PendingCount = await _dbContext.PendingCommands.CountAsync(c => c.Status == CommandStatus.Pending, cancellationToken),
                CompletedCount = await _dbContext.PendingCommands.CountAsync(c => c.Status == CommandStatus.Completed, cancellationToken),
                FailedCount = await _dbContext.PendingCommands.CountAsync(c => c.Status == CommandStatus.Failed, cancellationToken),
                CancelledCount = await _dbContext.PendingCommands.CountAsync(c => c.Status == CommandStatus.Cancelled, cancellationToken)
            };

            return Ok(stats);
        }
    }

    // Request/Response DTOs
    public sealed class QueueCommandRequest
    {
        public Guid DeviceId { get; set; }
        public DeviceConfigurationCommand Command { get; set; } = null!;
        public int? Priority { get; set; }
        public DateTimeOffset? ScheduledFor { get; set; }
    }

    public sealed class BatchCommandRequest
    {
        public List<Guid> DeviceIds { get; set; } = new();
        public DeviceConfigurationCommand Command { get; set; } = null!;
        public int? Priority { get; set; }
        public DateTimeOffset? ScheduledFor { get; set; }
    }

    public sealed class BatchCommandResponse
    {
        public int TotalDevices { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public List<PendingCommandEntity> QueuedCommands { get; set; } = new();
        public List<Guid> FailedDeviceIds { get; set; } = new();
    }

    public sealed class CommandStatistics
    {
        public int TotalCommands { get; set; }
        public int PendingCount { get; set; }
        public int CompletedCount { get; set; }
        public int FailedCount { get; set; }
        public int CancelledCount { get; set; }
    }
}
