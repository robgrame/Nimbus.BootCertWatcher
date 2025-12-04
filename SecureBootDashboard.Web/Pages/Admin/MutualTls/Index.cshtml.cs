using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SecureBootDashboard.Web.Services;

namespace SecureBootDashboard.Web.Pages.Admin.MutualTls;

public class IndexModel : PageModel
{
    private readonly ISecureBootApiClient _apiClient;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(ISecureBootApiClient apiClient, ILogger<IndexModel> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    [BindProperty]
    public MutualTlsConfigViewModel Configuration { get; set; } = new();

    public string? StatusMessage { get; set; }
    public bool IsError { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        try
        {
            var config = await _apiClient.GetAsync<MutualTlsConfigDto>("/api/MutualTlsConfig");
            
            if (config != null)
            {
                Configuration = new MutualTlsConfigViewModel
                {
                    Id = config.Id,
                    Enabled = config.Enabled,
                    AllowSelfSignedCertificates = config.AllowSelfSignedCertificates,
                    CheckCertificateRevocation = config.CheckCertificateRevocation,
                    ValidateCertificateChain = config.ValidateCertificateChain,
                    RequireClientAuthEku = config.RequireClientAuthEku,
                    ValidateCertificateValidity = config.ValidateCertificateValidity,
                    ExpirationGracePeriodDays = config.ExpirationGracePeriodDays,
                    EnableThumbprintAllowlist = config.EnableThumbprintAllowlist,
                    AllowedThumbprints = config.AllowedThumbprints,
                    EnableIssuerAllowlist = config.EnableIssuerAllowlist,
                    EnableDetailedLogging = config.EnableDetailedLogging,
                    RevocationCheckTimeoutSeconds = config.RevocationCheckTimeoutSeconds,
                    ValidationNotes = config.ValidationNotes,
                    UpdatedAtUtc = config.UpdatedAtUtc,
                    UpdatedBy = config.UpdatedBy
                };
            }

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load mutual TLS configuration");
            StatusMessage = $"Failed to load configuration: {ex.Message}";
            IsError = true;
            return Page();
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            StatusMessage = "Please correct the validation errors";
            IsError = true;
            return Page();
        }

        try
        {
            var updateRequest = new
            {
                Configuration.Enabled,
                Configuration.AllowSelfSignedCertificates,
                Configuration.CheckCertificateRevocation,
                Configuration.ValidateCertificateChain,
                Configuration.RequireClientAuthEku,
                Configuration.ValidateCertificateValidity,
                Configuration.ExpirationGracePeriodDays,
                Configuration.EnableThumbprintAllowlist,
                Configuration.AllowedThumbprints,
                Configuration.EnableIssuerAllowlist,
                Configuration.EnableDetailedLogging,
                Configuration.RevocationCheckTimeoutSeconds,
                Configuration.ValidationNotes
            };

            await _apiClient.PutAsync<object>("/api/MutualTlsConfig", updateRequest);

            StatusMessage = "Mutual TLS configuration updated successfully";
            IsError = false;

            _logger.LogInformation("Mutual TLS configuration updated by {User}", User.Identity?.Name ?? "Anonymous");

            // Reload configuration
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update mutual TLS configuration");
            StatusMessage = $"Failed to update configuration: {ex.Message}";
            IsError = true;
            return Page();
        }
    }

    public async Task<IActionResult> OnPostToggleAsync()
    {
        try
        {
            var enabled = Request.Form["enabled"] == "true";

            await _apiClient.PatchAsync("/api/MutualTlsConfig/enabled", new { Enabled = enabled });

            StatusMessage = $"Mutual TLS {(enabled ? "enabled" : "disabled")} successfully";
            IsError = false;

            _logger.LogInformation("Mutual TLS {Action} by {User}", 
                enabled ? "enabled" : "disabled", 
                User.Identity?.Name ?? "Anonymous");

            return RedirectToPage();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle mutual TLS");
            StatusMessage = $"Failed to toggle mutual TLS: {ex.Message}";
            IsError = true;
            return RedirectToPage();
        }
    }
}

public class MutualTlsConfigViewModel
{
    public int Id { get; set; }
    public bool Enabled { get; set; }
    public bool AllowSelfSignedCertificates { get; set; }
    public bool CheckCertificateRevocation { get; set; }
    public bool ValidateCertificateChain { get; set; }
    public bool RequireClientAuthEku { get; set; }
    public bool ValidateCertificateValidity { get; set; }
    public int ExpirationGracePeriodDays { get; set; }
    public bool EnableThumbprintAllowlist { get; set; }
    public string? AllowedThumbprints { get; set; }
    public bool EnableIssuerAllowlist { get; set; }
    public bool EnableDetailedLogging { get; set; }
    public int RevocationCheckTimeoutSeconds { get; set; }
    public string? ValidationNotes { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
}

public class MutualTlsConfigDto
{
    public int Id { get; set; }
    public bool Enabled { get; set; }
    public bool AllowSelfSignedCertificates { get; set; }
    public bool CheckCertificateRevocation { get; set; }
    public bool ValidateCertificateChain { get; set; }
    public bool RequireClientAuthEku { get; set; }
    public bool ValidateCertificateValidity { get; set; }
    public int ExpirationGracePeriodDays { get; set; }
    public bool EnableThumbprintAllowlist { get; set; }
    public string? AllowedThumbprints { get; set; }
    public bool EnableIssuerAllowlist { get; set; }
    public bool EnableDetailedLogging { get; set; }
    public int RevocationCheckTimeoutSeconds { get; set; }
    public string? ValidationNotes { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
}
