using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SecureBootDashboard.Web.Services;

namespace SecureBootDashboard.Web.Pages.Devices
{
    public sealed class CleanupModel : PageModel
    {
        private readonly ISecureBootApiClient _apiClient;
        private readonly ILogger<CleanupModel> _logger;

        public CleanupModel(ISecureBootApiClient apiClient, ILogger<CleanupModel> logger)
        {
            _apiClient = apiClient;
            _logger = logger;
        }

        public CleanupConfigResponse? Config { get; set; }
        public CleanupPreviewResponse? Preview { get; set; }
        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }
        public bool ApiHealthy { get; set; }

        public async Task OnGetAsync()
        {
            try
            {
                // Check API health
                ApiHealthy = await _apiClient.CheckHealthAsync();
                if (!ApiHealthy)
                {
                    ErrorMessage = "API is not available. Please check the connection.";
                    return;
                }

                // Get cleanup configuration
                Config = await _apiClient.GetCleanupConfigAsync();

                // Get preview of devices that would be cleaned up
                Preview = await _apiClient.GetCleanupPreviewAsync(Config?.InactiveDaysThreshold);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading cleanup configuration");
                ErrorMessage = $"Error loading data: {ex.Message}";
            }
        }
    }
}
