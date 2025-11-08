using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SecureBootDashboard.Api.Services;
using SecureBootWatcher.Shared.Models;

namespace SecureBootDashboard.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public sealed class AnomaliesController : ControllerBase
    {
        private readonly IAnomalyDetectionService _anomalyService;
        private readonly ILogger<AnomaliesController> _logger;

        public AnomaliesController(
            IAnomalyDetectionService anomalyService,
            ILogger<AnomaliesController> logger)
        {
            _anomalyService = anomalyService;
            _logger = logger;
        }

        /// <summary>
        /// Get all active anomalies
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(IReadOnlyList<AnomalyDetectionResult>), 200)]
        public async Task<IActionResult> GetActiveAnomaliesAsync(CancellationToken cancellationToken)
        {
            try
            {
                var anomalies = await _anomalyService.GetActiveAnomaliesAsync(cancellationToken);
                return Ok(anomalies);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving active anomalies");
                return StatusCode(500, "Error retrieving anomalies");
            }
        }

        /// <summary>
        /// Get a specific anomaly by ID
        /// </summary>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(AnomalyDetectionResult), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetAnomalyAsync(Guid id, CancellationToken cancellationToken)
        {
            try
            {
                var anomaly = await _anomalyService.GetAnomalyAsync(id, cancellationToken);
                if (anomaly == null)
                {
                    return NotFound();
                }
                return Ok(anomaly);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving anomaly {AnomalyId}", id);
                return StatusCode(500, "Error retrieving anomaly");
            }
        }

        /// <summary>
        /// Trigger manual anomaly detection scan
        /// </summary>
        [HttpPost("scan")]
        [ProducesResponseType(typeof(IReadOnlyList<AnomalyDetectionResult>), 200)]
        public async Task<IActionResult> TriggerScanAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Manual anomaly detection scan triggered");
                var anomalies = await _anomalyService.DetectAnomaliesAsync(cancellationToken);
                return Ok(anomalies);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during manual anomaly detection scan");
                return StatusCode(500, "Error during anomaly detection");
            }
        }

        /// <summary>
        /// Resolve an anomaly
        /// </summary>
        [HttpPost("{id:guid}/resolve")]
        [ProducesResponseType(204)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> ResolveAnomalyAsync(
            Guid id, 
            [FromBody] ResolveAnomalyRequest request, 
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.ResolvedBy))
            {
                return BadRequest(new { error = "ResolvedBy is required" });
            }
            if (request.ResolvedBy != null && request.ResolvedBy.Length > 256)
            {
                return BadRequest(new { error = "ResolvedBy must not exceed 256 characters" });
            }
            try
            {
                var anomaly = await _anomalyService.GetAnomalyAsync(id, cancellationToken);
                if (anomaly == null)
                {
                    return NotFound();
                }

                await _anomalyService.ResolveAnomalyAsync(id, request.ResolvedBy, cancellationToken);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving anomaly {AnomalyId}", id);
                return StatusCode(500, "Error resolving anomaly");
            }
        }

        public sealed record ResolveAnomalyRequest(string? ResolvedBy);
    }
}
