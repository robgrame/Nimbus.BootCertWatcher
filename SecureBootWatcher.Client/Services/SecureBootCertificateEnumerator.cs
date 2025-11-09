using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using SecureBootWatcher.Shared.Models;

namespace SecureBootWatcher.Client.Services
{
    /// <summary>
    /// Enumerates Secure Boot certificates from UEFI firmware databases via registry.
    /// </summary>
    internal sealed class SecureBootCertificateEnumerator : ISecureBootCertificateEnumerator
    {
        private readonly ILogger<SecureBootCertificateEnumerator> _logger;

        private const string SecureBootBasePath = "SYSTEM\\CurrentControlSet\\Control\\SecureBoot";
        private const string StateKeyPath = "SYSTEM\\CurrentControlSet\\Control\\SecureBoot\\State";

        public SecureBootCertificateEnumerator(ILogger<SecureBootCertificateEnumerator> logger)
        {
            _logger = logger;
        }

        public Task<SecureBootCertificateCollection> EnumerateAsync(CancellationToken cancellationToken)
        {
            var collection = new SecureBootCertificateCollection
            {
                CollectedAtUtc = DateTimeOffset.UtcNow
            };

            try
            {
                // Check if Secure Boot is enabled
                collection.SecureBootEnabled = IsSecureBootEnabled();

                if (collection.SecureBootEnabled != true)
                {
                    _logger.LogInformation("Secure Boot is not enabled on this device. Certificate enumeration will proceed to inventory firmware databases.");
                }

                // Enumerate certificates from each database
                EnumerateDatabaseCertificates(collection, "db", collection.SignatureDatabase);
                EnumerateDatabaseCertificates(collection, "dbx", collection.ForbiddenDatabase);
                EnumerateDatabaseCertificates(collection, "KEK", collection.KeyExchangeKeys);
                EnumerateDatabaseCertificates(collection, "PK", collection.PlatformKeys);

                // Calculate statistics
                var now = DateTimeOffset.UtcNow;
                var allCerts = collection.SignatureDatabase
                    .Concat(collection.ForbiddenDatabase)
                    .Concat(collection.KeyExchangeKeys)
                    .Concat(collection.PlatformKeys)
                    .ToList();

                collection.ExpiredCertificateCount = allCerts.Count(c => c.IsExpired);
                collection.ExpiringCertificateCount = allCerts.Count(c => !c.IsExpired && c.NotAfter.HasValue && (c.NotAfter.Value - now).TotalDays <= 90);

                _logger.LogInformation(
                      "Enumerated {TotalCount} certificates: db={DbCount}, dbx={DbxCount}, KEK={KekCount}, PK={PkCount}",
                      collection.TotalCertificateCount,
                      collection.SignatureDatabase.Count,
                      collection.ForbiddenDatabase.Count,
                      collection.KeyExchangeKeys.Count,
                      collection.PlatformKeys.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enumerate Secure Boot certificates");
                collection.ErrorMessage = ex.Message;
            }

            return Task.FromResult(collection);
        }

        private bool? IsSecureBootEnabled()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(StateKeyPath, false);
                if (key == null)
                {
                    return null;
                }

                var value = key.GetValue("UEFISecureBootEnabled");
                if (value == null)
                {
                    return null;
                }

                return Convert.ToInt32(value) == 1;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read Secure Boot enabled state");
                return null;
            }
        }

        private void EnumerateDatabaseCertificates(SecureBootCertificateCollection collection, string databaseName, IList<SecureBootCertificate> targetList)
        {
            try
            {
                // Try multiple possible registry paths where certificates might be stored
                var possiblePaths = new[]
                {
                    $"{SecureBootBasePath}\\{databaseName}", $"{SecureBootBasePath}\\Platform\\{databaseName}", $"SYSTEM\\CurrentControlSet\\Control\\Cryptography\\Configuration\\Local\\SSL\\00010002\\{databaseName}"
                };

                foreach (var path in possiblePaths)
                {
                    try
                    {
                        using var key = Registry.LocalMachine.OpenSubKey(path, false);
                        if (key == null)
                        {
                            continue;
                        }

                        // Try to read certificate data from registry values
                        var valueNames = key.GetValueNames();
                        foreach (var valueName in valueNames)
                        {
                            try
                            {
                                var data = key.GetValue(valueName) as byte[];
                                if (data == null || data.Length == 0)
                                {
                                    continue;
                                }

                                var cert = ParseCertificate(data, databaseName);
                                if (cert != null)
                                {
                                    targetList.Add(cert);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogDebug(ex, "Failed to parse certificate from {Path}\\{Value}", path, valueName);
                            }
                        }

                        if (targetList.Count > 0)
                        {
                            break; // Found certificates, no need to check other paths
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to read from registry path: {Path}", path);
                    }
                }

                // If we couldn't find certificates in registry, try WMI as fallback
                if (targetList.Count == 0)
                {
                    EnumerateCertificatesViaWmi(databaseName, targetList);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to enumerate {Database} certificates", databaseName);
            }
        }

        private void EnumerateCertificatesViaWmi(string databaseName, IList<SecureBootCertificate> targetList)
        {
            try
            {
                // Note: This is a placeholder for WMI-based enumeration
                // Real implementation would use Win32_BIOS or custom WMI providers
                // For now, we'll use PowerShell Get-SecureBootUEFI cmdlet approach via process
                _logger.LogDebug("WMI enumeration not yet implemented for {Database}", databaseName);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "WMI enumeration failed for {Database}", databaseName);
            }
        }

        private SecureBootCertificate? ParseCertificate(byte[] data, string databaseName)
        {
            try
            {
                // Try to parse as X509 certificate
                var x509 = new X509Certificate2(data);

                var now = DateTimeOffset.UtcNow;
                var notAfter = x509.NotAfter != DateTime.MinValue ? new DateTimeOffset(x509.NotAfter) : (DateTimeOffset?)null;
                var notBefore = x509.NotBefore != DateTime.MinValue ? new DateTimeOffset(x509.NotBefore) : (DateTimeOffset?)null;

                var daysUntilExpiration = notAfter.HasValue ? (int)(notAfter.Value - now).TotalDays : (int?)null;

                var cert = new SecureBootCertificate
                {
                    Database = databaseName,
                    Thumbprint = x509.Thumbprint,
                    Subject = x509.Subject,
                    Issuer = x509.Issuer,
                    SerialNumber = x509.SerialNumber,
                    NotBefore = notBefore,
                    NotAfter = notAfter,
                    SignatureAlgorithm = x509.SignatureAlgorithm?.FriendlyName,
                    PublicKeyAlgorithm = x509.PublicKey?.Oid?.FriendlyName,
                    KeySize = x509.PublicKey?.Key?.KeySize,
                    IsExpired = notAfter.HasValue && notAfter.Value < now,
                    DaysUntilExpiration = daysUntilExpiration,
                    Version = x509.Version,
                    IsMicrosoftCertificate = IsMicrosoftCert(x509.Subject, x509.Issuer),
                    RawData = Convert.ToBase64String(data)
                };

                x509.Dispose();
                return cert;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to parse certificate from {Database}", databaseName);
                return null;
            }
        }

        private static bool IsMicrosoftCert(string subject, string issuer)
        {
            var microsoftIdentifiers = new[]
           {
                "Microsoft",
                "Windows",
                "UEFI CA",
                "O=Microsoft Corporation"
          };

            var combined = $"{subject} {issuer}".ToUpperInvariant();
            return microsoftIdentifiers.Any(id => combined.Contains(id.ToUpperInvariant()));
        }
    }
}
