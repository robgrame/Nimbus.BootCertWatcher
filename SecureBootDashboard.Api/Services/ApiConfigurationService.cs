using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SecureBootDashboard.Api.Configuration;
using SecureBootDashboard.Api.Data;

namespace SecureBootDashboard.Api.Services;

/// <summary>
/// Service for retrieving API configuration from database with caching.
/// Provides dynamic configuration for File Store and Device Cleanup.
/// </summary>
public interface IApiConfigurationService
{
    /// <summary>
    /// Gets the active API configuration from database with caching.
    /// </summary>
    Task<ApiConfigurationEntity?> GetActiveConfigurationAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates the configuration cache, forcing a refresh from database on next access.
    /// </summary>
    void InvalidateCache();
}

public sealed class ApiConfigurationService : IApiConfigurationService
{
    private readonly SecureBootDbContext _dbContext;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ApiConfigurationService> _logger;
    private const string CacheKey = "ApiConfiguration_Active";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(5);

    public ApiConfigurationService(
        SecureBootDbContext dbContext,
        IMemoryCache cache,
        ILogger<ApiConfigurationService> logger)
    {
        _dbContext = dbContext;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ApiConfigurationEntity?> GetActiveConfigurationAsync(CancellationToken cancellationToken = default)
    {
        // Try to get from cache first
        if (_cache.TryGetValue<ApiConfigurationEntity>(CacheKey, out var cachedConfig))
        {
            _logger.LogDebug("Returning cached API configuration");
            return cachedConfig;
        }

        // Fetch from database
        _logger.LogDebug("Fetching active API configuration from database");

        var config = await _dbContext.ApiConfiguration
            .Where(c => c.IsActive)
            .OrderByDescending(c => c.UpdatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (config == null)
        {
            _logger.LogWarning("No active API configuration found in database");
            return null;
        }

        // Cache the result
        _cache.Set(CacheKey, config, CacheExpiration);

        _logger.LogInformation(
            "Loaded active API configuration from database: Id={Id}, QueueEnabled={QueueEnabled}, FileStoreEnabled={FileStoreEnabled}",
            config.Id,
            config.QueueProcessorEnabled,
            config.FileReportStoreEnabled);

        return config;
    }

    public void InvalidateCache()
    {
        _cache.Remove(CacheKey);
        _logger.LogInformation("API configuration cache invalidated");
    }
}
