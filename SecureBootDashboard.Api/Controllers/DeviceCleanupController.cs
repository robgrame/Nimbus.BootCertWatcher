using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SecureBootDashboard.Api.Data;

namespace SecureBootDashboard.Api.Controllers
{
    /// <summary>
    /// API endpoints for managing device cleanup configuration and operations.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public sealed class DeviceCleanupController : ControllerBase
    {
        private readonly SecureBootDbContext _dbContext;
        private readonly ILogger<DeviceCleanupController> _logger;

        public DeviceCleanupController(
            SecureBootDbContext dbContext,
            ILogger<DeviceCleanupController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        /// <summary>
        /// Get current cleanup configuration.
        /// </summary>
        [HttpGet("config")]
        [ProducesResponseType(typeof(DeviceCleanupConfigResponse), StatusCodes.Status200OK)]
        public async Task<ActionResult<DeviceCleanupConfigResponse>> GetConfigAsync(
            CancellationToken cancellationToken)
        {
            var config = await _dbContext.DeviceCleanupConfig
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken);

            if (config == null)
            {
                return NotFound(new { Error = "Cleanup configuration not found" });
            }

            var response = new DeviceCleanupConfigResponse
            {
                Id = config.Id,
                Enabled = config.Enabled,
                InactiveDaysThreshold = config.InactiveDaysThreshold,
                CleanupSchedule = config.CleanupSchedule,
                DeleteAssociatedData = config.DeleteAssociatedData,
                NotifyOnCleanup = config.NotifyOnCleanup,
                NotificationEmail = config.NotificationEmail,
                LastCleanupRunUtc = config.LastCleanupRunUtc,
                LastCleanupDeviceCount = config.LastCleanupDeviceCount,
                CreatedAtUtc = config.CreatedAtUtc,
                UpdatedAtUtc = config.UpdatedAtUtc
            };

            return Ok(response);
        }

        /// <summary>
        /// Update cleanup configuration.
        /// </summary>
        [HttpPut("config")]
        [ProducesResponseType(typeof(DeviceCleanupConfigResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<DeviceCleanupConfigResponse>> UpdateConfigAsync(
            [FromBody] UpdateCleanupConfigRequest request,
            CancellationToken cancellationToken)
        {
            var config = await _dbContext.DeviceCleanupConfig.FirstOrDefaultAsync(cancellationToken);

            if (config == null)
            {
                return NotFound(new { Error = "Cleanup configuration not found" });
            }

            // Validate threshold
            if (request.InactiveDaysThreshold < 1)
            {
                return BadRequest(new { Error = "InactiveDaysThreshold must be at least 1 day" });
            }

            // Update configuration
            config.Enabled = request.Enabled;
            config.InactiveDaysThreshold = request.InactiveDaysThreshold;
            config.CleanupSchedule = request.CleanupSchedule;
            config.DeleteAssociatedData = request.DeleteAssociatedData;
            config.NotifyOnCleanup = request.NotifyOnCleanup;
            config.NotificationEmail = request.NotificationEmail;
            config.UpdatedAtUtc = DateTimeOffset.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Cleanup configuration updated. Enabled: {Enabled}, Threshold: {Days} days",
                config.Enabled, config.InactiveDaysThreshold);

            var response = new DeviceCleanupConfigResponse
            {
                Id = config.Id,
                Enabled = config.Enabled,
                InactiveDaysThreshold = config.InactiveDaysThreshold,
                CleanupSchedule = config.CleanupSchedule,
                DeleteAssociatedData = config.DeleteAssociatedData,
                NotifyOnCleanup = config.NotifyOnCleanup,
                NotificationEmail = config.NotificationEmail,
                LastCleanupRunUtc = config.LastCleanupRunUtc,
                LastCleanupDeviceCount = config.LastCleanupDeviceCount,
                CreatedAtUtc = config.CreatedAtUtc,
                UpdatedAtUtc = config.UpdatedAtUtc
            };

            return Ok(response);
        }

        /// <summary>
        /// Get list of devices that would be cleaned up based on current threshold.
        /// </summary>
        [HttpGet("preview")]
        [ProducesResponseType(typeof(CleanupPreviewResponse), StatusCodes.Status200OK)]
        public async Task<ActionResult<CleanupPreviewResponse>> GetCleanupPreviewAsync(
            [FromQuery] int? daysThreshold = null,
            CancellationToken cancellationToken = default)
        {
            var config = await _dbContext.DeviceCleanupConfig
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken);

            var threshold = daysThreshold ?? config?.InactiveDaysThreshold ?? 90;
            var cutoffDate = DateTimeOffset.UtcNow.AddDays(-threshold);

            var inactiveDevices = await _dbContext.Devices
                .AsNoTracking()
                .Where(d => d.LastSeenUtc < cutoffDate)
                .OrderBy(d => d.LastSeenUtc)
                .Select(d => new InactiveDeviceInfo
                {
                    Id = d.Id,
                    MachineName = d.MachineName,
                    DomainName = d.DomainName,
                    LastSeenUtc = d.LastSeenUtc,
                    DaysInactive = (int)(DateTimeOffset.UtcNow - d.LastSeenUtc).TotalDays,
                    ReportCount = d.Reports.Count
                })
                .ToListAsync(cancellationToken);

            var response = new CleanupPreviewResponse
            {
                Threshold = threshold,
                CutoffDate = cutoffDate,
                DeviceCount = inactiveDevices.Count,
                Devices = inactiveDevices
            };

            return Ok(response);
        }

        /// <summary>
        /// Manually trigger cleanup for specific devices.
        /// </summary>
        [HttpPost("delete")]
        [ProducesResponseType(typeof(ManualCleanupResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ManualCleanupResponse>> DeleteDevicesAsync(
            [FromBody] ManualCleanupRequest request,
            CancellationToken cancellationToken)
        {
            if (request.DeviceIds == null || request.DeviceIds.Count == 0)
            {
                return BadRequest(new { Error = "No device IDs provided" });
            }

            var devices = await _dbContext.Devices
                .Where(d => request.DeviceIds.Contains(d.Id))
                .ToListAsync(cancellationToken);

            if (devices.Count == 0)
            {
                return BadRequest(new { Error = "No devices found matching provided IDs" });
            }

            var deletedDevices = devices.Select(d => new DeletedDeviceInfo
            {
                Id = d.Id,
                MachineName = d.MachineName,
                LastSeenUtc = d.LastSeenUtc
            }).ToList();

            _dbContext.Devices.RemoveRange(devices);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogWarning(
                "Manual device cleanup: {Count} devices deleted by user request",
                devices.Count);

            var response = new ManualCleanupResponse
            {
                DeletedCount = devices.Count,
                DeletedDevices = deletedDevices
            };

            return Ok(response);
        }

        /// <summary>
        /// Manually trigger full cleanup based on current configuration.
        /// </summary>
        [HttpPost("run")]
        [ProducesResponseType(typeof(ManualCleanupResponse), StatusCodes.Status200OK)]
        public async Task<ActionResult<ManualCleanupResponse>> RunCleanupAsync(
            CancellationToken cancellationToken)
        {
            var config = await _dbContext.DeviceCleanupConfig.FirstOrDefaultAsync(cancellationToken);

            if (config == null)
            {
                return NotFound(new { Error = "Cleanup configuration not found" });
            }

            var cutoffDate = DateTimeOffset.UtcNow.AddDays(-config.InactiveDaysThreshold);

            var inactiveDevices = await _dbContext.Devices
                .Where(d => d.LastSeenUtc < cutoffDate)
                .ToListAsync(cancellationToken);

            if (inactiveDevices.Count == 0)
            {
                return Ok(new ManualCleanupResponse
                {
                    DeletedCount = 0,
                    DeletedDevices = new List<DeletedDeviceInfo>()
                });
            }

            var deletedDevices = inactiveDevices.Select(d => new DeletedDeviceInfo
            {
                Id = d.Id,
                MachineName = d.MachineName,
                LastSeenUtc = d.LastSeenUtc
            }).ToList();

            _dbContext.Devices.RemoveRange(inactiveDevices);
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Update config
            config.LastCleanupRunUtc = DateTimeOffset.UtcNow;
            config.LastCleanupDeviceCount = inactiveDevices.Count;
            config.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogWarning(
                "Manual cleanup run: {Count} devices deleted. Threshold: {Days} days",
                inactiveDevices.Count, config.InactiveDaysThreshold);

            var response = new ManualCleanupResponse
            {
                DeletedCount = inactiveDevices.Count,
                DeletedDevices = deletedDevices
            };

            return Ok(response);
        }
    }

    // Response models
    public sealed record DeviceCleanupConfigResponse
    {
        public int Id { get; init; }
        public bool Enabled { get; init; }
        public int InactiveDaysThreshold { get; init; }
        public string? CleanupSchedule { get; init; }
        public bool DeleteAssociatedData { get; init; }
        public bool NotifyOnCleanup { get; init; }
        public string? NotificationEmail { get; init; }
        public DateTimeOffset? LastCleanupRunUtc { get; init; }
        public int LastCleanupDeviceCount { get; init; }
        public DateTimeOffset CreatedAtUtc { get; init; }
        public DateTimeOffset UpdatedAtUtc { get; init; }
    }

    public sealed record UpdateCleanupConfigRequest
    {
        public bool Enabled { get; init; }
        public int InactiveDaysThreshold { get; init; }
        public string? CleanupSchedule { get; init; }
        public bool DeleteAssociatedData { get; init; }
        public bool NotifyOnCleanup { get; init; }
        public string? NotificationEmail { get; init; }
    }

    public sealed record CleanupPreviewResponse
    {
        public int Threshold { get; init; }
        public DateTimeOffset CutoffDate { get; init; }
        public int DeviceCount { get; init; }
        public List<InactiveDeviceInfo> Devices { get; init; } = new();
    }

    public sealed record InactiveDeviceInfo
    {
        public Guid Id { get; init; }
        public string MachineName { get; init; } = string.Empty;
        public string? DomainName { get; init; }
        public DateTimeOffset LastSeenUtc { get; init; }
        public int DaysInactive { get; init; }
        public int ReportCount { get; init; }
    }

    public sealed record ManualCleanupRequest
    {
        public List<Guid> DeviceIds { get; init; } = new();
    }

    public sealed record ManualCleanupResponse
    {
        public int DeletedCount { get; init; }
        public List<DeletedDeviceInfo> DeletedDevices { get; init; } = new();
    }

    public sealed record DeletedDeviceInfo
    {
        public Guid Id { get; init; }
        public string MachineName { get; init; } = string.Empty;
        public DateTimeOffset LastSeenUtc { get; init; }
    }
}
