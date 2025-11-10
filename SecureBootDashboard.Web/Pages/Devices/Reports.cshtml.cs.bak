using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SecureBootDashboard.Web.Services;

namespace SecureBootDashboard.Web.Pages.Devices;

[Authorize]
public class ReportsModel : PageModel
{
    private readonly ISecureBootApiClient _apiClient;
    private readonly ILogger<ReportsModel> _logger;

    public ReportsModel(ISecureBootApiClient apiClient, ILogger<ReportsModel> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public DeviceDetail? Device { get; private set; }
    public IReadOnlyList<ReportHistoryItem> Reports { get; private set; } = Array.Empty<ReportHistoryItem>();
    public List<StateChangeEvent> StateChanges { get; private set; } = new();
    public string? ErrorMessage { get; private set; }

    [BindProperty(SupportsGet = true)]
    public int Limit { get; set; } = 50;

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        try
        {
            // Load device info
            Device = await _apiClient.GetDeviceAsync(id, HttpContext.RequestAborted);

            if (Device == null)
            {
                ErrorMessage = "Device not found.";
                return Page();
            }

            // Load report history
            Reports = await _apiClient.GetDeviceReportsAsync(id, Limit, HttpContext.RequestAborted);

            // Detect state changes
            DetectStateChanges();

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading report history for device {DeviceId}", id);
            ErrorMessage = "Errore nel caricamento dello storico report.";
            return Page();
        }
    }

    private void DetectStateChanges()
    {
        string? previousState = null;

        foreach (var report in Reports.OrderBy(r => r.CreatedAtUtc))
        {
            if (report.DeploymentState != previousState && previousState != null)
            {
                StateChanges.Add(new StateChangeEvent(
                    report.CreatedAtUtc,
                    previousState,
                    report.DeploymentState ?? "Unknown"));
            }
            previousState = report.DeploymentState;
        }
    }
}

public sealed record StateChangeEvent(
    DateTimeOffset Timestamp,
    string FromState,
    string ToState);
