using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using static System.Net.Mime.MediaTypeNames;

namespace SecureBootWatcher.Shared.Models
{
    /// <summary>
    /// Captures the state of Secure Boot servicing registry keys at the time of collection.
    /// </summary>
    public sealed class SecureBootRegistrySnapshot
    {
        // Registry path WITHOUT HKEY_LOCAL_MACHINE prefix (used with Registry.LocalMachine.OpenSubKey)
        public const string RegistryRootPath = "SYSTEM\\CurrentControlSet\\Control\\SecureBoot";

        // #### SecureBoot root registry Key
        /// <summary>
        // Enable Secure Boot Certificate Deployment
        // This policy setting allows you to enable or disable the
        // Secure Boot Certificate Deployment process on devices.
        // When enabled, policy get the value of 0x5944 (hex) or 22852 (decimal)
        // and  Windows will automatically begin the certificate deployment process
        // to devices where this policy has been applied. 
        /// </summary>  
        public uint? AvailableUpdatesPolicy { get; set; }

        /// <summary>
        // This policy setting allows you to enable or disable the
        // Secure Boot Certificate Deployment process on devices.
        // I suppose this is equivalent to the "Enable Secure Booot Certificate Deployment" policy.
        /// </summary>
        public uint? AvailableUpdates { get; set; }

        /// <summary>
        // Automatic Certificate Deployment via Updates
        // For devices where test results are available that indicate that
        // the device can process the certificate updates successfully,
        // the updates will be initiated automatically as part of the servicing updates.
        // This policy is enabled by default.
        // For enterprises that desire managing automatic update, use this policy to explicitly enable or disable the feature.
        /// </summary> 
        public bool? HighConfidenceOptOut { get; set; }

        /// <summary>
        /// Certificate Deployment via Controlled Feature Rollout (CFR) GPO
        /// For enterprises that desire assistance in deploying the new Secure Boot certificates to their devices,
        /// this setting can be enabled.
        // Note: The device must be sending required diagnostic data to Microsoft to use this feature.
        /// </summary>
        public bool? MicrosoftUpdateManagedOptIn { get; set; } = false;

        // #### SBAT Key
        public SecureBootSbatRegistrySnapshot? Sbat { get; set; }

        // #### Servicing Key
        public SecureBootServicingRegistrySnapshot? Servicing { get; set; }

        // #### State Key
        public SecureBootStateRegistrySnapshot? State { get; set; }

        public DateTimeOffset CollectedAtUtc { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Gets the progression state description based on AvailableUpdates value.
        /// </summary>
        public string ProgressionState => SecureBootUpdateFlagsExtensions.GetProgressionState(AvailableUpdates);

        /// <summary>
        /// Gets the deployment completion percentage (0-100).
        /// </summary>
        public int CompletionPercentage => SecureBootUpdateFlagsExtensions.GetCompletionPercentage(AvailableUpdates);

        /// <summary>
        /// Gets a list of active (pending) update flags.
        /// </summary>
        public IReadOnlyList<string> PendingUpdates => SecureBootUpdateFlagsExtensions.GetActiveFlags(AvailableUpdates);

        /// <summary>
        /// Gets detailed information about each deployment step.
        /// </summary>
        public IReadOnlyList<SecureBootUpdateStepInfo> UpdateSteps => SecureBootUpdateFlagsExtensions.GetUpdateSteps(AvailableUpdates);

    }

    public sealed class SecureBootSbatRegistrySnapshot
    {
        // Registry path WITHOUT HKEY_LOCAL_MACHINE prefix (used with Registry.LocalMachine.OpenSubKey)
        public const string RegistryRootPath = "SYSTEM\\CurrentControlSet\\Control\\SecureBoot\\SBAT";

        /// <summary>
        /// Secure Boot Attestation (SBAT) Key.
        /// This key is used to indicate the SBAT version and is used for telemetry purposes.
        /// </summary>
        public byte[]? SbatLevel { get; set; }

        /// <summary>
        /// Update status telemetry value associated with SBAT processing.
        /// </summary>
        public uint? UpdateStatus { get; set; }

        public DateTimeOffset CollectedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    }

    public sealed class SecureBootStateRegistrySnapshot
    {
        // Registry path WITHOUT HKEY_LOCAL_MACHINE prefix (used with Registry.LocalMachine.OpenSubKey)
        public const string RegistryRootPath = "SYSTEM\\CurrentControlSet\\Control\\SecureBoot\\State";

        /// <summary>
        /// \SecureBoot\State PolicyPublisher key.
        /// No documentation available.
        /// </summary>
        public string? PolicyPublisher { get; set; }

        /// <summary>
        /// \SecureBoot\State PolicyVersion key.
        /// No documentation available.
        /// </summary>
        public uint? PolicyVersion { get; set; }

        /// <summary>
        /// \SecureBoot\State UEFISecureBootEnabled key.
        /// Indicates whether UEFI Secure Boot is enabled.
        /// </summary>
        public bool? UEFISecureBootEnabled { get; set; }

        public DateTimeOffset CollectedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    }



    public sealed class SecureBootServicingRegistrySnapshot
    {
        // Registry path WITHOUT HKEY_LOCAL_MACHINE prefix (used with Registry.LocalMachine.OpenSubKey)
        public const string RegistryRootPath = "SYSTEM\\CurrentControlSet\\Control\\SecureBoot\\Servicing";

        // #### Servicing Key
        /// <summary>
        /// Hash value used for telemetry and device profile tracking.
        /// Indicates device configuration signature for Microsoft tracking purposes.
        /// </summary>
        public string? BucketHash { get; set; }

        /// <summary>
        /// Microsoft's assessment of firmware confidence for supporting Secure Boot updates.
        /// Values: "High", "Medium", "Low", or empty string (not assessed).
        /// </summary>
        public string? ConfidenceLevel { get; set; }

        /// <summary>
        /// \SecureBoot\Servicing UefiCa2023Status key
        /// Deployment status indicator.
        /// Reflects the current state of the Secure Boot key update on the device.It will be set to one of the following text values:
        /// - NotStarted: The update has not yet run.
        /// - Updated: The update has completed successfully
        /// - InProgress: The update is currently running.
        /// 
        /// Initially the status is NotStarted. It changes to InProgress once the update begins, 
        /// and finally to Updated when all new keys and the new boot manager have been deployed. 
        /// If there is an error, then the UEFICA2023Error registry value is set to a non-zero code.
        /// </summary>
        public SecureBootDeploymentState? UefiCa2023Status { get; set; } = SecureBootDeploymentState.NotStarted;

        /// <summary>
        ///  \SecureBoot\Servicing UefiCa2023Error key
        ///  Error code (if any).
        ///  This value remains 0 on success. If the update process encounters a fault,
        ///  UEFICA2023Error is set to a non-zero error code corresponding to the first error encountered.
        ///  An error here implies the Secure Boot update did not fully succeed and may require investigation or remediation on that device.
        ///  For example, if updating the DB (database of trusted signatures) failed due to a firmware issue,
        ///  this registry key might show an error code from the firmware.When this key exists and is non-zero,
        ///  we recommend that you look for Secure Boot events in the Windows Event Logs - see Secure Boot DB and DBX variable update eventsfor more details.
        /// </summary>
        public uint? UefiCa2023Error { get; set; }

        /// <summary>
        /// \SecureBoot\Servicing WindowsUEFICA2023Capable key
        /// This registry key is intended for limited deployment scenarios and is not recommended for general use.
        /// For most cases, use the UEFICA2023Status registry key instead.
        /// Valid values:
        /// 0 – or key does not exist - “Windows UEFI CA 2023” certificate is not in the DB
        /// 1 - “Windows UEFI CA 2023” certificate is in the DB
        /// 2 - “Windows UEFI CA 2023” certificate is in the DB and the system is starting from the 2023 signed boot manager ​​​​​​​
        /// </summary>
        public uint? WindowsUEFICA2023Capable { get; set; }

        public uint? RebootRequestedKEK { get; set; }   
        public uint? RebootRequestedDB { get; set; }
        public uint? RebootRequestedDBX { get; set; }


        public DateTimeOffset CollectedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    }

    public sealed class SecureBootDeviceAttributesRegistrySnapshot
    {
        // Registry path WITHOUT HKEY_LOCAL_MACHINE prefix (used with Registry.LocalMachine.OpenSubKey)
        public const string RegistryRootPath = "SYSTEM\\CurrentControlSet\\Control\\SecureBoot\\Servicing\\DeviceAttributes";

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

    /// <summary>
    /// Captures telemetry settings that affect eligibility for Microsoft Controlled Feature Rollout (CFR).
    /// </summary>
    public sealed class TelemetryPolicySnapshot
    {
        // Registry path WITHOUT HKEY_LOCAL_MACHINE prefix (used with Registry.LocalMachine.OpenSubKey)
        public const string RegistryRootPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\DataCollection";

        /// <summary>
        /// Telemetry level: 0=Security (Enterprise/Education/Server only), 1=Basic, 2=Enhanced, 3=Full.
        /// Required and Optional diagnostic data participation requires level  (Basic) or higher.
        /// </summary>
        public uint? AllowTelemetry { get; set; }

        public DateTimeOffset CollectedAtUtc { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Gets a human-readable description of the telemetry level.
        /// </summary>
        public string TelemetryLevelDescription
        {
            get
            {
                if (!AllowTelemetry.HasValue)
                    return "Unknown";

                return AllowTelemetry.Value switch
                {
                    0 => "Security (Enterprise/Education/Server only)",
                    1 => "Basic",
                    2 => "Enhanced",
                    3 => "Full",
                    _ => $"Unknown ({AllowTelemetry.Value})"
                };
            }
        }

        /// <summary>
        /// Indicates if the device meets the minimum telemetry requirement for CFR eligibility.
        /// CFR requires Security (0) or higher telemetry level.
        /// </summary>
        public bool MeetsCfrTelemetryRequirement => AllowTelemetry.HasValue && AllowTelemetry.Value >= 0;
    }
}
