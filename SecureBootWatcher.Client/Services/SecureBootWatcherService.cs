using System;
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
        private readonly IOptionsMonitor<SecureBootWatcherOptions> _options;

        public SecureBootWatcherService(
            ILogger<SecureBootWatcherService> logger,
            IReportBuilder reportBuilder,
            IReportSink reportSink,
            IOptionsMonitor<SecureBootWatcherOptions> options)
        {
            _logger = logger;
            _reportBuilder = reportBuilder;
            _reportSink = reportSink;
            _options = options;
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
                    var report = await _reportBuilder.BuildAsync(cancellationToken).ConfigureAwait(false);
                    if (!ReportValidator.TryValidate(report, out var errors))
                    {
                        _logger.LogWarning("Secure Boot report validation failed: {Errors}", string.Join("; ", errors));
                    }
                    else
                    {
                        await _reportSink.EmitAsync(report, cancellationToken).ConfigureAwait(false);
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
    }
}
