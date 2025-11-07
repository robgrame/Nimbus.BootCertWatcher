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

        public string? FleetId { get; set; }

        public string? TagsJson { get; set; }

        public DateTimeOffset CreatedAtUtc { get; set; }

        public DateTimeOffset LastSeenUtc { get; set; }

        public ICollection<SecureBootReportEntity> Reports { get; set; } = new List<SecureBootReportEntity>();
    }
}
