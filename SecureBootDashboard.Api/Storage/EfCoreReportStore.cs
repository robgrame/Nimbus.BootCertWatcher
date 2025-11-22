using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SecureBootDashboard.Api.Data;
using SecureBootWatcher.Shared.Models;
using SecureBootWatcher.Shared.Storage;

namespace SecureBootDashboard.Api.Storage
{
    public sealed class EfCoreReportStore : IReportStore
    {
        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = false
        };

        private readonly SecureBootDbContext _dbContext;
        private readonly ILogger<EfCoreReportStore> _logger;

        public EfCoreReportStore(SecureBootDbContext dbContext, ILogger<EfCoreReportStore> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<Guid> SaveAsync(SecureBootStatusReport report, CancellationToken cancellationToken = default)
        {
            var utcNow = DateTimeOffset.UtcNow;

            var device = await _dbContext.Devices
                .FirstOrDefaultAsync(d => d.MachineName == report.Device.MachineName && d.DomainName == report.Device.DomainName, cancellationToken)
                .ConfigureAwait(false);

            if (device == null)
            {
                device = new DeviceEntity
                {
                    Id = Guid.NewGuid(),
                    MachineName = report.Device.MachineName,
                    DomainName = report.Device.DomainName,
                    UserPrincipalName = report.Device.UserPrincipalName,
                    Manufacturer = report.Device.Manufacturer,
                    Model = report.Device.Model,
                    FirmwareVersion = report.Device.FirmwareVersion,
                    FirmwareReleaseDate = report.Device.FirmwareReleaseDate,
                    UEFISecureBootEnabled = report.Registry?.UEFISecureBootEnabled,
                    ClientVersion = report.Device.ClientVersion,
                    OperatingSystem = report.Device.OperatingSystem,
                    OSVersion = report.Device.OSVersion,
                    OSProductType = report.Device.OSProductType,
                    ChassisTypesJson = report.Device.ChassisTypes != null ? Serialize(report.Device.ChassisTypes) : null,
                    IsVirtualMachine = report.Device.IsVirtualMachine,
                    VirtualizationPlatform = report.Device.VirtualizationPlatform,
                    FleetId = TryGetFleet(report.Device.Tags),
                    TagsJson = Serialize(report.Device.Tags ?? new Dictionary<string, string>()),
                    CreatedAtUtc = utcNow,
                    LastSeenUtc = utcNow
                };

                _dbContext.Devices.Add(device);
            }
            else
            {
                device.DomainName = report.Device.DomainName;
                device.UserPrincipalName = report.Device.UserPrincipalName;
                device.Manufacturer = report.Device.Manufacturer;
                device.Model = report.Device.Model;
                device.FirmwareVersion = report.Device.FirmwareVersion;
                device.FirmwareReleaseDate = report.Device.FirmwareReleaseDate ?? device.FirmwareReleaseDate;
                device.UEFISecureBootEnabled = report.Registry?.UEFISecureBootEnabled ?? device.UEFISecureBootEnabled;
                device.ClientVersion = report.Device.ClientVersion ?? device.ClientVersion;
                device.OperatingSystem = report.Device.OperatingSystem ?? device.OperatingSystem;
                device.OSVersion = report.Device.OSVersion ?? device.OSVersion;
                device.OSBuildNumber = report.Device.OSBuildNumber ?? device.OSBuildNumber;
                device.OSProductType = report.Device.OSProductType ?? device.OSProductType;
                device.ChassisTypesJson = report.Device.ChassisTypes != null ? Serialize(report.Device.ChassisTypes) : device.ChassisTypesJson;
                device.IsVirtualMachine = report.Device.IsVirtualMachine ?? device.IsVirtualMachine;
                device.VirtualizationPlatform = report.Device.VirtualizationPlatform ?? device.VirtualizationPlatform;
                device.FleetId = TryGetFleet(report.Device.Tags) ?? device.FleetId;
                device.TagsJson = Serialize(report.Device.Tags ?? new Dictionary<string, string>());
                device.LastSeenUtc = utcNow;
            }

            var reportEntity = new SecureBootReportEntity
            {
                Id = Guid.NewGuid(),
                Device = device,
                RegistryStateJson = Serialize(report.Registry),
                CertificatesJson = report.Certificates != null ? Serialize(report.Certificates) : null,
                AlertsJson = Serialize(report.Alerts ?? Array.Empty<string>()),
                // Use InferredDeploymentState for smarter state detection based on AvailableUpdates
                DeploymentState = report.Registry?.InferredDeploymentState.ToString(),
                ClientVersion = report.ClientVersion,
                CorrelationId = report.CorrelationId,
                CreatedAtUtc = report.CreatedAtUtc == default ? utcNow : report.CreatedAtUtc
            };

            foreach (var evt in report.Events ?? Array.Empty<SecureBootEventRecord>())
            {
                reportEntity.Events.Add(new SecureBootEventEntity
                {
                    Id = Guid.NewGuid(),
                    ProviderName = evt.ProviderName,
                    EventId = evt.EventId,
                    Level = evt.Level,
                    TimestampUtc = evt.TimestampUtc == default ? reportEntity.CreatedAtUtc : evt.TimestampUtc,
                    Message = evt.Message,
                    RawXml = evt.RawXml
                });
            }

            _dbContext.Reports.Add(reportEntity);

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist secure boot report for machine {Machine}", report.Device.MachineName);
                throw;
            }

            return reportEntity.Id;
        }

        public async Task<ReportDetail?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var entity = await _dbContext.Reports
                .Include(r => r.Device)
                .Include(r => r.Events)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
                .ConfigureAwait(false);

            return entity == null ? null : Map(entity);
        }

        public async Task<IReadOnlyList<ReportSummary>> GetRecentAsync(int limit, CancellationToken cancellationToken = default)
        {
            limit = Math.Clamp(limit, 1, 200);

            var reports = await _dbContext.Reports
                .Include(r => r.Device)
                .AsNoTracking()
                .OrderByDescending(r => r.CreatedAtUtc)
                .Take(limit)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return reports.Select(r => new ReportSummary(r.Id, r.Device?.MachineName ?? string.Empty, r.Device?.DomainName, r.CreatedAtUtc, r.DeploymentState)).ToArray();
        }

        private static string Serialize<T>(T value)
        {
            return JsonSerializer.Serialize(value, SerializerOptions);
        }

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

        private static ReportDetail Map(SecureBootReportEntity entity)
        {
            var device = entity.Device ?? new DeviceEntity
            {
                Id = Guid.Empty,
                MachineName = string.Empty,
                CreatedAtUtc = entity.CreatedAtUtc,
                LastSeenUtc = entity.CreatedAtUtc
            };

            var deviceSnapshot = new DeviceSnapshot(
                device.Id,
                device.MachineName,
                device.DomainName,
                device.UserPrincipalName,
                device.Manufacturer,
                device.Model,
                device.FirmwareVersion,
                device.FleetId,
                device.TagsJson,
                device.CreatedAtUtc,
                device.LastSeenUtc);

            var events = entity.Events
                .OrderByDescending(e => e.TimestampUtc)
                .Select(e => new EventSnapshot(e.Id, e.ProviderName, e.EventId, e.TimestampUtc, e.Level, e.Message, e.RawXml))
                .ToArray();

            return new ReportDetail(
                entity.Id,
                deviceSnapshot,
                entity.RegistryStateJson,
                entity.CertificatesJson,
                entity.AlertsJson,
                entity.DeploymentState,
                entity.ClientVersion,
                entity.CorrelationId,
                entity.CreatedAtUtc,
                events);
        }
    }
}
