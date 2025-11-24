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

    public List<WindowsBuildDto> Builds { get; set; } = new();
    public WindowsBuildDto? LatestSecureBuild { get; set; }
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
            var apiBaseUrl = _configuration["ApiSettings:BaseUrl"] ?? "https://localhost:5001";
            var httpClient = _httpClientFactory.CreateClient();

            // Get builds for version
            var buildsResponse = await httpClient.GetFromJsonAsync<List<WindowsBuildDto>>(
                $"{apiBaseUrl}/api/WindowsVersion/versions/{Version}/builds",
                cancellationToken);

            Builds = buildsResponse ?? new List<WindowsBuildDto>();

            // Get latest secure build
            try
            {
                var latestResponse = await httpClient.GetFromJsonAsync<WindowsBuildDto>(
                    $"{apiBaseUrl}/api/WindowsVersion/versions/{Version}/latest-secure",
                    cancellationToken);

                LatestSecureBuild = latestResponse;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("No secure build found for version {Version}", Version);
            }

            _logger.LogInformation("Loaded {Count} builds for Windows version {Version}", Builds.Count, Version);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            ErrorMessage = $"Version '{Version}' not found";
            _logger.LogWarning("Version {Version} not found", Version);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading builds for version {Version}", Version);
            ErrorMessage = $"Failed to load builds: {ex.Message}";
        }

        return Page();
    }
}

#region DTOs

public record WindowsBuildDto
{
    public int Id { get; init; }
    public required string BuildNumber { get; init; }
    public int MajorBuild { get; init; }
    public int? MinorBuild { get; init; }
    public DateTime? ReleaseDate { get; init; }
    public string? KbArticle { get; init; }
    public bool IsSecure { get; init; }
    public bool IsLatest { get; init; }
    public string? SecurityNotes { get; init; }
    public DateTime LastSyncedUtc { get; init; }
}

#endregion
