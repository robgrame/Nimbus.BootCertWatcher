using System;
using SecureBootWatcher.Shared.Models;

namespace SecureBootWatcher.Shared.Transport
{
    /// <summary>
    /// Represents the payload persisted to Azure Queue Storage for downstream processing.
    /// </summary>
    public sealed class SecureBootQueueEnvelope
    {
        public string Version { get; set; } = "1.0";

        public string MessageType { get; set; } = "SecureBootStatus";

        public SecureBootStatusReport Report { get; set; } = new SecureBootStatusReport();

        public DateTimeOffset EnqueuedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    }
}
