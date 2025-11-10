using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SecureBootDashboard.Web.Services;
using SecureBootWatcher.Shared.Storage;

namespace SecureBootDashboard.Web.Pages.Devices;

[Authorize]
public class ListModel : PageModel
{
    private readonly ISecureBootApiClient _apiClient;
    private readonly ILogger<ListModel> _logger;

    public ListModel(ISecureBootApiClient apiClient, ILogger<ListModel> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public IReadOnlyList<DeviceSummary> Devices { get; private set; } = Array.Empty<DeviceSummary>();
    public bool ApiHealthy { get; private set; }
    public string? ErrorMessage { get; private set; }

    // Filter parameters
    public string? FilterState { get; set; }
    public string? FilterFleet { get; set; }
    public string? SearchTerm { get; set; }

    // Statistics
    public int TotalDevices => FilteredDevices.Count;
    public int ActiveDevices => FilteredDevices.Count(d => d.LastSeenUtc > DateTimeOffset.UtcNow.AddHours(-24));
    public int InactiveDevices => FilteredDevices.Count(d => d.LastSeenUtc < DateTimeOffset.UtcNow.AddDays(-7));
    public int DeployedDevices => FilteredDevices.Count(d => d.LatestDeploymentState == "Deployed");
    public int PendingDevices => FilteredDevices.Count(d => d.LatestDeploymentState == "Pending");
    public int ErrorDevices => FilteredDevices.Count(d => d.LatestDeploymentState == "Error");

    public IReadOnlyList<DeviceSummary> FilteredDevices
    {
        get
        {
            var filtered = Devices.AsEnumerable();

            // Apply state filter
            if (!string.IsNullOrEmpty(FilterState) && FilterState != "All")
            {
                filtered = filtered.Where(d => d.LatestDeploymentState == FilterState);
            }

            // Apply fleet filter
            if (!string.IsNullOrEmpty(FilterFleet) && FilterFleet != "All")
            {
                filtered = filtered.Where(d => d.FleetId == FilterFleet);
            }

            // Apply search term
            if (!string.IsNullOrEmpty(SearchTerm))
            {
                var searchLower = SearchTerm.ToLowerInvariant();
                filtered = filtered.Where(d =>
                    (d.MachineName?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (d.DomainName?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (d.Manufacturer?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (d.Model?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ?? false)
                );
            }

            return filtered.ToList();
        }
    }

    public IEnumerable<string> AvailableFleets =>
        Devices
            .Select(d => d.FleetId)
            .Where(f => !string.IsNullOrEmpty(f))
            .Distinct()
            .OrderBy(f => f);

    public async Task OnGetAsync(string? state = null, string? fleet = null, string? search = null)
    {
        FilterState = state;
        FilterFleet = fleet;
        SearchTerm = search;

        try
        {
            ApiHealthy = await _apiClient.IsHealthyAsync(HttpContext.RequestAborted);

            if (!ApiHealthy)
            {
                ErrorMessage = "API non disponibile. Verificare la connessione.";
                return;
            }

            Devices = await _apiClient.GetDevicesAsync(HttpContext.RequestAborted);

            _logger.LogInformation("Loaded {Count} devices for device list page", Devices.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore nel caricamento della lista dispositivi");
            ErrorMessage = "Errore nel caricamento dei dati.";
        }
    }
}
