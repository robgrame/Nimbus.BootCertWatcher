using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Json;

namespace SecureBootDashboard.Web.Pages.Windows;

public class OutdatedDevicesModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OutdatedDevicesModel> _logger;

    public OutdatedDevicesModel(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<OutdatedDevicesModel> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public List<DeviceWithBuildStatus> OutdatedDevices { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public int TotalCount => OutdatedDevices.Count;
    public int CriticalCount => OutdatedDevices.Count(d => !d.IsSecure && d.SecurityNotes?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true);
    public int OutdatedCount => OutdatedDevices.Count(d => !d.IsSecure && d.SecurityNotes?.Contains("not found", StringComparison.OrdinalIgnoreCase) == false);

    public async Task OnGetAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var apiBaseUrl = _configuration["ApiSettings:BaseUrl"] ?? "https://localhost:5001";
            var httpClient = _httpClientFactory.CreateClient();

            var response = await httpClient.GetFromJsonAsync<List<DeviceWithBuildStatus>>(
                $"{apiBaseUrl}/api/WindowsVersion/devices/outdated",
                cancellationToken);

            OutdatedDevices = response ?? new List<DeviceWithBuildStatus>();

            _logger.LogInformation("Loaded {Count} devices with outdated builds", OutdatedDevices.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading outdated devices");
            ErrorMessage = $"Failed to load devices: {ex.Message}";
        }
    }
}

#region DTOs

public record DeviceWithBuildStatus
{
    public Guid DeviceId { get; init; }
    public required string MachineName { get; init; }
    public string? DomainName { get; init; }
    public string? OSBuildNumber { get; init; }
    public bool IsSecure { get; init; }
    public bool IsLatest { get; init; }
    public string? SecurityNotes { get; init; }
    public DateTime? LastSeenUtc { get; init; }
}

#endregion
