using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SecureBootWatcher.Shared.Models;

namespace SecureBootWatcher.Shared.Storage
{
    public interface IReportStore
    {
        Task<Guid> SaveAsync(SecureBootStatusReport report, CancellationToken cancellationToken = default);

        Task<ReportDetail?> GetAsync(Guid id, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<ReportSummary>> GetRecentAsync(int limit, CancellationToken cancellationToken = default);
    }

    public sealed class ReportSummary
    {
        public ReportSummary(Guid id, string machineName, string? domainName, DateTimeOffset createdAtUtc, string? deploymentState)
        {
            Id = id;
            MachineName = machineName;
            DomainName = domainName;
            CreatedAtUtc = createdAtUtc;
            DeploymentState = deploymentState;
        }

        public Guid Id { get; }

        public string MachineName { get; }

        public string? DomainName { get; }

        public DateTimeOffset CreatedAtUtc { get; }

        public string? DeploymentState { get; }
    }

    public sealed class DeviceSnapshot
    {
        public DeviceSnapshot(Guid id, string machineName, string? domainName, string? userPrincipalName, string? manufacturer, string? model, string? firmwareVersion, string? fleetId, string? tagsJson, DateTimeOffset firstSeenUtc, DateTimeOffset lastSeenUtc)
        {
            Id = id;
            MachineName = machineName;
            DomainName = domainName;
            UserPrincipalName = userPrincipalName;
            Manufacturer = manufacturer;
            Model = model;
            FirmwareVersion = firmwareVersion;
            FleetId = fleetId;
            TagsJson = tagsJson;
            FirstSeenUtc = firstSeenUtc;
            LastSeenUtc = lastSeenUtc;
        }

        public Guid Id { get; }

        public string MachineName { get; }

        public string? DomainName { get; }

        public string? UserPrincipalName { get; }

        public string? Manufacturer { get; }

        public string? Model { get; }

        public string? FirmwareVersion { get; }

        public string? FleetId { get; }

        public string? TagsJson { get; }

        public DateTimeOffset FirstSeenUtc { get; }

        public DateTimeOffset LastSeenUtc { get; }
    }

    public sealed class EventSnapshot
    {
        public EventSnapshot(Guid id, string providerName, int eventId, DateTimeOffset timestampUtc, string? level, string? message, string? rawXml)
        {
            Id = id;
            ProviderName = providerName;
            EventId = eventId;
            TimestampUtc = timestampUtc;
            Level = level;
            Message = message;
            RawXml = rawXml;
        }

        public Guid Id { get; }

        public string ProviderName { get; }

        public int EventId { get; }

        public DateTimeOffset TimestampUtc { get; }

        public string? Level { get; }

        public string? Message { get; }

        public string? RawXml { get; }
    }

    public sealed class ReportDetail
    {
        public ReportDetail(Guid id, DeviceSnapshot device, string registryStateJson, string? alertsJson, string? deploymentState, string? clientVersion, string? correlationId, DateTimeOffset createdAtUtc, IReadOnlyList<EventSnapshot> events)
        {
            Id = id;
            Device = device;
            RegistryStateJson = registryStateJson;
            AlertsJson = alertsJson;
            DeploymentState = deploymentState;
            ClientVersion = clientVersion;
            CorrelationId = correlationId;
            CreatedAtUtc = createdAtUtc;
            Events = events;
        }

        public Guid Id { get; }

        public DeviceSnapshot Device { get; }

        public string RegistryStateJson { get; }

        public string? AlertsJson { get; }

        public string? DeploymentState { get; }

        public string? ClientVersion { get; }

        public string? CorrelationId { get; }

        public DateTimeOffset CreatedAtUtc { get; }

        public IReadOnlyList<EventSnapshot> Events { get; }
    }
}
