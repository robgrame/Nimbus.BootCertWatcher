using System.Threading;
using System.Threading.Tasks;
using SecureBootWatcher.Shared.Models;

namespace SecureBootWatcher.Client.Services
{
    internal interface IReportBuilder
    {
        Task<SecureBootStatusReport> BuildAsync(CancellationToken cancellationToken);
    }
}
