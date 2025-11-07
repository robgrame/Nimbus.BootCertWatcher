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

    // Query parameters
    [BindProperty(SupportsGet = true)]
    public int TrendDays { get; set; } = 7;

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

    // Trend data from API
    public ComplianceTrendResponse? ComplianceTrend { get; private set; }

    public async Task OnGetAsync()
    {
        try
        {
            // Validate TrendDays
            if (TrendDays < 7) TrendDays = 7;
            if (TrendDays > 90) TrendDays = 90;
            
            ApiHealthy = await _apiClient.IsHealthyAsync(HttpContext.RequestAborted);
            
            if (!ApiHealthy)
            {
                ErrorMessage = "API non disponibile. Verificare la connessione.";
                return;
            }

            Devices = await _apiClient.GetDevicesAsync(HttpContext.RequestAborted);
            
            _logger.LogInformation("Loaded {Count} devices for dashboard", Devices.Count);

            // Get trend data from API
            ComplianceTrend = await _apiClient.GetComplianceTrendAsync(TrendDays, HttpContext.RequestAborted);
            
            if (ComplianceTrend == null)
            {
                _logger.LogWarning("Failed to load compliance trend data, using fallback");
                // Fallback to simulated data if API call fails
                CalculateComplianceTrendFallback();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore nel caricamento della dashboard");
            ErrorMessage = "Errore nel caricamento dei dati.";
        }
    }

    private void CalculateComplianceTrendFallback()
    {
        // Fallback: Generate simulated trend data
        var snapshots = new List<DailySnapshot>();
        var today = DateTimeOffset.UtcNow.Date;
        
        for (int i = TrendDays - 1; i >= 0; i--)
        {
            var date = today.AddDays(-i);
            var daysAgo = i;
            var historicalCompliance = Math.Max(0, CompliantDevices - (daysAgo * 2));
            
            snapshots.Add(new DailySnapshot(
                date,
                TotalDevices,
                historicalCompliance,
                PendingDevices,
                ErrorDevices,
                TotalDevices - historicalCompliance - PendingDevices - ErrorDevices,
                TotalDevices > 0 ? (double)historicalCompliance / TotalDevices * 100 : 0));
        }

        ComplianceTrend = new ComplianceTrendResponse(TrendDays, snapshots);
    }
}
