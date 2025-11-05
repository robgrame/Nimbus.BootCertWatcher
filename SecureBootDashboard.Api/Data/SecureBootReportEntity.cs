using System;
using System.Collections.Generic;

namespace SecureBootDashboard.Api.Data
{
    public sealed class SecureBootReportEntity
    {
        public Guid Id { get; set; }

        public Guid DeviceId { get; set; }

        public DeviceEntity? Device { get; set; }

    public string RegistryStateJson { get; set; } = string.Empty;

    public string? AlertsJson { get; set; }

    public string? DeploymentState { get; set; }

    public string? ClientVersion { get; set; }

    public string? CorrelationId { get; set; }

        public DateTimeOffset CreatedAtUtc { get; set; }

        public ICollection<SecureBootEventEntity> Events { get; set; } = new List<SecureBootEventEntity>();
    }
}
