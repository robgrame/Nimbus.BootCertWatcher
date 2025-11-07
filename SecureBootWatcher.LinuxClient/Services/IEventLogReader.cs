using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SecureBootWatcher.Shared.Models;

namespace SecureBootWatcher.LinuxClient.Services
{
    internal interface IEventLogReader
    {
        Task<IReadOnlyList<SecureBootEventRecord>> ReadRecentEventsAsync(CancellationToken cancellationToken);
    }
}
