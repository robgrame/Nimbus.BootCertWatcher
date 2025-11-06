using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SecureBootDashboard.Api.Data;

namespace SecureBootDashboard.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public sealed class DevicesController : ControllerBase
    {
        private readonly SecureBootDbContext _dbContext;
        private readonly ILogger<DevicesController> _logger;

        public DevicesController(SecureBootDbContext dbContext, ILogger<DevicesController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        /// <summary>
        /// Get all devices with their latest report summary
        /// </summary>
        [HttpGet]
        public async Task<IReadOnlyCollection<DeviceSummaryResponse>> GetDevicesAsync(CancellationToken cancellationToken)
        {
            var devices = await _dbContext.Devices
                .AsNoTracking()
                .Include(d => d.Reports.OrderByDescending(r => r.CreatedAtUtc).Take(1))
                .OrderByDescending(d => d.LastSeenUtc)
                .ToListAsync(cancellationToken);

            return devices.Select(d =>
            {
                var latestReport = d.Reports.FirstOrDefault();
                return new DeviceSummaryResponse(
                    d.Id,
                    d.MachineName,
                    d.DomainName,
                    d.FleetId,
                    d.Manufacturer,
                    d.Model,
                    d.CreatedAtUtc,
                    d.LastSeenUtc,
                    d.Reports.Count,
                    latestReport?.DeploymentState,
                    latestReport?.CreatedAtUtc);
            }).ToArray();
        }

        /// <summary>
        /// Get device details by ID
        /// </summary>
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<DeviceDetailResponse>> GetDeviceAsync(Guid id, CancellationToken cancellationToken)
        {
            var device = await _dbContext.Devices
                .AsNoTracking()
                .Include(d => d.Reports.OrderByDescending(r => r.CreatedAtUtc).Take(10))
                .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

            if (device == null)
            {
                return NotFound();
            }

            return new DeviceDetailResponse(
                device.Id,
                device.MachineName,
                device.DomainName,
                device.UserPrincipalName,
                device.FleetId,
                device.Manufacturer,
                device.Model,
                device.FirmwareVersion,
                device.TagsJson,
                device.CreatedAtUtc,
                device.LastSeenUtc,
                device.Reports.Select(r => new ReportHistoryItem(
                    r.Id,
                    r.CreatedAtUtc,
                    r.DeploymentState,
                    r.ClientVersion)).ToArray());
        }

        /// <summary>
        /// Get report history for a specific device
        /// </summary>
        [HttpGet("{id:guid}/reports")]
        public async Task<IReadOnlyCollection<ReportHistoryItem>> GetDeviceReportsAsync(
            Guid id,
            [FromQuery] int limit = 50,
            CancellationToken cancellationToken = default)
        {
            limit = Math.Clamp(limit, 1, 200);

            var reports = await _dbContext.Reports
                .AsNoTracking()
                .Where(r => r.DeviceId == id)
                .OrderByDescending(r => r.CreatedAtUtc)
                .Take(limit)
                .Select(r => new ReportHistoryItem(
                    r.Id,
                    r.CreatedAtUtc,
                    r.DeploymentState,
                    r.ClientVersion))
                .ToListAsync(cancellationToken);

            return reports;
        }

        public sealed record DeviceSummaryResponse(
            Guid Id,
            string MachineName,
            string? DomainName,
            string? FleetId,
            string? Manufacturer,
            string? Model,
            DateTimeOffset FirstSeenUtc,
            DateTimeOffset LastSeenUtc,
            int ReportCount,
            string? LatestDeploymentState,
            DateTimeOffset? LatestReportDate);

        public sealed record DeviceDetailResponse(
            Guid Id,
            string MachineName,
            string? DomainName,
            string? UserPrincipalName,
            string? FleetId,
            string? Manufacturer,
            string? Model,
            string? FirmwareVersion,
            string? TagsJson,
            DateTimeOffset FirstSeenUtc,
            DateTimeOffset LastSeenUtc,
            IReadOnlyCollection<ReportHistoryItem> RecentReports);

        public sealed record ReportHistoryItem(
            Guid ReportId,
            DateTimeOffset CreatedAtUtc,
            string? DeploymentState,
            string? ClientVersion);
    }
}
