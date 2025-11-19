using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using SecureBootWatcher.Shared.Storage;
using SecureBootWatcher.Shared.Models;
using System.Text.Json;

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

    public async Task<SecureBootStatusReport?> GetReportAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching complete report for ID: {ReportId}", id);
            
            var reportDetail = await GetReportDetailAsync(id, cancellationToken);
            
            if (reportDetail == null)
            {
                _logger.LogWarning("GetReportDetailAsync returned null for report {ReportId}", id);
                return null;
            }

            _logger.LogDebug("Report detail retrieved for {ReportId}, CertificatesJson length: {Length}", 
                id, reportDetail.CertificatesJson?.Length ?? 0);

            // Deserialize registry state
            var registry = JsonSerializer.Deserialize<SecureBootRegistrySnapshot>(reportDetail.RegistryStateJson);
            
            // Deserialize certificates if present
            SecureBootCertificateCollection? certificates = null;
            if (!string.IsNullOrEmpty(reportDetail.CertificatesJson))
            {
                try
                {
                    _logger.LogDebug("Attempting to deserialize certificates JSON for report {ReportId}", id);
                    certificates = JsonSerializer.Deserialize<SecureBootCertificateCollection>(reportDetail.CertificatesJson);
                    
                    if (certificates != null)
                    {
                        _logger.LogInformation(
                            "Successfully deserialized certificates for report {ReportId}: " +
                            "Total={Total}, db={Db}, dbx={Dbx}, KEK={Kek}, PK={Pk}",
                            id,
                            certificates.TotalCertificateCount,
                            certificates.SignatureDatabase?.Count ?? 0,
                            certificates.ForbiddenDatabase?.Count ?? 0,
                            certificates.KeyExchangeKeys?.Count ?? 0,
                            certificates.PlatformKeys?.Count ?? 0);
                    }
                    else
                    {
                        _logger.LogWarning("Certificates deserialized to NULL for report {ReportId}", id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize certificates for report {ReportId}. JSON sample: {JsonSample}", 
                        id, 
                        reportDetail.CertificatesJson.Substring(0, Math.Min(200, reportDetail.CertificatesJson.Length)));
                }
            }
            else
            {
                _logger.LogWarning("CertificatesJson is null or empty for report {ReportId}", id);
            }

            // Deserialize alerts
            List<string>? alerts = null;
            if (!string.IsNullOrEmpty(reportDetail.AlertsJson))
            {
                try
                {
                    alerts = JsonSerializer.Deserialize<List<string>>(reportDetail.AlertsJson);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize alerts for report {ReportId}", id);
                }
            }

            // Build SecureBootStatusReport
            var report = new SecureBootStatusReport
            {
                Device = new DeviceIdentity
                {
                    MachineName = reportDetail.Device.MachineName,
                    DomainName = reportDetail.Device.DomainName,
                    UserPrincipalName = reportDetail.Device.UserPrincipalName,
                    Manufacturer = reportDetail.Device.Manufacturer,
                    Model = reportDetail.Device.Model,
                    FirmwareVersion = reportDetail.Device.FirmwareVersion
                },
                Registry = registry ?? new SecureBootRegistrySnapshot(),
                Certificates = certificates,
                CreatedAtUtc = reportDetail.CreatedAtUtc,
                Alerts = alerts ?? new List<string>(),
                ClientVersion = reportDetail.ClientVersion,
                CorrelationId = reportDetail.CorrelationId
            };

            // Add FleetId to tags if available
            if (!string.IsNullOrEmpty(reportDetail.Device.FleetId))
            {
                report.Device.Tags["FleetId"] = reportDetail.Device.FleetId;
            }

            _logger.LogDebug("Returning SecureBootStatusReport for {ReportId}, Certificates is {CertStatus}",
                id, certificates == null ? "NULL" : "NOT NULL");

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch complete report for ID: {ReportId}", id);
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
}
