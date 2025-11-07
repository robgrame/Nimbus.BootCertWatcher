using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SecureBootDashboard.Web.Services;
using SecureBootWatcher.Shared.Storage;

namespace SecureBootDashboard.Web.Pages;

[Authorize]
public class IndexModel : PageModel
{
    private readonly ISecureBootApiClient _apiClient;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(ISecureBootApiClient apiClient, ILogger<IndexModel> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public IReadOnlyList<DeviceSummary> Devices { get; private set; } = Array.Empty<DeviceSummary>();
    public bool ApiHealthy { get; private set; }
    public string? ErrorMessage { get; private set; }
    public int ActiveAnomaliesCount { get; private set; }

    // Statistics
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

    // Trend data (last 7 days)
    public Dictionary<string, int> ComplianceTrendData { get; private set; } = new();

    public async Task OnGetAsync()
    {
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

            // Calculate trend data (last 7 days)
            CalculateComplianceTrend();

            // Get anomaly count
            try
            {
                var anomalies = await _apiClient.GetActiveAnomaliesAsync(HttpContext.RequestAborted);
                ActiveAnomaliesCount = anomalies.Count;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load anomaly count");
                ActiveAnomaliesCount = 0;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore nel caricamento della dashboard");
            ErrorMessage = "Errore nel caricamento dei dati.";
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
}
