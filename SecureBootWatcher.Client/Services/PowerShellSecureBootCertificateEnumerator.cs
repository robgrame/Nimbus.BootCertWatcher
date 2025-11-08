using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
            var collection = new SecureBootCertificateCollection
  {
       CollectedAtUtc = DateTimeOffset.UtcNow
      };

       try
       {
           // Check if Secure Boot is enabled
         collection.SecureBootEnabled = await CheckSecureBootEnabledAsync(cancellationToken);

    if (collection.SecureBootEnabled != true)
       {
    _logger.LogInformation("Secure Boot is not enabled on this device. Certificate enumeration skipped.");
      collection.ErrorMessage = "Secure Boot is not enabled";
            return collection;
                }

          // Enumerate each database
            await EnumerateDatabaseAsync("db", collection.SignatureDatabase, cancellationToken);
                await EnumerateDatabaseAsync("dbx", collection.ForbiddenDatabase, cancellationToken);
    await EnumerateDatabaseAsync("KEK", collection.KeyExchangeKeys, cancellationToken);
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
            catch (Exception ex)
      {
             _logger.LogError(ex, "Failed to enumerate Secure Boot certificates");
              collection.ErrorMessage = ex.Message;
            }

  return collection;
        }

        private async Task<bool?> CheckSecureBootEnabledAsync(CancellationToken cancellationToken)
    {
       try
            {
              var script = "Confirm-SecureBootUEFI";
    var result = await ExecutePowerShellAsync(script, cancellationToken);

      if (result.IndexOf("True", StringComparison.OrdinalIgnoreCase) >= 0)
    {
           return true;
        }
                else if (result.IndexOf("False", StringComparison.OrdinalIgnoreCase) >= 0)
         {
        return false;
        }

  return null;
            }
         catch (Exception ex)
            {
          _logger.LogWarning(ex, "Failed to check Secure Boot enabled state via PowerShell");
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
                // Get the UEFI variable bytes
     var script = $@"
     try {{
          $bytes = (Get-SecureBootUEFI -Name {databaseName}).Bytes
               if ($bytes) {{
     [Convert]::ToBase64String($bytes)
    }}
    }} catch {{
            Write-Error $_.Exception.Message
             }}
       ";

       var base64Data = await ExecutePowerShellAsync(script, cancellationToken);

   if (string.IsNullOrWhiteSpace(base64Data) || base64Data.IndexOf("Error", StringComparison.OrdinalIgnoreCase) >= 0)
    {
    _logger.LogDebug("No data returned for {Database}", databaseName);
 return;
  }

    // Convert from base64
       var rawData = Convert.FromBase64String(base64Data.Trim());

         // Parse the EFI signature list format
       var certificates = ParseEfiSignatureList(rawData, databaseName);

         foreach (var cert in certificates)
           {
       targetList.Add(cert);
   }

 _logger.LogDebug("Found {Count} certificates in {Database}", certificates.Count, databaseName);
    }
            catch (Exception ex)
      {
           _logger.LogWarning(ex, "Failed to enumerate {Database} via PowerShell", databaseName);
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

  private async Task<string> ExecutePowerShellAsync(string script, CancellationToken cancellationToken)
        {
        var startInfo = new ProcessStartInfo
        {
  FileName = "powershell.exe",
 Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{script.Replace("\"", "\\\"")}\"",
             RedirectStandardOutput = true,
                RedirectStandardError = true,
         UseShellExecute = false,
                CreateNoWindow = true
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

    process.Start();
  process.BeginOutputReadLine();
          process.BeginErrorReadLine();

   await Task.Run(() => process.WaitForExit(), cancellationToken);

     if (error.Length > 0)
            {
              _logger.LogDebug("PowerShell stderr: {Error}", error.ToString());
}

       return output.ToString().Trim();
        }
    }
}
