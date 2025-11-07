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
    [ApiController]
    [Route("api/[controller]")]
    public sealed class AnalyticsController : ControllerBase
    {
        private readonly SecureBootDbContext _dbContext;
        private readonly ILogger<AnalyticsController> _logger;

        public AnalyticsController(SecureBootDbContext dbContext, ILogger<AnalyticsController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        /// <summary>
        /// Get compliance trend data for the specified number of days
        /// </summary>
        /// <param name="days">Number of days to retrieve (7, 30, 60, or 90)</param>
        [HttpGet("compliance-trend")]
        public async Task<ActionResult<ComplianceTrendResponse>> GetComplianceTrendAsync(
            [FromQuery] int days = 7,
            CancellationToken cancellationToken = default)
        {
            // Validate days parameter
            if (days < 1 || days > 365)
            {
                return BadRequest("Days must be between 1 and 365");
            }

            var startDate = DateTimeOffset.UtcNow.Date.AddDays(-days);
            
            // Get all reports within the date range
            var reports = await _dbContext.Reports
                .AsNoTracking()
                .Where(r => r.CreatedAtUtc >= startDate)
                .Select(r => new { r.CreatedAtUtc, r.DeploymentState, r.DeviceId })
                .ToListAsync(cancellationToken);

            // Get all devices to calculate total device count per day
            var allDevices = await _dbContext.Devices
                .AsNoTracking()
                .Where(d => d.CreatedAtUtc <= DateTimeOffset.UtcNow.Date)
                .Select(d => new { d.Id, d.CreatedAtUtc })
                .ToListAsync(cancellationToken);

            // Calculate daily snapshots
            var dailySnapshots = new Dictionary<string, DailySnapshot>();
            
            for (int i = 0; i < days; i++)
            {
                var date = DateTimeOffset.UtcNow.Date.AddDays(-i);
                var dateKey = date.ToString("yyyy-MM-dd");
                
                // Devices that existed on this date
                var devicesOnDate = allDevices.Where(d => d.CreatedAtUtc.Date <= date).Select(d => d.Id).ToHashSet();
                
                // For each device, find its most recent deployment state as of this date
                var deviceStates = new Dictionary<Guid, string?>();
                
                foreach (var deviceId in devicesOnDate)
                {
                    var latestReport = reports
                        .Where(r => r.DeviceId == deviceId && r.CreatedAtUtc.Date <= date)
                        .OrderByDescending(r => r.CreatedAtUtc)
                        .FirstOrDefault();
                    
                    deviceStates[deviceId] = latestReport?.DeploymentState;
                }
                
                var totalDevices = devicesOnDate.Count;
                var deployedDevices = deviceStates.Count(kvp => kvp.Value == "Deployed");
                var pendingDevices = deviceStates.Count(kvp => kvp.Value == "Pending");
                var errorDevices = deviceStates.Count(kvp => kvp.Value == "Error");
                var unknownDevices = totalDevices - deployedDevices - pendingDevices - errorDevices;
                
                dailySnapshots[dateKey] = new DailySnapshot(
                    date,
                    totalDevices,
                    deployedDevices,
                    pendingDevices,
                    errorDevices,
                    unknownDevices,
                    totalDevices > 0 ? (double)deployedDevices / totalDevices * 100 : 0);
            }

            // Sort by date ascending
            var sortedSnapshots = dailySnapshots
                .OrderBy(kvp => kvp.Key)
                .Select(kvp => kvp.Value)
                .ToArray();

            return new ComplianceTrendResponse(days, sortedSnapshots);
        }

        /// <summary>
        /// Get enrollment trend showing new devices over time
        /// </summary>
        [HttpGet("enrollment-trend")]
        public async Task<ActionResult<EnrollmentTrendResponse>> GetEnrollmentTrendAsync(
            [FromQuery] int days = 30,
            CancellationToken cancellationToken = default)
        {
            if (days < 1 || days > 365)
            {
                return BadRequest("Days must be between 1 and 365");
            }

            var startDate = DateTimeOffset.UtcNow.Date.AddDays(-days);
            
            var devices = await _dbContext.Devices
                .AsNoTracking()
                .Where(d => d.CreatedAtUtc >= startDate)
                .Select(d => d.CreatedAtUtc)
                .ToListAsync(cancellationToken);

            // Group by date and count
            var dailyEnrollments = new Dictionary<string, int>();
            
            for (int i = 0; i < days; i++)
            {
                var date = DateTimeOffset.UtcNow.Date.AddDays(-i);
                var dateKey = date.ToString("yyyy-MM-dd");
                var count = devices.Count(d => d.Date == date);
                dailyEnrollments[dateKey] = count;
            }

            var sortedEnrollments = dailyEnrollments
                .OrderBy(kvp => kvp.Key)
                .Select(kvp => new EnrollmentDataPoint(DateTimeOffset.Parse(kvp.Key), kvp.Value))
                .ToArray();

            return new EnrollmentTrendResponse(days, sortedEnrollments);
        }

        public sealed record ComplianceTrendResponse(
            int Days,
            IReadOnlyCollection<DailySnapshot> Snapshots);

        public sealed record DailySnapshot(
            DateTimeOffset Date,
            int TotalDevices,
            int DeployedDevices,
            int PendingDevices,
            int ErrorDevices,
            int UnknownDevices,
            double CompliancePercentage);

        public sealed record EnrollmentTrendResponse(
            int Days,
            IReadOnlyCollection<EnrollmentDataPoint> DataPoints);

        public sealed record EnrollmentDataPoint(
            DateTimeOffset Date,
            int NewDevices);
    }
}
