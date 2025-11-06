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

    public IReadOnlyList<ReportSummary> Reports { get; private set; } = Array.Empty<ReportSummary>();
    public bool ApiHealthy { get; private set; }
    public string? ErrorMessage { get; private set; }

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

            Reports = await _apiClient.GetRecentReportsAsync(100, HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore nel caricamento della dashboard");
            ErrorMessage = "Errore nel caricamento dei dati.";
        }
    }
}
