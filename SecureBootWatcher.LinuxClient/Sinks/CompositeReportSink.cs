using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SecureBootWatcher.Shared.Models;

namespace SecureBootWatcher.LinuxClient.Sinks
{
    internal sealed class CompositeReportSink : IReportSink
    {
        private readonly IReadOnlyCollection<IReportSink> _sinks;

        public CompositeReportSink(IReadOnlyCollection<IReportSink> sinks)
        {
            _sinks = sinks;
        }

        public async Task EmitAsync(SecureBootStatusReport report, CancellationToken cancellationToken)
        {
            foreach (var sink in _sinks)
            {
                await sink.EmitAsync(report, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
