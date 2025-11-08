using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SecureBootWatcher.Shared.Models;

namespace SecureBootWatcher.LinuxClient.Services
{
    /// <summary>
    /// Enumerates Secure Boot certificates on Linux using efivar tools or direct reading from /sys/firmware/efi/efivars
    /// </summary>
    internal sealed class LinuxSecureBootCertificateEnumerator : ISecureBootCertificateEnumerator
    {
        private readonly ILogger<LinuxSecureBootCertificateEnumerator> _logger;
        private const string EfiVarsPath = "/sys/firmware/efi/efivars";

        public LinuxSecureBootCertificateEnumerator(ILogger<LinuxSecureBootCertificateEnumerator> logger)
        {
            _logger = logger;
        }

        public async Task<SecureBootCertificateCollection> EnumerateAsync(CancellationToken cancellationToken)
        {
            var collection = new SecureBootCertificateCollection
            {
                CollectedAtUtc = DateTimeOffset.UtcNow
            };

            try
            {
                // Check if Secure Boot is enabled
                collection.SecureBootEnabled = await CheckSecureBootEnabledAsync(cancellationToken).ConfigureAwait(false);

                if (collection.SecureBootEnabled != true)
                {
                    _logger.LogWarning("Secure Boot is not enabled on this device.");
                    collection.ErrorMessage = "Secure Boot is not enabled";
                    return collection;
                }

                // Enumerate each database
                await EnumerateDatabaseAsync("db", collection.SignatureDatabase, cancellationToken).ConfigureAwait(false);
                await EnumerateDatabaseAsync("dbx", collection.ForbiddenDatabase, cancellationToken).ConfigureAwait(false);
                await EnumerateDatabaseAsync("KEK", collection.KeyExchangeKeys, cancellationToken).ConfigureAwait(false);
                await EnumerateDatabaseAsync("PK", collection.PlatformKeys, cancellationToken).ConfigureAwait(false);

                // Calculate statistics
                var now = DateTimeOffset.UtcNow;
                var allCerts = collection.SignatureDatabase
                    .Concat(collection.ForbiddenDatabase)
                    .Concat(collection.KeyExchangeKeys)
                    .Concat(collection.PlatformKeys)
                    .ToList();

                collection.ExpiredCertificateCount = allCerts.Count(c => c.IsExpired);
                collection.ExpiringCertificateCount = allCerts.Count(c =>
                    !c.IsExpired &&
                    c.NotAfter.HasValue &&
                    (c.NotAfter.Value - now).TotalDays <= 90);

                _logger.LogInformation(
                    "Enumerated {TotalCount} certificates: db={DbCount}, dbx={DbxCount}, KEK={KekCount}, PK={PkCount}, Expired={ExpiredCount}",
                    collection.TotalCertificateCount,
                    collection.SignatureDatabase.Count,
                    collection.ForbiddenDatabase.Count,
                    collection.KeyExchangeKeys.Count,
                    collection.PlatformKeys.Count,
                    collection.ExpiredCertificateCount);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Access denied while enumerating Secure Boot certificates. Try running with elevated permissions.");
                collection.ErrorMessage = ex.Message;
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "I/O error while enumerating Secure Boot certificates");
                collection.ErrorMessage = ex.Message;
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Invalid operation while enumerating Secure Boot certificates");
                collection.ErrorMessage = ex.Message;
            }
            // Optionally, catch other expected exceptions here
            // If you want to catch truly unexpected exceptions, uncomment below:
            // catch (Exception ex) when (!(ex is OutOfMemoryException || ex is StackOverflowException || ex is ThreadAbortException))
            // {
            //     _logger.LogError(ex, "Unexpected error while enumerating Secure Boot certificates");
            //     collection.ErrorMessage = ex.Message;
            // }

            return collection;
        }

        private async Task<bool?> CheckSecureBootEnabledAsync(CancellationToken cancellationToken)
        {
            try
            {
                // On Linux, check /sys/firmware/efi/efivars/SecureBoot-*
                var secureBootFiles = Directory.GetFiles(EfiVarsPath, "SecureBoot-*");
                
                if (secureBootFiles.Length > 0)
                {
                    var data = await File.ReadAllBytesAsync(secureBootFiles[0], cancellationToken).ConfigureAwait(false);
                    // Skip first 4 bytes (attributes), check if the value byte is 1
                    if (data.Length >= 5)
                    {
                        return data[4] == 1;
                    }
                }

                // Alternative: use mokutil if available
                return await CheckSecureBootWithMokutilAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Failed to check Secure Boot enabled state. Try running with sudo.");
                return null;
            }
            // Let other exceptions propagate
        }

        private async Task<bool?> CheckSecureBootWithMokutilAsync(CancellationToken cancellationToken)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "mokutil",
                    Arguments = "--sb-state",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = startInfo };
                process.Start();

                var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

                if (output.Contains("SecureBoot enabled", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                else if (output.Contains("SecureBoot disabled", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                return null;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // mokutil not found
                _logger.LogDebug("mokutil command not found");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to check Secure Boot with mokutil");
                return null;
            }
        }

        private async Task EnumerateDatabaseAsync(
            string databaseName,
            IList<SecureBootCertificate> targetList,
            CancellationToken cancellationToken)
        {
            try
            {
                // Try to read EFI variable directly from /sys/firmware/efi/efivars
                var varPattern = $"{databaseName}-*";
                var varFiles = Directory.GetFiles(EfiVarsPath, varPattern);

                if (varFiles.Length == 0)
                {
                    _logger.LogDebug("No EFI variable found for {Database}", databaseName);
                    return;
                }

                foreach (var varFile in varFiles)
                {
                    try
                    {
                        var rawData = await File.ReadAllBytesAsync(varFile, cancellationToken).ConfigureAwait(false);
                        
                        // Skip first 4 bytes (EFI variable attributes)
                        if (rawData.Length <= 4)
                        {
                            _logger.LogDebug("EFI variable {File} too small", varFile);
                            continue;
                        }

                        var data = rawData.Skip(4).ToArray();
                        
                        // Parse the EFI signature list format
                        var certificates = ParseEfiSignatureList(data, databaseName);
                        
                        foreach (var cert in certificates)
                        {
                            targetList.Add(cert);
                        }

                        _logger.LogDebug("Found {Count} certificates in {Database} from {File}", 
                            certificates.Count, databaseName, Path.GetFileName(varFile));
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        _logger.LogWarning(ex, "Access denied reading {File}. Try running with sudo.", varFile);
                    }
                    catch (IOException ex)
                    {
                        _logger.LogDebug(ex, "I/O error reading {File}", varFile);
                    }
                    catch (FileNotFoundException ex)
                    {
                        _logger.LogDebug(ex, "File not found: {File}", varFile);
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Access denied accessing EFI variables for {Database}. Try running with sudo.", databaseName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to enumerate {Database}", databaseName);
            }
        }

        private List<SecureBootCertificate> ParseEfiSignatureList(byte[] data, string databaseName)
        {
            var certificates = new List<SecureBootCertificate>();

            try
            {
                // EFI_SIGNATURE_LIST structure (same format as Windows):
                // GUID SignatureType (16 bytes)
                // UINT32 SignatureListSize (4 bytes)
                // UINT32 SignatureHeaderSize (4 bytes)
                // UINT32 SignatureSize (4 bytes)
                // SignatureHeader (SignatureHeaderSize bytes)
                // Signatures[] (array of SignatureSize entries)

                int offset = 0;

                while (offset + 28 <= data.Length) // Minimum header size
                {
                    // Read signature type GUID
                    var signatureTypeGuid = new Guid(data.Skip(offset).Take(16).ToArray());
                    offset += 16;

                    // Read list size
                    var signatureListSize = BitConverter.ToUInt32(data, offset);
                    offset += 4;

                    // Read header size
                    var signatureHeaderSize = BitConverter.ToUInt32(data, offset);
                    offset += 4;

                    // Read signature size
                    var signatureSize = BitConverter.ToUInt32(data, offset);
                    offset += 4;

                    // Skip signature header
                    offset += (int)signatureHeaderSize;

                    // Calculate number of signatures
                    var remainingSize = signatureListSize - 28 - signatureHeaderSize;
                    var signatureCount = signatureSize > 0 ? remainingSize / signatureSize : 0;

                    // Parse each signature
                    for (int i = 0; i < signatureCount && offset < data.Length; i++)
                    {
                        if (offset + signatureSize > data.Length)
                            break;

                        // EFI_SIGNATURE_DATA structure:
                        // GUID SignatureOwner (16 bytes)
                        // UINT8 SignatureData[]

                        // Skip SignatureOwner (16 bytes)
                        offset += 16;

                        var certDataSize = (int)signatureSize - 16;
                        if (certDataSize > 0 && offset + certDataSize <= data.Length)
                        {
                            var certData = data.Skip(offset).Take(certDataSize).ToArray();
                            offset += certDataSize;

                            try
                            {
                                var cert = ParseCertificate(certData, databaseName, signatureTypeGuid);
                                if (cert != null)
                                {
                                    certificates.Add(cert);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogDebug(ex, "Failed to parse individual certificate in {Database}", databaseName);
                            }
                        }
                        else
                        {
                            break;
                        }
                    }

                    // Move to next signature list (if any)
                    if (signatureListSize == 0)
                        break;
                }
            }
            catch (Exception ex) when (
                !(ex is OutOfMemoryException) &&
                !(ex is StackOverflowException) &&
                !(ex is ThreadAbortException))
            {
                _logger.LogWarning(ex, "Failed to parse EFI signature list for {Database}", databaseName);
            }

            return certificates;
        }

        private SecureBootCertificate? ParseCertificate(byte[] certData, string databaseName, Guid signatureType)
        {
            try
            {
                // Check if this is X509 certificate (EFI_CERT_X509_GUID)
                var x509Guid = new Guid("a5c059a1-94e4-4aa7-87b5-ab155c2bf072");

                if (signatureType != x509Guid)
                {
                    // This might be a hash or other signature type, not a full certificate
                    _logger.LogDebug("Skipping non-X509 signature type {Type} in {Database}", signatureType, databaseName);
                    return null;
                }

                var x509 = new X509Certificate2(certData);

                var now = DateTimeOffset.UtcNow;
                var notAfter = x509.NotAfter != DateTime.MinValue
                    ? new DateTimeOffset(x509.NotAfter)
                    : (DateTimeOffset?)null;
                var notBefore = x509.NotBefore != DateTime.MinValue
                    ? new DateTimeOffset(x509.NotBefore)
                    : (DateTimeOffset?)null;

                var daysUntilExpiration = notAfter.HasValue
                    ? (int)(notAfter.Value - now).TotalDays
                    : (int?)null;

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
                    RawData = Convert.ToBase64String(certData)
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
