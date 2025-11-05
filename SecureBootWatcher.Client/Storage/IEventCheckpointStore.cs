using System;
using System.Threading;
using System.Threading.Tasks;

namespace SecureBootWatcher.Client.Storage
{
    internal interface IEventCheckpointStore
    {
        Task<DateTimeOffset?> GetLastCheckpointAsync(CancellationToken cancellationToken);

        Task SetCheckpointAsync(DateTimeOffset timestampUtc, CancellationToken cancellationToken);
    }
}
