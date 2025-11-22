using System;

namespace SecureBootWatcher.Shared.Models
{
    /// <summary>
    /// Represents a command to update device configuration settings.
    /// </summary>
    public class DeviceConfigurationCommand
    {
        /// <summary>
        /// Unique identifier for this command instance.
        /// </summary>
        public Guid CommandId { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Timestamp when the command was created.
        /// </summary>
        public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Type of configuration command to execute.
        /// </summary>
        public DeviceConfigurationType ConfigurationType { get; set; }

        /// <summary>
        /// Optional description or reason for the configuration change.
        /// </summary>
        public string? Description { get; set; }
    }

    /// <summary>
    /// Command to update certificate deployment on a device or group of devices.
    /// </summary>
    public sealed class CertificateUpdateCommand : DeviceConfigurationCommand
    {
        /// <summary>
        /// Type of certificate update to perform (DB, Boot Manager, etc.).
        /// </summary>
        public uint? UpdateType { get; set; }

        /// <summary>
        /// Force immediate update even if conditions are not met.
        /// </summary>
        public bool ForceUpdate { get; set; }

        public CertificateUpdateCommand()
        {
            ConfigurationType = DeviceConfigurationType.CertificateUpdate;
        }
    }

    /// <summary>
    /// Command to configure Microsoft Update Managed Opt-In status for CFR eligibility.
    /// </summary>
    public sealed class MicrosoftUpdateOptInCommand : DeviceConfigurationCommand
    {
        /// <summary>
        /// Enable (true) or disable (false) Microsoft Update Managed Opt-In.
        /// When enabled, device becomes eligible for Controlled Feature Rollout if telemetry requirements are met.
        /// </summary>
        public bool OptIn { get; set; }

        public MicrosoftUpdateOptInCommand()
        {
            ConfigurationType = DeviceConfigurationType.MicrosoftUpdateOptIn;
        }
    }

    /// <summary>
    /// Command to validate and optionally set telemetry level for CFR eligibility.
    /// </summary>
    public sealed class TelemetryConfigurationCommand : DeviceConfigurationCommand
    {
        /// <summary>
        /// Required minimum telemetry level: 0=Security (Enterprise only), 1=Basic, 2=Enhanced, 3=Full.
        /// For CFR eligibility, level must be 1 (Basic) or higher.
        /// </summary>
        public uint RequiredTelemetryLevel { get; set; } = 1;

        /// <summary>
        /// If true, command will only validate current telemetry level without changing it.
        /// If false, command will attempt to set the telemetry level to RequiredTelemetryLevel.
        /// </summary>
        public bool ValidateOnly { get; set; }

        public TelemetryConfigurationCommand()
        {
            ConfigurationType = DeviceConfigurationType.TelemetryConfiguration;
        }
    }

    /// <summary>
    /// Types of device configuration commands.
    /// </summary>
    public enum DeviceConfigurationType
    {
        /// <summary>
        /// Command to update certificates (DB, Boot Manager, etc.).
        /// </summary>
        CertificateUpdate,

        /// <summary>
        /// Command to configure Microsoft Update Managed Opt-In status.
        /// </summary>
        MicrosoftUpdateOptIn,

        /// <summary>
        /// Command to validate or configure telemetry level settings.
        /// </summary>
        TelemetryConfiguration
    }

    /// <summary>
    /// Result of executing a device configuration command.
    /// </summary>
    public sealed class DeviceConfigurationResult
    {
        /// <summary>
        /// The command ID that was executed.
        /// </summary>
        public Guid CommandId { get; set; }

        /// <summary>
        /// Device ID that received the command.
        /// </summary>
        public Guid DeviceId { get; set; }

        /// <summary>
        /// Indicates if the command was successfully queued/accepted.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Human-readable message describing the result.
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// Timestamp when the result was generated.
        /// </summary>
        public DateTimeOffset ResultTimestampUtc { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Current configuration state after command execution (optional).
        /// </summary>
        public DeviceConfigurationState? CurrentState { get; set; }
    }

    /// <summary>
    /// Represents the current configuration state of a device.
    /// </summary>
    public sealed class DeviceConfigurationState
    {
        /// <summary>
        /// Current Microsoft Update Managed Opt-In status.
        /// </summary>
        public bool? MicrosoftUpdateManagedOptIn { get; set; }

        /// <summary>
        /// Current telemetry level: 0=Security, 1=Basic, 2=Enhanced, 3=Full.
        /// </summary>
        public uint? AllowTelemetry { get; set; }

        /// <summary>
        /// Whether device is capable of Windows UEFI CA 2023 update.
        /// </summary>
        public uint? WindowsUEFICA2023Capable { get; set; }

        /// <summary>
        /// Whether device meets CFR eligibility requirements (telemetry >= 1 and opt-in enabled).
        /// </summary>
        public bool IsCfrEligible => 
            MicrosoftUpdateManagedOptIn == true && 
            AllowTelemetry.HasValue && 
            AllowTelemetry.Value >= 1;

        /// <summary>
        /// Timestamp of the state snapshot.
        /// </summary>
        public DateTimeOffset SnapshotTimestampUtc { get; set; } = DateTimeOffset.UtcNow;
    }
}
