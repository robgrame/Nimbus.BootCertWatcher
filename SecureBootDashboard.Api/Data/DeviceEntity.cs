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
        /// Chassis types from Win32_SystemEnclosure as JSON array (e.g., "[3,12]" for Desktop with Docking Station).
        /// Stored as JSON string to support multiple chassis types.
        /// </summary>
        public string? ChassisTypesJson { get; set; }

        /// <summary>
        /// Indicates whether this device is a virtual machine.
        /// </summary>
        public bool? IsVirtualMachine { get; set; }

        /// <summary>
        /// The virtualization platform/hypervisor if IsVirtualMachine is true.
        /// Examples: "Hyper-V", "VMware", "VirtualBox", "KVM", "Xen", etc.
        /// </summary>
        public string? VirtualizationPlatform { get; set; }

        public string? FleetId { get; set; }

        public string? TagsJson { get; set; }

        public DateTimeOffset CreatedAtUtc { get; set; }

        public DateTimeOffset LastSeenUtc { get; set; }

        public ICollection<SecureBootReportEntity> Reports { get; set; } = new List<SecureBootReportEntity>();
    }
}
