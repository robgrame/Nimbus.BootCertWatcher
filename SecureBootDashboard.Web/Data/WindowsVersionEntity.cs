namespace SecureBootDashboard.Api.Data;

/// <summary>
/// Represents a major Windows version (e.g., Windows 10, Windows 11)
/// </summary>
public class WindowsVersionEntity
{
    /// <summary>
    /// Primary key
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Version number (e.g., "10", "11")
    /// </summary>
    public required string Version { get; set; }

    /// <summary>
    /// Display name (e.g., "Windows 10", "Windows 11")
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Release date of this version
    /// </summary>
    public DateTime? ReleaseDate { get; set; }

    /// <summary>
    /// End of support date
    /// </summary>
    public DateTime? EndOfSupportDate { get; set; }

    /// <summary>
    /// When this record was last synchronized from WindowsVersionsCore
    /// </summary>
    public DateTime LastSyncedUtc { get; set; }

    /// <summary>
    /// Navigation property to builds
    /// </summary>
    public ICollection<WindowsBuildEntity> Builds { get; set; } = new List<WindowsBuildEntity>();
}
