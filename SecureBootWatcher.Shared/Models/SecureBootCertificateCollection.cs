using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SecureBootWatcher.Shared.Models
{
    /// <summary>
    /// Represents the collection of Secure Boot certificates enumerated from UEFI firmware databases.
    /// </summary>
    public sealed class SecureBootCertificateCollection
    {
        /// <summary>
        /// Certificates from the signature database (db) - authorized to load.
        /// </summary>
        [JsonPropertyName("signatureDatabase")]
        public IList<SecureBootCertificate> SignatureDatabase { get; set; } = new List<SecureBootCertificate>();

        /// <summary>
        /// Certificates from the forbidden signature database (dbx) - blocked from loading.
        /// </summary>
        [JsonPropertyName("forbiddenDatabase")]
        public IList<SecureBootCertificate> ForbiddenDatabase { get; set; } = new List<SecureBootCertificate>();

        /// <summary>
        /// Key Exchange Keys (KEK) - authorized to update db and dbx.
        /// </summary>
        [JsonPropertyName("keyExchangeKeys")]
        public IList<SecureBootCertificate> KeyExchangeKeys { get; set; } = new List<SecureBootCertificate>();

        /// <summary>
        /// Platform Key (PK) - top-level key, owner of the platform.
        /// </summary>
        [JsonPropertyName("platformKeys")]
        public IList<SecureBootCertificate> PlatformKeys { get; set; } = new List<SecureBootCertificate>();

        /// <summary>
        /// Timestamp when certificates were enumerated.
        /// </summary>
        [JsonPropertyName("collectedAtUtc")]
        public DateTimeOffset CollectedAtUtc { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Indicates if Secure Boot is currently enabled.
        /// </summary>
        [JsonPropertyName("secureBootEnabled")]
        public bool? SecureBootEnabled { get; set; }

        /// <summary>
        /// Total count of all certificates across all databases.
        /// </summary>
        [JsonPropertyName("totalCertificateCount")]
        public int TotalCertificateCount => 
            SignatureDatabase.Count + 
            ForbiddenDatabase.Count + 
            KeyExchangeKeys.Count + 
            PlatformKeys.Count;

        /// <summary>
        /// Count of expired certificates.
        /// </summary>
        [JsonPropertyName("expiredCertificateCount")]
        public int ExpiredCertificateCount { get; set; }

        /// <summary>
        /// Count of certificates expiring within 90 days.
        /// </summary>
        [JsonPropertyName("expiringCertificateCount")]
        public int ExpiringCertificateCount { get; set; }

        /// <summary>
        /// Error message if certificate enumeration failed.
        /// </summary>
        [JsonPropertyName("errorMessage")]
        public string? ErrorMessage { get; set; }
    }
}
