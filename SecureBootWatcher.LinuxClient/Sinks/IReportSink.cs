using System.Threading;
using System.Threading.Tasks;
using SecureBootWatcher.Shared.Models;

namespace SecureBootWatcher.LinuxClient.Sinks
{
    internal interface IReportSink
    {
        Task EmitAsync(SecureBootStatusReport report, CancellationToken cancellationToken);
    }
}
