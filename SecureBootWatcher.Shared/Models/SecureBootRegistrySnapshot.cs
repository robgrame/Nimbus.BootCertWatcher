using System;

namespace SecureBootWatcher.Shared.Models
{
    /// <summary>
    /// Captures the state of Secure Boot servicing registry keys at the time of collection.
    /// </summary>
    public sealed class SecureBootRegistrySnapshot
    {
        public const string RegistryRootPath = "HKEY_LOCAL_MACHINE\\SYSTEM\\CurrentControlSet\\Control\\SecureBoot";

        public uint? AvailableUpdates { get; set; }
        public bool? HighConfidenceOptOut { get; set; }

        public bool? MicrosoftUpdateManagedOptIn { get; set; }

        // Servicing related keys
        public SecureBootDeploymentState UefiCa2023Status { get; set; } = SecureBootDeploymentState.Unknown;
        public uint? UefiCa2023Error { get; set; }
        public uint? WindowsUEFICA2023CapableCode { get; set; }

        // State related keys
        public string? PolicyPublisher { get; set; }
        public uint? PolicyVersion { get; set; }
        public uint? UEFISecureBootEnabled { get; set; }

        public DateTimeOffset CollectedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    }


    public sealed class SecureBootDeviceAttributesRegistrySnapshot
    {
        public const string RegistryRootPath = "HKEY_LOCAL_MACHINE\\SYSTEM\\CurrentControlSet\\Control\\SecureBoot\\Servicing\\DeviceAttributes";

        public DateTimeOffset? CanAttemptUpdateAfter { get; set; }
        public string? OEMManufacturerName { get; set; }
        public string? OEMModelSystemVersion { get; set; }
        public string? BaseBoardManufacturer { get; set; }
        public string? FirmwareManufacturer { get; set; }
        public string? OEMModelBaseBoard { get; set; }
        public string? FirmwareVersion { get; set; }
        public string? OEMModelNumber { get; set; }
        public string? OEMModelSystemFamily { get; set; }
        public string? OEMName { get; set; }
        public string? OSArchitecture { get; set; }
        public string? OEMModelSKU { get; set; }
        public DateTime? FirmwareReleaseDate { get; set; }
        public string? OEMModelBaseBoardVersion { get; set; }
        public string? StateAttributes { get; set; }

        public DateTimeOffset CollectedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    }
}
