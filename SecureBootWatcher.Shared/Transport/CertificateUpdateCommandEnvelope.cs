using System;
using SecureBootWatcher.Shared.Models;

namespace SecureBootWatcher.Shared.Transport
{
    /// <summary>
    /// Represents the payload for certificate update commands in Azure Queue Storage.
    /// </summary>
    public sealed class CertificateUpdateCommandEnvelope
    {
        public string Version { get; set; } = "1.0";

        public string MessageType { get; set; } = "CertificateUpdateCommand";

        public CertificateUpdateCommand Command { get; set; } = new CertificateUpdateCommand();

        public DateTimeOffset EnqueuedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    }
}
