using System;

namespace SecureBootDashboard.Api.Data
{
    /// <summary>
    /// Configuration entity for automatic device cleanup policy.
    /// Defines rules for removing inactive devices from the database.
    /// </summary>
    public sealed class DeviceCleanupConfigEntity
    {
        /// <summary>
        /// Configuration identifier (typically only one record exists).
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Enable or disable automatic cleanup.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Number of days of inactivity before a device is eligible for cleanup.
        /// A device is considered inactive if LastSeenUtc is older than this value.
        /// </summary>
        public int InactiveDaysThreshold { get; set; }

        /// <summary>
        /// Schedule for automatic cleanup execution (cron expression or interval).
        /// Example: "0 2 * * *" (daily at 2 AM)
        /// </summary>
        public string? CleanupSchedule { get; set; }

        /// <summary>
        /// If true, also delete all associated reports and events when deleting a device.
        /// If false, only the device record is removed (reports/events cascade based on FK).
        /// </summary>
        public bool DeleteAssociatedData { get; set; }

        /// <summary>
        /// If true, send notification email when devices are cleaned up.
        /// </summary>
        public bool NotifyOnCleanup { get; set; }

        /// <summary>
        /// Email address to notify about cleanup operations.
        /// </summary>
        public string? NotificationEmail { get; set; }

        /// <summary>
        /// Last time the cleanup service ran.
        /// </summary>
        public DateTimeOffset? LastCleanupRunUtc { get; set; }

        /// <summary>
        /// Number of devices deleted in the last cleanup run.
        /// </summary>
        public int LastCleanupDeviceCount { get; set; }

        /// <summary>
        /// When this configuration was created.
        /// </summary>
        public DateTimeOffset CreatedAtUtc { get; set; }

        /// <summary>
        /// When this configuration was last modified.
        /// </summary>
        public DateTimeOffset UpdatedAtUtc { get; set; }
    }
}
