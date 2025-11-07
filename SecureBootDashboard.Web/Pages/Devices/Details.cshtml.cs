using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SecureBootDashboard.Web.Services;
using SecureBootWatcher.Shared.Models;

namespace SecureBootDashboard.Web.Pages.Devices;

[Authorize]
public class DetailsModel : PageModel
{
    private readonly ISecureBootApiClient _apiClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DetailsModel> _logger;

    public DetailsModel(
        ISecureBootApiClient apiClient, 
        IHttpClientFactory httpClientFactory,
        ILogger<DetailsModel> logger)
    {
        _apiClient = apiClient;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public DeviceDetail? Device { get; private set; }
    public ComplianceResult? ComplianceStatus { get; private set; }
    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        try
        {
            Device = await _apiClient.GetDeviceAsync(id, HttpContext.RequestAborted);

            if (Device == null)
            {
                ErrorMessage = "Device not found.";
                return Page();
            }

            // Try to load compliance status
            try
            {
                var client = _httpClientFactory.CreateClient("SecureBootApi");
                var response = await client.GetAsync($"/api/Compliance/devices/{id}");
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    ComplianceStatus = JsonSerializer.Deserialize<ComplianceResult>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load compliance status for device {DeviceId}", id);
                // Don't fail the whole page if compliance check fails
            }

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading device details for {DeviceId}", id);
            ErrorMessage = "Errore nel caricamento dei dettagli del dispositivo.";
            return Page();
        }
    }
}
