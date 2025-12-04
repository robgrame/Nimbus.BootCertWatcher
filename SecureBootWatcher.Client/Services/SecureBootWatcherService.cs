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
            _logger.LogTrace("RunAsync: Entering method");
            var options = _options.CurrentValue;
            var runOnce = options.RunMode.Equals("Once", StringComparison.OrdinalIgnoreCase);
            _logger.LogDebug("RunAsync: RunMode={RunMode}, RunOnce={RunOnce}", options.RunMode, runOnce);

            IClientUpdateService? updateService = null;
            UpdateCheckResult? updateCheck = null;
            bool autoInstallEnabled = false;

            // Try to resolve update service from report builder if available
            _logger.LogTrace("RunAsync: Attempting to resolve IClientUpdateService from ReportBuilder");
            if (_reportBuilder is ReportBuilder rb)
            {
                var field = typeof(ReportBuilder).GetField("_updateService", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                updateService = field?.GetValue(rb) as IClientUpdateService;
                _logger.LogDebug("RunAsync: IClientUpdateService resolved={Resolved}", updateService != null);
            }

            if (updateService != null && options.ClientUpdate.CheckForUpdates)
            {
                _logger.LogDebug("RunAsync: Checking for client updates (CheckForUpdates={Enabled})", options.ClientUpdate.CheckForUpdates);
                try
                {
                    updateCheck = await updateService.CheckForUpdateAsync(cancellationToken).ConfigureAwait(false);
                    _logger.LogDebug("RunAsync: Update check completed - UpdateAvailable={Available}, CurrentVersion={Current}, LatestVersion={Latest}", 
                        updateCheck?.UpdateAvailable, updateCheck?.CurrentVersion, updateCheck?.LatestVersion);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "RunAsync: Failed to check for client updates at startup");
                }
            }
            else
            {
                _logger.LogDebug("RunAsync: Skipping update check - UpdateService={Service}, CheckForUpdates={Check}", 
                    updateService != null, options.ClientUpdate.CheckForUpdates);
            }
            autoInstallEnabled = options.ClientUpdate.AutoInstallEnabled;
            _logger.LogDebug("RunAsync: AutoInstallEnabled={AutoInstall}", autoInstallEnabled);

            if (runOnce)
            {
                _logger.LogInformation("RunAsync: Secure Boot watcher started in single-shot mode (will exit after one cycle).");
            }
            else
            {
                _logger.LogInformation("RunAsync: Secure Boot watcher started in continuous mode.");
            }

            do
            {
                _logger.LogTrace("RunAsync: Starting new execution cycle");
                try
                {
                    // === PHASE 1: PROCESS COMMANDS FIRST (if enabled) ===
                    if (options.Commands.EnableCommandProcessing && 
                        options.Commands.ProcessBeforeInventory && 
                        _commandProcessor != null)
                    {
                        _logger.LogDebug("RunAsync: Processing commands BEFORE inventory (ProcessBeforeInventory=true)");
                        await ProcessCommandsAsync(cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        _logger.LogTrace("RunAsync: Skipping pre-inventory command processing - Enabled={Enabled}, ProcessBefore={Before}, Processor={Processor}", 
                            options.Commands.EnableCommandProcessing, options.Commands.ProcessBeforeInventory, _commandProcessor != null);
                    }

                    // === PHASE 2: BUILD AND SEND INVENTORY REPORT ===
                    _logger.LogDebug("RunAsync: Building inventory report");
                    var report = await _reportBuilder.BuildAsync(cancellationToken).ConfigureAwait(false);
                    _logger.LogTrace("RunAsync: Report built - MachineName={MachineName}, CreatedAt={CreatedAt}, CorrelationId={CorrelationId}", 
                        report.Device?.MachineName, report.CreatedAtUtc, report.CorrelationId);
                    
                    if (!ReportValidator.TryValidate(report, out var errors))
                    {
                        _logger.LogWarning("RunAsync: Secure Boot report validation failed: {Errors}", string.Join("; ", errors));
                    }
                    else
                    {
                        _logger.LogDebug("RunAsync: Report validated successfully, emitting to sink");
                        await _reportSink.EmitAsync(report, cancellationToken).ConfigureAwait(false);
                        _logger.LogTrace("RunAsync: Report emitted successfully");
                    }

                    // === PHASE 3: PROCESS COMMANDS AFTER (if configured) ===
                    if (options.Commands.EnableCommandProcessing && 
                        !options.Commands.ProcessBeforeInventory && 
                        _commandProcessor != null)
                    {
                        _logger.LogDebug("RunAsync: Processing commands AFTER inventory (ProcessBeforeInventory=false)");
                        await ProcessCommandsAsync(cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        _logger.LogTrace("RunAsync: Skipping post-inventory command processing");
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogDebug("RunAsync: Cycle cancelled by cancellation token");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "RunAsync: Unexpected error while executing Secure Boot watcher cycle.");
                }

                // Exit loop if running in single-shot mode
                if (runOnce)
                {
                    _logger.LogDebug("RunAsync: Exiting loop (single-shot mode)");
                    break;
                }

                var delay = CalculateDelay();
                _logger.LogDebug("RunAsync: Secure Boot watcher sleeping for {Delay}.", delay);

                try
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogDebug("RunAsync: Sleep cancelled by cancellation token");
                    break;
                }
            }
            while (!cancellationToken.IsCancellationRequested);

            // After main process completes, schedule upgrade if needed
            if (updateService != null && updateCheck?.UpdateAvailable == true && autoInstallEnabled && !string.IsNullOrWhiteSpace(updateCheck.DownloadUrl))
            {
                _logger.LogDebug("RunAsync: Update available and auto-install enabled, scheduling upgrade");
                try
                {
                    _logger.LogInformation("RunAsync: Scheduling client upgrade after main process completes...");
                    var downloadResult = await updateService.DownloadUpdateAsync(updateCheck.DownloadUrl!, cancellationToken);
                    _logger.LogDebug("RunAsync: Download completed - Success={Success}, LocalPath={Path}", 
                        downloadResult.Success, downloadResult.LocalPath);
                    
                    if (downloadResult.Success && !string.IsNullOrWhiteSpace(downloadResult.LocalPath))
                    {
                        var scheduled = await updateService.ScheduleUpdateAsync(downloadResult.LocalPath!, cancellationToken);
                        if (scheduled)
                        {
                            _logger.LogInformation("RunAsync: Client upgrade scheduled successfully.");
                        }
                        else
                        {
                            _logger.LogWarning("RunAsync: Failed to schedule client upgrade.");
                        }
                    }
                    else
                    {
                        _logger.LogWarning("RunAsync: Failed to download update package for upgrade: {Error}", downloadResult.ErrorMessage);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "RunAsync: Error scheduling client upgrade after main process.");
                }
            }
            else
            {
                _logger.LogTrace("RunAsync: Skipping upgrade - UpdateAvailable={Available}, AutoInstall={Auto}, DownloadUrl={Url}", 
                    updateCheck?.UpdateAvailable, autoInstallEnabled, !string.IsNullOrWhiteSpace(updateCheck?.DownloadUrl));
            }

            _logger.LogInformation("RunAsync: Secure Boot watcher stopped.");
            _logger.LogTrace("RunAsync: Exiting method");
        }

        private TimeSpan CalculateDelay()
        {
            _logger.LogTrace("CalculateDelay: Calculating next polling interval");
            var options = _options.CurrentValue;
            var interval = options.RegistryPollInterval;
            _logger.LogDebug("CalculateDelay: RegistryPollInterval={Registry}", interval);
            
            if (options.EventQueryInterval < interval)
            {
                _logger.LogDebug("CalculateDelay: Using EventQueryInterval={Event} (shorter than registry interval)", 
                    options.EventQueryInterval);
                interval = options.EventQueryInterval;
            }

            if (interval <= TimeSpan.Zero)
            {
                _logger.LogWarning("CalculateDelay: Configured interval is invalid ({Interval}), using default 30 minutes", 
                    interval);
                interval = TimeSpan.FromMinutes(30);
            }

            _logger.LogDebug("CalculateDelay: Calculated delay={Delay}", interval);
            return interval;
        }

        private async Task ProcessCommandsAsync(CancellationToken cancellationToken)
        {
            _logger.LogTrace("ProcessCommandsAsync: Entering method");
            if (_commandProcessor == null)
            {
                _logger.LogDebug("ProcessCommandsAsync: Command processor is null, exiting");
                return;
            }

            var options = _options.CurrentValue.Commands;
            _logger.LogDebug("ProcessCommandsAsync: Configuration - MaxPerCycle={Max}, ExecutionDelay={Delay}, ContinueOnFailure={Continue}", 
                options.MaxCommandsPerCycle, options.CommandExecutionDelay, options.ContinueOnCommandFailure);

            try
            {
                _logger.LogInformation("========================================");
                _logger.LogInformation("PHASE: Command Processing");
                _logger.LogInformation("========================================");

                // Step 1: Fetch pending commands
                _logger.LogTrace("ProcessCommandsAsync: Fetching pending commands");
                var commands = await _commandProcessor.FetchPendingCommandsAsync(cancellationToken).ConfigureAwait(false);

                if (commands.Count == 0)
                {
                    _logger.LogInformation("ProcessCommandsAsync: No pending commands to process");
                    return;
                }

                _logger.LogInformation("ProcessCommandsAsync: Fetched {Count} pending command(s)", commands.Count);
                _logger.LogDebug("ProcessCommandsAsync: Command IDs: {CommandIds}", 
                    string.Join(", ", commands.Select(c => c.CommandId)));

                // Limit commands per cycle
                var commandsToProcess = commands.Count > options.MaxCommandsPerCycle
                    ? commands.Take(options.MaxCommandsPerCycle).ToList()
                    : commands.ToList();

                if (commandsToProcess.Count < commands.Count)
                {
                    _logger.LogWarning(
                        "ProcessCommandsAsync: Limiting command processing to {Max} commands (total pending: {Total})",
                        options.MaxCommandsPerCycle,
                        commands.Count);
                }

                // Step 2: Execute each command
                int successCount = 0;
                int failureCount = 0;
                _logger.LogTrace("ProcessCommandsAsync: Beginning command execution loop");
                
                foreach (var command in commandsToProcess)
                {
                    try
                    {
                        _logger.LogInformation(
                            "ProcessCommandsAsync: Processing command {CommandId} of type {Type}",
                            command.CommandId,
                            command.ConfigurationType);
                        _logger.LogDebug("ProcessCommandsAsync: Command details - CreatedAt={Created}, Description={Desc}", 
                            command.CreatedAtUtc, command.Description);

                        // Execute command
                        _logger.LogTrace("ProcessCommandsAsync: Executing command {CommandId}", command.CommandId);
                        var result = await _commandProcessor.ExecuteCommandAsync(command, cancellationToken).ConfigureAwait(false);

                        // Verify result locally
                        if (result.Success)
                        {
                            _logger.LogInformation(
                                "ProcessCommandsAsync: Command {CommandId} executed successfully: {Message}",
                                command.CommandId,
                                result.Message);

                            _logger.LogTrace("ProcessCommandsAsync: Verifying command result for {CommandId}", command.CommandId);
                            var currentState = await _commandProcessor.VerifyCommandResultAsync(command, cancellationToken).ConfigureAwait(false);
                            result.CurrentState = currentState;

                            _logger.LogDebug(
                                "ProcessCommandsAsync: Command {CommandId} verification complete. State: MicrosoftUpdateManagedOptIn={OptIn}, AllowTelemetry={Telemetry}, CA2023Capable={Capable}",
                                command.CommandId,
                                currentState.MicrosoftUpdateManagedOptIn,
                                currentState.AllowTelemetry,
                                currentState.WindowsUEFICA2023Capable);
                            
                            successCount++;
                        }
                        else
                        {
                            _logger.LogWarning(
                                "ProcessCommandsAsync: Command {CommandId} execution failed: {Message}",
                                command.CommandId,
                                result.Message);
                            failureCount++;
                        }

                        // Step 3: Report result back to API
                        _logger.LogTrace("ProcessCommandsAsync: Reporting result for command {CommandId}", command.CommandId);
                        var reported = await _commandProcessor.ReportResultAsync(result, cancellationToken).ConfigureAwait(false);

                        if (reported)
                        {
                            _logger.LogInformation("ProcessCommandsAsync: Command {CommandId} result reported to API", command.CommandId);
                        }
                        else
                        {
                            _logger.LogWarning("ProcessCommandsAsync: Failed to report command {CommandId} result to API", command.CommandId);
                        }

                        // Delay between commands to allow registry propagation
                        if (options.CommandExecutionDelay > TimeSpan.Zero)
                        {
                            _logger.LogTrace("ProcessCommandsAsync: Delaying {Delay} before next command", 
                                options.CommandExecutionDelay);
                            await Task.Delay(options.CommandExecutionDelay, cancellationToken).ConfigureAwait(false);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "ProcessCommandsAsync: Failed to process command {CommandId}", command.CommandId);
                        failureCount++;

                        // Continue processing other commands unless configured otherwise
                        if (!options.ContinueOnCommandFailure)
                        {
                            _logger.LogWarning("ProcessCommandsAsync: ContinueOnCommandFailure=false, aborting command processing");
                            throw;
                        }
                        else
                        {
                            _logger.LogDebug("ProcessCommandsAsync: ContinueOnCommandFailure=true, continuing to next command");
                        }
                    }
                }

                _logger.LogInformation("========================================");
                _logger.LogInformation("ProcessCommandsAsync: Command processing phase complete - Success={Success}, Failures={Failures}", 
                    successCount, failureCount);
                _logger.LogInformation("========================================");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ProcessCommandsAsync: Command processing phase failed");

                // Re-throw if configured to stop on failure
                if (!options.ContinueOnCommandFailure)
                {
                    _logger.LogWarning("ProcessCommandsAsync: ContinueOnCommandFailure=false, re-throwing exception");
                    throw;
                }
                else
                {
                    _logger.LogDebug("ProcessCommandsAsync: ContinueOnCommandFailure=true, swallowing exception");
                }
            }
            
            _logger.LogTrace("ProcessCommandsAsync: Exiting method");
        }
    }
}
