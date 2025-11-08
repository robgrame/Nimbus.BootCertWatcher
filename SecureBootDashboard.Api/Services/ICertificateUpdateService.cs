using System;
using System.Threading;
using System.Threading.Tasks;
using SecureBootWatcher.Shared.Models;

namespace SecureBootDashboard.Api.Services
{
    /// <summary>
    /// Service for managing certificate update commands.
    /// </summary>
    public interface ICertificateUpdateService
    {
        /// <summary>
        /// Sends a certificate update command for a fleet or group of devices.
        /// </summary>
        /// <param name="command">The update command to send.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The command ID and number of target devices.</returns>
        Task<CertificateUpdateResult> SendUpdateCommandAsync(
            CertificateUpdateCommand command,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the status of a certificate update command.
        /// </summary>
        /// <param name="commandId">The command ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The command status.</returns>
        Task<CertificateUpdateCommandStatus?> GetCommandStatusAsync(
            Guid commandId,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Result of sending a certificate update command.
    /// </summary>
    public sealed record CertificateUpdateResult(
        Guid CommandId,
        int TargetDeviceCount,
        string Message);

    /// <summary>
    /// Status of a certificate update command.
    /// </summary>
    public sealed record CertificateUpdateCommandStatus(
        Guid CommandId,
        string? FleetId,
        int TargetDeviceCount,
        int ProcessedDeviceCount,
        DateTimeOffset IssuedAtUtc,
        DateTimeOffset? CompletedAtUtc,
        string Status);
}
