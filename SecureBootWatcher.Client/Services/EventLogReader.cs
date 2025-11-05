using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SecureBootWatcher.Client.Storage;
using SecureBootWatcher.Shared.Configuration;
using SecureBootWatcher.Shared.Models;

namespace SecureBootWatcher.Client.Services
{
    internal sealed class EventLogReader : IEventLogReader
    {
        private static readonly int[] SecureBootEventIds =
        {
            1032, 1033, 1034, 1036, 1037, 1043, 1044, 1045,
            1795, 1796, 1797, 1798, 1799, 1801, 1808
        };

        private readonly ILogger<EventLogReader> _logger;
        private readonly IOptionsMonitor<SecureBootWatcherOptions> _options;
        private readonly IEventCheckpointStore _checkpointStore;

        public EventLogReader(
            ILogger<EventLogReader> logger,
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

            foreach (var channel in options.EventChannels ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(channel))
                {
                    continue;
                }

                try
                {
                    foreach (var record in QueryChannel(channel, since))
                    {
                        records.Add(record);
                    }
                }
                catch (EventLogNotFoundException ex)
                {
                    _logger.LogWarning(ex, "Secure Boot event channel '{Channel}' was not found on this device.", channel);
                }
                catch (UnauthorizedAccessException ex)
                {
                    _logger.LogError(ex, "Access denied while reading Secure Boot events from channel '{Channel}'.", channel);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error while reading Secure Boot events from channel '{Channel}'.", channel);
                }
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

        private static IEnumerable<SecureBootEventRecord> QueryChannel(string channel, DateTimeOffset since)
        {
            var providerFilter = BuildEventIdFilter();
            var queryBuilder = new StringBuilder();
            queryBuilder.Append("*[System[(");
            queryBuilder.Append(providerFilter);
            queryBuilder.Append(") and TimeCreated[@SystemTime >= '");
            queryBuilder.Append(since.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture));
            queryBuilder.Append("']]]");

            var query = new EventLogQuery(channel, PathType.LogName, queryBuilder.ToString())
            {
                ReverseDirection = false
            };

            using var reader = new System.Diagnostics.Eventing.Reader.EventLogReader(query);
            for (EventRecord? eventRecord = reader.ReadEvent(); eventRecord != null; eventRecord = reader.ReadEvent())
            {
                using (eventRecord)
                {
                    yield return new SecureBootEventRecord
                    {
                        EventId = eventRecord.Id,
                        ProviderName = eventRecord.ProviderName ?? string.Empty,
                        TimestampUtc = eventRecord.TimeCreated?.ToUniversalTime() ?? DateTimeOffset.UtcNow,
                        Level = eventRecord.LevelDisplayName ?? eventRecord.Level?.ToString() ?? string.Empty,
                        Message = TryRenderMessage(eventRecord),
                        RawXml = eventRecord.ToXml()
                    };
                }
            }
        }

        private static string BuildEventIdFilter()
        {
            var builder = new StringBuilder();
            for (var i = 0; i < SecureBootEventIds.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(" or ");
                }

                builder.Append("EventID=");
                builder.Append(SecureBootEventIds[i].ToString(CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static string? TryRenderMessage(EventRecord record)
        {
            try
            {
                return record.FormatDescription();
            }
            catch
            {
                return null;
            }
        }
    }
}
