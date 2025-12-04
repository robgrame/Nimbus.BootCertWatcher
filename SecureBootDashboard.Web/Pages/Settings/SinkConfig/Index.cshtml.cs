using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SecureBootDashboard.Web.Services;
using System.Text.Json;

namespace SecureBootDashboard.Web.Pages.Settings.SinkConfig;

public sealed class ClientSinkConfigViewModel
{
    public int Id { get; set; }
    public bool EnableFileShare { get; set; }
    public bool EnableAzureQueue { get; set; }
    public bool EnableWebApi { get; set; }
    public string ExecutionStrategy { get; set; } = "StopOnFirstSuccess";
    public string SinkPriority { get; set; } = "AzureQueue,WebApi,FileShare";
    public int MaxRetryAttempts { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 300;
    public bool UseExponentialBackoff { get; set; }
    
    public string? FileShareRootPath { get; set; }
    public string FileShareExtension { get; set; } = ".json";
    public bool FileShareAppendTimestamp { get; set; } = true;
    
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
    public int AzureQueueVisibilityTimeoutSeconds { get; set; } = 300;
    public int AzureQueueMaxSendRetryCount { get; set; } = 5;
    
    public string? WebApiBaseAddress { get; set; }
    public string WebApiIngestionRoute { get; set; } = "/api/SecureBootReports";
    public int WebApiTimeoutSeconds { get; set; } = 30;
    public bool WebApiUseCertAuth { get; set; }
    public string? WebApiCertPath { get; set; }
    public string? WebApiCertPassword { get; set; }
    public string? WebApiCertThumbprint { get; set; }
    public string WebApiCertStoreLocation { get; set; } = "LocalMachine";
    public string WebApiCertStoreName { get; set; } = "My";
    
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

    public List<ClientSinkConfigViewModel> Configurations { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        try
        {
            var response = await _apiClient.GetAsync<List<ClientSinkConfigViewModel>>(
                "/api/ClientSinkConfig",
                HttpContext.RequestAborted);

            if (response != null)
            {
                Configurations = response;
            }

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading sink configurations");
            ErrorMessage = $"Error loading configurations: {ex.Message}";
            return Page();
        }
    }

    public async Task<IActionResult> OnPostActivateAsync(int id)
    {
        try
        {
            await _apiClient.PostAsync<object>(
                $"/api/ClientSinkConfig/{id}/activate",
                null,
                HttpContext.RequestAborted);

            SuccessMessage = "Configuration activated successfully";
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating configuration {Id}", id);
            ErrorMessage = $"Error activating configuration: {ex.Message}";
            return RedirectToPage();
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        try
        {
            await _apiClient.DeleteAsync(
                $"/api/ClientSinkConfig/{id}",
                HttpContext.RequestAborted);

            SuccessMessage = "Configuration deleted successfully";
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting configuration {Id}", id);
            ErrorMessage = $"Error deleting configuration: {ex.Message}";
            return RedirectToPage();
        }
    }
}
