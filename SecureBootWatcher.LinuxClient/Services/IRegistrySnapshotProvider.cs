using System.Threading;
using System.Threading.Tasks;
using SecureBootWatcher.Shared.Models;

namespace SecureBootWatcher.LinuxClient.Services
{
    internal interface IRegistrySnapshotProvider
    {
        Task<SecureBootRegistrySnapshot> CaptureAsync(CancellationToken cancellationToken);
    }
}
