using System.Threading;
using System.Threading.Tasks;
using SecureBootWatcher.Shared.Models;

namespace SecureBootWatcher.LinuxClient.Services
{
    internal interface IReportBuilder
    {
        Task<SecureBootStatusReport> BuildAsync(CancellationToken cancellationToken);
    }
}
