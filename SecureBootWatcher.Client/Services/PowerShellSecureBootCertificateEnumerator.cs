using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SecureBootWatcher.Shared.Models;

namespace SecureBootWatcher.Client.Services
{
    /// <summary>
    /// Enumerates Secure Boot certificates using PowerShell Get-SecureBootUEFI cmdlet.
    /// This is more reliable than direct registry access for UEFI variables.
    /// </summary>
    internal sealed class PowerShellSecureBootCertificateEnumerator : ISecureBootCertificateEnumerator
    {
        private readonly ILogger<PowerShellSecureBootCertificateEnumerator> _logger;

        public PowerShellSecureBootCertificateEnumerator(ILogger<PowerShellSecureBootCertificateEnumerator> logger)
        {
            _logger = logger;
        }

        public async Task<SecureBootCertificateCollection> EnumerateAsync(CancellationToken cancellationToken)
        {
            _logger.LogDebug("PowerShellSecureBootCertificateEnumerator: Starting certificate enumeration");
            
            var collection = new SecureBootCertificateCollection
            {
                CollectedAtUtc = DateTimeOffset.UtcNow
            };

            try
            {
                // Check if Secure Boot is enabled
                _logger.LogTrace("PowerShellSecureBootCertificateEnumerator: Checking Secure Boot enabled status");
                collection.SecureBootEnabled = await CheckSecureBootEnabledAsync(cancellationToken);

                if (collection.SecureBootEnabled != true)
                {
                    _logger.LogInformation("PowerShellSecureBootCertificateEnumerator: Secure Boot is not enabled on this device. Certificate enumeration will proceed to inventory firmware databases.");
                }
                else
                {
                    _logger.LogDebug("PowerShellSecureBootCertificateEnumerator: Secure Boot is enabled");
                }

                // Enumerate each database
                _logger.LogTrace("PowerShellSecureBootCertificateEnumerator: Enumerating db (Signature Database)");
                await EnumerateDatabaseAsync("db", collection.SignatureDatabase, cancellationToken);
                
                _logger.LogTrace("PowerShellSecureBootCertificateEnumerator: Enumerating dbx (Forbidden Database)");
                await EnumerateDatabaseAsync("dbx", collection.ForbiddenDatabase, cancellationToken);
                
                _logger.LogTrace("PowerShellSecureBootCertificateEnumerator: Enumerating KEK (Key Exchange Keys)");
                await EnumerateDatabaseAsync("KEK", collection.KeyExchangeKeys, cancellationToken);
                
                _logger.LogTrace("PowerShellSecureBootCertificateEnumerator: Enumerating PK (Platform Keys)");
                await EnumerateDatabaseAsync("PK", collection.PlatformKeys, cancellationToken);

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
                    (c.NotAfter.Value - now).TotalDays <= 90
                 );

                _logger.LogInformation(
                    "PowerShellSecureBootCertificateEnumerator: Enumerated {TotalCount} certificates: db={DbCount}, dbx={DbxCount}, KEK={KekCount}, PK={PkCount}, Expired={ExpiredCount}, Expiring={ExpiringCount}",
                    collection.TotalCertificateCount,
                    collection.SignatureDatabase.Count,
                    collection.ForbiddenDatabase.Count,
                    collection.KeyExchangeKeys.Count,
                    collection.PlatformKeys.Count,
                    collection.ExpiredCertificateCount,
                    collection.ExpiringCertificateCount);
                    
                _logger.LogDebug("PowerShellSecureBootCertificateEnumerator: Certificate enumeration completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PowerShellSecureBootCertificateEnumerator: Failed to enumerate Secure Boot certificates");
                collection.ErrorMessage = ex.Message;
            }

            return collection;
        }

        private async Task<bool?> CheckSecureBootEnabledAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogTrace("PowerShellSecureBootCertificateEnumerator.CheckSecureBootEnabledAsync: Executing Confirm-SecureBootUEFI");
                var script = "Confirm-SecureBootUEFI";
                var result = await ExecutePowerShellAsync(script, cancellationToken);

                _logger.LogTrace("PowerShellSecureBootCertificateEnumerator.CheckSecureBootEnabledAsync: Result={Result}", result);

                if (result.IndexOf("True", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _logger.LogDebug("PowerShellSecureBootCertificateEnumerator.CheckSecureBootEnabledAsync: Secure Boot is enabled");
                    return true;
                }
                else if (result.IndexOf("False", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _logger.LogDebug("PowerShellSecureBootCertificateEnumerator.CheckSecureBootEnabledAsync: Secure Boot is disabled");
                    return false;
                }

                _logger.LogWarning("PowerShellSecureBootCertificateEnumerator.CheckSecureBootEnabledAsync: Unable to determine Secure Boot status from result: {Result}", result);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PowerShellSecureBootCertificateEnumerator.CheckSecureBootEnabledAsync: Failed to check Secure Boot enabled state via PowerShell");
                return null;
            }
        }

        private async Task EnumerateDatabaseAsync(string databaseName, IList<SecureBootCertificate> targetList, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogDebug("PowerShellSecureBootCertificateEnumerator.EnumerateDatabaseAsync: Retrieving {Database} data via PowerShell", databaseName);
                
                // Get the UEFI variable bytes
                var script = $@"
                    try {{
                        $bytes = (Get-SecureBootUEFI -Name {databaseName}).Bytes
                        if ($bytes) {{
                            [Convert]::ToBase64String($bytes)
                        }}
                    }} catch {{
                        Write-Error $_.Exception.Message
                    }}";

                var base64Data = await ExecutePowerShellAsync(script, cancellationToken);


                if (string.IsNullOrWhiteSpace(base64Data) || base64Data.IndexOf("Error", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _logger.LogDebug("PowerShellSecureBootCertificateEnumerator.EnumerateDatabaseAsync: No data returned for {Database}", databaseName);
                    return;
                }

                _logger.LogTrace("PowerShellSecureBootCertificateEnumerator.EnumerateDatabaseAsync: Retrieved {ByteCount} bytes (base64) for {Database}", 
                    base64Data.Length, databaseName);

                // Convert from base64
                var rawData = Convert.FromBase64String(base64Data.Trim());
                _logger.LogTrace("PowerShellSecureBootCertificateEnumerator.EnumerateDatabaseAsync: Decoded {RawByteCount} raw bytes for {Database}", 
                    rawData.Length, databaseName);

                // Parse the EFI signature list format
                var certificates = ParseEfiSignatureList(rawData, databaseName);

                foreach (var cert in certificates)
                {
                    targetList.Add(cert);
                }

                _logger.LogDebug("PowerShellSecureBootCertificateEnumerator.EnumerateDatabaseAsync: Found {Count} certificates in {Database}", 
                    certificates.Count, databaseName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PowerShellSecureBootCertificateEnumerator.EnumerateDatabaseAsync: Failed to enumerate {Database} via PowerShell", databaseName);
            }
        }

        private List<SecureBootCertificate> ParseEfiSignatureList(byte[] data, string databaseName)
        {
            var certificates = new List<SecureBootCertificate>();

            try
            {
                // EFI_SIGNATURE_LIST structure:
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

                        var signatureOwner = new Guid(data.Skip(offset).Take(16).ToArray());
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
            catch (Exception ex)
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

                // Note: Using old constructor for .NET Framework 4.8 compatibility
                #pragma warning disable SYSLIB0057 // X509Certificate2(byte[]) is obsolete
                var x509 = new X509Certificate2(certData);
                #pragma warning restore SYSLIB0057

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

        private async Task<string> ExecutePowerShellAsync(string script, CancellationToken cancellationToken)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{script.Replace("\"", "\\\"")}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                // Ensure we're running in the correct working directory
                WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System)
            };

            using var process = new Process { StartInfo = startInfo };

            var output = new StringBuilder();
            var error = new StringBuilder();

            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    output.AppendLine(e.Data);
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    error.AppendLine(e.Data);
                }
            };

            try
            {
                _logger.LogDebug("Executing PowerShell script: {Script}", script.Substring(0, Math.Min(100, script.Length)));
                
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Wait for process to exit with cancellation support
                await Task.Run(() =>
                {
                    if (!process.WaitForExit(30000)) // 30 second timeout
                    {
                        _logger.LogWarning("PowerShell process timeout - killing process");
                        try
                        {
                            process.Kill();
                        }
                        catch (Exception killEx)
                        {
                            _logger.LogDebug(killEx, "Failed to kill PowerShell process");
                        }
                    }
                }, cancellationToken);

                // Give async operations time to complete
                await Task.Delay(100, cancellationToken);

                var exitCode = process.ExitCode;
                var outputText = output.ToString().Trim();
                var errorText = error.ToString().Trim();

                // Log detailed execution results
                _logger.LogDebug(
                    "PowerShell execution completed - ExitCode: {ExitCode}, Output length: {OutputLength}, Error length: {ErrorLength}",
                    exitCode,
                    outputText.Length,
                    errorText.Length);

                if (!string.IsNullOrEmpty(errorText))
                {
                    _logger.LogDebug("PowerShell stderr: {Error}", errorText);
                }

                // Check exit code
                if (exitCode != 0)
                {
                    _logger.LogWarning(
                        "PowerShell exited with non-zero code {ExitCode}. Output: {Output}, Error: {Error}",
                        exitCode,
                        outputText.Length > 0 ? outputText.Substring(0, Math.Min(200, outputText.Length)) : "(empty)",
                        errorText.Length > 0 ? errorText.Substring(0, Math.Min(200, errorText.Length)) : "(empty)");
                }

                // Return output even if there were errors (some cmdlets write warnings to stderr)
                return outputText;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute PowerShell script");
                return string.Empty;
            }
        }
    }
}
