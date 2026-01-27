using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SecureBootDashboard.Api.Data;
using SecureBootDashboard.Api.Services;
using SecureBootWatcher.Shared.Models;
using SecureBootWatcher.Shared.Storage;

namespace SecureBootDashboard.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public sealed class DevicesController : ControllerBase
    {
        private readonly SecureBootDbContext _dbContext;
        private readonly IExportService _exportService;
        private readonly ISecureBootReadinessService _readinessService;
        private readonly ILogger<DevicesController> _logger;

        public DevicesController(
            SecureBootDbContext dbContext,
            IExportService exportService,
            ISecureBootReadinessService readinessService,
            ILogger<DevicesController> logger)
        {
            _dbContext = dbContext;
            _exportService = exportService;
            _readinessService = readinessService;
            _logger = logger;
        }

        /// <summary>
        /// Get all devices with their latest report summary
        /// </summary>
        [HttpGet]
        [OutputCache(PolicyName = "DeviceList")]
        [EnableRateLimiting("api")]
        public async Task<IReadOnlyCollection<DeviceSummaryResponse>> GetDevicesAsync(CancellationToken cancellationToken)
        {
            _logger.LogDebug("GetDevicesAsync: Retrieving all devices with latest report summaries");
            
            var devices = await _dbContext.Devices
                .AsNoTracking()
                .Include(d => d.Reports.OrderByDescending(r => r.CreatedAtUtc).Take(1))
                .OrderByDescending(d => d.LastSeenUtc)
                .ToListAsync(cancellationToken);

            _logger.LogTrace("GetDevicesAsync: Retrieved {DeviceCount} devices from database", devices.Count);

            return devices.Select(d =>
            {
                var latestReport = d.Reports.FirstOrDefault();
                
                // Deserialize registry state to extract telemetry and CFR data
                uint? allowTelemetry = null;
                bool? microsoftUpdateManagedOptIn = null;
                uint? windowsUEFICA2023Capable = null;
                SecureBootWatcher.Shared.Models.SecureBootCertificateCollection? certificates = null;
                
                if (latestReport != null && !string.IsNullOrEmpty(latestReport.RegistryStateJson))
                {
                    try
                    {
                        // Deserialize the full report to get Registry and TelemetryPolicy
                        var report = System.Text.Json.JsonSerializer.Deserialize<SecureBootWatcher.Shared.Models.SecureBootStatusReport>(
                            latestReport.RegistryStateJson);
                        
                        if (report != null)
                        {
                            // Extract from Registry snapshot
                            microsoftUpdateManagedOptIn = report.Registry?.MicrosoftUpdateManagedOptIn;
                            windowsUEFICA2023Capable = report.Registry?.WindowsUEFICA2023Capable;
                            
                            // Extract from TelemetryPolicy snapshot
                            allowTelemetry = report.TelemetryPolicy?.AllowTelemetry;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug("GetDevicesAsync: Failed to deserialize registry state for device {DeviceId} ({MachineName})", 
                            d.Id, d.MachineName);
                        _logger.LogTrace(ex, "GetDevicesAsync: Registry deserialization exception details for device {DeviceId}", d.Id);
                    }
                }
                
                // Deserialize certificates
                if (latestReport != null && !string.IsNullOrEmpty(latestReport.CertificatesJson))
                {
                    try
                    {
                        certificates = System.Text.Json.JsonSerializer.Deserialize<SecureBootWatcher.Shared.Models.SecureBootCertificateCollection>(
                            latestReport.CertificatesJson);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug("GetDevicesAsync: Failed to deserialize certificates for device {DeviceId} ({MachineName})", 
                            d.Id, d.MachineName);
                        _logger.LogTrace(ex, "GetDevicesAsync: Certificates deserialization exception details for device {DeviceId}", d.Id);
                    }
                }
                
                // Evaluate readiness using the service
                _logger.LogTrace("GetDevicesAsync: Evaluating readiness for device {DeviceId} ({MachineName})", d.Id, d.MachineName);
                var readinessEvaluation = _readinessService.EvaluateReadiness(
                    certificates,
                    d.OSVersion,
                    d.OSBuildNumber,
                    d.FirmwareReleaseDate);
                
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
                    d.UEFISecureBootEnabled,
                    d.ClientVersion,
                    d.OperatingSystem,
                    d.OSVersion,
                    d.OSBuildNumber,
                    d.OSProductType,
                    d.ChassisTypesJson,
                    d.IsVirtualMachine,
                    d.VirtualizationPlatform,
                    d.FirmwareReleaseDate,
                    allowTelemetry,
                    microsoftUpdateManagedOptIn,
                    windowsUEFICA2023Capable)
                {
                    // Set readiness properties from evaluation
                    IsReadyToUpdate = readinessEvaluation.IsReadyToUpdate,
                    IsOSReady = readinessEvaluation.IsOSReady,
                    AreOemCertificatesValid = readinessEvaluation.AreOemCertificatesValid,
                    HasWindowsUEFICA2023 = readinessEvaluation.HasWindowsUEFICA2023,
                    HasNoOemCertificates = readinessEvaluation.HasNoOemCertificates,
                    HasLegacyCertificatesExpiring2026 = readinessEvaluation.HasLegacyCertificatesExpiring2026,
                    LegacyCertificateCount2026 = readinessEvaluation.LegacyCertificateCount2026,
                    ExpiredOemCertificateCount = readinessEvaluation.ExpiredOemCertificateCount,
                    CriticalOemCertificateCount = readinessEvaluation.CriticalOemCertificateCount,
                    WarningOemCertificateCount = readinessEvaluation.WarningOemCertificateCount,
                    ValidOemCertificateCount = readinessEvaluation.ValidOemCertificateCount,
                    OSEvaluationDetails = readinessEvaluation.OSEvaluationDetails,
                    CertificateEvaluationDetails = readinessEvaluation.CertificateEvaluationDetails,
                    FirmwareConfidence = readinessEvaluation.FirmwareConfidence,
                    FirmwareEvaluationDetails = readinessEvaluation.FirmwareEvaluationDetails
                };
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
                _logger.LogInformation("ExportDevicesToExcelAsync: Starting device export to Excel");
                _logger.LogDebug("ExportDevicesToExcelAsync: Querying database for device data");

                // Get devices from database
                var devices = await _dbContext.Devices
                    .AsNoTracking()
                    .Include(d => d.Reports.OrderByDescending(r => r.CreatedAtUtc).Take(1))
                    .OrderByDescending(d => d.LastSeenUtc)
                    .ToListAsync(cancellationToken);

                _logger.LogDebug("ExportDevicesToExcelAsync: Retrieved {DeviceCount} devices", devices.Count);

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

                _logger.LogTrace("ExportDevicesToExcelAsync: Mapped {Count} device summaries", deviceSummaries.Count);

                // Export to Excel
                _logger.LogDebug("ExportDevicesToExcelAsync: Calling export service to generate Excel file");
                var excelBytes = await _exportService.ExportDevicesToExcelAsync(deviceSummaries, cancellationToken);

                var fileName = $"SecureBoot_Devices_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
                _logger.LogInformation("ExportDevicesToExcelAsync: Successfully exported {DeviceCount} devices to Excel file {FileName} ({SizeKB} KB)", 
                    deviceSummaries.Count, fileName, excelBytes.Length / 1024);
                
                return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ExportDevicesToExcelAsync: Failed to export devices to Excel");
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
                _logger.LogInformation("ExportDevicesToCsvAsync: Starting device export to CSV");
                _logger.LogDebug("ExportDevicesToCsvAsync: Querying database for device data");

                // Get devices from database
                var devices = await _dbContext.Devices
                    .AsNoTracking()
                    .Include(d => d.Reports.OrderByDescending(r => r.CreatedAtUtc).Take(1))
                    .OrderByDescending(d => d.LastSeenUtc)
                    .ToListAsync(cancellationToken);

                _logger.LogDebug("ExportDevicesToCsvAsync: Retrieved {DeviceCount} devices", devices.Count);

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

                _logger.LogTrace("ExportDevicesToCsvAsync: Mapped {Count} device summaries", deviceSummaries.Count);

                // Export to CSV
                _logger.LogDebug("ExportDevicesToCsvAsync: Calling export service to generate CSV file");
                var csvBytes = await _exportService.ExportDevicesToCsvAsync(deviceSummaries, cancellationToken);

                var fileName = $"SecureBoot_Devices_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
                _logger.LogInformation("ExportDevicesToCsvAsync: Successfully exported {DeviceCount} devices to CSV file {FileName} ({SizeKB} KB)", 
                    deviceSummaries.Count, fileName, csvBytes.Length / 1024);
                
                return File(csvBytes, "text/csv", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ExportDevicesToCsvAsync: Failed to export devices to CSV");
                return StatusCode(500, new { Error = "Failed to export devices to CSV" });
            }
        }

        /// <summary>
        /// Get device details by ID
        /// </summary>
        [HttpGet("{id:guid}")]
        [OutputCache(PolicyName = "DeviceDetails")]
        [EnableRateLimiting("api")]
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

            var latestReport = device.Reports.FirstOrDefault();
            
            // Deserialize certificates for readiness evaluation
            SecureBootWatcher.Shared.Models.SecureBootCertificateCollection? certificates = null;
            if (latestReport != null && !string.IsNullOrEmpty(latestReport.CertificatesJson))
            {
                try
                {
                    certificates = System.Text.Json.JsonSerializer.Deserialize<SecureBootWatcher.Shared.Models.SecureBootCertificateCollection>(
                        latestReport.CertificatesJson);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize certificates for device {DeviceId}", device.Id);
                }
            }
            
            // Evaluate readiness using the service
            var readinessEvaluation = _readinessService.EvaluateReadiness(
                certificates,
                device.OSVersion,
                device.OSBuildNumber,
                device.FirmwareReleaseDate);

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
                device.UEFISecureBootEnabled,
                latestReport?.RegistryStateJson,
                latestReport?.CertificatesJson,
                device.Reports.Select(r => new ReportHistoryItem(
                    r.Id,
                    r.CreatedAtUtc,
                    r.DeploymentState,
                    r.ClientVersion)).ToArray())
            {
                // Add readiness properties from evaluation
                IsReadyToUpdate = readinessEvaluation.IsReadyToUpdate,
                IsOSReady = readinessEvaluation.IsOSReady,
                AreOemCertificatesValid = readinessEvaluation.AreOemCertificatesValid,
                HasWindowsUEFICA2023 = readinessEvaluation.HasWindowsUEFICA2023,
                HasNoOemCertificates = readinessEvaluation.HasNoOemCertificates,
                HasLegacyCertificatesExpiring2026 = readinessEvaluation.HasLegacyCertificatesExpiring2026,
                LegacyCertificateCount2026 = readinessEvaluation.LegacyCertificateCount2026,
                ExpiredOemCertificateCount = readinessEvaluation.ExpiredOemCertificateCount,
                CriticalOemCertificateCount = readinessEvaluation.CriticalOemCertificateCount,
                WarningOemCertificateCount = readinessEvaluation.WarningOemCertificateCount,
                ValidOemCertificateCount = readinessEvaluation.ValidOemCertificateCount,
                OSEvaluationDetails = readinessEvaluation.OSEvaluationDetails,
                CertificateEvaluationDetails = readinessEvaluation.CertificateEvaluationDetails,
                FirmwareConfidence = readinessEvaluation.FirmwareConfidence,
                FirmwareEvaluationDetails = readinessEvaluation.FirmwareEvaluationDetails
            };
        }

        /// <summary>
        /// Get report history for a specific device
        /// </summary>
        [HttpGet("{id:guid}/reports")]
        public async Task<ActionResult<IReadOnlyList<ReportHistoryItem>>> GetDeviceReportsAsync(
            Guid id, 
            [FromQuery] int limit = 50, 
            CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Fetching reports for device {DeviceId} (limit: {Limit})", id, limit);

            // Verify device exists
            var deviceExists = await _dbContext.Devices
                .AsNoTracking()
                .AnyAsync(d => d.Id == id, cancellationToken);

            if (!deviceExists)
            {
                _logger.LogWarning("Device {DeviceId} not found", id);
                return NotFound(new { Error = $"Device with ID {id} not found" });
            }

            // Get reports for this device
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

            _logger.LogInformation("Found {Count} reports for device {DeviceId}", reports.Count, id);

            return Ok(reports);
        }
    }

    // Response DTOs
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
        bool? UEFISecureBootEnabled,
        string? ClientVersion,
        string? OperatingSystem,
        string? OSVersion,
        string? OSBuildNumber,
        int? OSProductType,
        string? ChassisTypesJson,
        bool? IsVirtualMachine,
        string? VirtualizationPlatform,
        DateTime? FirmwareReleaseDate,
        uint? AllowTelemetry,
        bool? MicrosoftUpdateManagedOptIn,
        uint? WindowsUEFICA2023Capable)
    {
        /// <summary>
        /// Overall readiness status - set by SecureBootReadinessService
        /// </summary>
        public bool IsReadyToUpdate { get; init; }

        /// <summary>
        /// OS version meets minimum requirements - set by SecureBootReadinessService
        /// </summary>
        public bool IsOSReady { get; init; }

        /// <summary>
        /// OEM certificates are valid (not expired and not expiring soon) - set by SecureBootReadinessService
        /// </summary>
        public bool AreOemCertificatesValid { get; init; }

        /// <summary>
        /// Windows UEFI CA 2023 is present in db - set by SecureBootReadinessService
        /// </summary>
        public bool HasWindowsUEFICA2023 { get; init; }

        /// <summary>
        /// Indicates if no OEM certificates were found (VM, consumer device, or read error)
        /// </summary>
        public bool HasNoOemCertificates { get; init; }

        /// <summary>
        /// Indicates if device has legacy Microsoft certificates (e.g., Windows Production PCA 2011) 
        /// that will expire in April 2026 and needs to be updated to Windows UEFI CA 2023
        /// </summary>
        public bool HasLegacyCertificatesExpiring2026 { get; init; }

        /// <summary>
        /// Number of legacy Microsoft certificates expiring in 2026
        /// </summary>
        public int LegacyCertificateCount2026 { get; init; }

        /// <summary>
        /// Number of expired OEM certificates
        /// </summary>
        public int ExpiredOemCertificateCount { get; init; }

        /// <summary>
        /// Number of OEM certificates expiring within critical threshold
        /// </summary>
        public int CriticalOemCertificateCount { get; init; }

        /// <summary>
        /// Number of OEM certificates expiring within warning threshold
        /// </summary>
        public int WarningOemCertificateCount { get; init; }

        /// <summary>
        /// Number of valid OEM certificates
        /// </summary>
        public int ValidOemCertificateCount { get; init; }

        /// <summary>
        /// Detailed OS evaluation message
        /// </summary>
        public string OSEvaluationDetails { get; init; } = string.Empty;

        /// <summary>
        /// Detailed certificate evaluation message
        /// </summary>
        public string CertificateEvaluationDetails { get; init; } = string.Empty;

        /// <summary>
        /// Firmware compatibility confidence level based on release date.
        /// HIGH: Released after Jan 1, 2025 (Green)
        /// MEDIUM: Released during 2024 (Yellow)
        /// LOW: Released before 2024 (Red)
        /// UNKNOWN: Release date not available (Gray)
        /// </summary>
        public FirmwareConfidenceLevel FirmwareConfidence { get; init; }

        /// <summary>
        /// Detailed firmware evaluation message including confidence level explanation
        /// </summary>
        public string FirmwareEvaluationDetails { get; init; } = string.Empty;
    }

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
        bool? UEFISecureBootEnabled,
        string? LatestRegistryStateJson,
        string? LatestCertificatesJson,
        IReadOnlyCollection<ReportHistoryItem> RecentReports)
    {
        /// <summary>
        /// Overall readiness status
        /// </summary>
        public bool IsReadyToUpdate { get; init; }

        /// <summary>
        /// OS version meets minimum requirements
        /// </summary>
        public bool IsOSReady { get; init; }

        /// <summary>
        /// OEM certificates are valid (not expired and not expiring soon)
        /// </summary>
        public bool AreOemCertificatesValid { get; init; }

        /// <summary>
        /// Windows UEFI CA 2023 is present in db
        /// </summary>
        public bool HasWindowsUEFICA2023 { get; init; }

        /// <summary>
        /// Indicates if no OEM certificates were found (VM, consumer device, or read error)
        /// </summary>
        public bool HasNoOemCertificates { get; init; }

        /// <summary>
        /// Indicates if device has legacy Microsoft certificates (e.g., Windows Production PCA 2011) 
        /// that will expire in April 2026 and needs to be updated to Windows UEFI CA 2023
        /// </summary>
        public bool HasLegacyCertificatesExpiring2026 { get; init; }

        /// <summary>
        /// Number of legacy Microsoft certificates expiring in 2026
        /// </summary>
        public int LegacyCertificateCount2026 { get; init; }

        /// <summary>
        /// Number of expired OEM certificates
        /// </summary>
        public int ExpiredOemCertificateCount { get; init; }

        /// <summary>
        /// Number of OEM certificates expiring within critical threshold
        /// </summary>
        public int CriticalOemCertificateCount { get; init; }

        /// <summary>
        /// Number of OEM certificates expiring within warning threshold
        /// </summary>
        public int WarningOemCertificateCount { get; init; }

        /// <summary>
        /// Number of valid OEM certificates
        /// </summary>
        public int ValidOemCertificateCount { get; init; }

        /// <summary>
        /// Detailed OS evaluation message
        /// </summary>
        public string OSEvaluationDetails { get; init; } = string.Empty;

        /// <summary>
        /// Detailed certificate evaluation message
        /// </summary>
        public string CertificateEvaluationDetails { get; init; } = string.Empty;

        /// <summary>
        /// Firmware compatibility confidence level based on release date.
        /// HIGH: Released after Jan 1, 2025 (Green)
        /// MEDIUM: Released during 2024 (Yellow)
        /// LOW: Released before 2024 (Red)
        /// UNKNOWN: Release date not available (Gray)
        /// </summary>
        public FirmwareConfidenceLevel FirmwareConfidence { get; init; }

        /// <summary>
        /// Detailed firmware evaluation message including confidence level explanation
        /// </summary>
        public string FirmwareEvaluationDetails { get; init; } = string.Empty;
    }

    public sealed record ReportHistoryItem(
        Guid ReportId,
        DateTimeOffset CreatedAtUtc,
        string? DeploymentState,
        string? ClientVersion);
}

