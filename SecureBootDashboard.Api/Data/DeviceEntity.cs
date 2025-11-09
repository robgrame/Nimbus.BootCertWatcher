using System;
using System.Collections.Generic;

namespace SecureBootDashboard.Api.Data
{
    public sealed class DeviceEntity
    {
        public Guid Id { get; set; }

        public string MachineName { get; set; } = string.Empty;

        public string? DomainName { get; set; }

        public string? UserPrincipalName { get; set; }

        public string? Manufacturer { get; set; }

        public string? Model { get; set; }

        public string? FirmwareVersion { get; set; }

        public DateTime? FirmwareReleaseDate { get; set; } // Added property

        public bool? UEFISecureBootEnabled { get; set; } // Secure Boot enabled status

        /// <summary>
        /// Version of the SecureBootWatcher client that last reported from this device.
        /// Format: Major.Minor.Build.Revision (e.g., "1.0.0.0")
        /// </summary>
        public string? ClientVersion { get; set; }

        public string? FleetId { get; set; }

        public string? TagsJson { get; set; }

        public DateTimeOffset CreatedAtUtc { get; set; }

        public DateTimeOffset LastSeenUtc { get; set; }

        public ICollection<SecureBootReportEntity> Reports { get; set; } = new List<SecureBootReportEntity>();
    }
}
