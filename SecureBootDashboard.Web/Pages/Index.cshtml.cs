using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using SecureBootDashboard.Web.Services;
using SecureBootWatcher.Shared.Storage;
using System.Net.Http.Json;

namespace SecureBootDashboard.Web.Pages;

[Authorize]
public class IndexModel : PageModel
{
    private readonly ISecureBootApiClient _apiClient;
    private readonly ILogger<IndexModel> _logger;
    private readonly ApiSettings _apiSettings;
    private readonly IHttpClientFactory _httpClientFactory;

    public IndexModel(
        ISecureBootApiClient apiClient, 
        ILogger<IndexModel> logger,
        IOptions<ApiSettings> apiSettings,
        IHttpClientFactory httpClientFactory)
    {
        _apiClient = apiClient;
        _logger = logger;
        _apiSettings = apiSettings.Value;
        _httpClientFactory = httpClientFactory;
    }

    public IReadOnlyList<DeviceSummary> Devices { get; private set; } = Array.Empty<DeviceSummary>();
    public bool ApiHealthy { get; private set; }
    public string? ErrorMessage { get; set; }

    // Device statistics
    public int TotalDevices => Devices.Count;
    public int ActiveDevices => Devices.Count(d => d.LastSeenUtc > DateTimeOffset.UtcNow.AddHours(-24));
    public int InactiveDevices => Devices.Count(d => d.LastSeenUtc < DateTimeOffset.UtcNow.AddDays(-7));
    public int DeployedDevices => Devices.Count(d => d.LatestDeploymentState == "Deployed");
    public int PendingDevices => Devices.Count(d => d.LatestDeploymentState == "Pending");
    public int ErrorDevices => Devices.Count(d => d.LatestDeploymentState == "Error");

    // Compliance metrics for charts
    public int CompliantDevices => DeployedDevices;
    public int NonCompliantDevices => TotalDevices - DeployedDevices;
    public double CompliancePercentage => TotalDevices > 0 ? (double)CompliantDevices / TotalDevices * 100 : 0;

    // Secure Boot status metrics
    public int SecureBootEnabledDevices => Devices.Count(d => d.UEFISecureBootEnabled == true);
    public int SecureBootDisabledDevices => Devices.Count(d => d.UEFISecureBootEnabled == false);
    public int SecureBootUnknownDevices => Devices.Count(d => !d.UEFISecureBootEnabled.HasValue);
    public double SecureBootEnabledPercentage => TotalDevices > 0 ? (double)SecureBootEnabledDevices / TotalDevices * 100 : 0;

    // Command statistics
    public CommandStatistics? CommandStats { get; private set; }

    // Trend data (last 7 days)
    public Dictionary<string, int> ComplianceTrendData { get; private set; } = new();

    public async Task OnGetAsync()
    {
        // Set API base URL for SignalR connection
        ViewData["ApiBaseUrl"] = _apiSettings.BaseUrl;
        
        try
        {
            ApiHealthy = await _apiClient.IsHealthyAsync(HttpContext.RequestAborted);
            
            if (!ApiHealthy)
            {
                ErrorMessage = "API non disponibile. Verificare la connessione.";
                return;
            }

            Devices = await _apiClient.GetDevicesAsync(HttpContext.RequestAborted);
            
            _logger.LogInformation("Loaded {Count} devices for dashboard", Devices.Count);

            // Load command statistics
            await LoadCommandStatisticsAsync();

            // Calculate trend data (last 7 days)
            CalculateComplianceTrend();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error nel caricamento della dashboard");
            ErrorMessage = "Error nel caricamento dei dati.";
        }
    }

    private async Task LoadCommandStatisticsAsync()
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient("SecureBootApi");
            var apiBaseUrl = _apiSettings.BaseUrl ?? "https://localhost:5001";

            CommandStats = await httpClient.GetFromJsonAsync<CommandStatistics>(
                $"{apiBaseUrl}/api/CommandManagement/statistics",
                HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load command statistics");
            // Don't fail the whole page if commands aren't available
            CommandStats = null;
        }
    }

    private void CalculateComplianceTrend()
    {
        // Generate trend data for the last 7 days
        // In a real implementation, this would query historical data from the database
        // For now, we'll simulate trend data based on current state
        
        var today = DateTimeOffset.UtcNow.Date;
        
        for (int i = 6; i >= 0; i--)
        {
            var date = today.AddDays(-i);
            var dateKey = date.ToString("yyyy-MM-dd");
            
            // Simulate historical compliance growth
            // In production, this should query actual historical data
            var daysAgo = i;
            var historicalCompliance = Math.Max(0, CompliantDevices - (daysAgo * 2));
            
            ComplianceTrendData[dateKey] = historicalCompliance;
        }
    }

    public class CommandStatistics
    {
        public int TotalCommands { get; set; }
        public int PendingCount { get; set; }
        public int CompletedCount { get; set; }
        public int FailedCount { get; set; }
        public int CancelledCount { get; set; }
    }
}
