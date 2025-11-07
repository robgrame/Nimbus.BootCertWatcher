using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SecureBootWatcher.Shared.Configuration;
using SecureBootWatcher.Shared.Models;

namespace SecureBootWatcher.LinuxClient.Sinks
{
    /// <summary>
    /// Coordinates execution of multiple report sinks with priority, retry, and failover support.
    /// </summary>
    internal sealed class SinkCoordinator : IReportSink
    {
        private readonly ILogger<SinkCoordinator> _logger;
        private readonly IOptionsMonitor<SecureBootWatcherOptions> _options;
        private readonly IEnumerable<IReportSink> _sinks;

        public SinkCoordinator(
            ILogger<SinkCoordinator> logger,
            IOptionsMonitor<SecureBootWatcherOptions> options,
            IEnumerable<IReportSink> sinks)
        {
            _logger = logger;
            _options = options;
            _sinks = sinks;
        }

        public async Task EmitAsync(SecureBootStatusReport report, CancellationToken cancellationToken)
        {
            var sinkOptions = _options.CurrentValue.Sinks;
            var executionStrategy = sinkOptions.ExecutionStrategy ?? "StopOnFirstSuccess";
            var maxRetries = sinkOptions.MaxRetryAttempts;
            var retryDelay = sinkOptions.RetryDelay;
            var useExponentialBackoff = sinkOptions.UseExponentialBackoff;

            // Get ordered sinks based on priority and enabled status
            var orderedSinks = GetOrderedSinks(sinkOptions);

            if (orderedSinks.Count == 0)
            {
                _logger.LogWarning("No sinks are enabled. Report will not be sent.");
                return;
            }

            _logger.LogInformation(
                "Sending report using strategy: {Strategy}. Enabled sinks: {EnabledSinks}. Retry config: {MaxRetries} attempts, {RetryDelay} delay{Backoff}",
                executionStrategy,
                string.Join(", ", orderedSinks.Select(s => s.GetType().Name)),
                maxRetries,
                retryDelay,
                useExponentialBackoff ? " (exponential backoff)" : "");

            var results = new List<SinkResult>();

            foreach (var sink in orderedSinks)
            {
                var sinkName = sink.GetType().Name.Replace("ReportSink", "");
                var attemptNumber = 0;
                var succeeded = false;
                Exception lastException = null;

                // Retry loop for current sink
                while (attemptNumber <= maxRetries && !succeeded && !cancellationToken.IsCancellationRequested)
                {
                    attemptNumber++;

                    try
                    {
                        if (attemptNumber == 1)
                        {
                            _logger.LogDebug("Attempting to send report to {SinkName}...", sinkName);
                        }
                        else
                        {
                            _logger.LogInformation(
                                "Retry attempt {Attempt}/{MaxRetries} for {SinkName} after {Delay}...",
                                attemptNumber,
                                maxRetries + 1,
                                sinkName,
                                GetCurrentDelay(attemptNumber - 1, retryDelay, useExponentialBackoff));
                        }

                        await sink.EmitAsync(report, cancellationToken).ConfigureAwait(false);

                        _logger.LogInformation("? Successfully sent report to {SinkName}{AttemptInfo}",
                            sinkName,
                            attemptNumber > 1 ? $" (after {attemptNumber} attempts)" : "");

                        results.Add(new SinkResult(sinkName, true, null, attemptNumber));
                        succeeded = true;

                        // Stop on first success if strategy is set
                        if (executionStrategy.Equals("StopOnFirstSuccess", StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogInformation("StopOnFirstSuccess strategy: stopping after first successful sink.");
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;

                        if (attemptNumber <= maxRetries)
                        {
                            // Calculate delay for next retry
                            var delay = GetCurrentDelay(attemptNumber, retryDelay, useExponentialBackoff);

                            _logger.LogWarning(
                                "? Attempt {Attempt}/{MaxRetries} failed for {SinkName}: {ErrorMessage}. Retrying in {Delay}...",
                                attemptNumber,
                                maxRetries + 1,
                                sinkName,
                                ex.Message,
                                delay);

                            // Wait before retry (unless it's the last attempt)
                            if (attemptNumber <= maxRetries)
                            {
                                try
                                {
                                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                                }
                                catch (OperationCanceledException)
                                {
                                    _logger.LogInformation("Retry cancelled for {SinkName}", sinkName);
                                    break;
                                }
                            }
                        }
                        else
                        {
                            // Log error without full stack trace since we'll try next sink
                            _logger.LogWarning(
                                "? All {TotalAttempts} attempts failed for {SinkName}: {ErrorMessage}. Moving to next sink.",
                                attemptNumber,
                                sinkName,
                                lastException?.Message ?? "Unknown error");
                        }
                    }
                }

                // Record failure if all retries exhausted
                if (!succeeded)
                {
                    results.Add(new SinkResult(sinkName, false, lastException?.Message ?? "Unknown error", attemptNumber));
                }

                // If succeeded with StopOnFirstSuccess, break out of sink loop
                if (succeeded && executionStrategy.Equals("StopOnFirstSuccess", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
            }

            // Log summary
            var successCount = results.Count(r => r.Success);
            var failureCount = results.Count(r => !r.Success);
            var totalAttempts = results.Sum(r => r.Attempts);

            if (successCount > 0)
            {
                _logger.LogInformation(
                    "Report delivery summary: {SuccessCount} succeeded, {FailureCount} failed (total attempts: {TotalAttempts}).",
                    successCount,
                    failureCount,
                    totalAttempts);
            }
            else
            {
                _logger.LogError(
                    "Report delivery failed: All {TotalCount} enabled sink(s) failed after {TotalAttempts} attempts.",
                    failureCount,
                    totalAttempts);

                throw new AggregateException(
                    "All enabled sinks failed to send the report",
                    results.Where(r => !r.Success).Select(r => new Exception($"{r.SinkName}: {r.ErrorMessage}")));
            }
        }

        private TimeSpan GetCurrentDelay(int attemptNumber, TimeSpan baseDelay, bool useExponentialBackoff)
        {
            if (!useExponentialBackoff || attemptNumber == 0)
            {
                return baseDelay;
            }

            // Exponential backoff: delay * 2^(attemptNumber - 1)
            // Attempt 1: baseDelay
            // Attempt 2: baseDelay * 2
            // Attempt 3: baseDelay * 4
            // etc.
            var multiplier = Math.Pow(2, attemptNumber - 1);
            var delayMs = baseDelay.TotalMilliseconds * multiplier;

            // Cap at 30 minutes to prevent extremely long delays
            var maxDelayMs = TimeSpan.FromMinutes(30).TotalMilliseconds;
            delayMs = Math.Min(delayMs, maxDelayMs);

            return TimeSpan.FromMilliseconds(delayMs);
        }

        private List<IReportSink> GetOrderedSinks(SinkOptions sinkOptions)
        {
            // Parse priority string
            var priorityOrder = (sinkOptions.SinkPriority ?? "AzureQueue,WebApi,FileShare")
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .ToList();

            // Map sink names to actual sink instances and filter by enabled status
            var sinkMap = new Dictionary<string, SinkInfo>
            {
                ["AzureQueue"] = new SinkInfo(_sinks.OfType<AzureQueueReportSink>().FirstOrDefault(), sinkOptions.EnableAzureQueue),
                ["WebApi"] = new SinkInfo(_sinks.OfType<WebApiReportSink>().FirstOrDefault(), sinkOptions.EnableWebApi),
                ["FileShare"] = new SinkInfo(_sinks.OfType<FileShareReportSink>().FirstOrDefault(), sinkOptions.EnableFileShare)
            };

            var orderedSinks = new List<IReportSink>();

            // Add sinks in priority order (only if enabled)
            foreach (var sinkName in priorityOrder)
            {
                if (sinkMap.TryGetValue(sinkName, out var sinkInfo) && sinkInfo.Enabled && sinkInfo.Sink != null)
                {
                    orderedSinks.Add(sinkInfo.Sink);
                    _logger.LogDebug("Added sink to execution queue: {SinkName} (priority: {Priority})",
                        sinkName, orderedSinks.Count);
                }
            }

            // Add any enabled sinks not in priority list (fallback)
            foreach (var kvp in sinkMap)
            {
                if (kvp.Value.Enabled && kvp.Value.Sink != null && !orderedSinks.Contains(kvp.Value.Sink))
                {
                    orderedSinks.Add(kvp.Value.Sink);
                    _logger.LogDebug("Added sink to execution queue (not in priority list): {SinkName}", kvp.Key);
                }
            }

            return orderedSinks;
        }

        private sealed class SinkResult
        {
            public SinkResult(string sinkName, bool success, string errorMessage, int attempts = 1)
            {
                SinkName = sinkName;
                Success = success;
                ErrorMessage = errorMessage;
                Attempts = attempts;
            }

            public string SinkName { get; }
            public bool Success { get; }
            public string ErrorMessage { get; }
            public int Attempts { get; }
        }

        private sealed class SinkInfo
        {
            public SinkInfo(IReportSink sink, bool enabled)
            {
                Sink = sink;
                Enabled = enabled;
            }

            public IReportSink Sink { get; }
            public bool Enabled { get; }
        }
    }
}
