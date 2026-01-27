using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace SecureBootDashboard.Api.Services;

/// <summary>
/// Client for officeversions.azurewebsites.net API (v2)
/// Provides Windows version and build information from Microsoft sources
/// 
/// API Documentation: https://officeversions.azurewebsites.net/swagger
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
    /// Get latest versions for both Windows 10 and 11
    /// </summary>
    Task<OfficeVersionsResponse<WindowsLatestVersions>> GetLatestVersionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a specific build is the latest for its version
    /// </summary>
    Task<bool> IsLatestBuildAsync(string buildNumber, CancellationToken cancellationToken = default);
}

public class OfficeVersionsApiClient : IOfficeVersionsApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OfficeVersionsApiClient> _logger;
    private const string BaseUrl = "https://officeversions.azurewebsites.net";

    // New API endpoints (v2)
    private const string Windows10Endpoint = "/api/WindowsVersions/Windows10";
    private const string Windows11Endpoint = "/api/WindowsVersions/Windows11";
    private const string LatestVersionsEndpoint = "/api/WindowsVersions/latest-versions";

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
        return await GetWindowsVersionsAsync(Windows10Endpoint, "Windows 10", cancellationToken);
    }

    public async Task<OfficeVersionsResponse<List<WindowsVersionInfo>>> GetWindows11VersionsAsync(
        CancellationToken cancellationToken = default)
    {
        return await GetWindowsVersionsAsync(Windows11Endpoint, "Windows 11", cancellationToken);
    }

    private async Task<OfficeVersionsResponse<List<WindowsVersionInfo>>> GetWindowsVersionsAsync(
        string endpoint,
        string windowsName,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Fetching {WindowsName} versions from Office Versions API v2: {Endpoint}", 
                windowsName, endpoint);
            
            var response = await _httpClient.GetAsync(endpoint, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Office Versions API returned {StatusCode} for {WindowsName} versions", 
                    response.StatusCode, windowsName);
                return OfficeVersionsResponse<List<WindowsVersionInfo>>.Failure(
                    $"API returned {response.StatusCode}");
            }

            // New API returns a wrapper with Data property
            var apiResponse = await response.Content.ReadFromJsonAsync<WindowsVersionListApiResponse>(cancellationToken);
            
            if (apiResponse == null || !apiResponse.Success)
            {
                _logger.LogWarning("Office Versions API returned unsuccessful response for {WindowsName}", windowsName);
                return OfficeVersionsResponse<List<WindowsVersionInfo>>.Failure(
                    apiResponse?.Message ?? "API returned null response");
            }

            var versions = ConvertToWindowsVersionInfo(apiResponse.Data);
            
            _logger.LogInformation("Successfully fetched {Count} {WindowsName} versions", 
                versions?.Count ?? 0, windowsName);
            
            return OfficeVersionsResponse<List<WindowsVersionInfo>>.Success(versions ?? new List<WindowsVersionInfo>());
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error fetching {WindowsName} versions from Office Versions API", windowsName);
            return OfficeVersionsResponse<List<WindowsVersionInfo>>.Failure($"HTTP error: {ex.Message}");
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogError(ex, "JSON parsing error for {WindowsName} versions - API format may have changed", windowsName);
            return OfficeVersionsResponse<List<WindowsVersionInfo>>.Failure($"JSON parsing error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching {WindowsName} versions", windowsName);
            return OfficeVersionsResponse<List<WindowsVersionInfo>>.Failure($"Error: {ex.Message}");
        }
    }

    public async Task<OfficeVersionsResponse<WindowsLatestVersions>> GetLatestVersionsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching latest Windows versions from Office Versions API v2");
            
            var response = await _httpClient.GetAsync(LatestVersionsEndpoint, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Office Versions API returned {StatusCode} for latest versions", response.StatusCode);
                return OfficeVersionsResponse<WindowsLatestVersions>.Failure($"API returned {response.StatusCode}");
            }

            var latestVersions = await response.Content.ReadFromJsonAsync<WindowsLatestVersions>(cancellationToken);
            
            _logger.LogInformation("Successfully fetched latest Windows versions");
            
            return OfficeVersionsResponse<WindowsLatestVersions>.Success(latestVersions ?? new WindowsLatestVersions());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching latest Windows versions");
            return OfficeVersionsResponse<WindowsLatestVersions>.Failure($"Error: {ex.Message}");
        }
    }

    public async Task<OfficeVersionsResponse<WindowsSummary>> GetWindowsSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching Windows summary from Office Versions API v2");
            
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
                22000 or 22621 or 22631 or 26100 or 26200 => await GetWindows11VersionsAsync(cancellationToken),
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

    /// <summary>
    /// Convert API v2 response to our internal WindowsVersionInfo format
    /// </summary>
    private List<WindowsVersionInfo>? ConvertToWindowsVersionInfo(List<WindowsVersionApiData>? apiData)
    {
        if (apiData == null) return null;

        return apiData.Select(v => new WindowsVersionInfo
        {
            Version = v.Version ?? string.Empty,
            Name = $"{v.Version} ({v.ServiceOption ?? "Unknown"})",
            ReleaseDate = v.ReleaseDate,
            EndOfSupport = ParseEndOfSupport(v),
            IsSupported = IsVersionSupported(v),
            Builds = v.Build != null ? new List<WindowsBuildInfo>
            {
                new WindowsBuildInfo
                {
                    BuildNumber = v.Build,
                    ReleaseDate = v.ReleaseDate,
                    KbArticle = v.KbNumber,
                    IsLatest = v.IsLatestUpdate,
                    SecurityUpdate = true // Assume security update
                }
            } : null
        }).ToList();
    }

    /// <summary>
    /// Parse end of support date from API response
    /// </summary>
    private DateTime? ParseEndOfSupport(WindowsVersionApiData data)
    {
        // Try standard end of servicing first
        if (!string.IsNullOrEmpty(data.EndOfServicingStandard) && 
            data.EndOfServicingStandard != "End of servicing" &&
            DateTime.TryParse(data.EndOfServicingStandard, out var standardDate))
        {
            return standardDate;
        }

        // Try enterprise end of servicing
        if (!string.IsNullOrEmpty(data.EndOfServicingEnterprise) && 
            data.EndOfServicingEnterprise != "End of servicing" &&
            DateTime.TryParse(data.EndOfServicingEnterprise, out var enterpriseDate))
        {
            return enterpriseDate;
        }

        // Try mainstream support end date (for LTSC)
        if (!string.IsNullOrEmpty(data.MainstreamSupportEndDate) &&
            DateTime.TryParse(data.MainstreamSupportEndDate, out var mainstreamDate))
        {
            return mainstreamDate;
        }

        return null;
    }

    /// <summary>
    /// Determine if version is still supported
    /// </summary>
    private bool IsVersionSupported(WindowsVersionApiData data)
    {
        // If marked as "End of servicing", it's not supported
        if (data.EndOfServicingStandard == "End of servicing" ||
            data.EndOfServicingEnterprise == "End of servicing")
        {
            return false;
        }

        // If current version, it's supported
        if (data.IsCurrentVersion)
        {
            return true;
        }

        // Check date-based end of support
        var endDate = ParseEndOfSupport(data);
        if (endDate.HasValue)
        {
            return endDate.Value > DateTime.UtcNow;
        }

        // Default to supported if we can't determine
        return true;
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
/// API v2 response wrapper
/// </summary>
public record WindowsVersionListApiResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("data")]
    public List<WindowsVersionApiData>? Data { get; init; }

    [JsonPropertyName("timestamp")]
    public DateTime? Timestamp { get; init; }

    [JsonPropertyName("source")]
    public string? Source { get; init; }
}

/// <summary>
/// API v2 Windows version data structure
/// </summary>
public record WindowsVersionApiData
{
    [JsonPropertyName("version")]
    public string? Version { get; init; }

    [JsonPropertyName("build")]
    public string? Build { get; init; }

    [JsonPropertyName("kbNumber")]
    public string? KbNumber { get; init; }

    [JsonPropertyName("releaseDate")]
    public DateTime? ReleaseDate { get; init; }

    [JsonPropertyName("serviceOption")]
    public string? ServiceOption { get; init; }

    [JsonPropertyName("availability")]
    public string? Availability { get; init; }

    [JsonPropertyName("edition")]
    public int? Edition { get; init; }

    [JsonPropertyName("isCurrentVersion")]
    public bool IsCurrentVersion { get; init; }

    [JsonPropertyName("isLatestUpdate")]
    public bool IsLatestUpdate { get; init; }

    [JsonPropertyName("endOfServicingStandard")]
    public string? EndOfServicingStandard { get; init; }

    [JsonPropertyName("endOfServicingEnterprise")]
    public string? EndOfServicingEnterprise { get; init; }

    [JsonPropertyName("mainstreamSupportEndDate")]
    public string? MainstreamSupportEndDate { get; init; }

    [JsonPropertyName("extendedSupportEndDate")]
    public string? ExtendedSupportEndDate { get; init; }

    [JsonPropertyName("latestUpdate")]
    public string? LatestUpdate { get; init; }

    [JsonPropertyName("latestRevisionDate")]
    public string? LatestRevisionDate { get; init; }

    [JsonPropertyName("servicingType")]
    public string? ServicingType { get; init; }

    [JsonPropertyName("additionalNotes")]
    public string? AdditionalNotes { get; init; }
}

/// <summary>
/// Windows version information (internal format)
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
/// Windows build information
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

/// <summary>
/// Latest versions response from API v2
/// </summary>
public record WindowsLatestVersions
{
    [JsonPropertyName("windows10")]
    public WindowsLatestVersion? Windows10 { get; init; }

    [JsonPropertyName("windows11")]
    public WindowsLatestVersion? Windows11 { get; init; }
}

/// <summary>
/// Latest version info for a specific Windows edition
/// </summary>
public record WindowsLatestVersion
{
    [JsonPropertyName("version")]
    public string? Version { get; init; }

    [JsonPropertyName("build")]
    public string? Build { get; init; }

    [JsonPropertyName("releaseDate")]
    public DateTime? ReleaseDate { get; init; }
}

#endregion
