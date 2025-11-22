using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SecureBootWatcher.Client.Sinks;
using SecureBootWatcher.Shared.Configuration;
using SecureBootWatcher.Shared.Models;
using SecureBootWatcher.Shared.Validation;

namespace SecureBootWatcher.Client.Services
{
    internal sealed class SecureBootWatcherService
    {
        private readonly ILogger<SecureBootWatcherService> _logger;
        private readonly IReportBuilder _reportBuilder;
        private readonly IReportSink _reportSink;
        private readonly ICommandProcessor? _commandProcessor;
        private readonly IOptionsMonitor<SecureBootWatcherOptions> _options;

        public SecureBootWatcherService(
            ILogger<SecureBootWatcherService> logger,
            IReportBuilder reportBuilder,
            IReportSink reportSink,
            IOptionsMonitor<SecureBootWatcherOptions> options,
            ICommandProcessor? commandProcessor = null)
        {
            _logger = logger;
            _reportBuilder = reportBuilder;
            _reportSink = reportSink;
            _options = options;
            _commandProcessor = commandProcessor;
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            var options = _options.CurrentValue;
            var runOnce = options.RunMode.Equals("Once", StringComparison.OrdinalIgnoreCase);

            IClientUpdateService? updateService = null;
            UpdateCheckResult? updateCheck = null;
            bool autoInstallEnabled = false;

            // Try to resolve update service from report builder if available
            if (_reportBuilder is ReportBuilder rb)
            {
                var field = typeof(ReportBuilder).GetField("_updateService", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                updateService = field?.GetValue(rb) as IClientUpdateService;
            }

            if (updateService != null && options.ClientUpdate.CheckForUpdates)
            {
                try
                {
                    updateCheck = await updateService.CheckForUpdateAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to check for client updates at startup");
                }
            }
            autoInstallEnabled = options.ClientUpdate.AutoInstallEnabled;

            if (runOnce)
            {
                _logger.LogInformation("Secure Boot watcher started in single-shot mode (will exit after one cycle).");
            }
            else
            {
                _logger.LogInformation("Secure Boot watcher started in continuous mode.");
            }

            do
            {
                try
                {
                    // === PHASE 1: PROCESS COMMANDS FIRST (if enabled) ===
                    if (options.Commands.EnableCommandProcessing && 
                        options.Commands.ProcessBeforeInventory && 
                        _commandProcessor != null)
                    {
                        await ProcessCommandsAsync(cancellationToken).ConfigureAwait(false);
                    }

                    // === PHASE 2: BUILD AND SEND INVENTORY REPORT ===
                    var report = await _reportBuilder.BuildAsync(cancellationToken).ConfigureAwait(false);
                    if (!ReportValidator.TryValidate(report, out var errors))
                    {
                        _logger.LogWarning("Secure Boot report validation failed: {Errors}", string.Join("; ", errors));
                    }
                    else
                    {
                        await _reportSink.EmitAsync(report, cancellationToken).ConfigureAwait(false);
                    }

                    // === PHASE 3: PROCESS COMMANDS AFTER (if configured) ===
                    if (options.Commands.EnableCommandProcessing && 
                        !options.Commands.ProcessBeforeInventory && 
                        _commandProcessor != null)
                    {
                        await ProcessCommandsAsync(cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error while executing Secure Boot watcher cycle.");
                }

                // Exit loop if running in single-shot mode
                if (runOnce)
                {
                    break;
                }

                var delay = CalculateDelay();
                _logger.LogDebug("Secure Boot watcher sleeping for {Delay}.", delay);

                try
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
            while (!cancellationToken.IsCancellationRequested);

            // After main process completes, schedule upgrade if needed
            if (updateService != null && updateCheck?.UpdateAvailable == true && autoInstallEnabled && !string.IsNullOrWhiteSpace(updateCheck.DownloadUrl))
            {
                try
                {
                    _logger.LogInformation("Scheduling client upgrade after main process completes...");
                    var downloadResult = await updateService.DownloadUpdateAsync(updateCheck.DownloadUrl!, cancellationToken);
                    if (downloadResult.Success && !string.IsNullOrWhiteSpace(downloadResult.LocalPath))
                    {
                        var scheduled = await updateService.ScheduleUpdateAsync(downloadResult.LocalPath!, cancellationToken);
                        if (scheduled)
                        {
                            _logger.LogInformation("Client upgrade scheduled successfully.");
                        }
                        else
                        {
                            _logger.LogWarning("Failed to schedule client upgrade.");
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Failed to download update package for upgrade: {Error}", downloadResult.ErrorMessage);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error scheduling client upgrade after main process.");
                }
            }

            _logger.LogInformation("Secure Boot watcher stopped.");
        }

        private TimeSpan CalculateDelay()
        {
            var options = _options.CurrentValue;
            var interval = options.RegistryPollInterval;
            if (options.EventQueryInterval < interval)
            {
                interval = options.EventQueryInterval;
            }

            if (interval <= TimeSpan.Zero)
            {
                interval = TimeSpan.FromMinutes(30);
            }

            return interval;
        }

        private async Task ProcessCommandsAsync(CancellationToken cancellationToken)
        {
            if (_commandProcessor == null)
            {
                return;
            }

            var options = _options.CurrentValue.Commands;

            try
            {
                _logger.LogInformation("========================================");
                _logger.LogInformation("PHASE: Command Processing");
                _logger.LogInformation("========================================");

                // Step 1: Fetch pending commands
                var commands = await _commandProcessor.FetchPendingCommandsAsync(cancellationToken).ConfigureAwait(false);

                if (commands.Count == 0)
                {
                    _logger.LogInformation("No pending commands to process");
                    return;
                }

                _logger.LogInformation("Fetched {Count} pending command(s)", commands.Count);

                // Limit commands per cycle
                var commandsToProcess = commands.Count > options.MaxCommandsPerCycle
                    ? commands.Take(options.MaxCommandsPerCycle).ToList()
                    : commands.ToList();

                if (commandsToProcess.Count < commands.Count)
                {
                    _logger.LogWarning(
                        "Limiting command processing to {Max} commands (total pending: {Total})",
                        options.MaxCommandsPerCycle,
                        commands.Count);
                }

                // Step 2: Execute each command
                foreach (var command in commandsToProcess)
                {
                    try
                    {
                        _logger.LogInformation(
                            "Processing command {CommandId} of type {Type}",
                            command.CommandId,
                            command.ConfigurationType);

                        // Execute command
                        var result = await _commandProcessor.ExecuteCommandAsync(command, cancellationToken).ConfigureAwait(false);

                        // Verify result locally
                        if (result.Success)
                        {
                            _logger.LogInformation(
                                "Command {CommandId} executed successfully: {Message}",
                                command.CommandId,
                                result.Message);

                            var currentState = await _commandProcessor.VerifyCommandResultAsync(command, cancellationToken).ConfigureAwait(false);
                            result.CurrentState = currentState;

                            _logger.LogDebug(
                                "Command {CommandId} verification complete. State: MicrosoftUpdateManagedOptIn={OptIn}, AllowTelemetry={Telemetry}, CA2023Capable={Capable}",
                                command.CommandId,
                                currentState.MicrosoftUpdateManagedOptIn,
                                currentState.AllowTelemetry,
                                currentState.WindowsUEFICA2023Capable);
                        }
                        else
                        {
                            _logger.LogWarning(
                                "Command {CommandId} execution failed: {Message}",
                                command.CommandId,
                                result.Message);
                        }

                        // Step 3: Report result back to API
                        var reported = await _commandProcessor.ReportResultAsync(result, cancellationToken).ConfigureAwait(false);

                        if (reported)
                        {
                            _logger.LogInformation("Command {CommandId} result reported to API", command.CommandId);
                        }
                        else
                        {
                            _logger.LogWarning("Failed to report command {CommandId} result to API", command.CommandId);
                        }

                        // Delay between commands to allow registry propagation
                        if (options.CommandExecutionDelay > TimeSpan.Zero)
                        {
                            await Task.Delay(options.CommandExecutionDelay, cancellationToken).ConfigureAwait(false);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to process command {CommandId}", command.CommandId);

                        // Continue processing other commands unless configured otherwise
                        if (!options.ContinueOnCommandFailure)
                        {
                            throw;
                        }
                    }
                }

                _logger.LogInformation("========================================");
                _logger.LogInformation("Command processing phase complete");
                _logger.LogInformation("========================================");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Command processing phase failed");

                // Re-throw if configured to stop on failure
                if (!options.ContinueOnCommandFailure)
                {
                    throw;
                }
            }
        }
    }
}
