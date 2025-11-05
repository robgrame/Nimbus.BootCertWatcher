using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SecureBootWatcher.Shared.Configuration;
using SecureBootWatcher.Shared.Models;

namespace SecureBootWatcher.Client.Sinks
{
    /// <summary>
    /// Coordinates execution of multiple report sinks with priority and failover support.
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

  // Get ordered sinks based on priority and enabled status
 var orderedSinks = GetOrderedSinks(sinkOptions);

     if (orderedSinks.Count == 0)
            {
        _logger.LogWarning("No sinks are enabled. Report will not be sent.");
       return;
            }

            _logger.LogInformation("Sending report using strategy: {Strategy}. Enabled sinks: {EnabledSinks}",
                executionStrategy,
 string.Join(", ", orderedSinks.Select(s => s.GetType().Name)));

       var results = new List<SinkResult>();

     foreach (var sink in orderedSinks)
       {
       var sinkName = sink.GetType().Name.Replace("ReportSink", "");
     
                try
                {
  _logger.LogDebug("Attempting to send report to {SinkName}...", sinkName);
   
         await sink.EmitAsync(report, cancellationToken).ConfigureAwait(false);
      
         _logger.LogInformation("? Successfully sent report to {SinkName}", sinkName);
           results.Add(new SinkResult(sinkName, true, null));

      // Stop on first success if strategy is set
               if (executionStrategy.Equals("StopOnFirstSuccess", StringComparison.OrdinalIgnoreCase))
              {
          _logger.LogInformation("StopOnFirstSuccess strategy: stopping after first successful sink.");
         break;
          }
     }
       catch (Exception ex)
  {
   _logger.LogWarning(ex, "? Failed to send report to {SinkName}: {ErrorMessage}", sinkName, ex.Message);
        results.Add(new SinkResult(sinkName, false, ex.Message));
        }
   }

        // Log summary
         var successCount = results.Count(r => r.Success);
       var failureCount = results.Count(r => !r.Success);

            if (successCount > 0)
{
  _logger.LogInformation("Report delivery summary: {SuccessCount} succeeded, {FailureCount} failed.",
       successCount, failureCount);
            }
   else
            {
         _logger.LogError("Report delivery failed: All {TotalCount} enabled sink(s) failed.", failureCount);
          throw new AggregateException(
   "All enabled sinks failed to send the report",
          results.Where(r => !r.Success).Select(r => new Exception($"{r.SinkName}: {r.ErrorMessage}")));
     }
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
            public SinkResult(string sinkName, bool success, string errorMessage)
 {
        SinkName = sinkName;
        Success = success;
       ErrorMessage = errorMessage;
     }

      public string SinkName { get; }
     public bool Success { get; }
       public string ErrorMessage { get; }
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
