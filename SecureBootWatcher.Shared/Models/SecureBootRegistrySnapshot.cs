using System;

namespace SecureBootWatcher.Shared.Models
{
    /// <summary>
    /// Captures the state of Secure Boot servicing registry keys at the time of collection.
    /// </summary>
    public sealed class SecureBootRegistrySnapshot
    {
        public const string RegistryRootPath = "HKEY_LOCAL_MACHINE\\SYSTEM\\CurrentControlSet\\Control\\SecureBoot\\Servicing";

        public uint? AvailableUpdates { get; set; }

        public string? UefiCa2023StatusRaw { get; set; }

        public uint? UefiCa2023ErrorCode { get; set; }

        public bool? HighConfidenceOptOut { get; set; }

        public bool? MicrosoftUpdateManagedOptIn { get; set; }

        public SecureBootDeploymentState DeploymentState { get; set; } = SecureBootDeploymentState.Unknown;

        public DateTimeOffset CollectedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    }
}
