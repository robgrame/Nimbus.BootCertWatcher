using System;
using System.Collections.Generic;

namespace SecureBootWatcher.Shared.Models
{
    /// <summary>
    /// Captures the state of Secure Boot servicing registry keys at the time of collection.
    /// </summary>
    public sealed class SecureBootRegistrySnapshot
    {
        // Registry path WITHOUT HKEY_LOCAL_MACHINE prefix (used with Registry.LocalMachine.OpenSubKey)
        public const string RegistryRootPath = "SYSTEM\\CurrentControlSet\\Control\\SecureBoot";

        public uint? AvailableUpdates { get; set; }
        public uint? UpdateType { get; set; }
        public bool? HighConfidenceOptOut { get; set; }

        public bool? MicrosoftUpdateManagedOptIn { get; set; } = false;

        // Servicing related keys - CA 2023
        public SecureBootDeploymentState UefiCa2023Status { get; set; } = SecureBootDeploymentState.Unknown;
        public uint? UefiCa2023Error { get; set; }
        public uint? WindowsUEFICA2023CapableCode { get; set; }

        // State related keys
        public string? PolicyPublisher { get; set; }
        public uint? PolicyVersion { get; set; }
        public bool? UEFISecureBootEnabled { get; set; } = false;

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

        /// <summary>
        /// Gets the inferred deployment state based on AvailableUpdates value.
        /// This is more accurate than UefiCa2023Status when AvailableUpdates is present.
        /// </summary>
        public SecureBootDeploymentState InferredDeploymentState
        {
            get
            {
                // If AvailableUpdates is available, use it for more accurate state inference
                if (AvailableUpdates.HasValue)
                {
                    return AvailableUpdates.Value switch
                    {
                        // All updates completed
                        0x0000 => SecureBootDeploymentState.Updated,
                        
                        // Deployment complete (conditional flag remains) - also considered "Updated"
                        0x4000 => SecureBootDeploymentState.Updated,
                        
                        // Initial state - not started
                        0x5944 => SecureBootDeploymentState.NotStarted,
                        
                        // Any other value with pending updates - in progress
                        _ => CompletionPercentage > 0 && CompletionPercentage < 100
                            ? SecureBootDeploymentState.InProgress
                            : SecureBootDeploymentState.Unknown
                    };
                }

                // Fallback to UefiCa2023Status if AvailableUpdates is not present
                // Also check for error conditions
                if (UefiCa2023Error.HasValue && UefiCa2023Error.Value != 0)
                {
                    return SecureBootDeploymentState.Error;
                }

                return UefiCa2023Status;
            }
        }
    }

    public sealed class SecureBootServicingRegistrySnapshot
    {
        // Registry path WITHOUT HKEY_LOCAL_MACHINE prefix (used with Registry.LocalMachine.OpenSubKey)
        public const string RegistryRootPath = "SYSTEM\\CurrentControlSet\\Control\\SecureBoot\\Servicing";
        public string? UefiCa2023StatusRaw { get; set; }
        public uint? UefiCa2023ErrorCode { get; set; }
        public uint? WindowsUEFICA2023CapableCode { get; set; }
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
        /// Required and Optional diagnostic data participation requires level 1 (Basic) or higher.
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
        /// CFR requires Basic (1) or higher telemetry level.
        /// </summary>
        public bool MeetsCfrTelemetryRequirement => AllowTelemetry.HasValue && AllowTelemetry.Value >= 1;
    }
}
