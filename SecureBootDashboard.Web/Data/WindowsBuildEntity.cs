namespace SecureBootDashboard.Api.Data;

/// <summary>
/// Represents a specific Windows build number and its security status
/// </summary>
public class WindowsBuildEntity
{
    /// <summary>
    /// Primary key
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to WindowsVersion
    /// </summary>
    public int WindowsVersionId { get; set; }

    /// <summary>
    /// Full build number (e.g., "19045.3803")
    /// </summary>
    public required string BuildNumber { get; set; }

    /// <summary>
    /// Major build number (e.g., "19045")
    /// </summary>
    public int MajorBuild { get; set; }

    /// <summary>
    /// Minor build/revision number (e.g., "3803")
    /// </summary>
    public int? MinorBuild { get; set; }

    /// <summary>
    /// Release date of this build
    /// </summary>
    public DateTime? ReleaseDate { get; set; }

    /// <summary>
    /// KB article number (e.g., "KB5035857")
    /// </summary>
    public string? KbArticle { get; set; }

    /// <summary>
    /// Whether this build is considered secure/up-to-date
    /// </summary>
    public bool IsSecure { get; set; }

    /// <summary>
    /// Whether this is the latest build for this version
    /// </summary>
    public bool IsLatest { get; set; }

    /// <summary>
    /// Known security issues or notes
    /// </summary>
    public string? SecurityNotes { get; set; }

    /// <summary>
    /// When this record was last synchronized
    /// </summary>
    public DateTime LastSyncedUtc { get; set; }

    /// <summary>
    /// Navigation property to version
    /// </summary>
    public WindowsVersionEntity? Version { get; set; }
}
