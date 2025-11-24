using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SecureBootDashboard.Web.Services;
using System.Net.Http.Json;

namespace SecureBootDashboard.Web.Pages.Windows;

public class VersionsModel : PageModel
{
    private readonly ISecureBootApiClient _apiClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<VersionsModel> _logger;

    public VersionsModel(
        ISecureBootApiClient apiClient,
        IHttpClientFactory httpClientFactory,
        IConfiguration _configuration,
        ILogger<VersionsModel> logger)
    {
        _apiClient = apiClient;
        _httpClientFactory = httpClientFactory;
        this._configuration = _configuration;
        _logger = logger;
    }

    public List<WindowsVersionDto> Versions { get; set; } = new();
    public WindowsBuildStatistics? Statistics { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
    public bool IsSyncing { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var apiBaseUrl = _configuration["ApiSettings:BaseUrl"] ?? "https://localhost:5001";
            var httpClient = _httpClientFactory.CreateClient();

            // Get all versions
            var versionsResponse = await httpClient.GetFromJsonAsync<List<WindowsVersionDto>>(
                $"{apiBaseUrl}/api/WindowsVersion/versions",
                cancellationToken);

            Versions = versionsResponse ?? new List<WindowsVersionDto>();

            // Get statistics
            var statsResponse = await httpClient.GetFromJsonAsync<WindowsBuildStatistics>(
                $"{apiBaseUrl}/api/WindowsVersion/statistics",
                cancellationToken);

            Statistics = statsResponse;

            _logger.LogInformation("Loaded {Count} Windows versions", Versions.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading Windows versions");
            ErrorMessage = $"Failed to load Windows versions: {ex.Message}";
        }
    }

    public async Task<IActionResult> OnPostSyncAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IsSyncing = true;

            var apiBaseUrl = _configuration["ApiSettings:BaseUrl"] ?? "https://localhost:5001";
            var httpClient = _httpClientFactory.CreateClient();

            _logger.LogInformation("Starting Windows version sync");

            var response = await httpClient.PostAsync(
                $"{apiBaseUrl}/api/WindowsVersion/sync",
                null,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<WindowsVersionSyncResult>(
                    cancellationToken: cancellationToken);

                if (result?.Success == true)
                {
                    SuccessMessage = $"Sync completed successfully! Synced {result.VersionsSynced} versions and {result.BuildsSynced} builds.";
                    _logger.LogInformation(
                        "Windows version sync completed. Versions: {Versions}, Builds: {Builds}",
                        result.VersionsSynced,
                        result.BuildsSynced);
                }
                else
                {
                    ErrorMessage = $"Sync failed: {result?.ErrorMessage ?? "Unknown error"}";
                    _logger.LogWarning("Windows version sync failed: {Error}", result?.ErrorMessage);
                }
            }
            else
            {
                ErrorMessage = $"Sync request failed with status code: {response.StatusCode}";
                _logger.LogError("Windows version sync request failed: {StatusCode}", response.StatusCode);
            }

            // Reload data
            await OnGetAsync(cancellationToken);

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Windows version sync");
            ErrorMessage = $"Sync failed: {ex.Message}";
            IsSyncing = false;
            return Page();
        }
    }
}

#region DTOs

public record WindowsVersionDto
{
    public int Id { get; init; }
    public required string Version { get; init; }
    public required string Name { get; init; }
    public DateTime? ReleaseDate { get; init; }
    public DateTime? EndOfSupportDate { get; init; }
    public DateTime LastSyncedUtc { get; init; }
}

public record WindowsBuildStatistics
{
    public int TotalDevices { get; init; }
    public int DevicesWithSecureBuilds { get; init; }
    public int DevicesWithOutdatedBuilds { get; init; }
    public int DevicesWithUnknownBuilds { get; init; }
    public double SecureBuildPercentage { get; init; }
    public Dictionary<string, int> BuildDistribution { get; init; } = new();
}

public record WindowsVersionSyncResult
{
    public bool Success { get; init; }
    public int VersionsSynced { get; init; }
    public int BuildsSynced { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime? LastSyncedUtc { get; init; }
}

#endregion
