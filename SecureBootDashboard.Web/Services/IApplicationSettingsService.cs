namespace SecureBootDashboard.Api.Services;

/// <summary>
/// Service for managing application settings stored in database
/// </summary>
public interface IApplicationSettingsService
{
    /// <summary>
    /// Get setting value by key
    /// </summary>
    Task<T?> GetSettingAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get setting value by key with default value
    /// </summary>
    Task<T> GetSettingAsync<T>(string key, T defaultValue, CancellationToken cancellationToken = default);

    /// <summary>
    /// Set setting value
    /// </summary>
    Task SetSettingAsync<T>(string key, T value, string? updatedBy = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all settings in a category
    /// </summary>
    Task<Dictionary<string, string>> GetCategorySettingsAsync(string category, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refresh settings cache
    /// </summary>
    Task RefreshCacheAsync(CancellationToken cancellationToken = default);
}
