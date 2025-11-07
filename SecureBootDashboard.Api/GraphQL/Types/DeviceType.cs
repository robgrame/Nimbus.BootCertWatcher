using SecureBootDashboard.Api.Data;

namespace SecureBootDashboard.Api.GraphQL.Types;

/// <summary>
/// GraphQL type representing a device in the Secure Boot monitoring system.
/// </summary>
public class DeviceType
{
    public Guid Id { get; set; }

    public string MachineName { get; set; } = string.Empty;

    public string? DomainName { get; set; }

    public string? UserPrincipalName { get; set; }

    public string? Manufacturer { get; set; }

    public string? Model { get; set; }

    public string? FirmwareVersion { get; set; }

    public string? FleetId { get; set; }

    public string? TagsJson { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset LastSeenUtc { get; set; }

    public int ReportCount { get; set; }

    public string? LatestDeploymentState { get; set; }

    public DateTimeOffset? LatestReportDate { get; set; }

    public static DeviceType FromEntity(DeviceEntity entity, SecureBootReportEntity? latestReport = null)
    {
        return new DeviceType
        {
            Id = entity.Id,
            MachineName = entity.MachineName,
            DomainName = entity.DomainName,
            UserPrincipalName = entity.UserPrincipalName,
            Manufacturer = entity.Manufacturer,
            Model = entity.Model,
            FirmwareVersion = entity.FirmwareVersion,
            FleetId = entity.FleetId,
            TagsJson = entity.TagsJson,
            CreatedAtUtc = entity.CreatedAtUtc,
            LastSeenUtc = entity.LastSeenUtc,
            ReportCount = entity.Reports?.Count ?? 0,
            LatestDeploymentState = latestReport?.DeploymentState,
            LatestReportDate = latestReport?.CreatedAtUtc
        };
    }
}
