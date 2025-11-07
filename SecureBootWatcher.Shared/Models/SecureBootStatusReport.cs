using System;
using System.Collections.Generic;

namespace SecureBootWatcher.Shared.Models
{
    /// <summary>
    /// Aggregates the Secure Boot telemetry captured by the client for downstream processing.
    /// </summary>
    public sealed class SecureBootStatusReport
    {
        public DeviceIdentity Device { get; set; } = new DeviceIdentity();

        public SecureBootRegistrySnapshot Registry { get; set; } = new SecureBootRegistrySnapshot();

        public SecureBootDeviceAttributesRegistrySnapshot? DeviceAttributes { get; set; } // Added property

        public SecureBootCertificateCollection? Certificates { get; set; }

        public IList<SecureBootEventRecord> Events { get; set; } = new List<SecureBootEventRecord>();

        public IList<string> Alerts { get; set; } = new List<string>();

        public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

        public string? ClientVersion { get; set; }

        public string? CorrelationId { get; set; }
    }
}
