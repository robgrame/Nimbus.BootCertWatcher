using System.Globalization;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using SecureBootWatcher.Shared.Storage;

namespace SecureBootDashboard.Api.Services;

/// <summary>
/// Service for exporting dashboard data to Excel and CSV formats.
/// </summary>
public class ExportService : IExportService
{
    private readonly ILogger<ExportService> _logger;

    public ExportService(ILogger<ExportService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Export devices to Excel format with formatting and styling.
    /// </summary>
    public async Task<byte[]> ExportDevicesToExcelAsync(IEnumerable<ExportDeviceSummary> devices, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Exporting {Count} devices to Excel", devices.Count());

        return await Task.Run(() =>
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Devices");

            // Header row
            var headers = new[]
            {
                "Machine Name", "Domain", "Fleet ID", "Manufacturer", "Model",
                "Report Count", "Latest State", "Last Seen UTC", "Status"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = worksheet.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#4472C4");
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            // Data rows
            int row = 2;
            foreach (var device in devices)
            {
                worksheet.Cell(row, 1).Value = device.MachineName;
                worksheet.Cell(row, 2).Value = device.DomainName ?? "N/A";
                worksheet.Cell(row, 3).Value = device.FleetId ?? "N/A";
                worksheet.Cell(row, 4).Value = device.Manufacturer ?? "N/A";
                worksheet.Cell(row, 5).Value = device.Model ?? "N/A";
                worksheet.Cell(row, 6).Value = device.ReportCount;
                worksheet.Cell(row, 7).Value = device.LatestDeploymentState ?? "Unknown";
                worksheet.Cell(row, 8).Value = device.LastSeenUtc.ToString("yyyy-MM-dd HH:mm:ss");

                // Status column with color coding
                var daysSinceLastSeen = (DateTimeOffset.UtcNow - device.LastSeenUtc).TotalDays;
                var statusCell = worksheet.Cell(row, 9);

                if (daysSinceLastSeen < 1)
                {
                    statusCell.Value = "Active";
                    statusCell.Style.Fill.BackgroundColor = XLColor.FromHtml("#C6EFCE");
                    statusCell.Style.Font.FontColor = XLColor.FromHtml("#006100");
                }
                else if (daysSinceLastSeen < 7)
                {
                    statusCell.Value = "Recent";
                    statusCell.Style.Fill.BackgroundColor = XLColor.FromHtml("#FFEB9C");
                    statusCell.Style.Font.FontColor = XLColor.FromHtml("#9C5700");
                }
                else
                {
                    statusCell.Value = "Inactive";
                    statusCell.Style.Fill.BackgroundColor = XLColor.FromHtml("#FFC7CE");
                    statusCell.Style.Font.FontColor = XLColor.FromHtml("#9C0006");
                }

                // Color code deployment state
                var stateCell = worksheet.Cell(row, 7);
                switch (device.LatestDeploymentState)
                {
                    case "Deployed":
                        stateCell.Style.Fill.BackgroundColor = XLColor.FromHtml("#C6EFCE");
                        stateCell.Style.Font.FontColor = XLColor.FromHtml("#006100");
                        break;
                    case "Pending":
                        stateCell.Style.Fill.BackgroundColor = XLColor.FromHtml("#FFEB9C");
                        stateCell.Style.Font.FontColor = XLColor.FromHtml("#9C5700");
                        break;
                    case "Error":
                        stateCell.Style.Fill.BackgroundColor = XLColor.FromHtml("#FFC7CE");
                        stateCell.Style.Font.FontColor = XLColor.FromHtml("#9C0006");
                        break;
                }

                row++;
            }

            // Auto-fit columns
            worksheet.Columns().AdjustToContents();

            // Freeze header row
            worksheet.SheetView.FreezeRows(1);

            // Add filter to header row
            worksheet.RangeUsed().SetAutoFilter();

            // Add summary at the bottom
            row += 2;
            var summaryCell = worksheet.Cell(row, 1);
            summaryCell.Value = $"Total Devices: {devices.Count()}";
            summaryCell.Style.Font.Bold = true;

            worksheet.Cell(row + 1, 1).Value = $"Exported: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC";
            worksheet.Cell(row + 1, 1).Style.Font.Italic = true;

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }, cancellationToken);
    }

    /// <summary>
    /// Export devices to CSV format.
    /// </summary>
    public async Task<byte[]> ExportDevicesToCsvAsync(IEnumerable<ExportDeviceSummary> devices, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Exporting {Count} devices to CSV", devices.Count());

        return await Task.Run(() =>
        {
            using var memoryStream = new MemoryStream();
            using var writer = new StreamWriter(memoryStream, System.Text.Encoding.UTF8);
            using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true
            });

            // Map devices to CSV-friendly format
            var records = devices.Select(d => new
            {
                MachineName = d.MachineName,
                Domain = d.DomainName ?? "N/A",
                FleetId = d.FleetId ?? "N/A",
                Manufacturer = d.Manufacturer ?? "N/A",
                Model = d.Model ?? "N/A",
                ReportCount = d.ReportCount,
                LatestState = d.LatestDeploymentState ?? "Unknown",
                LastSeenUtc = d.LastSeenUtc.ToString("yyyy-MM-dd HH:mm:ss"),
                DaysSinceLastSeen = (DateTimeOffset.UtcNow - d.LastSeenUtc).TotalDays,
                Status = (DateTimeOffset.UtcNow - d.LastSeenUtc).TotalDays < 1 ? "Active" :
                         (DateTimeOffset.UtcNow - d.LastSeenUtc).TotalDays < 7 ? "Recent" : "Inactive"
            });

            csv.WriteRecords(records);
            writer.Flush();

            return memoryStream.ToArray();
        }, cancellationToken);
    }

    /// <summary>
    /// Export reports to Excel format with formatting and styling.
    /// </summary>
    public async Task<byte[]> ExportReportsToExcelAsync(IEnumerable<ReportSummary> reports, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Exporting {Count} reports to Excel", reports.Count());

        return await Task.Run(() =>
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Reports");

            // Header row
            var headers = new[] { "Report ID", "Machine Name", "Domain", "Deployment State", "Created UTC", "Age (Days)" };

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = worksheet.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#4472C4");
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            // Data rows
            int row = 2;
            foreach (var report in reports)
            {
                worksheet.Cell(row, 1).Value = report.Id.ToString();
                worksheet.Cell(row, 2).Value = report.MachineName;
                worksheet.Cell(row, 3).Value = report.DomainName ?? "N/A";
                worksheet.Cell(row, 4).Value = report.DeploymentState ?? "Unknown";
                worksheet.Cell(row, 5).Value = report.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm:ss");

                var age = (DateTimeOffset.UtcNow - report.CreatedAtUtc).TotalDays;
                worksheet.Cell(row, 6).Value = Math.Round(age, 1);

                // Color code deployment state
                var stateCell = worksheet.Cell(row, 4);
                switch (report.DeploymentState)
                {
                    case "Deployed":
                        stateCell.Style.Fill.BackgroundColor = XLColor.FromHtml("#C6EFCE");
                        stateCell.Style.Font.FontColor = XLColor.FromHtml("#006100");
                        break;
                    case "Pending":
                        stateCell.Style.Fill.BackgroundColor = XLColor.FromHtml("#FFEB9C");
                        stateCell.Style.Font.FontColor = XLColor.FromHtml("#9C5700");
                        break;
                    case "Error":
                        stateCell.Style.Fill.BackgroundColor = XLColor.FromHtml("#FFC7CE");
                        stateCell.Style.Font.FontColor = XLColor.FromHtml("#9C0006");
                        break;
                }

                row++;
            }

            // Auto-fit columns
            worksheet.Columns().AdjustToContents();

            // Freeze header row
            worksheet.SheetView.FreezeRows(1);

            // Add filter to header row
            worksheet.RangeUsed().SetAutoFilter();

            // Add summary
            row += 2;
            var summaryCell = worksheet.Cell(row, 1);
            summaryCell.Value = $"Total Reports: {reports.Count()}";
            summaryCell.Style.Font.Bold = true;

            worksheet.Cell(row + 1, 1).Value = $"Exported: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC";
            worksheet.Cell(row + 1, 1).Style.Font.Italic = true;

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }, cancellationToken);
    }

    /// <summary>
    /// Export reports to CSV format.
    /// </summary>
    public async Task<byte[]> ExportReportsToCsvAsync(IEnumerable<ReportSummary> reports, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Exporting {Count} reports to CSV", reports.Count());

        return await Task.Run(() =>
        {
            using var memoryStream = new MemoryStream();
            using var writer = new StreamWriter(memoryStream, System.Text.Encoding.UTF8);
            using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true
            });

            // Map reports to CSV-friendly format
            var records = reports.Select(r => new
            {
                ReportId = r.Id.ToString(),
                MachineName = r.MachineName,
                Domain = r.DomainName ?? "N/A",
                DeploymentState = r.DeploymentState ?? "Unknown",
                CreatedUtc = r.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm:ss"),
                AgeDays = Math.Round((DateTimeOffset.UtcNow - r.CreatedAtUtc).TotalDays, 1)
            });

            csv.WriteRecords(records);
            writer.Flush();

            return memoryStream.ToArray();
        }, cancellationToken);
    }
}
