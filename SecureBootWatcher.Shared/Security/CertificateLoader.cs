using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;

namespace SecureBootWatcher.Shared.Security
{
    /// <summary>
    /// Utility class for loading X.509 certificates from various sources.
    /// </summary>
    public static class CertificateLoader
    {
        /// <summary>
        /// Loads a client certificate from either the Windows Certificate Store or a file.
        /// </summary>
        /// <param name="thumbprint">Certificate thumbprint (SHA-1, without spaces or colons). Optional if using certificatePath.</param>
        /// <param name="storeLocation">Certificate store location ("LocalMachine" or "CurrentUser"). Default: "LocalMachine"</param>
        /// <param name="storeName">Certificate store name ("My", "Root", "CA", etc.). Default: "My"</param>
        /// <param name="certificatePath">Path to certificate file (.pfx). Optional if using thumbprint.</param>
        /// <param name="certificatePassword">Password for the certificate file. Optional.</param>
        /// <param name="logger">Optional action for logging messages.</param>
        /// <returns>The loaded certificate, or null if not found.</returns>
        public static X509Certificate2? LoadCertificate(
            string? thumbprint,
            string storeLocation = "LocalMachine",
            string storeName = "My",
            string? certificatePath = null,
            string? certificatePassword = null,
            Action<string>? logger = null)
        {
            try
            {
                // Try loading from certificate store first (preferred method)
                if (!string.IsNullOrEmpty(thumbprint))
                {
                    var cert = LoadFromStore(thumbprint, storeLocation, storeName, logger);
                    if (cert != null)
                    {
                        return cert;
                    }
                }

                // Try loading from file path as fallback
                if (!string.IsNullOrEmpty(certificatePath))
                {
                    var cert = LoadFromFile(certificatePath, certificatePassword, logger);
                    if (cert != null)
                    {
                        return cert;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                logger?.Invoke($"Error loading client certificate: {ex.Message}");
                return null;
            }
        }

        private static X509Certificate2? LoadFromStore(
            string thumbprint,
            string storeLocationString,
            string storeNameString,
            Action<string>? logger)
        {
            var location = storeLocationString.Equals("LocalMachine", StringComparison.OrdinalIgnoreCase)
                ? StoreLocation.LocalMachine
                : StoreLocation.CurrentUser;

            var name = Enum.TryParse<StoreName>(storeNameString, true, out var parsedStoreName)
                ? parsedStoreName
                : StoreName.My;

            using (var store = new X509Store(name, location))
            {
                store.Open(OpenFlags.ReadOnly);
                var certs = store.Certificates.Find(
                    X509FindType.FindByThumbprint,
                    thumbprint.Replace(" ", "").Replace(":", ""),
                    validOnly: false);

                if (certs.Count > 0)
                {
                    logger?.Invoke($"Client certificate loaded from store: {location}\\{name}");
                    return certs[0];
                }
                else
                {
                    logger?.Invoke($"Certificate with thumbprint {thumbprint} not found in {location}\\{name}");
                }
            }

            return null;
        }

        private static X509Certificate2? LoadFromFile(
            string certificatePath,
            string? certificatePassword,
            Action<string>? logger)
        {
            if (!File.Exists(certificatePath))
            {
                logger?.Invoke($"Certificate file not found: {certificatePath}");
                return null;
            }

            var cert = string.IsNullOrEmpty(certificatePassword)
                ? new X509Certificate2(certificatePath)
                : new X509Certificate2(certificatePath, certificatePassword);

            logger?.Invoke($"Client certificate loaded from file: {certificatePath}");
            return cert;
        }
    }
}
