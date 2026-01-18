using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SecureBootWatcher.Shared.Configuration;

namespace SecureBootWatcher.Client.Services
{
    /// <summary>
    /// Provides sink configuration with database-first approach and appsettings.json fallback.
    /// Priority: Database > appsettings.json
    /// </summary>
    internal sealed class SinkConfigurationProvider
    {
        private readonly ILogger<SinkConfigurationProvider> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly SinkOptions _fallbackOptions;
        private SinkOptions? _cachedOptions;
        private DateTimeOffset _lastFetch = DateTimeOffset.MinValue;
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);

        public SinkConfigurationProvider(
            ILogger<SinkConfigurationProvider> logger,
            IHttpClientFactory httpClientFactory,
            SinkOptions fallbackOptions)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _fallbackOptions = fallbackOptions;
        }

        /// <summary>
        /// Gets sink configuration from database or falls back to appsettings.json.
        /// Implements caching to avoid excessive API calls.
        /// </summary>
        public async Task<SinkOptions> GetConfigurationAsync(CancellationToken cancellationToken = default)
        {
            // Check cache first
            if (_cachedOptions != null && (DateTimeOffset.UtcNow - _lastFetch) < _cacheExpiration)
            {
                _logger.LogDebug("Returning cached sink configuration (age: {Age})", 
                    DateTimeOffset.UtcNow - _lastFetch);
                return _cachedOptions;
            }

            // Try to fetch from database via API
            try
            {
                var dbConfig = await FetchFromDatabaseAsync(cancellationToken);
                
                if (dbConfig != null)
                {
                    _logger.LogInformation("? Using sink configuration from DATABASE");
                    _cachedOptions = dbConfig;
                    _lastFetch = DateTimeOffset.UtcNow;
                    return dbConfig;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch sink configuration from database, falling back to appsettings.json");
            }

            // Fallback to appsettings.json
            _logger.LogInformation("?? Using sink configuration from appsettings.json (fallback)");
            return _fallbackOptions;
        }

        /// <summary>
        /// Fetches active sink configuration from the API/database.
        /// </summary>
        private async Task<SinkOptions?> FetchFromDatabaseAsync(CancellationToken cancellationToken)
        {
            // Check if WebApi sink is configured in fallback (needed to know API endpoint)
            if (_fallbackOptions.WebApi?.BaseAddress == null)
            {
                _logger.LogDebug("WebApi BaseAddress not configured, cannot fetch from database");
                return null;
            }

            // Basic DNS sanity check to avoid repeated failed calls when host is not resolvable
            try
            {
                var host = _fallbackOptions.WebApi.BaseAddress.Host;
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
                _logger.LogWarning(ex, "DNS resolution failed for {Host}. Skipping database sink config fetch and using fallback.", _fallbackOptions.WebApi.BaseAddress.Host);
                return null;
            }

            var httpClient = _httpClientFactory.CreateClient("SecureBootIngestion");
            
            try
            {
                _logger.LogDebug("Fetching sink configuration from {Url}", 
                    $"{_fallbackOptions.WebApi.BaseAddress}/api/ClientSinkConfig/active");

                var response = await httpClient.GetAsync(
                    $"{_fallbackOptions.WebApi.BaseAddress}/api/ClientSinkConfig/active",
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        _logger.LogWarning("No active sink configuration found in database (404)");
                    }
                    else
                    {
                        _logger.LogWarning("Failed to fetch sink configuration: {StatusCode}", response.StatusCode);
                    }
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                };

                var config = JsonSerializer.Deserialize<SinkOptions>(json, options);
                
                if (config != null)
                {
                    _logger.LogInformation("Successfully fetched sink configuration from database");
                    LogSinkConfiguration(config);
                }

                return config;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "HTTP error fetching sink configuration from database");
                return null;
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Timeout fetching sink configuration from database");
                return null;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize sink configuration from database");
                return null;
            }
        }

        /// <summary>
        /// Forces a refresh of the cached configuration on next access.
        /// </summary>
        public void InvalidateCache()
        {
            _logger.LogDebug("Sink configuration cache invalidated");
            _cachedOptions = null;
            _lastFetch = DateTimeOffset.MinValue;
        }

        private void LogSinkConfiguration(SinkOptions config)
        {
            _logger.LogDebug("Sink Configuration from Database:");
            _logger.LogDebug("  FileShare: {Enabled}", config.EnableFileShare);
            _logger.LogDebug("  AzureQueue: {Enabled}", config.EnableAzureQueue);
            _logger.LogDebug("  WebApi: {Enabled}", config.EnableWebApi);
            _logger.LogDebug("  Strategy: {Strategy}", config.ExecutionStrategy);
            _logger.LogDebug("  Priority: {Priority}", config.SinkPriority);
            _logger.LogDebug("  Max Retries: {Retries}", config.MaxRetryAttempts);
            _logger.LogDebug("  Retry Delay: {Delay}", config.RetryDelay);
            _logger.LogDebug("  Exponential Backoff: {Backoff}", config.UseExponentialBackoff);
        }
    }
}
