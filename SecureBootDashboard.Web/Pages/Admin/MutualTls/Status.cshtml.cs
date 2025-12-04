using Microsoft.AspNetCore.Mvc.RazorPages;
using SecureBootDashboard.Web.Services;

namespace SecureBootDashboard.Web.Pages.Admin.MutualTls;

public class StatusModel : PageModel
{
    private readonly ISecureBootApiClient _apiClient;
    private readonly ILogger<StatusModel> _logger;

    public StatusModel(ISecureBootApiClient apiClient, ILogger<StatusModel> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public MutualTlsStatusDto? Status { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        try
        {
            Status = await _apiClient.GetAsync<MutualTlsStatusDto>("/api/MutualTlsConfig/status");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load mutual TLS status");
            ErrorMessage = $"Failed to load status: {ex.Message}";
        }
    }
}

public class MutualTlsStatusDto
{
    public bool IsEnabled { get; set; }
    public int TotalTrustedCAs { get; set; }
    public int EnabledTrustedCAs { get; set; }
    public int ExpiredCAs { get; set; }
    public int ExpiringSoonCAs { get; set; }
    public bool IssuerAllowlistEnabled { get; set; }
    public bool ThumbprintAllowlistEnabled { get; set; }
    public bool RevocationCheckEnabled { get; set; }
    public bool ChainValidationEnabled { get; set; }
    public DateTimeOffset? ConfigurationLastUpdated { get; set; }
    public string? ConfigurationUpdatedBy { get; set; }
}
