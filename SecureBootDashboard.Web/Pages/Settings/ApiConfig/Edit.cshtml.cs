using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SecureBootDashboard.Web.Services;
using System.ComponentModel.DataAnnotations;

namespace SecureBootDashboard.Web.Pages.Settings.ApiConfig;

// Share the DTO from Index
public class EditModel : PageModel
{
    private readonly ISecureBootApiClient _apiClient;
    private readonly ILogger<EditModel> _logger;

    public EditModel(ISecureBootApiClient apiClient, ILogger<EditModel> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    [BindProperty]
    public string? Description { get; set; }

    [BindProperty]
    public bool IsActive { get; set; }

    // Queue Processor Settings
    [BindProperty]
    public bool QueueProcessorEnabled { get; set; }

    [BindProperty]
    public string? QueueServiceUri { get; set; }

    [BindProperty]
    [Required]
    public string QueueName { get; set; } = "secureboot-reports";

    [BindProperty]
    [Required]
    public string QueueAuthenticationMethod { get; set; } = "ManagedIdentity";

    [BindProperty]
    public string? QueueClientId { get; set; }

    [BindProperty]
    public string? QueueTenantId { get; set; }

    [BindProperty]
    public string? QueueCertificateThumbprint { get; set; }

    [BindProperty]
    public string QueueCertificateStoreLocation { get; set; } = "LocalMachine";

    [BindProperty]
    public string QueueCertificateStoreName { get; set; } = "My";

    [BindProperty]
    public int QueueMaxMessages { get; set; } = 10;

    [BindProperty]
    public int QueueProcessingIntervalSeconds { get; set; } = 5;

    // File Report Store Settings
    [BindProperty]
    public bool FileReportStoreEnabled { get; set; }

    [BindProperty]
    public string? FileReportStoreBasePath { get; set; }

    [BindProperty]
    public string FileReportStoreExtension { get; set; } = ".json";

    // Device Cleanup Settings
    [BindProperty]
    public bool DeviceCleanupEnabled { get; set; }

    [BindProperty]
    public int DeviceCleanupDaysThreshold { get; set; } = 90;

    [BindProperty]
    public string DeviceCleanupSchedule { get; set; } = "0 2 * * 0";

    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        try
        {
            var config = await _apiClient.GetAsync<ApiConfigDto>($"/api/ApiConfig/{Id}");

            if (config == null)
            {
                ErrorMessage = $"Configuration #{Id} not found";
                return Page();
            }

            // Map to page model properties
            Description = config.Description;
            IsActive = config.IsActive;

            QueueProcessorEnabled = config.QueueProcessorEnabled;
            QueueServiceUri = config.QueueServiceUri;
            QueueName = config.QueueName;
            QueueAuthenticationMethod = config.QueueAuthenticationMethod;
            QueueClientId = config.QueueClientId;
            QueueTenantId = config.QueueTenantId;
            QueueCertificateThumbprint = config.QueueCertificateThumbprint;
            QueueCertificateStoreLocation = config.QueueCertificateStoreLocation;
            QueueCertificateStoreName = config.QueueCertificateStoreName;
            QueueMaxMessages = config.QueueMaxMessages;
            QueueProcessingIntervalSeconds = config.QueueProcessingIntervalSeconds;

            FileReportStoreEnabled = config.FileReportStoreEnabled;
            FileReportStoreBasePath = config.FileReportStoreBasePath;
            FileReportStoreExtension = config.FileReportStoreExtension;

            DeviceCleanupEnabled = config.DeviceCleanupEnabled;
            DeviceCleanupDaysThreshold = config.DeviceCleanupDaysThreshold;
            DeviceCleanupSchedule = config.DeviceCleanupSchedule;

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading API configuration {Id}", Id);
            ErrorMessage = $"Error loading configuration: {ex.Message}";
            return Page();
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            // Get existing configuration
            var config = await _apiClient.GetAsync<ApiConfigDto>($"/api/ApiConfig/{Id}");

            if (config == null)
            {
                ErrorMessage = $"Configuration #{Id} not found";
                return Page();
            }

            // Update with form values
            config.Description = Description;
            config.IsActive = IsActive;

            config.QueueProcessorEnabled = QueueProcessorEnabled;
            config.QueueServiceUri = QueueServiceUri;
            config.QueueName = QueueName;
            config.QueueAuthenticationMethod = QueueAuthenticationMethod;
            config.QueueClientId = QueueClientId;
            config.QueueTenantId = QueueTenantId;
            config.QueueCertificateThumbprint = QueueCertificateThumbprint;
            config.QueueCertificateStoreLocation = QueueCertificateStoreLocation;
            config.QueueCertificateStoreName = QueueCertificateStoreName;
            config.QueueMaxMessages = QueueMaxMessages;
            config.QueueProcessingIntervalSeconds = QueueProcessingIntervalSeconds;

            config.FileReportStoreEnabled = FileReportStoreEnabled;
            config.FileReportStoreBasePath = FileReportStoreBasePath;
            config.FileReportStoreExtension = FileReportStoreExtension;

            config.DeviceCleanupEnabled = DeviceCleanupEnabled;
            config.DeviceCleanupDaysThreshold = DeviceCleanupDaysThreshold;
            config.DeviceCleanupSchedule = DeviceCleanupSchedule;

            // Save changes
            await _apiClient.PutAsync<ApiConfigDto>($"/api/ApiConfig/{Id}", config);

            // Invalidate cache
            await _apiClient.PostAsync<object>("/api/ApiConfig/invalidate-cache", null);

            TempData["Message"] = $"Configuration #{Id} updated successfully. API cache invalidated.";
            TempData["IsSuccess"] = true;

            _logger.LogInformation("Updated API configuration {Id}", Id);

            return RedirectToPage("Index");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating API configuration {Id}", Id);
            ErrorMessage = $"Error saving configuration: {ex.Message}";
            return Page();
        }
    }

    public async Task<IActionResult> OnPostInvalidateCacheAsync()
    {
        try
        {
            await _apiClient.PostAsync<object>("/api/ApiConfig/invalidate-cache", null);

            TempData["Message"] = "API configuration cache invalidated successfully";
            TempData["IsSuccess"] = true;

            _logger.LogInformation("Invalidated API configuration cache");

            return RedirectToPage("Index");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating cache");
            ErrorMessage = $"Error invalidating cache: {ex.Message}";
            return Page();
        }
    }
}
