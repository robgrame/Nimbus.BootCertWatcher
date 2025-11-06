using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
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
        private readonly ILogger<SecureBootReportsController> _logger;

        public SecureBootReportsController(IReportStore reportStore, ILogger<SecureBootReportsController> logger)
        {
            _reportStore = reportStore;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> IngestAsync([FromBody] SecureBootStatusReport? report)
        {
            if (report is null)
            {
                return BadRequest(new { Errors = new[] { "Report payload is null." } });
            }

            if (!ReportValidator.TryValidate(report, out var errors))
            {
                return BadRequest(new { Errors = errors });
            }

            try
            {
                var id = await _reportStore.SaveAsync(report, HttpContext.RequestAborted).ConfigureAwait(false);
                
                _logger.LogInformation("Successfully ingested report {ReportId} for device {MachineName}", 
                    id, report.Device.MachineName);
                
                // Usa CreatedAtRoute invece di CreatedAtAction per evitare problemi di routing
                return CreatedAtRoute("GetReport", new { id }, new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to ingest secure boot report for machine {Machine}", report.Device.MachineName);
                return StatusCode(500, new { Error = "Failed to persist report." });
            }
        }

        [HttpGet("{id:guid}", Name = "GetReport")]
        public async Task<IActionResult> GetReportAsync(Guid id)
        {
            var report = await _reportStore.GetAsync(id, HttpContext.RequestAborted).ConfigureAwait(false);

            if (report == null)
            {
                return NotFound();
            }

            return Ok(new ReportDetailResponse(report));
        }

        [HttpGet("recent")]
        public async Task<IReadOnlyCollection<ReportSummaryResponse>> GetRecentAsync([FromQuery] int limit = 50)
        {
            var reports = await _reportStore.GetRecentAsync(limit, HttpContext.RequestAborted).ConfigureAwait(false);
            return reports.Select(r => new ReportSummaryResponse(r)).ToArray();
        }

        public sealed record ReportSummaryResponse(Guid Id, string MachineName, string? DomainName, DateTimeOffset CreatedAtUtc, string? DeploymentState)
        {
            public ReportSummaryResponse(ReportSummary summary)
                : this(summary.Id, summary.MachineName, summary.DomainName, summary.CreatedAtUtc, summary.DeploymentState)
            {
            }
        }

        public sealed record ReportDetailResponse(Guid Id, DeviceDetail Device, DateTimeOffset CreatedAtUtc, string RegistryStateJson, string? AlertsJson, string? DeploymentState, string? ClientVersion, string? CorrelationId, IReadOnlyCollection<EventDetail> Events)
        {
            public ReportDetailResponse(ReportDetail detail)
                : this(
                    detail.Id,
                    new DeviceDetail(detail.Device),
                    detail.CreatedAtUtc,
                    detail.RegistryStateJson,
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
