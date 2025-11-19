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
                _logger.LogWarning("Certificate details requested with null or empty reportId");
                return NotFound();
            }

            // Parse reportId to Guid
            if (!Guid.TryParse(reportId, out var reportGuid))
            {
                _logger.LogWarning("Invalid report ID format: {ReportId}", reportId);
                return NotFound("Invalid report ID format");
            }

            ReportId = reportId;
            _logger.LogInformation("Loading certificate details for report {ReportId}", reportId);

            try
            {
                // Get full report details
                var report = await _apiClient.GetReportAsync(reportGuid);

                if (report == null)
                {
                    _logger.LogWarning("Report {ReportId} not found via API", reportId);
                    return NotFound();
                }

                _logger.LogInformation("Report {ReportId} retrieved successfully from API", reportId);

                DeviceId = report.Device?.Tags.ContainsKey("DeviceId") == true 
                    ? report.Device.Tags["DeviceId"] 
                    : string.Empty;
                DeviceName = report.Device?.MachineName ?? "Unknown Device";
                CollectedAt = report.CreatedAtUtc;
                Certificates = report.Certificates;

                // Diagnostic logging
                if (Certificates == null)
                {
                    _logger.LogWarning("Report {ReportId} for device {DeviceName}: Certificates collection is NULL", 
                        reportId, DeviceName);
                }
                else
                {
                    _logger.LogInformation(
                        "Report {ReportId} for device {DeviceName}: Certificates loaded - " +
                        "Total: {TotalCount}, db: {DbCount}, dbx: {DbxCount}, KEK: {KekCount}, PK: {PkCount}, " +
                        "SecureBootEnabled: {SecureBootEnabled}, Expired: {ExpiredCount}, Expiring: {ExpiringCount}",
                        reportId,
                        DeviceName,
                        Certificates.TotalCertificateCount,
                        Certificates.SignatureDatabase?.Count ?? 0,
                        Certificates.ForbiddenDatabase?.Count ?? 0,
                        Certificates.KeyExchangeKeys?.Count ?? 0,
                        Certificates.PlatformKeys?.Count ?? 0,
                        Certificates.SecureBootEnabled,
                        Certificates.ExpiredCertificateCount,
                        Certificates.ExpiringCertificateCount);
                    
                    if (Certificates.TotalCertificateCount == 0)
                    {
                        _logger.LogWarning("Report {ReportId}: Certificate collection is empty (TotalCount = 0)", reportId);
                    }
                    
                    if (!string.IsNullOrEmpty(Certificates.ErrorMessage))
                    {
                        _logger.LogWarning("Report {ReportId}: Certificate enumeration error: {ErrorMessage}", 
                            reportId, Certificates.ErrorMessage);
                    }
                }

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
