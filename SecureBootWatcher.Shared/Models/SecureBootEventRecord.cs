using System;

namespace SecureBootWatcher.Shared.Models
{
    /// <summary>
    /// Represents a Secure Boot related Windows event log entry.
    /// </summary>
    public sealed class SecureBootEventRecord
    {
        public int EventId { get; set; }

        public string ProviderName { get; set; } = string.Empty;

        public DateTimeOffset TimestampUtc { get; set; }

        public string Level { get; set; } = string.Empty;

        public string? Message { get; set; }

        public string? RawXml { get; set; }
    }
}
