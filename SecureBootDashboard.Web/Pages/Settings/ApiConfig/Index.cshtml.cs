using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SecureBootDashboard.Web.Services;
using System.Text.Json.Serialization;

namespace SecureBootDashboard.Web.Pages.Settings.ApiConfig;

// DTO matching ApiConfigurationEntity from API
public class ApiConfigDto
{
    public int Id { get; set; }
    public bool QueueProcessorEnabled { get; set; }
    public string? QueueServiceUri { get; set; }
    public string QueueName { get; set; } = "secureboot-reports";
    public string QueueAuthenticationMethod { get; set; } = "ManagedIdentity";
    public string? QueueClientId { get; set; }
    public string? QueueTenantId { get; set; }
    public string? QueueCertificateThumbprint { get; set; }
    public string QueueCertificateStoreLocation { get; set; } = "LocalMachine";
    public string QueueCertificateStoreName { get; set; } = "My";
    public int QueueMaxMessages { get; set; }
    public int QueueProcessingIntervalSeconds { get; set; }
    public int QueueEmptyQueuePollIntervalSeconds { get; set; }
    public int QueueVisibilityTimeoutSeconds { get; set; }
    public int QueueMaxDequeueCount { get; set; }
    public bool FileReportStoreEnabled { get; set; }
    public string? FileReportStoreBasePath { get; set; }
    public string FileReportStoreExtension { get; set; } = ".json";
    public bool FileReportStoreAppendTimestamp { get; set; }
    public bool DeviceCleanupEnabled { get; set; }
    public string DeviceCleanupSchedule { get; set; } = "0 2 * * 0";
    public int DeviceCleanupDaysThreshold { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
}

public class IndexModel : PageModel
{
    private readonly ISecureBootApiClient _apiClient;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(ISecureBootApiClient apiClient, ILogger<IndexModel> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public List<ApiConfigDto>? Configurations { get; set; }

    [TempData]
    public string? Message { get; set; }

    [TempData]
    public bool IsSuccess { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        try
        {
            Configurations = await _apiClient.GetAsync<List<ApiConfigDto>>("/api/ApiConfig");

            if (Configurations == null)
            {
                Configurations = new List<ApiConfigDto>();
                _logger.LogWarning("Failed to load API configurations from API");
            }

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading API configurations");
            Message = $"Error loading configurations: {ex.Message}";
            IsSuccess = false;
            Configurations = new List<ApiConfigDto>();
            return Page();
        }
    }

    public async Task<IActionResult> OnPostActivateAsync(int id)
    {
        try
        {
            // Get the configuration to activate
            var config = await _apiClient.GetAsync<ApiConfigDto>($"/api/ApiConfig/{id}");

            if (config == null)
            {
                Message = "Configuration not found";
                IsSuccess = false;
                return RedirectToPage();
            }

            // Set IsActive = true
            config.IsActive = true;

            // Update via API
            await _apiClient.PutAsync<ApiConfigDto>($"/api/ApiConfig/{id}", config);

            // Invalidate cache
            await _apiClient.PostAsync<object>("/api/ApiConfig/invalidate-cache", null);

            Message = $"Configuration #{id} activated successfully. API cache invalidated.";
            IsSuccess = true;

            _logger.LogInformation("Activated API configuration {Id}", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating configuration {Id}", id);
            Message = $"Error activating configuration: {ex.Message}";
            IsSuccess = false;
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        try
        {
            // Prevent deletion of default configuration
            if (id == 1)
            {
                Message = "Cannot delete the default configuration (ID=1)";
                IsSuccess = false;
                return RedirectToPage();
            }

            await _apiClient.DeleteAsync($"/api/ApiConfig/{id}");

            Message = $"Configuration #{id} deleted successfully";
            IsSuccess = true;

            _logger.LogInformation("Deleted API configuration {Id}", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting configuration {Id}", id);
            Message = $"Error deleting configuration: {ex.Message}";
            IsSuccess = false;
        }

        return RedirectToPage();
    }
}
