using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecureBootDashboard.Api.Data
{
    /// <summary>
    /// Represents a pending command queued for execution on a device.
    /// Commands are fetched by clients on each execution cycle.
    /// </summary>
    [Table("PendingCommands")]
    public sealed class PendingCommandEntity
    {
        /// <summary>
        /// Unique identifier for this pending command entry.
        /// </summary>
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// The device that should execute this command.
        /// </summary>
        [Required]
        public Guid DeviceId { get; set; }

        /// <summary>
        /// Foreign key navigation to the device.
        /// </summary>
        [ForeignKey(nameof(DeviceId))]
        public DeviceEntity? Device { get; set; }

        /// <summary>
        /// Unique identifier of the command (from DeviceConfigurationCommand.CommandId).
        /// Used to track command execution and results.
        /// </summary>
        [Required]
        public Guid CommandId { get; set; }

        /// <summary>
        /// Type of command: CertificateUpdate, MicrosoftUpdateOptIn, TelemetryConfiguration.
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string CommandType { get; set; } = string.Empty;

        /// <summary>
        /// Serialized JSON of the DeviceConfigurationCommand object.
        /// Contains all parameters needed for command execution.
        /// </summary>
        [Required]
        public string CommandJson { get; set; } = string.Empty;

        /// <summary>
        /// Current status of the command.
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = CommandStatus.Pending;

        /// <summary>
        /// When the command was created/queued.
        /// </summary>
        [Required]
        public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// When the command was last fetched by the client (for retry tracking).
        /// </summary>
        public DateTimeOffset? LastFetchedAtUtc { get; set; }

        /// <summary>
        /// When the command execution completed (success or failure).
        /// </summary>
        public DateTimeOffset? ProcessedAtUtc { get; set; }

        /// <summary>
        /// Serialized JSON of the DeviceConfigurationResult (if command was executed).
        /// </summary>
        public string? ResultJson { get; set; }

        /// <summary>
        /// User or system that created/queued this command.
        /// </summary>
        [MaxLength(256)]
        public string? CreatedBy { get; set; }

        /// <summary>
        /// Optional description or reason for the command.
        /// </summary>
        [MaxLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// Number of times the client fetched this command (for retry tracking).
        /// </summary>
        public int FetchCount { get; set; } = 0;

        /// <summary>
        /// Scheduled execution time (for future execution).
        /// If null, command should be executed immediately.
        /// </summary>
        public DateTimeOffset? ScheduledForUtc { get; set; }

        /// <summary>
        /// Priority of the command (higher = more urgent).
        /// Default is 0 (normal priority).
        /// </summary>
        public int Priority { get; set; } = 0;

        /// <summary>
        /// Whether this command has expired and should not be executed.
        /// Commands can expire based on business logic (e.g., 7 days old).
        /// </summary>
        [NotMapped]
        public bool IsExpired => Status == CommandStatus.Pending && 
                                 CreatedAtUtc.AddDays(7) < DateTimeOffset.UtcNow;

        /// <summary>
        /// Whether this command is ready to be executed (not scheduled for future).
        /// </summary>
        [NotMapped]
        public bool IsReadyForExecution => 
            Status == CommandStatus.Pending && 
            (!ScheduledForUtc.HasValue || ScheduledForUtc.Value <= DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Command status constants.
    /// </summary>
    public static class CommandStatus
    {
        public const string Pending = "Pending";
        public const string Fetched = "Fetched";
        public const string Processing = "Processing";
        public const string Completed = "Completed";
        public const string Failed = "Failed";
        public const string Cancelled = "Cancelled";
        public const string Expired = "Expired";
    }
}
