using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using SecureBootDashboard.Api.Hubs;
using SecureBootWatcher.Shared.Models;
using SecureBootWatcher.Shared.Storage;
using SecureBootWatcher.Shared.Validation;

namespace SecureBootDashboard.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public sealed class SecureBootReportsController : ControllerBase
    {
        private readonly IReportStore _reportStore;
        private readonly IHubContext<DashboardHub> _hubContext;
        private readonly ILogger<SecureBootReportsController> _logger;

        public SecureBootReportsController(
            IReportStore reportStore,
            IHubContext<DashboardHub> hubContext,
            ILogger<SecureBootReportsController> logger)
        {
            _reportStore = reportStore;
            _hubContext = hubContext;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> IngestAsync([FromBody] SecureBootStatusReport? report)
        {
            _logger.LogDebug("IngestAsync: Received report ingestion request from {RemoteIp}", HttpContext.Connection.RemoteIpAddress);
            
            if (report is null)
            {
                _logger.LogWarning("IngestAsync: Report payload is null, rejecting request");
                return BadRequest(new { Errors = new[] { "Report payload is null." } });
            }

            _logger.LogTrace("IngestAsync: Validating report from device {MachineName}, CorrelationId={CorrelationId}, ClientVersion={ClientVersion}", 
                report.Device?.MachineName ?? "Unknown", report.CorrelationId ?? "None", report.ClientVersion ?? "Unknown");

            if (!ReportValidator.TryValidate(report, out var errors))
            {
                _logger.LogWarning("IngestAsync: Report validation failed for device {MachineName}. Errors: {Errors}", 
                    report.Device?.MachineName ?? "Unknown", string.Join("; ", errors));
                return BadRequest(new { Errors = errors });
            }

            _logger.LogDebug("IngestAsync: Report validation successful for device {MachineName}, proceeding to save", 
                report.Device.MachineName);

            try
            {
                _logger.LogTrace("IngestAsync: Saving report to store for device {MachineName}", report.Device.MachineName);
                var id = await _reportStore.SaveAsync(report, HttpContext.RequestAborted).ConfigureAwait(false);
                
                _logger.LogInformation("Successfully ingested report {ReportId} for device {MachineName} (Domain: {DomainName})", 
                    id, report.Device.MachineName, report.Device.DomainName ?? "None");
                
                _logger.LogDebug("IngestAsync: Report details - Events: {EventCount}, Certificates: {HasCertificates}, UefiCa2023Status: {Status}", 
                    report.Events?.Count ?? 0, 
                    report.Certificates != null ? "Yes" : "No",
                    report.Registry?.UefiCa2023Status ?? SecureBootWatcher.Shared.Models.SecureBootDeploymentState.Unknown);

                // Broadcast new report notification via SignalR
                try
                {
                    _logger.LogTrace("IngestAsync: Broadcasting SignalR notification for device {MachineName}", report.Device.MachineName);
                    
                    // Generate a consistent device identifier from machine name using MD5 hash
                    var hashBytes = System.Security.Cryptography.MD5.HashData(
                        System.Text.Encoding.UTF8.GetBytes(report.Device.MachineName.ToLowerInvariant()));
                    var deviceIdentifier = new Guid(hashBytes);
                    
                    await _hubContext.BroadcastNewReport(
                        deviceIdentifier,
                        id,
                        report.Device.MachineName);
                    
                    _logger.LogDebug("IngestAsync: Successfully broadcasted SignalR notification for device {MachineName}, DeviceId={DeviceId}", 
                        report.Device.MachineName, deviceIdentifier);
                }
                catch (Exception signalREx)
                {
                    // Log but don't fail the request if SignalR broadcast fails
                    _logger.LogWarning(signalREx, "IngestAsync: Failed to broadcast SignalR notification for report {ReportId}", id);
                }
                
                _logger.LogTrace("IngestAsync: Returning CreatedAtRoute response for report {ReportId}", id);
                // Usa CreatedAtRoute invece di CreatedAtAction per evitare problemi di routing
                return CreatedAtRoute("GetReport", new { id }, new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "IngestAsync: Failed to ingest secure boot report for machine {Machine}, CorrelationId={CorrelationId}", 
                    report.Device.MachineName, report.CorrelationId ?? "None");
                return StatusCode(500, new { Error = "Failed to persist report." });
            }
        }

        [HttpGet("{id:guid}", Name = "GetReport")]
        public async Task<IActionResult> GetReportAsync(Guid id)
        {
            _logger.LogDebug("GetReportAsync: Retrieving report {ReportId}", id);
            
            var report = await _reportStore.GetAsync(id, HttpContext.RequestAborted).ConfigureAwait(false);

            if (report == null)
            {
                _logger.LogDebug("GetReportAsync: Report {ReportId} not found", id);
                return NotFound();
            }

            _logger.LogTrace("GetReportAsync: Found report {ReportId} for device {MachineName}, returning details", 
                id, report.Device?.MachineName ?? "Unknown");
            return Ok(new ReportDetailResponse(report));
        }

        [HttpGet("recent")]
        public async Task<IReadOnlyCollection<ReportSummaryResponse>> GetRecentAsync([FromQuery] int limit = 50)
        {
            _logger.LogDebug("GetRecentAsync: Retrieving recent reports with limit={Limit}", limit);
            
            var reports = await _reportStore.GetRecentAsync(limit, HttpContext.RequestAborted).ConfigureAwait(false);
            
            _logger.LogTrace("GetRecentAsync: Found {Count} recent reports", reports.Count);
            return reports.Select(r => new ReportSummaryResponse(r)).ToArray();
        }

        public sealed record ReportSummaryResponse(Guid Id, string MachineName, string? DomainName, DateTimeOffset CreatedAtUtc, string? DeploymentState)
        {
            public ReportSummaryResponse(ReportSummary summary)
                : this(summary.Id, summary.MachineName, summary.DomainName, summary.CreatedAtUtc, summary.DeploymentState)
            {
            }
        }

        public sealed record ReportDetailResponse(
            Guid Id, 
            DeviceDetail Device, 
            DateTimeOffset CreatedAtUtc, 
            string RegistryStateJson, 
            string? CertificatesJson,  // ADDED
            string? AlertsJson, 
            string? DeploymentState, 
            string? ClientVersion, 
            string? CorrelationId, 
            IReadOnlyCollection<EventDetail> Events)
        {
            public ReportDetailResponse(ReportDetail detail)
                : this(
                    detail.Id,
                    new DeviceDetail(detail.Device),
                    detail.CreatedAtUtc,
                    detail.RegistryStateJson,
                    detail.CertificatesJson, // ADDED
                    detail.AlertsJson,
                    detail.DeploymentState,
                    detail.ClientVersion,
                    detail.CorrelationId,
                    detail.Events.Select(e => new EventDetail(e)).ToArray())
            {
            }
        }

        public sealed record DeviceDetail(Guid Id, string MachineName, string? DomainName, string? UserPrincipalName, string? Manufacturer, string? Model, string? FirmwareVersion, string? FleetId, string? TagsJson, DateTimeOffset FirstSeenUtc, DateTimeOffset LastSeenUtc)
        {
            public DeviceDetail(DeviceSnapshot snapshot)
                : this(
                    snapshot.Id,
                    snapshot.MachineName,
                    snapshot.DomainName,
                    snapshot.UserPrincipalName,
                    snapshot.Manufacturer,
                    snapshot.Model,
                    snapshot.FirmwareVersion,
                    snapshot.FleetId,
                    snapshot.TagsJson,
                    snapshot.FirstSeenUtc,
                    snapshot.LastSeenUtc)
            {
            }
        }

        public sealed record EventDetail(Guid Id, string ProviderName, int EventId, DateTimeOffset TimestampUtc, string? Level, string? Message, string? RawXml)
        {
            public EventDetail(EventSnapshot snapshot)
                : this(snapshot.Id, snapshot.ProviderName, snapshot.EventId, snapshot.TimestampUtc, snapshot.Level, snapshot.Message, snapshot.RawXml)
            {
            }
        }
    }
}
