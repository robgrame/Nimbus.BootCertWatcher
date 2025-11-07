using SecureBootDashboard.Api.Data;

namespace SecureBootDashboard.Api.GraphQL.Types;

/// <summary>
/// GraphQL type representing a Secure Boot report.
/// </summary>
public class ReportType
{
    public Guid Id { get; set; }

    public Guid DeviceId { get; set; }

    public string RegistryStateJson { get; set; } = string.Empty;

    public string? CertificatesJson { get; set; }

    public string? AlertsJson { get; set; }

    public string? DeploymentState { get; set; }

    public string? ClientVersion { get; set; }

    public string? CorrelationId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public int EventCount { get; set; }

    public static ReportType FromEntity(SecureBootReportEntity entity)
    {
        return new ReportType
        {
            Id = entity.Id,
            DeviceId = entity.DeviceId,
            RegistryStateJson = entity.RegistryStateJson,
            CertificatesJson = entity.CertificatesJson,
            AlertsJson = entity.AlertsJson,
            DeploymentState = entity.DeploymentState,
            ClientVersion = entity.ClientVersion,
            CorrelationId = entity.CorrelationId,
            CreatedAtUtc = entity.CreatedAtUtc,
            EventCount = entity.Events?.Count ?? 0
        };
    }
}
