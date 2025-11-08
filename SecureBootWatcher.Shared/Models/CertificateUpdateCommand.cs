using System;

namespace SecureBootWatcher.Shared.Models
{
    /// <summary>
    /// Represents a command to trigger certificate updates on client devices.
    /// </summary>
    public sealed class CertificateUpdateCommand
    {
        /// <summary>
        /// Unique identifier for this command.
        /// </summary>
        public Guid CommandId { get; set; } = Guid.NewGuid();

        /// <summary>
        /// The fleet ID this command targets. If null, targets all devices.
        /// </summary>
        public string? FleetId { get; set; }

        /// <summary>
        /// Specific device machine names to target. If empty, targets all devices in fleet.
        /// </summary>
        public string[] TargetDevices { get; set; } = Array.Empty<string>();

        /// <summary>
        /// The update flags to apply. If null, uses Windows defaults.
        /// </summary>
        public uint? UpdateFlags { get; set; }

        /// <summary>
        /// When this command was issued.
        /// </summary>
        public DateTimeOffset IssuedAtUtc { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// When this command expires (optional).
        /// </summary>
        public DateTimeOffset? ExpiresAtUtc { get; set; }

        /// <summary>
        /// User or system that issued this command.
        /// </summary>
        public string? IssuedBy { get; set; }

        /// <summary>
        /// Additional notes or reason for this update command.
        /// </summary>
        public string? Notes { get; set; }
    }
}
