using System;
using System.Text.Json.Serialization;

namespace SecureBootWatcher.Shared.Models
{
    /// <summary>
    /// Represents the Secure Boot servicing state and device attributes from Windows registry.
    /// Collected from: HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\SecureBoot\Servicing
    /// </summary>
    public sealed class SecureBootServicingState
    {
        /// <summary>
        /// Indicates the state of the Windows UEFI CA 2023 certificate in the Signature Database (db).
        /// Registry Key: WindowsUEFICA2023Capable (REG_DWORD)
        /// 
        /// NOTE: Despite the name "Capable", this registry key actually tracks the state of the 
        /// Windows UEFI CA 2023 certificate presence, NOT device firmware capability.
        /// This key is intended for limited deployment scenarios and should NOT be used for 
        /// general readiness evaluation. Use UEFICA2023Status instead.
        /// 
        /// Valid values:
        /// 0 (or key does not exist) = "Windows UEFI CA 2023" certificate is NOT in the DB
        /// 1 = "Windows UEFI CA 2023" certificate is in the DB
        /// 2 = "Windows UEFI CA 2023" certificate is in the DB AND system is booting from 2023 signed boot manager
        /// </summary>
        [JsonPropertyName("windowsUEFICA2023Capable")]
        public int? WindowsUEFICA2023Capable { get; set; }

        /// <summary>
        /// Current state of the Windows UEFI CA 2023 update process.
        /// Registry Key: UEFICA2023Status (REG_SZ)
        /// Possible values: "NotStarted", "InProgress", "Completed", "Failed", "Blocked"
        /// </summary>
        [JsonPropertyName("uefiCA2023Status")]
        public string? UEFICA2023Status { get; set; }

        /// <summary>
        /// Hash value used for telemetry and device profile tracking.
        /// Registry Key: BucketHash (REG_SZ)
        /// SHA-256 hash (64 hex characters) of device configuration
        /// </summary>
        [JsonPropertyName("bucketHash")]
        public string? BucketHash { get; set; }

        /// <summary>
        /// Microsoft's assessment of firmware confidence for supporting Secure Boot updates.
        /// Registry Key: ConfidenceLevel (REG_SZ)
        /// Possible values: "" (empty), "High", "Medium", "Low"
        /// </summary>
        [JsonPropertyName("confidenceLevel")]
        public string? ConfidenceLevel { get; set; }

        /// <summary>
        /// Timestamp indicating when the device can attempt the update.
        /// Registry Key: CanAttemptUpdateAfter (REG_BINARY - FILETIME format)
        /// Must be in the past for update to be allowed
        /// </summary>
        [JsonPropertyName("canAttemptUpdateAfter")]
        public DateTimeOffset? CanAttemptUpdateAfter { get; set; }

        /// <summary>
        /// Original Equipment Manufacturer name.
        /// Registry Key: OEMManufacturerName (REG_SZ)
        /// Example: "LENOVO", "DELL", "HP"
        /// </summary>
        [JsonPropertyName("oemManufacturerName")]
        public string? OEMManufacturerName { get; set; }

        /// <summary>
        /// OEM's system model identifier.
        /// Registry Key: OEMModelSystemVersion (REG_SZ)
        /// Example: "ThinkPad P16v Gen 1"
        /// </summary>
        [JsonPropertyName("oemModelSystemVersion")]
        public string? OEMModelSystemVersion { get; set; }

        /// <summary>
        /// Motherboard/baseboard manufacturer.
        /// Registry Key: BaseBoardManufacturer (REG_SZ)
        /// Example: "LENOVO"
        /// </summary>
        [JsonPropertyName("baseBoardManufacturer")]
        public string? BaseBoardManufacturer { get; set; }

        /// <summary>
        /// Firmware (BIOS/UEFI) manufacturer.
        /// Registry Key: FirmwareManufacturer (REG_SZ)
        /// Example: "LENOVO", "AMERICAN MEGATRENDS", "PHOENIX"
        /// </summary>
        [JsonPropertyName("firmwareManufacturer")]
        public string? FirmwareManufacturer { get; set; }

        /// <summary>
        /// OEM's baseboard model code.
        /// Registry Key: OEMModelBaseBoard (REG_SZ)
        /// Example: "21FDS0T01Y"
        /// </summary>
        [JsonPropertyName("oemModelBaseBoard")]
        public string? OEMModelBaseBoard { get; set; }

        /// <summary>
        /// Current firmware version string.
        /// Registry Key: FirmwareVersion (REG_SZ)
        /// Example: "N3UET40W (1.40 )"
        /// </summary>
        [JsonPropertyName("firmwareVersion")]
        public string? FirmwareVersion { get; set; }

        /// <summary>
        /// OEM's model number.
        /// Registry Key: OEMModelNumber (REG_SZ)
        /// Example: "21FDS0T01Y"
        /// </summary>
        [JsonPropertyName("oemModelNumber")]
        public string? OEMModelNumber { get; set; }

        /// <summary>
        /// OEM's system family classification.
        /// Registry Key: OEMModelSystemFamily (REG_SZ)
        /// Example: "ThinkPad P16v Gen 1"
        /// </summary>
        [JsonPropertyName("oemModelSystemFamily")]
        public string? OEMModelSystemFamily { get; set; }

        /// <summary>
        /// OEM name (often same as OEMManufacturerName).
        /// Registry Key: OEMName (REG_SZ)
        /// Example: "LENOVO"
        /// </summary>
        [JsonPropertyName("oemName")]
        public string? OEMName { get; set; }

        /// <summary>
        /// Operating system architecture.
        /// Registry Key: OSArchitecture (REG_SZ)
        /// Possible values: "x86", "AMD64", "ARM64"
        /// </summary>
        [JsonPropertyName("osArchitecture")]
        public string? OSArchitecture { get; set; }

        /// <summary>
        /// OEM's baseboard version.
        /// Registry Key: OEMModelBaseBoardVersion (REG_SZ)
        /// Example: "SDK0T76528 WIN"
        /// </summary>
        [JsonPropertyName("oemModelBaseBoardVersion")]
        public string? OEMModelBaseBoardVersion { get; set; }

        /// <summary>
        /// Firmware release date in MM/DD/YYYY format.
        /// Registry Key: FirmwareReleaseDate (REG_SZ)
        /// Example: "08/22/2025"
        /// Used to assess firmware confidence level:
        /// - After Jan 2025: HIGH confidence
        /// - During 2024: MEDIUM confidence
        /// - Before 2024: LOW confidence
        /// </summary>
        [JsonPropertyName("firmwareReleaseDate")]
        public string? FirmwareReleaseDate { get; set; }

        /// <summary>
        /// Encoded state machine representation of device readiness progression.
        /// Registry Key: StateAttributes (REG_SZ)
        /// Complex format with multiple state tracking sections
        /// Indicates device's progression through update state machine
        /// </summary>
        [JsonPropertyName("stateAttributes")]
        public string? StateAttributes { get; set; }

        /// <summary>
        /// Timestamp when this servicing state was collected from the registry.
        /// </summary>
        [JsonPropertyName("collectedAtUtc")]
        public DateTimeOffset CollectedAtUtc { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Any error or collection issue encountered when reading registry values.
        /// </summary>
        [JsonPropertyName("collectionError")]
        public string? CollectionError { get; set; }

        /// <summary>
        /// Indicates if the registry values were successfully collected.
        /// </summary>
        [JsonPropertyName("isValid")]
        public bool IsValid => string.IsNullOrEmpty(CollectionError);
    }
}
