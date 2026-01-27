using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SecureBootDashboard.Api.Data;
using System.Text.Json;

namespace SecureBootDashboard.Api.Services;

/// <summary>
/// Service for managing application settings stored in database with in-memory caching
/// </summary>
public sealed class ApplicationSettingsService : IApplicationSettingsService
{
    private readonly SecureBootDbContext _dbContext;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ApplicationSettingsService> _logger;
    private const string CacheKeyPrefix = "AppSetting:";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(5);

    public ApplicationSettingsService(
        SecureBootDbContext dbContext,
        IMemoryCache cache,
        ILogger<ApplicationSettingsService> logger)
    {
        _dbContext = dbContext;
        _cache = cache;
        _logger = logger;
    }

    public async Task<T?> GetSettingAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{CacheKeyPrefix}{key}";

        // Try cache first
        if (_cache.TryGetValue<T>(cacheKey, out var cachedValue))
        {
            _logger.LogDebug("Setting {Key} retrieved from cache", key);
            return cachedValue;
        }

        // Load from database
        var setting = await _dbContext.ApplicationSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == key, cancellationToken);

        if (setting == null)
        {
            _logger.LogWarning("Setting {Key} not found in database", key);
            return default;
        }

        // Deserialize value
        var value = DeserializeValue<T>(setting.Value, setting.ValueType);

        // Cache it
        _cache.Set(cacheKey, value, CacheExpiration);
        _logger.LogDebug("Setting {Key} loaded from database and cached", key);

        return value;
    }

    public async Task<T> GetSettingAsync<T>(string key, T defaultValue, CancellationToken cancellationToken = default)
    {
        var value = await GetSettingAsync<T>(key, cancellationToken);
        return value ?? defaultValue;
    }

    public async Task SetSettingAsync<T>(string key, T value, string? updatedBy = null, CancellationToken cancellationToken = default)
    {
        var setting = await _dbContext.ApplicationSettings
            .FirstOrDefaultAsync(s => s.Key == key, cancellationToken);

        if (setting == null)
        {
            _logger.LogWarning("Attempting to set non-existent setting {Key}", key);
            throw new InvalidOperationException($"Setting with key '{key}' does not exist");
        }

        // Serialize value
        var serializedValue = SerializeValue(value, setting.ValueType);

        // Update database
        setting.Value = serializedValue;
        setting.UpdatedAtUtc = DateTimeOffset.UtcNow;
        setting.UpdatedBy = updatedBy;

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Invalidate cache
        var cacheKey = $"{CacheKeyPrefix}{key}";
        _cache.Remove(cacheKey);

        _logger.LogInformation("Setting {Key} updated by {UpdatedBy}", key, updatedBy ?? "System");
    }

    public async Task<Dictionary<string, string>> GetCategorySettingsAsync(string category, CancellationToken cancellationToken = default)
    {
        var settings = await _dbContext.ApplicationSettings
            .AsNoTracking()
            .Where(s => s.Category == category)
            .ToListAsync(cancellationToken);

        return settings.ToDictionary(s => s.Key, s => s.Value);
    }

    public Task RefreshCacheAsync(CancellationToken cancellationToken = default)
    {
        // Clear all cached settings
        // Note: IMemoryCache doesn't have a Clear method, so we track keys or just let them expire
        _logger.LogInformation("Settings cache refresh requested - entries will expire naturally");
        return Task.CompletedTask;
    }

    private static T? DeserializeValue<T>(string value, string valueType)
    {
        return valueType.ToLowerInvariant() switch
        {
            "string" => (T)(object)JsonSerializer.Deserialize<string>(value)!,
            "int" => (T)(object)int.Parse(value),
            "bool" => (T)(object)bool.Parse(value),
            "timespan" => (T)(object)TimeSpan.Parse(value),
            "datetime" => (T)(object)DateTime.Parse(value),
            "datetimeoffset" => (T)(object)DateTimeOffset.Parse(value),
            "json" => JsonSerializer.Deserialize<T>(value),
            _ => throw new InvalidOperationException($"Unsupported value type: {valueType}")
        };
    }

    private static string SerializeValue<T>(T value, string valueType)
    {
        return valueType.ToLowerInvariant() switch
        {
            "string" => JsonSerializer.Serialize(value),
            "int" or "bool" => value?.ToString() ?? string.Empty,
            "timespan" => ((TimeSpan)(object)value!).ToString(),
            "datetime" => ((DateTime)(object)value!).ToString("O"),
            "datetimeoffset" => ((DateTimeOffset)(object)value!).ToString("O"),
            "json" => JsonSerializer.Serialize(value),
            _ => throw new InvalidOperationException($"Unsupported value type: {valueType}")
        };
    }
}
