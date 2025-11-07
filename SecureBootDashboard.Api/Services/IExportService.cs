using SecureBootWatcher.Shared.Storage;

namespace SecureBootDashboard.Api.Services;

/// <summary>
/// Service interface for exporting data to various formats (Excel, CSV).
/// </summary>
public interface IExportService
{
    /// <summary>
    /// Export devices to Excel format.
    /// </summary>
    Task<byte[]> ExportDevicesToExcelAsync(IEnumerable<ExportDeviceSummary> devices, CancellationToken cancellationToken = default);

    /// <summary>
    /// Export devices to CSV format.
    /// </summary>
    Task<byte[]> ExportDevicesToCsvAsync(IEnumerable<ExportDeviceSummary> devices, CancellationToken cancellationToken = default);

    /// <summary>
    /// Export reports to Excel format.
    /// </summary>
    Task<byte[]> ExportReportsToExcelAsync(IEnumerable<ReportSummary> reports, CancellationToken cancellationToken = default);

    /// <summary>
    /// Export reports to CSV format.
    /// </summary>
    Task<byte[]> ExportReportsToCsvAsync(IEnumerable<ReportSummary> reports, CancellationToken cancellationToken = default);
}

/// <summary>
/// Simple device summary for export purposes.
/// </summary>
public sealed record ExportDeviceSummary(
    Guid Id,
    string MachineName,
    string? DomainName,
    string? FleetId,
    string? Manufacturer,
    string? Model,
    int ReportCount,
    string? LatestDeploymentState,
    DateTimeOffset LastSeenUtc);
