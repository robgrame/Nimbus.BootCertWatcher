namespace SecureBootDashboard.Api.Configuration;

/// <summary>
/// Configuration for Windows build security requirements
/// </summary>
public class WindowsSecurityOptions
{
    /// <summary>
    /// Minimum secure build numbers for each Windows version
    /// </summary>
    public Dictionary<string, MinimumBuildInfo> MinimumSecureBuilds { get; set; } = new();

    /// <summary>
    /// Firmware release date threshold for UEFI CA 2023 support
    /// </summary>
    public DateTime FirmwareSecurityDate { get; set; } = new DateTime(2024, 1, 1);

    /// <summary>
    /// Reason for the firmware security date requirement
    /// </summary>
    public string FirmwareSecurityReason { get; set; } = string.Empty;

    /// <summary>
    /// Gets the minimum secure build number for a Windows version
    /// </summary>
    public string? GetMinimumBuildNumber(string windowsVersion)
    {
        if (MinimumSecureBuilds.TryGetValue(windowsVersion, out var buildInfo))
        {
            return buildInfo.BuildNumber;
        }
        return null;
    }

    /// <summary>
    /// Checks if a build number is secure for a given Windows version
    /// </summary>
    public bool IsBuildSecure(string windowsVersion, string buildNumber)
    {
        var minimumBuild = GetMinimumBuildNumber(windowsVersion);
        if (minimumBuild == null)
            return false;

        return CompareBuildNumbers(buildNumber, minimumBuild) >= 0;
    }

    /// <summary>
    /// Checks if firmware is secure based on release date
    /// </summary>
    public bool IsFirmwareSecure(DateTime? firmwareReleaseDate)
    {
        return firmwareReleaseDate.HasValue && 
               firmwareReleaseDate.Value >= FirmwareSecurityDate;
    }

    /// <summary>
    /// Compares two build numbers (e.g., "19045.5131" vs "19045.5000")
    /// Returns: <0 if build1 < build2, 0 if equal, >0 if build1 > build2
    /// </summary>
    private static int CompareBuildNumbers(string build1, string build2)
    {
        var parts1 = build1.Split('.');
        var parts2 = build2.Split('.');

        int maxLength = Math.Max(parts1.Length, parts2.Length);

        for (int i = 0; i < maxLength; i++)
        {
            int part1 = i < parts1.Length && int.TryParse(parts1[i], out var p1) ? p1 : 0;
            int part2 = i < parts2.Length && int.TryParse(parts2[i], out var p2) ? p2 : 0;

            int comparison = part1.CompareTo(part2);
            if (comparison != 0)
                return comparison;
        }

        return 0;
    }
}

/// <summary>
/// Information about a minimum secure build
/// </summary>
public class MinimumBuildInfo
{
    /// <summary>
    /// Build number (e.g., "19045.5131")
    /// </summary>
    public string BuildNumber { get; set; } = string.Empty;

    /// <summary>
    /// Friendly name of the build
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// KB article number
    /// </summary>
    public string KBArticle { get; set; } = string.Empty;

    /// <summary>
    /// Release date of the build
    /// </summary>
    public DateTime ReleaseDate { get; set; }

    /// <summary>
    /// Reason why this is the minimum secure build
    /// </summary>
    public string Reason { get; set; } = string.Empty;
}
