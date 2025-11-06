using System.Net.Http.Json;
using Microsoft.Extensions.Options;
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
