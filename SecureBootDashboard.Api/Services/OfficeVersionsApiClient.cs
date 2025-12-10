using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace SecureBootDashboard.Api.Services;

/// <summary>
/// Client for officeversions.azurewebsites.net API
/// Provides Windows version and build information from Microsoft sources
/// </summary>
public interface IOfficeVersionsApiClient
{
    /// <summary>
    /// Get all Windows 10 versions and builds
    /// </summary>
    Task<OfficeVersionsResponse<List<WindowsVersionInfo>>> GetWindows10VersionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all Windows 11 versions and builds
    /// </summary>
    Task<OfficeVersionsResponse<List<WindowsVersionInfo>>> GetWindows11VersionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get summary of all Windows versions
    /// </summary>
    Task<OfficeVersionsResponse<WindowsSummary>> GetWindowsSummaryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a specific build is the latest for its version
    /// </summary>
    Task<bool> IsLatestBuildAsync(string buildNumber, CancellationToken cancellationToken = default);
}

public class OfficeVersionsApiClient : IOfficeVersionsApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OfficeVersionsApiClient> _logger;
    private const string BaseUrl = "https://officeversions.azurewebsites.net/api";

    public OfficeVersionsApiClient(HttpClient httpClient, ILogger<OfficeVersionsApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        
        // Configure base address if not already set
        if (_httpClient.BaseAddress == null)
        {
            _httpClient.BaseAddress = new Uri(BaseUrl);
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }
    }

    public async Task<OfficeVersionsResponse<List<WindowsVersionInfo>>> GetWindows10VersionsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching Windows 10 versions from Office Versions API");
            
            var response = await _httpClient.GetAsync("/windows10", cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Office Versions API returned {StatusCode} for Windows 10 versions", response.StatusCode);
                return OfficeVersionsResponse<List<WindowsVersionInfo>>.Failure(
                    $"API returned {response.StatusCode}");
            }

            var versions = await response.Content.ReadFromJsonAsync<List<WindowsVersionInfo>>(cancellationToken);
            
            _logger.LogInformation("Successfully fetched {Count} Windows 10 versions", versions?.Count ?? 0);
            
            return OfficeVersionsResponse<List<WindowsVersionInfo>>.Success(versions ?? new List<WindowsVersionInfo>());
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error fetching Windows 10 versions from Office Versions API");
            return OfficeVersionsResponse<List<WindowsVersionInfo>>.Failure($"HTTP error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching Windows 10 versions");
            return OfficeVersionsResponse<List<WindowsVersionInfo>>.Failure($"Error: {ex.Message}");
        }
    }

    public async Task<OfficeVersionsResponse<List<WindowsVersionInfo>>> GetWindows11VersionsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching Windows 11 versions from Office Versions API");
            
            var response = await _httpClient.GetAsync("/windows11", cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Office Versions API returned {StatusCode} for Windows 11 versions", response.StatusCode);
                return OfficeVersionsResponse<List<WindowsVersionInfo>>.Failure(
                    $"API returned {response.StatusCode}");
            }

            var versions = await response.Content.ReadFromJsonAsync<List<WindowsVersionInfo>>(cancellationToken);
            
            _logger.LogInformation("Successfully fetched {Count} Windows 11 versions", versions?.Count ?? 0);
            
            return OfficeVersionsResponse<List<WindowsVersionInfo>>.Success(versions ?? new List<WindowsVersionInfo>());
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error fetching Windows 11 versions from Office Versions API");
            return OfficeVersionsResponse<List<WindowsVersionInfo>>.Failure($"HTTP error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching Windows 11 versions");
            return OfficeVersionsResponse<List<WindowsVersionInfo>>.Failure($"Error: {ex.Message}");
        }
    }

    public async Task<OfficeVersionsResponse<WindowsSummary>> GetWindowsSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching Windows summary from Office Versions API");
            
            // Fetch both Windows 10 and 11 versions
            var win10Task = GetWindows10VersionsAsync(cancellationToken);
            var win11Task = GetWindows11VersionsAsync(cancellationToken);

            await Task.WhenAll(win10Task, win11Task);

            var win10Response = await win10Task;
            var win11Response = await win11Task;

            var summary = new WindowsSummary
            {
                Windows10Versions = win10Response.IsSuccess ? win10Response.Data : new List<WindowsVersionInfo>(),
                Windows11Versions = win11Response.IsSuccess ? win11Response.Data : new List<WindowsVersionInfo>(),
                TotalVersions = (win10Response.Data?.Count ?? 0) + (win11Response.Data?.Count ?? 0),
                LastUpdated = DateTime.UtcNow
            };

            _logger.LogInformation("Successfully fetched Windows summary: {TotalVersions} total versions", summary.TotalVersions);
            
            return OfficeVersionsResponse<WindowsSummary>.Success(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Windows summary");
            return OfficeVersionsResponse<WindowsSummary>.Failure($"Error: {ex.Message}");
        }
    }

    public async Task<bool> IsLatestBuildAsync(string buildNumber, CancellationToken cancellationToken = default)
    {
        try
        {
            // Determine Windows version from build number
            var (major, _) = ParseBuildNumber(buildNumber);
            
            var versionsResponse = major switch
            {
                19045 or 19044 or 19043 or 19042 or 19041 => await GetWindows10VersionsAsync(cancellationToken),
                22000 or 22621 or 22631 or 26100 => await GetWindows11VersionsAsync(cancellationToken),
                _ => OfficeVersionsResponse<List<WindowsVersionInfo>>.Failure("Unknown Windows version")
            };

            if (!versionsResponse.IsSuccess || versionsResponse.Data == null)
            {
                _logger.LogWarning("Could not determine if build {BuildNumber} is latest: API call failed", buildNumber);
                return false;
            }

            // Find matching version and check if build is latest
            var matchingVersion = versionsResponse.Data
                .SelectMany(v => v.Builds ?? new List<WindowsBuildInfo>())
                .FirstOrDefault(b => b.BuildNumber == buildNumber);

            return matchingVersion?.IsLatest ?? false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if build {BuildNumber} is latest", buildNumber);
            return false;
        }
    }

    private static (int Major, int? Minor) ParseBuildNumber(string buildNumber)
    {
        var parts = buildNumber.Split('.');
        if (parts.Length == 0) return (0, null);

        if (!int.TryParse(parts[0], out var major))
            major = 0;

        int? minor = null;
        if (parts.Length > 1 && int.TryParse(parts[1], out var minorValue))
            minor = minorValue;

        return (major, minor);
    }
}

#region DTOs

/// <summary>
/// Response wrapper for Office Versions API calls
/// </summary>
public record OfficeVersionsResponse<T>
{
    public bool IsSuccess { get; init; }
    public T? Data { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    public static OfficeVersionsResponse<T> Success(T data) => new()
    {
        IsSuccess = true,
        Data = data
    };

    public static OfficeVersionsResponse<T> Failure(string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorMessage = errorMessage
    };
}

/// <summary>
/// Windows version information from Office Versions API
/// </summary>
public record WindowsVersionInfo
{
    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("releaseDate")]
    public DateTime? ReleaseDate { get; init; }

    [JsonPropertyName("endOfSupport")]
    public DateTime? EndOfSupport { get; init; }

    [JsonPropertyName("builds")]
    public List<WindowsBuildInfo>? Builds { get; init; }

    [JsonPropertyName("isSupported")]
    public bool IsSupported { get; init; }
}

/// <summary>
/// Windows build information from Office Versions API
/// </summary>
public record WindowsBuildInfo
{
    [JsonPropertyName("buildNumber")]
    public string BuildNumber { get; init; } = string.Empty;

    [JsonPropertyName("releaseDate")]
    public DateTime? ReleaseDate { get; init; }

    [JsonPropertyName("kbArticle")]
    public string? KbArticle { get; init; }

    [JsonPropertyName("isLatest")]
    public bool IsLatest { get; init; }

    [JsonPropertyName("securityUpdate")]
    public bool SecurityUpdate { get; init; }

    [JsonPropertyName("notes")]
    public string? Notes { get; init; }
}

/// <summary>
/// Summary of all Windows versions
/// </summary>
public record WindowsSummary
{
    public List<WindowsVersionInfo> Windows10Versions { get; init; } = new();
    public List<WindowsVersionInfo> Windows11Versions { get; init; } = new();
    public int TotalVersions { get; init; }
    public DateTime LastUpdated { get; init; }
}

#endregion
