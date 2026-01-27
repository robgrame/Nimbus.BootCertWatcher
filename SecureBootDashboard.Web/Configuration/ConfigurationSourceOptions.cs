namespace SecureBootDashboard.Api.Configuration;

/// <summary>
/// Options for specifying the source of application configuration.
/// Determines whether configuration is read from appsettings.json or database.
/// </summary>
public sealed class ConfigurationSourceOptions
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "ConfigurationSource";

    /// <summary>
    /// Gets or sets the configuration source provider.
    /// Valid values: "AppSettings" (default) or "Database"
    /// </summary>
    public string Provider { get; set; } = "AppSettings";

    /// <summary>
    /// Gets a value indicating whether database configuration is enabled.
    /// </summary>
    public bool UseDatabaseConfiguration => Provider.Equals("Database", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets a value indicating whether appsettings.json configuration is used.
    /// </summary>
    public bool UseAppSettingsConfiguration => Provider.Equals("AppSettings", StringComparison.OrdinalIgnoreCase);
}
