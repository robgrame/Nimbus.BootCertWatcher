using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Json;

namespace SecureBootDashboard.Web.Pages.Windows;

public class BuildsModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BuildsModel> _logger;

    public BuildsModel(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<BuildsModel> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public string Version { get; set; } = string.Empty;

    public WindowsVersionDetails? VersionDetails { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(Version))
        {
            ErrorMessage = "Version parameter is required";
            return Page();
        }

        try
        {
            _logger.LogInformation("Loading build details for Windows version: {Version}", Version);

            var apiBaseUrl = _configuration["ApiSettings:BaseUrl"] ?? "https://localhost:5001";
            var httpClient = _httpClientFactory.CreateClient();

            // Get all versions to find the matching one
            var versionsResponse = await httpClient.GetFromJsonAsync<List<WindowsVersionInfo>>(
                $"{apiBaseUrl}/api/WindowsVersion/versions",
                cancellationToken);

            if (versionsResponse == null)
            {
                ErrorMessage = "Failed to load Windows versions from API";
                return Page();
            }

            // Find the version (case-insensitive)
            var version = versionsResponse.FirstOrDefault(v => 
                v.Version.Equals(Version, StringComparison.OrdinalIgnoreCase));

            if (version == null)
            {
                ErrorMessage = $"Version '{Version}' not found. Available versions: {string.Join(", ", versionsResponse.Select(v => v.Version))}";
                _logger.LogWarning("Version {Version} not found in API response", Version);
                return Page();
            }

            // For now, show version info without detailed builds
            // In future, we can call Office Versions API directly to get build history
            VersionDetails = new WindowsVersionDetails
            {
                Version = version.Version,
                Name = version.Name,
                ReleaseDate = version.ReleaseDate,
                EndOfSupportDate = version.EndOfSupportDate,
                LastSyncedUtc = version.LastSyncedUtc,
                IsSupported = !version.EndOfSupportDate.HasValue || version.EndOfSupportDate.Value > DateTime.UtcNow
            };

            _logger.LogInformation("Successfully loaded details for version {Version}", Version);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error loading build details for version {Version}", Version);
            ErrorMessage = $"API request failed: {ex.Message}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading build details for version {Version}", Version);
            ErrorMessage = $"Failed to load build details: {ex.Message}";
        }

        return Page();
    }

    #region Nested DTOs

    public record WindowsVersionInfo
    {
        public int Id { get; init; }
        public required string Version { get; init; }
        public required string Name { get; init; }
        public DateTime? ReleaseDate { get; init; }
        public DateTime? EndOfSupportDate { get; init; }
        public DateTime LastSyncedUtc { get; init; }
    }

    public record WindowsVersionDetails
    {
        public required string Version { get; init; }
        public required string Name { get; init; }
        public DateTime? ReleaseDate { get; init; }
        public DateTime? EndOfSupportDate { get; init; }
        public DateTime LastSyncedUtc { get; init; }
        public bool IsSupported { get; init; }
    }

    #endregion
}
