using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SecureBootWatcher.Shared.Models;
using SecureBootWatcher.Shared.Storage;

namespace SecureBootDashboard.Api.Storage
{
    public sealed class FileReportStore : IReportStore
    {
        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };

        private readonly FileReportStoreOptions _options;
        private readonly ILogger<FileReportStore> _logger;
        private readonly SemaphoreSlim _gate = new(1, 1);

        public FileReportStore(IOptions<FileReportStoreOptions> options, ILogger<FileReportStore> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        public async Task<Guid> SaveAsync(SecureBootStatusReport report, CancellationToken cancellationToken = default)
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                Directory.CreateDirectory(_options.BasePath);

                var document = FileReportDocument.FromReport(report);
                var path = GetReportPath(document.Id);

                await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, useAsync: true);
                await JsonSerializer.SerializeAsync(stream, document, SerializerOptions, cancellationToken).ConfigureAwait(false);

                return document.Id;
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<ReportDetail?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var path = GetReportPath(id);
            if (!File.Exists(path))
            {
                return null;
            }

            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, useAsync: true);
            var document = await JsonSerializer.DeserializeAsync<FileReportDocument>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false);
            return document?.ToDetail();
        }

        public async Task<IReadOnlyList<ReportSummary>> GetRecentAsync(int limit, CancellationToken cancellationToken = default)
        {
            limit = Math.Clamp(limit, 1, 200);

            if (!Directory.Exists(_options.BasePath))
            {
                return Array.Empty<ReportSummary>();
            }

            var files = Directory
                .EnumerateFiles(_options.BasePath, "*.json", SearchOption.TopDirectoryOnly)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .Take(limit)
                .ToList();

            var results = new List<ReportSummary>(files.Count);

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, useAsync: true);
                    using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

                    if (document.RootElement.TryGetProperty("Id", out var idProperty) &&
                        document.RootElement.TryGetProperty("Device", out var deviceProperty) &&
                        document.RootElement.TryGetProperty("CreatedAtUtc", out var createdAtProperty))
                    {
                        var id = idProperty.GetGuid();
                        var machineName = deviceProperty.GetProperty("MachineName").GetString() ?? string.Empty;
                        var domainName = deviceProperty.TryGetProperty("DomainName", out var domainProp) ? domainProp.GetString() : null;
                        var deploymentState = document.RootElement.TryGetProperty("DeploymentState", out var stateProp) ? stateProp.GetString() : null;
                        var createdAt = createdAtProperty.GetDateTimeOffset();

                        results.Add(new ReportSummary(id, machineName, domainName, createdAt, deploymentState));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read report summary from file {FilePath}", file);
                }
            }

            return results;
        }

        private string GetReportPath(Guid id)
        {
            return Path.Combine(_options.BasePath, $"{id:N}.json");
        }

        private sealed record FileReportDocument(Guid Id, DeviceDocument Device, string RegistryStateJson, string? CertificatesJson, string? AlertsJson, string? DeploymentState, string? ClientVersion, string? CorrelationId, DateTimeOffset CreatedAtUtc, IReadOnlyList<EventDocument> Events)
        {
            public static FileReportDocument FromReport(SecureBootStatusReport report)
            {
                var utcNow = DateTimeOffset.UtcNow;
                var createdAt = report.CreatedAtUtc == default ? utcNow : report.CreatedAtUtc;

                var device = new DeviceDocument(
                    Guid.NewGuid(),
                    report.Device.MachineName,
                    report.Device.DomainName,
                    report.Device.UserPrincipalName,
                    report.Device.Manufacturer,
                    report.Device.Model,
                    report.Device.FirmwareVersion,
                    TryGetFleet(report.Device.Tags),
                    JsonSerializer.Serialize(report.Device.Tags ?? new Dictionary<string, string>(), SerializerOptions),
                    createdAt,
                    createdAt);

                var events = (report.Events ?? Array.Empty<SecureBootEventRecord>())
                    .Select(evt => new EventDocument(
                        Guid.NewGuid(),
                        evt.ProviderName,
                        evt.EventId,
                        evt.TimestampUtc == default ? createdAt : evt.TimestampUtc,
                        evt.Level,
                        evt.Message,
                        evt.RawXml))
                    .ToArray();

                return new FileReportDocument(
                    Guid.NewGuid(),
                    device,
                    JsonSerializer.Serialize(report.Registry, SerializerOptions),
                    report.Certificates != null ? JsonSerializer.Serialize(report.Certificates, SerializerOptions) : null,
                    JsonSerializer.Serialize(report.Alerts ?? Array.Empty<string>(), SerializerOptions),
                    // Use InferredDeploymentState for smarter state detection based on AvailableUpdates
                    report.Registry?.InferredDeploymentState.ToString(),
                    report.ClientVersion,
                    report.CorrelationId,
                    createdAt,
                    events);
            }

            public ReportDetail ToDetail()
            {
                var deviceSnapshot = new DeviceSnapshot(
                    Device.Id,
                    Device.MachineName,
                    Device.DomainName,
                    Device.UserPrincipalName,
                    Device.Manufacturer,
                    Device.Model,
                    Device.FirmwareVersion,
                    Device.FleetId,
                    Device.TagsJson,
                    Device.FirstSeenUtc,
                    Device.LastSeenUtc);

                var eventSnapshots = Events
                    .OrderByDescending(e => e.TimestampUtc)
                    .Select(e => new EventSnapshot(e.Id, e.ProviderName, e.EventId, e.TimestampUtc, e.Level, e.Message, e.RawXml))
                    .ToArray();

                return new ReportDetail(
                    Id,
                    deviceSnapshot,
                    RegistryStateJson,
                    CertificatesJson,
                    AlertsJson,
                    DeploymentState,
                    ClientVersion,
                    CorrelationId,
                    CreatedAtUtc,
                    eventSnapshots);
            }
        }

        private sealed record DeviceDocument(Guid Id, string MachineName, string? DomainName, string? UserPrincipalName, string? Manufacturer, string? Model, string? FirmwareVersion, string? FleetId, string TagsJson, DateTimeOffset FirstSeenUtc, DateTimeOffset LastSeenUtc);

        private sealed record EventDocument(Guid Id, string ProviderName, int EventId, DateTimeOffset TimestampUtc, string? Level, string? Message, string? RawXml);

        private static string? TryGetFleet(IDictionary<string, string>? tags)
        {
            if (tags == null)
            {
                return null;
            }

            // Try "FleetId" first (PascalCase - used by client)
            if (tags.TryGetValue("FleetId", out var fleetId) && !string.IsNullOrWhiteSpace(fleetId))
            {
                return fleetId;
            }

            // Fallback to "fleet" (lowercase - for backward compatibility)
            if (tags.TryGetValue("fleet", out var fleet) && !string.IsNullOrWhiteSpace(fleet))
            {
                return fleet;
            }

            return null;
        }
    }

    public sealed class FileReportStoreOptions
    {
        public string BasePath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SecureBootDashboard", "reports");
    }
}
