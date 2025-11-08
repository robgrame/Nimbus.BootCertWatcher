using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SecureBootWatcher.LinuxClient.Storage;
using SecureBootWatcher.Shared.Configuration;
using SecureBootWatcher.Shared.Models;

namespace SecureBootWatcher.LinuxClient.Services
{
    /// <summary>
    /// Linux implementation that reads Secure Boot events from journald/syslog
    /// </summary>
    internal sealed class LinuxEventLogReader : IEventLogReader
    {
        private readonly ILogger<LinuxEventLogReader> _logger;
        private readonly IOptionsMonitor<SecureBootWatcherOptions> _options;
        private readonly IEventCheckpointStore _checkpointStore;

        public LinuxEventLogReader(
            ILogger<LinuxEventLogReader> logger,
            IOptionsMonitor<SecureBootWatcherOptions> options,
            IEventCheckpointStore checkpointStore)
        {
            _logger = logger;
            _options = options;
            _checkpointStore = checkpointStore;
        }

        public async Task<IReadOnlyList<SecureBootEventRecord>> ReadRecentEventsAsync(CancellationToken cancellationToken)
        {
            var options = _options.CurrentValue;
            var records = new List<SecureBootEventRecord>();
            var since = DateTimeOffset.UtcNow - options.EventLookbackPeriod;

            var checkpoint = await _checkpointStore.GetLastCheckpointAsync(cancellationToken).ConfigureAwait(false);
            if (checkpoint.HasValue && checkpoint.Value > since)
            {
                since = checkpoint.Value;
            }

            try
            {
                // On Linux, we use journalctl to read systemd journal
                // Look for Secure Boot related events
                var events = await QueryJournalctlAsync(since, cancellationToken).ConfigureAwait(false);
                records.AddRange(events);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while reading Secure Boot events from journald.");
            }

            if (records.Count > 0)
            {
                var newest = DateTimeOffset.MinValue;
                foreach (var record in records)
                {
                    if (record.TimestampUtc > newest)
                    {
                        newest = record.TimestampUtc;
                    }
                }

                if (newest > DateTimeOffset.MinValue)
                {
                    await _checkpointStore.SetCheckpointAsync(newest, cancellationToken).ConfigureAwait(false);
                }
            }

            return records;
        }

        private async Task<List<SecureBootEventRecord>> QueryJournalctlAsync(DateTimeOffset since, CancellationToken cancellationToken)
        {
            var records = new List<SecureBootEventRecord>();

            try
            {
                // Use journalctl to query systemd journal for Secure Boot related entries
                // Format: --since="YYYY-MM-DD HH:MM:SS"
                var sinceStr = since.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = "journalctl",
                    Arguments = $"--since=\"{sinceStr}\" -o json -n 1000 -p info",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = startInfo };
                process.Start();

                var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
                var error = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
                
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

                if (process.ExitCode != 0)
                {
                    _logger.LogWarning("journalctl exited with code {ExitCode}. Error: {Error}", process.ExitCode, error);
                    return records;
                }

                // Parse journal entries and filter for Secure Boot related content
                var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    try
                    {
                        // Look for Secure Boot, UEFI, or boot-related messages
                        if (line.Contains("secure", StringComparison.OrdinalIgnoreCase) ||
                            line.Contains("uefi", StringComparison.OrdinalIgnoreCase) ||
                            line.Contains("boot", StringComparison.OrdinalIgnoreCase))
                        {
                            // For now, create a basic event record
                            // In production, you'd parse the JSON output from journalctl
                            var record = new SecureBootEventRecord
                            {
                                EventId = 0, // journald doesn't use Windows-style event IDs
                                ProviderName = "journald",
                                TimestampUtc = DateTimeOffset.UtcNow, // Should parse from JSON
                                Level = "Information",
                                Message = line.Length > 500 ? line.Substring(0, 500) + "..." : line,
                                RawXml = line // Store the full line
                            };

                            records.Add(record);
                        }
                    }
                    catch (ArgumentException ex)
                    {
                        _logger.LogDebug(ex, "Failed to parse journal entry: {Line}", line);
                    }
                    catch (ArgumentOutOfRangeException ex)
                    {
                        _logger.LogDebug(ex, "Failed to parse journal entry: {Line}", line);
                    }
                }

                _logger.LogDebug("Found {Count} Secure Boot related journal entries", records.Count);
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                _logger.LogWarning(ex, "journalctl command not found. Install systemd or run with appropriate permissions.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error querying journalctl");
            }

            return records;
        }
    }
}
