using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Identity;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using SecureBootWatcher.Shared.Configuration;
using SecureBootWatcher.Shared.Models;
using SecureBootWatcher.Shared.Transport;

namespace SecureBootWatcher.Client.Sinks
{
    internal sealed class AzureQueueReportSink : IReportSink
    {
        private readonly ILogger<AzureQueueReportSink> _logger;
        private readonly IOptionsMonitor<SecureBootWatcherOptions> _options;
        private readonly AsyncRetryPolicy _retryPolicy;

        public AzureQueueReportSink(ILogger<AzureQueueReportSink> logger, IOptionsMonitor<SecureBootWatcherOptions> options)
        {
            _logger = logger;
            _options = options;
            _retryPolicy = Policy
                .Handle<RequestFailedException>()
                .Or<TimeoutException>()
                .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)), (ex, span, attempt, _) =>
                {
                    _logger.LogWarning(ex, "Retrying Azure Queue send attempt {Attempt} after {Delay}.", attempt, span);
                });
        }

        public async Task EmitAsync(SecureBootStatusReport report, CancellationToken cancellationToken)
        {
            var sinkOptions = _options.CurrentValue.Sinks.AzureQueue;
            if (string.IsNullOrWhiteSpace(sinkOptions.QueueName))
            {
                _logger.LogDebug("Azure Queue sink is disabled because QueueName is not configured.");
                return;
            }

            var queueClient = CreateQueueClient(sinkOptions);
            if (queueClient == null)
            {
                _logger.LogWarning("Azure Queue sink skipped because QueueEndpoint or ConnectionString is not configured.");
                return;
            }

            await queueClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

            var envelope = new SecureBootQueueEnvelope
            {
                Report = report,
                EnqueuedAtUtc = DateTimeOffset.UtcNow
            };

            var payload = JsonSerializer.Serialize(envelope, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });

            await _retryPolicy.ExecuteAsync(async token =>
            {
                await queueClient.SendMessageAsync(
                    BinaryData.FromString(payload),
                    visibilityTimeout: sinkOptions.VisibilityTimeout,
                    cancellationToken: token).ConfigureAwait(false);

                _logger.LogInformation("Secure Boot report enqueued to {QueueName}.", queueClient.Name);
            }, cancellationToken).ConfigureAwait(false);
        }

        private QueueClient? CreateQueueClient(AzureQueueSinkOptions options)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(options.ConnectionString))
                {
                    return new QueueClient(options.ConnectionString, options.QueueName);
                }

                if (!string.IsNullOrWhiteSpace(options.QueueEndpoint))
                {
                    var uri = new Uri(new Uri(options.QueueEndpoint, UriKind.Absolute), options.QueueName);
                    return new QueueClient(uri, new DefaultAzureCredential());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create Azure Queue client for endpoint {Endpoint}.", options.QueueEndpoint);
            }

            return null;
        }
    }
}
