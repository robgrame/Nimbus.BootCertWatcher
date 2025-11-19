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
        public DateTime? FirmwareReleaseDate { get; set; }

        /// <summary>
        /// Version of the SecureBootWatcher client that generated this report.
        /// Format: Major.Minor.Build.Revision (e.g., "1.0.0.0")
        /// </summary>
        public string? ClientVersion { get; set; }

        /// <summary>
        /// Operating system caption (e.g., "Microsoft Windows 10 Pro", "Microsoft Windows Server 2022 Datacenter").
        /// </summary>
        public string? OperatingSystem { get; set; }

        /// <summary>
        /// Operating system version (e.g., "10.0.19045").
        /// </summary>
        public string? OSVersion { get; set; }

        /// <summary>
        /// Operating system product type: Workstation (1), Domain Controller (2), Server (3).
        /// </summary>
        public int? OSProductType { get; set; }

        /// <summary>
        /// Chassis types from Win32_SystemEnclosure (e.g., 3=Desktop, 9=Laptop, 10=Notebook, 23=Rack Mount Server).
        /// Multiple values possible if device has multiple chassis (e.g., laptop with docking station).
        /// </summary>
        public int[]? ChassisTypes { get; set; }

        /// <summary>
        /// Indicates whether this device is a virtual machine.
        /// Detected from Win32_ComputerSystem.Model, Win32_BIOS, or other virtualization indicators.
        /// </summary>
        public bool? IsVirtualMachine { get; set; }

        /// <summary>
        /// The virtualization platform/hypervisor if IsVirtualMachine is true.
        /// Examples: "Hyper-V", "VMware", "VirtualBox", "KVM", "Xen", etc.
        /// </summary>
        public string? VirtualizationPlatform { get; set; }

        public IDictionary<string, string> Tags { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}
