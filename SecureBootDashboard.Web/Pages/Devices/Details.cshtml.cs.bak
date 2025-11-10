using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SecureBootDashboard.Web.Services;
using SecureBootWatcher.Shared.Models;
using System.Text.Json;

namespace SecureBootDashboard.Web.Pages.Devices;

[Authorize]
public class DetailsModel : PageModel
{
    private readonly ISecureBootApiClient _apiClient;
    private readonly ILogger<DetailsModel> _logger;

    public DetailsModel(ISecureBootApiClient apiClient, ILogger<DetailsModel> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public DeviceDetail? Device { get; private set; }
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

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading device details for {DeviceId}", id);
            ErrorMessage = "Errore nel caricamento dei dettagli del dispositivo.";
            return Page();
        }
    }

    public SecureBootRegistrySnapshot? GetLatestRegistryState()
    {
        if (string.IsNullOrEmpty(Device?.LatestRegistryStateJson))
            return null;

        try
        {
            return JsonSerializer.Deserialize<SecureBootRegistrySnapshot>(Device.LatestRegistryStateJson);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize registry state JSON");
            return null;
        }
    }

    public SecureBootCertificateCollection? GetLatestCertificates()
    {
        if (string.IsNullOrEmpty(Device?.LatestCertificatesJson))
            return null;

        try
        {
            return JsonSerializer.Deserialize<SecureBootCertificateCollection>(Device.LatestCertificatesJson);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize certificates JSON");
            return null;
        }
    }
}
