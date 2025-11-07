using System.Threading;
using System.Threading.Tasks;
using SecureBootWatcher.Shared.Models;

namespace SecureBootWatcher.Client.Services
{
    internal interface IRegistrySnapshotProvider
    {
        Task<SecureBootRegistrySnapshot> CaptureAsync(CancellationToken cancellationToken);

        Task<SecureBootDeviceAttributesRegistrySnapshot> CaptureDeviceAttributesAsync(CancellationToken cancellationToken); // Added method
    }
}
