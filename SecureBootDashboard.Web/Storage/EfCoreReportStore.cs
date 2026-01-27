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
                // Determine deployment state by combining registry state and certificate presence
                DeploymentState = DetermineDeploymentState(report.Registry, report.Certificates),
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

        /// <summary>
        /// Determines the deployment state by checking both registry flags and certificate presence.
        /// This prevents false "Updated" status when AvailableUpdates=0 but certificate was never deployed.
        /// </summary>
        private static string DetermineDeploymentState(
            SecureBootRegistrySnapshot? registry, 
            SecureBootCertificateCollection? certificates)
        {
            // If no registry data, we can't determine state
            if (registry == null)
                return SecureBootDeploymentState.Unknown.ToString();

            // Check for explicit error condition
            if (registry.UefiCa2023Error.HasValue && registry.UefiCa2023Error.Value != 0)
                return SecureBootDeploymentState.Error.ToString();

            // Check if Windows UEFI CA 2023 certificate is actually present
            const string windowsUefiCa2023Thumbprint = "45a0fa32604773c82433c3b7d59e7466b3ac0c67";
            var hasWindowsUefiCa2023 = false;

            if (certificates?.SignatureDatabase != null)
            {
                hasWindowsUefiCa2023 = certificates.SignatureDatabase.Any(cert =>
                    cert.Thumbprint?.Equals(windowsUefiCa2023Thumbprint, StringComparison.OrdinalIgnoreCase) == true ||
                    cert.Subject?.Contains("Windows UEFI CA 2023", StringComparison.OrdinalIgnoreCase) == true);
            }

            // Use AvailableUpdates if present for accurate state detection
            if (registry.AvailableUpdates.HasValue)
            {
                var availableUpdates = registry.AvailableUpdates.Value;

                switch (availableUpdates)
                {
                    // All updates completed - but only if certificate is actually present
                    case 0x0000:
                        return hasWindowsUefiCa2023 
                            ? SecureBootDeploymentState.Updated.ToString()
                            : SecureBootDeploymentState.NotStarted.ToString();

                    // Deployment complete (conditional flag remains) - verify certificate presence
                    case 0x4000:
                        return hasWindowsUefiCa2023
                            ? SecureBootDeploymentState.Updated.ToString()
                            : SecureBootDeploymentState.NotStarted.ToString();

                    // Initial state - not started
                    case 0x5944:
                        return SecureBootDeploymentState.NotStarted.ToString();

                    // Any other value with pending updates
                    default:
                        var completionPercentage = SecureBootUpdateFlagsExtensions.GetCompletionPercentage(availableUpdates);
                        return completionPercentage > 0 && completionPercentage < 100
                            ? SecureBootDeploymentState.InProgress.ToString()
                            : SecureBootDeploymentState.Unknown.ToString();
                }
            }

            // Fallback to UefiCa2023Status if AvailableUpdates not present
            // But still verify certificate presence for "Updated" state
            if (registry.UefiCa2023Status == SecureBootDeploymentState.Updated)
            {
                return hasWindowsUefiCa2023
                    ? SecureBootDeploymentState.Updated.ToString()
                    : SecureBootDeploymentState.NotStarted.ToString();
            }

            return registry.UefiCa2023Status.ToString();
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
