using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SecureBootWatcher.Shared.Configuration;
using SecureBootWatcher.Shared.Models;

namespace SecureBootWatcher.LinuxClient.Sinks
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
                _logger.LogDebug("Web API sink skipped because BaseAddress is not configured.");
                return;
            }

            var client = _httpClientFactory.CreateClient("SecureBootIngestion");
            client.BaseAddress = sinkOptions.BaseAddress;
            client.Timeout = sinkOptions.HttpTimeout;

            var route = sinkOptions.IngestionRoute.StartsWith("/") ? sinkOptions.IngestionRoute : "/" + sinkOptions.IngestionRoute;

            try
            {
                var response = await client.PostAsJsonAsync(route, report, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    _logger.LogError("Secure Boot report submission failed with status {StatusCode}: {Body}", (int)response.StatusCode, content);
                    return;
                }

                _logger.LogInformation("Secure Boot report submitted to API at {Endpoint}.", client.BaseAddress);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Secure Boot report submission failed due to HTTP error: {Message}", ex.Message);
                return;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Secure Boot report submission was canceled or timed out: {Message}", ex.Message);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Secure Boot report submission failed due to unexpected error: {Message}", ex.Message);
                return;
            }
        }
    }
}
