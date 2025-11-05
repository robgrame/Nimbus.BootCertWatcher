using System;
using System.Collections.Generic;

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
    public IList<SecureBootCertificate> SignatureDatabase { get; set; } = new List<SecureBootCertificate>();

        /// <summary>
        /// Certificates from the forbidden signature database (dbx) - blocked from loading.
        /// </summary>
        public IList<SecureBootCertificate> ForbiddenDatabase { get; set; } = new List<SecureBootCertificate>();

        /// <summary>
        /// Key Exchange Keys (KEK) - authorized to update db and dbx.
    /// </summary>
  public IList<SecureBootCertificate> KeyExchangeKeys { get; set; } = new List<SecureBootCertificate>();

        /// <summary>
  /// Platform Key (PK) - top-level key, owner of the platform.
 /// </summary>
        public IList<SecureBootCertificate> PlatformKeys { get; set; } = new List<SecureBootCertificate>();

        /// <summary>
        /// Timestamp when certificates were enumerated.
        /// </summary>
        public DateTimeOffset CollectedAtUtc { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
   /// Indicates if Secure Boot is currently enabled.
        /// </summary>
   public bool? SecureBootEnabled { get; set; }

        /// <summary>
        /// Total count of all certificates across all databases.
        /// </summary>
public int TotalCertificateCount => 
  SignatureDatabase.Count + 
 ForbiddenDatabase.Count + 
    KeyExchangeKeys.Count + 
   PlatformKeys.Count;

     /// <summary>
  /// Count of expired certificates.
        /// </summary>
        public int ExpiredCertificateCount { get; set; }

      /// <summary>
    /// Count of certificates expiring within 90 days.
        /// </summary>
        public int ExpiringCertificateCount { get; set; }

        /// <summary>
        /// Error message if certificate enumeration failed.
        /// </summary>
     public string? ErrorMessage { get; set; }
    }
}
