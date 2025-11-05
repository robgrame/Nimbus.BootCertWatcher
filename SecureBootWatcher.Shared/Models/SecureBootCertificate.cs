using System;

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
        public string Database { get; set; } = string.Empty;

        /// <summary>
  /// Certificate thumbprint (SHA-1 hash).
      /// </summary>
      public string? Thumbprint { get; set; }

        /// <summary>
        /// Certificate subject name.
   /// </summary>
        public string? Subject { get; set; }

     /// <summary>
        /// Certificate issuer name.
 /// </summary>
        public string? Issuer { get; set; }

        /// <summary>
        /// Certificate serial number.
 /// </summary>
        public string? SerialNumber { get; set; }

    /// <summary>
        /// Certificate not valid before date.
        /// </summary>
        public DateTimeOffset? NotBefore { get; set; }

        /// <summary>
        /// Certificate not valid after date (expiration).
 /// </summary>
 public DateTimeOffset? NotAfter { get; set; }

        /// <summary>
   /// Signature algorithm used.
        /// </summary>
    public string? SignatureAlgorithm { get; set; }

        /// <summary>
    /// Public key algorithm.
        /// </summary>
   public string? PublicKeyAlgorithm { get; set; }

      /// <summary>
    /// Key size in bits.
      /// </summary>
    public int? KeySize { get; set; }

    /// <summary>
        /// Indicates if this certificate is expired.
        /// </summary>
        public bool IsExpired { get; set; }

        /// <summary>
        /// Days until expiration (negative if expired).
        /// </summary>
 public int? DaysUntilExpiration { get; set; }

        /// <summary>
        /// Certificate version.
 /// </summary>
        public int? Version { get; set; }

/// <summary>
  /// Indicates if this is a Microsoft certificate.
      /// </summary>
        public bool IsMicrosoftCertificate { get; set; }

        /// <summary>
     /// Raw certificate data (base64 encoded DER).
        /// </summary>
        public string? RawData { get; set; }
    }
}
