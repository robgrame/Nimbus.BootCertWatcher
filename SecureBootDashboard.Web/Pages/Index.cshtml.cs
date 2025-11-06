using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SecureBootDashboard.Web.Services;
using SecureBootWatcher.Shared.Storage;

namespace SecureBootDashboard.Web.Pages;

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

    // Statistics
    public int TotalDevices => Devices.Count;
    public int ActiveDevices => Devices.Count(d => d.LastSeenUtc > DateTimeOffset.UtcNow.AddHours(-24));
    public int InactiveDevices => Devices.Count(d => d.LastSeenUtc < DateTimeOffset.UtcNow.AddDays(-7));
    public int DeployedDevices => Devices.Count(d => d.LatestDeploymentState == "Deployed");
    public int PendingDevices => Devices.Count(d => d.LatestDeploymentState == "Pending");
    public int ErrorDevices => Devices.Count(d => d.LatestDeploymentState == "Error");

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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore nel caricamento della dashboard");
            ErrorMessage = "Errore nel caricamento dei dati.";
        }
    }
}
