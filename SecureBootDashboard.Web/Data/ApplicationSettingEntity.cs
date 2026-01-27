namespace SecureBootDashboard.Api.Data;

/// <summary>
/// Application settings stored in database for dynamic configuration
/// </summary>
public sealed class ApplicationSettingEntity
{
    /// <summary>
    /// Unique identifier for the setting
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Setting key (unique)
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// Setting value (JSON serialized)
    /// </summary>
    public required string Value { get; set; }

    /// <summary>
    /// Setting category (e.g., "QueueProcessor", "ClientUpdate", "SecureBootReadiness")
    /// </summary>
    public required string Category { get; set; }

    /// <summary>
    /// Data type of the value (e.g., "string", "int", "bool", "json")
    /// </summary>
    public required string ValueType { get; set; }

    /// <summary>
    /// Human-readable description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Whether this setting is sensitive (e.g., passwords, keys)
    /// </summary>
    public bool IsSensitive { get; set; }

    /// <summary>
    /// Whether this setting requires application restart to take effect
    /// </summary>
    public bool RequiresRestart { get; set; }

    /// <summary>
    /// When the setting was created
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>
    /// When the setting was last updated
    /// </summary>
    public DateTimeOffset UpdatedAtUtc { get; set; }

    /// <summary>
    /// User or system that last modified this setting
    /// </summary>
    public string? UpdatedBy { get; set; }
}
