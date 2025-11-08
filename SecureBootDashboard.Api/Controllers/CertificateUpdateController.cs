using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SecureBootDashboard.Api.Services;
using SecureBootWatcher.Shared.Models;

namespace SecureBootDashboard.Api.Controllers
{
    /// <summary>
    /// Controller for managing certificate update commands.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public sealed class CertificateUpdateController : ControllerBase
    {
        private readonly ICertificateUpdateService _updateService;
        private readonly ILogger<CertificateUpdateController> _logger;

        public CertificateUpdateController(
            ICertificateUpdateService updateService,
            ILogger<CertificateUpdateController> logger)
        {
            _updateService = updateService;
            _logger = logger;
        }

        /// <summary>
        /// Trigger certificate update for a fleet or group of devices.
        /// </summary>
        /// <param name="request">The update command request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Result of the update command.</returns>
        [HttpPost("trigger")]
        public async Task<ActionResult<CertificateUpdateResult>> TriggerUpdateAsync(
            [FromBody] CertificateUpdateRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                // Sanitize fleet ID for logging to prevent log forging
            var sanitizedFleetId = request.FleetId?.Replace("\r", "").Replace("\n", "") ?? "ALL";
            _logger.LogInformation(
                "Received certificate update request for fleet {FleetId}",
                sanitizedFleetId);

            var command = new CertificateUpdateCommand
            {
                FleetId = request.FleetId,
                TargetDevices = request.TargetDevices ?? Array.Empty<string>(),
                UpdateFlags = request.UpdateFlags,
                IssuedBy = request.IssuedBy,
                Notes = request.Notes,
                ExpiresAtUtc = request.ExpiresAtUtc
            };

            var result = await _updateService.SendUpdateCommandAsync(command, cancellationToken);

            return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to trigger certificate update for fleet {FleetId}", request.FleetId);
                return StatusCode(500, new { Error = "Failed to trigger certificate update" });
            }
        }

        /// <summary>
        /// Get the status of a certificate update command.
        /// </summary>
        /// <param name="commandId">The command ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Command status.</returns>
        [HttpGet("status/{commandId:guid}")]
        public async Task<ActionResult<CertificateUpdateCommandStatus>> GetCommandStatusAsync(
            Guid commandId,
            CancellationToken cancellationToken)
        {
            var status = await _updateService.GetCommandStatusAsync(commandId, cancellationToken);

            if (status == null)
            {
                return NotFound(new { Error = "Command not found" });
            }

            return Ok(status);
        }

        /// <summary>
        /// Request model for triggering certificate updates.
        /// </summary>
        public sealed record CertificateUpdateRequest
        {
            /// <summary>
            /// The fleet ID to target. If null, targets all devices.
            /// </summary>
            public string? FleetId { get; init; }

            /// <summary>
            /// Specific device machine names to target. If empty, targets all devices in fleet.
            /// </summary>
            public string[]? TargetDevices { get; init; }

            /// <summary>
            /// The update flags to apply. If null, uses Windows defaults.
            /// </summary>
            public uint? UpdateFlags { get; init; }

            /// <summary>
            /// When this command expires (optional).
            /// </summary>
            public DateTimeOffset? ExpiresAtUtc { get; init; }

            /// <summary>
            /// User or system that issued this command.
            /// </summary>
            [Required]
            public string? IssuedBy { get; init; }

            /// <summary>
            /// Additional notes or reason for this update command.
            /// </summary>
            public string? Notes { get; init; }
        }
    }
}
