using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SecureBootDashboard.Api.Data;
using SecureBootDashboard.Api.Services;
using SecureBootWatcher.Shared.Storage;

namespace SecureBootDashboard.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public sealed class DevicesController : ControllerBase
    {
        private readonly SecureBootDbContext _dbContext;
        private readonly IExportService _exportService;
        private readonly ILogger<DevicesController> _logger;

        public DevicesController(
            SecureBootDbContext dbContext,
            IExportService exportService,
            ILogger<DevicesController> logger)
        {
            _dbContext = dbContext;
            _exportService = exportService;
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
                    latestReport?.CreatedAtUtc,
                    d.UEFISecureBootEnabled);
            }).ToArray();
        }

        /// <summary>
        /// Export devices to Excel format
        /// </summary>
        [HttpGet("export/excel")]
        public async Task<IActionResult> ExportDevicesToExcelAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Exporting devices to Excel");

                // Get devices from database
                var devices = await _dbContext.Devices
                    .AsNoTracking()
                    .Include(d => d.Reports.OrderByDescending(r => r.CreatedAtUtc).Take(1))
                    .OrderByDescending(d => d.LastSeenUtc)
                    .ToListAsync(cancellationToken);

                // Map to ExportDeviceSummary
                var deviceSummaries = devices.Select(d =>
                {
                    var latestReport = d.Reports.FirstOrDefault();
                    return new ExportDeviceSummary(
                        d.Id,
                        d.MachineName,
                        d.DomainName,
                        d.FleetId,
                        d.Manufacturer,
                        d.Model,
                        d.Reports.Count,
                        latestReport?.DeploymentState,
                        d.LastSeenUtc,
                        d.UEFISecureBootEnabled
                    );
                }).ToList();

                // Export to Excel
                var excelBytes = await _exportService.ExportDevicesToExcelAsync(deviceSummaries, cancellationToken);

                var fileName = $"SecureBoot_Devices_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
                return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export devices to Excel");
                return StatusCode(500, new { Error = "Failed to export devices to Excel" });
            }
        }

        /// <summary>
        /// Export devices to CSV format
        /// </summary>
        [HttpGet("export/csv")]
        public async Task<IActionResult> ExportDevicesToCsvAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Exporting devices to CSV");

                // Get devices from database
                var devices = await _dbContext.Devices
                    .AsNoTracking()
                    .Include(d => d.Reports.OrderByDescending(r => r.CreatedAtUtc).Take(1))
                    .OrderByDescending(d => d.LastSeenUtc)
                    .ToListAsync(cancellationToken);

                // Map to ExportDeviceSummary
                var deviceSummaries = devices.Select(d =>
                {
                    var latestReport = d.Reports.FirstOrDefault();
                    return new ExportDeviceSummary(
                        d.Id,
                        d.MachineName,
                        d.DomainName,
                        d.FleetId,
                        d.Manufacturer,
                        d.Model,
                        d.Reports.Count,
                        latestReport?.DeploymentState,
                        d.LastSeenUtc,
                        d.UEFISecureBootEnabled
                    );
                }).ToList();

                // Export to CSV
                var csvBytes = await _exportService.ExportDevicesToCsvAsync(deviceSummaries, cancellationToken);

                var fileName = $"SecureBoot_Devices_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
                return File(csvBytes, "text/csv", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export devices to CSV");
                return StatusCode(500, new { Error = "Failed to export devices to CSV" });
            }
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

        /// <summary>
        /// Export reports for a specific device to Excel
        /// </summary>
        [HttpGet("{id:guid}/reports/export/excel")]
        public async Task<IActionResult> ExportDeviceReportsToExcelAsync(Guid id, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Exporting reports for device {DeviceId} to Excel", id);

                // Get device first to include machine name in filename
                var device = await _dbContext.Devices
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

                if (device == null)
                {
                    return NotFound();
                }

                // Get reports
                var reports = await _dbContext.Reports
                    .AsNoTracking()
                    .Where(r => r.DeviceId == id)
                    .OrderByDescending(r => r.CreatedAtUtc)
                    .ToListAsync(cancellationToken);

                // Map to ReportSummary
                var reportSummaries = reports.Select(r => new ReportSummary(
                    r.Id,
                    device.MachineName,
                    device.DomainName,
                    r.CreatedAtUtc,
                    r.DeploymentState
                )).ToList();

                // Export to Excel
                var excelBytes = await _exportService.ExportReportsToExcelAsync(reportSummaries, cancellationToken);

                var fileName = $"SecureBoot_Reports_{device.MachineName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
                return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export reports for device {DeviceId} to Excel", id);
                return StatusCode(500, new { Error = "Failed to export reports to Excel" });
            }
        }

        /// <summary>
        /// Export reports for a specific device to CSV
        /// </summary>
        [HttpGet("{id:guid}/reports/export/csv")]
        public async Task<IActionResult> ExportDeviceReportsToCsvAsync(Guid id, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Exporting reports for device {DeviceId} to CSV", id);

                // Get device first
                var device = await _dbContext.Devices
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

                if (device == null)
                {
                    return NotFound();
                }

                // Get reports
                var reports = await _dbContext.Reports
                    .AsNoTracking()
                    .Where(r => r.DeviceId == id)
                    .OrderByDescending(r => r.CreatedAtUtc)
                    .ToListAsync(cancellationToken);

                // Map to ReportSummary
                var reportSummaries = reports.Select(r => new ReportSummary(
                    r.Id,
                    device.MachineName,
                    device.DomainName,
                    r.CreatedAtUtc,
                    r.DeploymentState
                )).ToList();

                // Export to CSV
                var csvBytes = await _exportService.ExportReportsToCsvAsync(reportSummaries, cancellationToken);

                var fileName = $"SecureBoot_Reports_{device.MachineName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
                return File(csvBytes, "text/csv", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export reports for device {DeviceId} to CSV", id);
                return StatusCode(500, new { Error = "Failed to export reports to CSV" });
            }
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
            DateTimeOffset? LatestReportDate,
            bool? UEFISecureBootEnabled);

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
