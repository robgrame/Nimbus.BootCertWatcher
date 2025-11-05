using System.Threading;
using System.Threading.Tasks;
using SecureBootWatcher.Shared.Models;

namespace SecureBootWatcher.Client.Services
{
    /// <summary>
    /// Service for enumerating Secure Boot certificates from UEFI firmware databases.
  /// </summary>
    internal interface ISecureBootCertificateEnumerator
    {
        /// <summary>
        /// Enumerates all Secure Boot certificates from UEFI firmware.
  /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Collection of certificates organized by database type.</returns>
      Task<SecureBootCertificateCollection> EnumerateAsync(CancellationToken cancellationToken);
    }
}
