using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SecureBootDashboard.Web.Services;
using SecureBootWatcher.Shared.Models;

namespace SecureBootDashboard.Web.Pages.Certificates
{
    public class DetailsModel : PageModel
    {
        private readonly ISecureBootApiClient _apiClient;
        private readonly ILogger<DetailsModel> _logger;

        public DetailsModel(ISecureBootApiClient apiClient, ILogger<DetailsModel> logger)
        {
            _apiClient = apiClient;
            _logger = logger;
        }

        public string ReportId { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public DateTimeOffset? CollectedAt { get; set; }
        public SecureBootCertificateCollection? Certificates { get; set; }

        public async Task<IActionResult> OnGetAsync(string reportId)
        {
            if (string.IsNullOrEmpty(reportId))
            {
                return NotFound();
            }

            // Parse reportId to Guid
            if (!Guid.TryParse(reportId, out var reportGuid))
            {
                return NotFound("Invalid report ID format");
            }

            ReportId = reportId;

            try
            {
                // Get full report details
                var report = await _apiClient.GetReportAsync(reportGuid);

                if (report == null)
                {
                    return NotFound();
                }

                DeviceId = report.Device?.Tags.ContainsKey("DeviceId") == true 
                    ? report.Device.Tags["DeviceId"] 
                    : string.Empty;
                DeviceName = report.Device?.MachineName ?? "Unknown Device";
                CollectedAt = report.CreatedAtUtc;
                Certificates = report.Certificates;

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load certificate details for report {ReportId}", reportId);
                TempData["Error"] = "Failed to load certificate details. Please try again.";
                return RedirectToPage("/Index");
            }
        }
    }
}
