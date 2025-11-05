using System;
using System.Collections.Generic;

namespace SecureBootWatcher.Shared.Models
{
    /// <summary>
    /// Represents the device identity metadata included in every Secure Boot report.
    /// </summary>
    public sealed class DeviceIdentity
    {
        public string MachineName { get; set; } = Environment.MachineName;

        public string? DomainName { get; set; }

        public string? UserPrincipalName { get; set; }

        public string? Manufacturer { get; set; }

        public string? Model { get; set; }

        public string? FirmwareVersion { get; set; }

        public IDictionary<string, string> Tags { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}
