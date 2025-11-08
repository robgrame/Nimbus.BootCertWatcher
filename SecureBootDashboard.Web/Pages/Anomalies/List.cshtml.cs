using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SecureBootDashboard.Web.Services;
using SecureBootWatcher.Shared.Models;

namespace SecureBootDashboard.Web.Pages.Anomalies;

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

    public IReadOnlyList<AnomalyDetectionResult> Anomalies { get; private set; } = Array.Empty<AnomalyDetectionResult>();
    public string? ErrorMessage { get; private set; }
    public bool ScanTriggered { get; private set; }

    // Statistics
    public int HighSeverityCount => Anomalies.Count(a => a.Severity >= 0.7);
    public int MediumSeverityCount => Anomalies.Count(a => a.Severity >= 0.4 && a.Severity < 0.7);
    public int LowSeverityCount => Anomalies.Count(a => a.Severity < 0.4);

    public async Task OnGetAsync()
    {
        try
        {
            Anomalies = await _apiClient.GetActiveAnomaliesAsync(HttpContext.RequestAborted);
            _logger.LogInformation("Loaded {Count} active anomalies", Anomalies.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading anomalies");
            ErrorMessage = "Error loading anomalies. Please try again.";
        }
    }

    public async Task<IActionResult> OnGetTriggerScanAsync()
    {
        try
        {
            var userName = User.Identity?.Name ?? "Unknown User";
            _logger.LogInformation("Manual anomaly scan triggered by user {User}", userName);
            Anomalies = await _apiClient.TriggerAnomalyScanAsync(HttpContext.RequestAborted);
            ScanTriggered = true;
            _logger.LogInformation("Manual scan completed. Found {Count} anomalies", Anomalies.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering anomaly scan");
            ErrorMessage = "Error triggering anomaly scan. Please try again.";
            Anomalies = await _apiClient.GetActiveAnomaliesAsync(HttpContext.RequestAborted);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostResolveAsync(Guid anomalyId)
    {
        try
        {
            var userName = User.Identity?.Name ?? "Unknown User";
            await _apiClient.ResolveAnomalyAsync(anomalyId, userName, HttpContext.RequestAborted);
            _logger.LogInformation("Anomaly {AnomalyId} resolved by {User}", anomalyId, userName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving anomaly {AnomalyId}", anomalyId);
            ErrorMessage = "Error resolving anomaly. Please try again.";
        }

        return RedirectToPage();
    }
}
