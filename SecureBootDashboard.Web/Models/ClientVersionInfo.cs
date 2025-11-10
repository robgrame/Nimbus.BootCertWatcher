namespace SecureBootDashboard.Web.Models
{
    /// <summary>
    /// Represents a group of devices sharing the same client version.
    /// </summary>
    public sealed class ClientVersionInfo
    {
        public string Version { get; set; } = string.Empty;
        public int DeviceCount { get; set; }
        public bool IsLatest { get; set; }
        public bool IsOutdated { get; set; }
        public bool IsUnsupported { get; set; }
        public DateTimeOffset? OldestReportDate { get; set; }
        public DateTimeOffset? NewestReportDate { get; set; }
        public List<DeviceVersionSummary> Devices { get; set; } = new();
    }

    /// <summary>
    /// Represents a single device with its version information.
    /// </summary>
    public sealed class DeviceVersionSummary
    {
        public Guid Id { get; set; }
        public string MachineName { get; set; } = string.Empty;
        public string? DomainName { get; set; }
        public string? ClientVersion { get; set; }
        public DateTimeOffset LastSeenUtc { get; set; }
        public int DaysSinceLastSeen { get; set; }
        public string? Manufacturer { get; set; }
        public string? Model { get; set; }
        public string? FleetId { get; set; }
    }

    /// <summary>
    /// Configuration for client version comparison.
    /// </summary>
    public sealed class ClientVersionConfig
    {
        public string LatestVersion { get; set; } = "1.0.0.0";
        public string MinimumVersion { get; set; } = "1.0.0.0";
    }
}
