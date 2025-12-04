using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SecureBootDashboard.Web.Services;
using System.ComponentModel.DataAnnotations;

namespace SecureBootDashboard.Web.Pages.Settings.SinkConfig;

public class EditModel : PageModel
{
    private readonly ISecureBootApiClient _apiClient;
    private readonly ILogger<EditModel> _logger;

    public EditModel(ISecureBootApiClient apiClient, ILogger<EditModel> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public bool IsEditMode { get; set; }
    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        public int Id { get; set; }

        // General
        public bool EnableFileShare { get; set; }
        public bool EnableAzureQueue { get; set; }
        public bool EnableWebApi { get; set; } = true;

        [Required]
        public string ExecutionStrategy { get; set; } = "StopOnFirstSuccess";

        [Required]
        public string SinkPriority { get; set; } = "WebApi,AzureQueue,FileShare";

        [Range(0, 10)]
        public int MaxRetryAttempts { get; set; } = 3;

        [Range(1, 3600)]
        public int RetryDelaySeconds { get; set; } = 300;

        public bool UseExponentialBackoff { get; set; }

        // FileShare
        public string? FileShareRootPath { get; set; }
        public string FileShareExtension { get; set; } = ".json";
        public bool FileShareAppendTimestamp { get; set; } = true;

        // Azure Queue
        public string? AzureQueueServiceUri { get; set; }
        public string AzureQueueName { get; set; } = "secureboot-reports";
        public string AzureQueueAuthMethod { get; set; } = "DefaultAzureCredential";
        public string? AzureQueueConnectionString { get; set; }
        public string? AzureQueueClientId { get; set; }
        public string? AzureQueueTenantId { get; set; }
        public string? AzureQueueClientSecret { get; set; }
        public string? AzureQueueCertPath { get; set; }
        public string? AzureQueueCertPassword { get; set; }
        public string? AzureQueueCertThumbprint { get; set; }
        public string AzureQueueCertStoreLocation { get; set; } = "CurrentUser";
        public string AzureQueueCertStoreName { get; set; } = "My";
        
        [Range(1, 3600)]
        public int AzureQueueVisibilityTimeoutSeconds { get; set; } = 300;
        
        [Range(1, 10)]
        public int AzureQueueMaxSendRetryCount { get; set; } = 5;

        // Web API
        public string? WebApiBaseAddress { get; set; }
        public string WebApiIngestionRoute { get; set; } = "/api/SecureBootReports";
        
        [Range(1, 300)]
        public int WebApiTimeoutSeconds { get; set; } = 30;
        
        public bool WebApiUseCertAuth { get; set; }
        public string? WebApiCertPath { get; set; }
        public string? WebApiCertPassword { get; set; }
        public string? WebApiCertThumbprint { get; set; }
        public string WebApiCertStoreLocation { get; set; } = "LocalMachine";
        public string WebApiCertStoreName { get; set; } = "My";

        // Metadata
        public string? Description { get; set; }
        public bool IsActive { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        try
        {
            if (id.HasValue)
            {
                IsEditMode = true;
                var config = await _apiClient.GetAsync<ClientSinkConfigViewModel>(
                    $"/api/ClientSinkConfig/{id.Value}",
                    HttpContext.RequestAborted);

                if (config == null)
                {
                    ErrorMessage = "Configuration not found";
                    return Page();
                }

                // Map to input model
                Input = new InputModel
                {
                    Id = config.Id,
                    EnableFileShare = config.EnableFileShare,
                    EnableAzureQueue = config.EnableAzureQueue,
                    EnableWebApi = config.EnableWebApi,
                    ExecutionStrategy = config.ExecutionStrategy,
                    SinkPriority = config.SinkPriority,
                    MaxRetryAttempts = config.MaxRetryAttempts,
                    RetryDelaySeconds = config.RetryDelaySeconds,
                    UseExponentialBackoff = config.UseExponentialBackoff,
                    
                    FileShareRootPath = config.FileShareRootPath,
                    FileShareExtension = config.FileShareExtension,
                    FileShareAppendTimestamp = config.FileShareAppendTimestamp,
                    
                    AzureQueueServiceUri = config.AzureQueueServiceUri,
                    AzureQueueName = config.AzureQueueName,
                    AzureQueueAuthMethod = config.AzureQueueAuthMethod,
                    AzureQueueConnectionString = config.AzureQueueConnectionString,
                    AzureQueueClientId = config.AzureQueueClientId,
                    AzureQueueTenantId = config.AzureQueueTenantId,
                    AzureQueueClientSecret = config.AzureQueueClientSecret,
                    AzureQueueCertPath = config.AzureQueueCertPath,
                    AzureQueueCertPassword = config.AzureQueueCertPassword,
                    AzureQueueCertThumbprint = config.AzureQueueCertThumbprint,
                    AzureQueueCertStoreLocation = config.AzureQueueCertStoreLocation,
                    AzureQueueCertStoreName = config.AzureQueueCertStoreName,
                    AzureQueueVisibilityTimeoutSeconds = config.AzureQueueVisibilityTimeoutSeconds,
                    AzureQueueMaxSendRetryCount = config.AzureQueueMaxSendRetryCount,
                    
                    WebApiBaseAddress = config.WebApiBaseAddress,
                    WebApiIngestionRoute = config.WebApiIngestionRoute,
                    WebApiTimeoutSeconds = config.WebApiTimeoutSeconds,
                    WebApiUseCertAuth = config.WebApiUseCertAuth,
                    WebApiCertPath = config.WebApiCertPath,
                    WebApiCertPassword = config.WebApiCertPassword,
                    WebApiCertThumbprint = config.WebApiCertThumbprint,
                    WebApiCertStoreLocation = config.WebApiCertStoreLocation,
                    WebApiCertStoreName = config.WebApiCertStoreName,
                    
                    Description = config.Description,
                    IsActive = config.IsActive
                };
            }
            else
            {
                IsEditMode = false;
                // Input already has default values
            }

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading configuration");
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
            var config = new ClientSinkConfigViewModel
            {
                Id = Input.Id,
                EnableFileShare = Input.EnableFileShare,
                EnableAzureQueue = Input.EnableAzureQueue,
                EnableWebApi = Input.EnableWebApi,
                ExecutionStrategy = Input.ExecutionStrategy,
                SinkPriority = Input.SinkPriority,
                MaxRetryAttempts = Input.MaxRetryAttempts,
                RetryDelaySeconds = Input.RetryDelaySeconds,
                UseExponentialBackoff = Input.UseExponentialBackoff,
                
                FileShareRootPath = Input.FileShareRootPath,
                FileShareExtension = Input.FileShareExtension,
                FileShareAppendTimestamp = Input.FileShareAppendTimestamp,
                
                AzureQueueServiceUri = Input.AzureQueueServiceUri,
                AzureQueueName = Input.AzureQueueName,
                AzureQueueAuthMethod = Input.AzureQueueAuthMethod,
                AzureQueueConnectionString = Input.AzureQueueConnectionString,
                AzureQueueClientId = Input.AzureQueueClientId,
                AzureQueueTenantId = Input.AzureQueueTenantId,
                AzureQueueClientSecret = Input.AzureQueueClientSecret,
                AzureQueueCertPath = Input.AzureQueueCertPath,
                AzureQueueCertPassword = Input.AzureQueueCertPassword,
                AzureQueueCertThumbprint = Input.AzureQueueCertThumbprint,
                AzureQueueCertStoreLocation = Input.AzureQueueCertStoreLocation,
                AzureQueueCertStoreName = Input.AzureQueueCertStoreName,
                AzureQueueVisibilityTimeoutSeconds = Input.AzureQueueVisibilityTimeoutSeconds,
                AzureQueueMaxSendRetryCount = Input.AzureQueueMaxSendRetryCount,
                
                WebApiBaseAddress = Input.WebApiBaseAddress,
                WebApiIngestionRoute = Input.WebApiIngestionRoute,
                WebApiTimeoutSeconds = Input.WebApiTimeoutSeconds,
                WebApiUseCertAuth = Input.WebApiUseCertAuth,
                WebApiCertPath = Input.WebApiCertPath,
                WebApiCertPassword = Input.WebApiCertPassword,
                WebApiCertThumbprint = Input.WebApiCertThumbprint,
                WebApiCertStoreLocation = Input.WebApiCertStoreLocation,
                WebApiCertStoreName = Input.WebApiCertStoreName,
                
                Description = Input.Description,
                IsActive = Input.IsActive
            };

            if (IsEditMode && Input.Id > 0)
            {
                await _apiClient.PutAsync<ClientSinkConfigViewModel>(
                    $"/api/ClientSinkConfig/{Input.Id}",
                    config,
                    HttpContext.RequestAborted);
            }
            else
            {
                await _apiClient.PostAsync<ClientSinkConfigViewModel>(
                    "/api/ClientSinkConfig",
                    config,
                    HttpContext.RequestAborted);
            }

            return RedirectToPage("./Index");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving configuration");
            ErrorMessage = $"Error saving configuration: {ex.Message}";
            return Page();
        }
    }
}
