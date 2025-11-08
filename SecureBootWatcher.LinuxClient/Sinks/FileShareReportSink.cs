using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SecureBootWatcher.Shared.Configuration;
using SecureBootWatcher.Shared.Models;

namespace SecureBootWatcher.LinuxClient.Sinks
{
    internal sealed class FileShareReportSink : IReportSink
    {
        private readonly ILogger<FileShareReportSink> _logger;
        private readonly IOptionsMonitor<SecureBootWatcherOptions> _options;

        public FileShareReportSink(ILogger<FileShareReportSink> logger, IOptionsMonitor<SecureBootWatcherOptions> options)
        {
            _logger = logger;
            _options = options;
        }

        public async Task EmitAsync(SecureBootStatusReport report, CancellationToken cancellationToken)
        {
            var options = _options.CurrentValue.Sinks.FileShare;
            if (string.IsNullOrWhiteSpace(options.RootPath))
            {
                _logger.LogDebug("File share sink skipped because RootPath is not configured.");
                return;
            }

            var fileName = BuildFileName(report.CorrelationId, options.AppendTimestampToFileName, options.FileExtension);
            var directory = options.RootPath;

            Directory.CreateDirectory(directory);

            var path = Path.Combine(directory, fileName);
            var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            cancellationToken.ThrowIfCancellationRequested();
            await File.WriteAllTextAsync(path, json, cancellationToken);
            _logger.LogInformation("Secure Boot report persisted to file share at {Path}.", path);
        }

        private static string BuildFileName(string? correlationId, bool appendTimestamp, string extension)
        {
            var name = !string.IsNullOrWhiteSpace(correlationId) ? correlationId : Guid.NewGuid().ToString("N");
            if (appendTimestamp)
            {
                name = $"{DateTimeOffset.UtcNow:yyyyMMddHHmmss}_{name}";
            }

            extension = string.IsNullOrWhiteSpace(extension) ? ".json" : extension;
            if (!extension.StartsWith(".", StringComparison.Ordinal))
            {
                extension = "." + extension;
            }

            return name + extension;
        }
    }
}
