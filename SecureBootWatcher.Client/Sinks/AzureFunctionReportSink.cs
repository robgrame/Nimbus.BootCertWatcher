using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SecureBootWatcher.Shared.Configuration;
using SecureBootWatcher.Shared.Models;

namespace SecureBootWatcher.Client.Sinks
{
    /// <summary>
    /// Report sink that sends reports to an Azure Function endpoint using API key authentication
    /// and optionally client certificate authentication (mutual TLS).
    /// This simplifies client deployment by eliminating the need to distribute Azure Queue certificates.
    /// </summary>
    internal sealed class AzureFunctionReportSink : IReportSink
    {
        private readonly ILogger<AzureFunctionReportSink> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOptionsMonitor<SecureBootWatcherOptions> _options;

        public AzureFunctionReportSink(
            ILogger<AzureFunctionReportSink> logger,
            IHttpClientFactory httpClientFactory,
            IOptionsMonitor<SecureBootWatcherOptions> options)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _options = options;
        }

        public async Task EmitAsync(SecureBootStatusReport report, CancellationToken cancellationToken)
        {
            var sinkOptions = _options.CurrentValue.Sinks.AzureFunction;
            if (sinkOptions.FunctionUrl == null)
            {
                _logger.LogDebug("AzureFunctionReportSink: Skipped - FunctionUrl is not configured");
                return;
            }

            if (string.IsNullOrWhiteSpace(sinkOptions.ApiKey))
            {
                _logger.LogWarning("AzureFunctionReportSink: Skipped - ApiKey is not configured");
                return;
            }

            _logger.LogDebug("AzureFunctionReportSink: Preparing to submit report for device {MachineName} to {FunctionUrl}",
                report.Device?.MachineName ?? "Unknown", sinkOptions.FunctionUrl);
            _logger.LogTrace("AzureFunctionReportSink: Report details - CorrelationId={CorrelationId}, ClientVersion={ClientVersion}, EventCount={EventCount}",
                report.CorrelationId ?? "None", report.ClientVersion ?? "Unknown", report.Events?.Count ?? 0);

            // Note: Client certificate authentication (if enabled) is handled by HttpClientHandler
            // configuration in Program.cs during service registration
            var client = _httpClientFactory.CreateClient("SecureBootIngestion");
            client.Timeout = sinkOptions.HttpTimeout;

            // Pre-flight DNS check to avoid repeated failures when host is not resolvable
            try
            {
                var host = sinkOptions.FunctionUrl.Host;
                var dnsTask = Dns.GetHostAddressesAsync(host);
                var completed = await Task.WhenAny(dnsTask, Task.Delay(TimeSpan.FromSeconds(5), cancellationToken)).ConfigureAwait(false);
                if (completed != dnsTask)
                {
                    throw new TaskCanceledException("DNS resolution timeout");
                }
                _ = await dnsTask.ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is SocketException || ex is TaskCanceledException || ex is HttpRequestException)
            {
                throw new HttpRequestException($"DNS resolution failed for {sinkOptions.FunctionUrl.Host}", ex);
            }

            _logger.LogTrace("AzureFunctionReportSink: HTTP client configured - Timeout={Timeout}", sinkOptions.HttpTimeout);

            // Build request URL (with or without API key as query parameter)
            var requestUrl = sinkOptions.FunctionUrl.ToString();
            if (sinkOptions.UseApiKeyAsQueryParameter)
            {
                var separator = requestUrl.Contains("?") ? "&" : "?";
                requestUrl = $"{requestUrl}{separator}code={Uri.EscapeDataString(sinkOptions.ApiKey)}";
                _logger.LogDebug("AzureFunctionReportSink: API key will be sent as query parameter");
            }
            else
            {
                _logger.LogDebug("AzureFunctionReportSink: API key will be sent as X-API-Key header");
            }

            // Create request
            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            request.Content = JsonContent.Create(report);

            // Add API key as header if not using query parameter
            if (!sinkOptions.UseApiKeyAsQueryParameter)
            {
                request.Headers.Add("X-API-Key", sinkOptions.ApiKey);
            }

            _logger.LogDebug("AzureFunctionReportSink: Sending POST request to {FunctionUrl}", sinkOptions.FunctionUrl);

            // Send request (client certificate will be sent automatically if configured)
            var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var errorMessage = $"Azure Function report submission failed with status {(int)response.StatusCode} ({response.StatusCode}): {content}";
                _logger.LogError("AzureFunctionReportSink: {ErrorMessage} - Device={MachineName}, CorrelationId={CorrelationId}",
                    errorMessage, report.Device?.MachineName ?? "Unknown", report.CorrelationId ?? "None");
                _logger.LogTrace("AzureFunctionReportSink: Response content: {Content}", content);

                // Throw exception to allow SinkCoordinator to handle retry and failover
                throw new HttpRequestException(errorMessage);
            }

            _logger.LogInformation("AzureFunctionReportSink: Successfully submitted report for device {MachineName} to Azure Function at {Endpoint} (Status={StatusCode})",
                report.Device?.MachineName ?? "Unknown", sinkOptions.FunctionUrl, (int)response.StatusCode);

            if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Debug))
            {
                _logger.LogDebug("AzureFunctionReportSink: Response headers: {Headers}", response.Headers.ToString());
            }
        }
    }
}
