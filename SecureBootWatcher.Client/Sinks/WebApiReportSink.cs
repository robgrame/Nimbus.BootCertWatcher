using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SecureBootWatcher.Shared.Configuration;
using SecureBootWatcher.Shared.Models;

namespace SecureBootWatcher.Client.Sinks
{
    internal sealed class WebApiReportSink : IReportSink
    {
        private readonly ILogger<WebApiReportSink> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOptionsMonitor<SecureBootWatcherOptions> _options;

        public WebApiReportSink(
            ILogger<WebApiReportSink> logger,
            IHttpClientFactory httpClientFactory,
            IOptionsMonitor<SecureBootWatcherOptions> options)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _options = options;
        }

        public async Task EmitAsync(SecureBootStatusReport report, CancellationToken cancellationToken)
        {
            var sinkOptions = _options.CurrentValue.Sinks.WebApi;
            if (sinkOptions.BaseAddress == null)
            {
                _logger.LogDebug("WebApiReportSink: Skipped - BaseAddress is not configured");
                return;
            }

            _logger.LogDebug("WebApiReportSink: Preparing to submit report for device {MachineName} to {BaseAddress}", 
                report.Device?.MachineName ?? "Unknown", sinkOptions.BaseAddress);
            _logger.LogTrace("WebApiReportSink: Report details - CorrelationId={CorrelationId}, ClientVersion={ClientVersion}, EventCount={EventCount}", 
                report.CorrelationId ?? "None", report.ClientVersion ?? "Unknown", report.Events?.Count ?? 0);

            var client = _httpClientFactory.CreateClient("SecureBootIngestion");
            client.BaseAddress = sinkOptions.BaseAddress;
            client.Timeout = sinkOptions.HttpTimeout;

            _logger.LogTrace("WebApiReportSink: HTTP client configured - Timeout={Timeout}", sinkOptions.HttpTimeout);

            var route = sinkOptions.IngestionRoute.StartsWith("/") ? sinkOptions.IngestionRoute : "/" + sinkOptions.IngestionRoute;
            
            _logger.LogDebug("WebApiReportSink: Sending POST request to {Route}", route);
            var response = await client.PostAsJsonAsync(route, report, cancellationToken).ConfigureAwait(false);
            
            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var errorMessage = $"Secure Boot report submission failed with status {(int)response.StatusCode} ({response.StatusCode}): {content}";
                _logger.LogError("WebApiReportSink: {ErrorMessage} - Device={MachineName}, CorrelationId={CorrelationId}", 
                    errorMessage, report.Device?.MachineName ?? "Unknown", report.CorrelationId ?? "None");
                _logger.LogTrace("WebApiReportSink: Response content: {Content}", content);
                
                // Lancia eccezione per permettere al SinkCoordinator di gestire retry e failover
                throw new HttpRequestException(errorMessage);
            }

            _logger.LogInformation("WebApiReportSink: Successfully submitted report for device {MachineName} to API at {Endpoint} (Status={StatusCode})", 
                report.Device?.MachineName ?? "Unknown", client.BaseAddress, (int)response.StatusCode);
            _logger.LogDebug("WebApiReportSink: Response headers: {Headers}", response.Headers.ToString());
        }
    }
}
