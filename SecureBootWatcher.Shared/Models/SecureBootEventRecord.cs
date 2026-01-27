using System;
using System.Collections.Generic;

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

        // ===== Structured Event Data (from Event XML) =====

        /// <summary>
        /// Update type value. 0 = successful update, 0x5944 (22852) = High Confidence deployment.
        /// Present in events 1808, 1036, 1037, 1043, 1044, 1045.
        /// </summary>
        public int? UpdateType { get; set; }

        /// <summary>
        /// Bucket confidence level: "High Confidence", "Needs More Data", "Unknown", "Paused".
        /// Present in event 1808.
        /// </summary>
        public string? BucketConfidenceLevel { get; set; }

        /// <summary>
        /// Unique identifier for the device's update bucket.
        /// Present in event 1808.
        /// </summary>
        public string? BucketId { get; set; }

        /// <summary>
        /// HRESULT code from the event. S_OK (0) indicates success.
        /// Present in multiple events.
        /// </summary>
        public int? HResult { get; set; }

        /// <summary>
        /// Firmware manufacturer name.
        /// Present in events 1808, 1036, 1037.
        /// </summary>
        public string? FirmwareManufacturer { get; set; }

        /// <summary>
        /// Firmware version string.
        /// Present in events 1808, 1036, 1037.
        /// </summary>
        public string? FirmwareVersion { get; set; }

        /// <summary>
        /// OEM model number.
        /// Present in events 1808, 1036, 1037.
        /// </summary>
        public string? OEMModelNumber { get; set; }

        /// <summary>
        /// OEM manufacturer name.
        /// Present in events 1808, 1036, 1037.
        /// </summary>
        public string? OEMManufacturerName { get; set; }

        /// <summary>
        /// OS Architecture (e.g., "x64", "ARM64").
        /// Present in events 1808, 1036, 1037.
        /// </summary>
        public string? OSArchitecture { get; set; }

        /// <summary>
        /// Number of updates available/pending.
        /// Present in events 1044, 1045.
        /// </summary>
        public int? UpdatesAvailable { get; set; }

        /// <summary>
        /// Error code if the update failed.
        /// Present in events 1034, 1043.
        /// </summary>
        public int? ErrorCode { get; set; }

        /// <summary>
        /// Indicates if a reboot is required for the update to take effect.
        /// Derived from event types 1036, 1037.
        /// </summary>
        public bool? RebootRequired { get; set; }

        /// <summary>
        /// Additional structured data from the event (key-value pairs).
        /// Used for fields that don't have dedicated properties.
        /// </summary>
        public Dictionary<string, string>? AdditionalData { get; set; }
    }
}
