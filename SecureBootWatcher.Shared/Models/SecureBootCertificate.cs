using System;
using System.Text.Json.Serialization;

namespace SecureBootWatcher.Shared.Models
{
    /// <summary>
    /// Represents a Secure Boot certificate found in UEFI firmware databases.
    /// </summary>
    public sealed class SecureBootCertificate
    {
        /// <summary>
        /// The UEFI database where this certificate was found (db, dbx, KEK, PK).
        /// </summary>
        [JsonPropertyName("database")]
        public string Database { get; set; } = string.Empty;

        /// <summary>
        /// Certificate thumbprint (SHA-1 hash).
        /// </summary>
        [JsonPropertyName("thumbprint")]
        public string? Thumbprint { get; set; }

        /// <summary>
        /// Certificate subject name.
        /// </summary>
        [JsonPropertyName("subject")]
        public string? Subject { get; set; }

        /// <summary>
        /// Certificate issuer name.
        /// </summary>
        [JsonPropertyName("issuer")]
        public string? Issuer { get; set; }

        /// <summary>
        /// Certificate serial number.
        /// </summary>
        [JsonPropertyName("serialNumber")]
        public string? SerialNumber { get; set; }

        /// <summary>
        /// Certificate not valid before date.
        /// </summary>
        [JsonPropertyName("notBefore")]
        public DateTimeOffset? NotBefore { get; set; }

        /// <summary>
        /// Certificate not valid after date (expiration).
        /// </summary>
        [JsonPropertyName("notAfter")]
        public DateTimeOffset? NotAfter { get; set; }

        /// <summary>
        /// Signature algorithm used.
        /// </summary>
        [JsonPropertyName("signatureAlgorithm")]
        public string? SignatureAlgorithm { get; set; }

        /// <summary>
        /// Public key algorithm.
        /// </summary>
        [JsonPropertyName("publicKeyAlgorithm")]
        public string? PublicKeyAlgorithm { get; set; }

        /// <summary>
        /// Key size in bits.
        /// </summary>
        [JsonPropertyName("keySize")]
        public int? KeySize { get; set; }

        /// <summary>
        /// Indicates if this certificate is expired.
        /// </summary>
        [JsonPropertyName("isExpired")]
        public bool IsExpired { get; set; }

        /// <summary>
        /// Days until expiration (negative if expired).
        /// </summary>
        [JsonPropertyName("daysUntilExpiration")]
        public int? DaysUntilExpiration { get; set; }

        /// <summary>
        /// Certificate version.
        /// </summary>
        [JsonPropertyName("version")]
        public int? Version { get; set; }

        /// <summary>
        /// Indicates if this is a Microsoft certificate.
        /// </summary>
        [JsonPropertyName("isMicrosoftCertificate")]
        public bool IsMicrosoftCertificate { get; set; }

        /// <summary>
        /// Raw certificate data (base64 encoded DER).
        /// </summary>
        [JsonPropertyName("rawData")]
        public string? RawData { get; set; }
    }
}
