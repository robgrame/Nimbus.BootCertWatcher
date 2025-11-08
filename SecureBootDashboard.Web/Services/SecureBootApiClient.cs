using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using SecureBootWatcher.Shared.Models;
using SecureBootWatcher.Shared.Storage;

namespace SecureBootDashboard.Web.Services;

public sealed class SecureBootApiClient : ISecureBootApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SecureBootApiClient> _logger;

    public SecureBootApiClient(HttpClient httpClient, IOptions<ApiSettings> options, ILogger<SecureBootApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        var baseUrl = options.Value.BaseUrl;
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("ApiSettings:BaseUrl is not configured.");
        }

        _httpClient.BaseAddress = new Uri(baseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<IReadOnlyList<ReportSummary>> GetRecentReportsAsync(int limit = 50, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching recent reports (limit: {Limit})", limit);
            
            var reports = await _httpClient.GetFromJsonAsync<List<ReportSummary>>(
                $"/api/SecureBootReports/recent?limit={limit}",
                cancellationToken);

            if (reports == null)
            {
                return new List<ReportSummary>();
            }

            return reports;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch recent reports from API");
            return new List<ReportSummary>();
        }
    }

    public async Task<ReportDetail?> GetReportDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching report detail for ID: {ReportId}", id);
            
            var response = await _httpClient.GetAsync($"/api/SecureBootReports/{id}", cancellationToken);
            
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            
            return await response.Content.ReadFromJsonAsync<ReportDetail>(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch report detail for ID: {ReportId}", id);
            return null;
        }
    }

    public async Task<IReadOnlyList<DeviceSummary>> GetDevicesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching devices list");
            
            var devices = await _httpClient.GetFromJsonAsync<List<DeviceSummary>>(
                "/api/Devices",
                cancellationToken);

            if (devices == null)
            {
                return new List<DeviceSummary>();
            }

            return devices;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch devices from API");
            return new List<DeviceSummary>();
        }
    }

    public async Task<DeviceDetail?> GetDeviceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching device detail for ID: {DeviceId}", id);
            
            var response = await _httpClient.GetAsync($"/api/Devices/{id}", cancellationToken);
            
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            
            return await response.Content.ReadFromJsonAsync<DeviceDetail>(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch device detail for ID: {DeviceId}", id);
            return null;
        }
    }

    public async Task<IReadOnlyList<ReportHistoryItem>> GetDeviceReportsAsync(Guid deviceId, int limit = 50, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching reports for device {DeviceId} (limit: {Limit})", deviceId, limit);
            
            var reports = await _httpClient.GetFromJsonAsync<List<ReportHistoryItem>>(
                $"/api/Devices/{deviceId}/reports?limit={limit}",
                cancellationToken);

            if (reports == null)
            {
                return new List<ReportHistoryItem>();
            }

            return reports;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch reports for device {DeviceId}", deviceId);
            return new List<ReportHistoryItem>();
        }
    }

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<AnomalyDetectionResult>> GetActiveAnomaliesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching active anomalies");
            
            var anomalies = await _httpClient.GetFromJsonAsync<List<AnomalyDetectionResult>>(
                "/api/Anomalies",
                cancellationToken);

            if (anomalies == null)
            {
                return new List<AnomalyDetectionResult>();
            }

            return anomalies;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch active anomalies from API");
            return new List<AnomalyDetectionResult>();
        }
    }

    public async Task<AnomalyDetectionResult?> GetAnomalyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching anomaly detail for ID: {AnomalyId}", id);
            
            var response = await _httpClient.GetAsync($"/api/Anomalies/{id}", cancellationToken);
            
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            
            return await response.Content.ReadFromJsonAsync<AnomalyDetectionResult>(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch anomaly detail for ID: {AnomalyId}", id);
            return null;
        }
    }

    public async Task<IReadOnlyList<AnomalyDetectionResult>> TriggerAnomalyScanAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Triggering manual anomaly scan");
            
            var response = await _httpClient.PostAsync("/api/Anomalies/scan", null, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var anomalies = await response.Content.ReadFromJsonAsync<List<AnomalyDetectionResult>>(cancellationToken);
            return anomalies ?? new List<AnomalyDetectionResult>();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to trigger anomaly scan");
            return new List<AnomalyDetectionResult>();
        }
    }

    public async Task ResolveAnomalyAsync(Guid id, string resolvedBy, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Resolving anomaly {AnomalyId} by {ResolvedBy}", id, resolvedBy);
            
            var content = JsonContent.Create(new { ResolvedBy = resolvedBy });
            var response = await _httpClient.PostAsync($"/api/Anomalies/{id}/resolve", content, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to resolve anomaly {AnomalyId}", id);
            // Suppress exception for consistency with other methods
            return;
        }
    }
}
