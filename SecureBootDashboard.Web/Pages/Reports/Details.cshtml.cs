using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SecureBootDashboard.Web.Services;
using SecureBootWatcher.Shared.Storage;
using System.Text.Json;

namespace SecureBootDashboard.Web.Pages.Reports;

public class DetailsModel : PageModel
{
    private readonly ISecureBootApiClient _apiClient;
    private readonly ILogger<DetailsModel> _logger;

    public DetailsModel(ISecureBootApiClient apiClient, ILogger<DetailsModel> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public ReportDetail? Report { get; private set; }
    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        try
        {
            Report = await _apiClient.GetReportDetailAsync(id, HttpContext.RequestAborted);

            if (Report == null)
            {
                return NotFound();
            }

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore nel caricamento del report {ReportId}", id);
            ErrorMessage = "Errore nel caricamento del report.";
            return Page();
        }
    }

    public string GetRegistryStateFormatted()
    {
        if (Report?.RegistryStateJson == null) return "N/A";

        try
        {
            var jsonDoc = JsonDocument.Parse(Report.RegistryStateJson);
            return JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return Report.RegistryStateJson;
        }
    }

    public string GetCertificatesFormatted()
    {
        if (Report?.CertificatesJson == null) return "N/A";

        try
        {
            var jsonDoc = JsonDocument.Parse(Report.CertificatesJson);
            return JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return Report.CertificatesJson;
        }
    }

    public List<string> GetAlerts()
    {
        if (Report?.AlertsJson == null) return new List<string>();

        try
        {
            return JsonSerializer.Deserialize<List<string>>(Report.AlertsJson) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }
}
