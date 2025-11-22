using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SecureBootWatcher.Shared.Models;

namespace SecureBootWatcher.Client.Services
{
    /// <summary>
    /// Service for processing device configuration commands from the API.
    /// Commands are fetched, executed locally, verified, and results are reported back.
    /// </summary>
    internal interface ICommandProcessor
    {
        /// <summary>
        /// Fetches pending commands from the API for this device.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of pending commands to execute.</returns>
        Task<IReadOnlyList<DeviceConfigurationCommand>> FetchPendingCommandsAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Executes a single configuration command locally.
        /// This will modify registry keys or system settings as required.
        /// </summary>
        /// <param name="command">The command to execute.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Result of the command execution including success status and verification.</returns>
        Task<DeviceConfigurationResult> ExecuteCommandAsync(DeviceConfigurationCommand command, CancellationToken cancellationToken);

        /// <summary>
        /// Verifies that a command was successfully applied by reading current registry state.
        /// </summary>
        /// <param name="command">The command that was executed.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Current device configuration state after command execution.</returns>
        Task<DeviceConfigurationState> VerifyCommandResultAsync(DeviceConfigurationCommand command, CancellationToken cancellationToken);

        /// <summary>
        /// Reports command execution result back to the API.
        /// </summary>
        /// <param name="result">The result of command execution.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if result was successfully reported.</returns>
        Task<bool> ReportResultAsync(DeviceConfigurationResult result, CancellationToken cancellationToken);
    }
}
