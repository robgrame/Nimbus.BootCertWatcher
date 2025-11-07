using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SecureBootWatcher.LinuxClient.Sinks;
using SecureBootWatcher.Shared.Configuration;
using SecureBootWatcher.Shared.Models;
using SecureBootWatcher.Shared.Validation;

namespace SecureBootWatcher.LinuxClient.Services
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
            _logger.LogInformation("Secure Boot watcher started.");

            while (!cancellationToken.IsCancellationRequested)
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
