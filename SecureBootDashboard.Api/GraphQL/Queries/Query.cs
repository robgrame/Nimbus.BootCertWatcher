using Microsoft.EntityFrameworkCore;
using SecureBootDashboard.Api.Data;
using SecureBootDashboard.Api.GraphQL.Types;

namespace SecureBootDashboard.Api.GraphQL.Queries;

/// <summary>
/// GraphQL queries for devices and reports.
/// </summary>
public class Query
{
    private const int MinLimit = 1;
    private const int MaxLimit = 200;

    /// <summary>
    /// Get all devices with their latest report summary.
    /// </summary>
    public async Task<IReadOnlyList<DeviceType>> GetDevices(
        [Service] SecureBootDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var devices = await dbContext.Devices
            .AsNoTracking()
            .Include(d => d.Reports.OrderByDescending(r => r.CreatedAtUtc).Take(1))
            .OrderByDescending(d => d.LastSeenUtc)
            .ToListAsync(cancellationToken);

        return devices.Select(d =>
        {
            var latestReport = d.Reports.FirstOrDefault();
            return DeviceType.FromEntity(d, latestReport);
        }).ToList();
    }

    /// <summary>
    /// Get a specific device by ID.
    /// </summary>
    public async Task<DeviceType?> GetDevice(
        Guid id,
        [Service] SecureBootDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var device = await dbContext.Devices
            .AsNoTracking()
            .Include(d => d.Reports.OrderByDescending(r => r.CreatedAtUtc).Take(1))
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (device == null)
        {
            return null;
        }

        var latestReport = device.Reports.FirstOrDefault();
        return DeviceType.FromEntity(device, latestReport);
    }

    /// <summary>
    /// Get reports for a specific device.
    /// </summary>
    public async Task<IReadOnlyList<ReportType>> GetDeviceReports(
        Guid deviceId,
        int limit,
        [Service] SecureBootDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var clampedLimit = ClampLimit(limit);

        var reports = await dbContext.Reports
            .AsNoTracking()
            .Where(r => r.DeviceId == deviceId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .Take(clampedLimit)
            .Include(r => r.Events)
            .ToListAsync(cancellationToken);

        return reports.Select(ReportType.FromEntity).ToList();
    }

    /// <summary>
    /// Get a specific report by ID.
    /// </summary>
    public async Task<ReportType?> GetReport(
        Guid id,
        [Service] SecureBootDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var report = await dbContext.Reports
            .AsNoTracking()
            .Include(r => r.Events)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        return report == null ? null : ReportType.FromEntity(report);
    }

    /// <summary>
    /// Get recent reports across all devices.
    /// </summary>
    public async Task<IReadOnlyList<ReportType>> GetRecentReports(
        int limit,
        [Service] SecureBootDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var clampedLimit = ClampLimit(limit);

        var reports = await dbContext.Reports
            .AsNoTracking()
            .OrderByDescending(r => r.CreatedAtUtc)
            .Take(clampedLimit)
            .Include(r => r.Events)
            .ToListAsync(cancellationToken);

        return reports.Select(ReportType.FromEntity).ToList();
    }

    /// <summary>
    /// Get events for a specific report.
    /// </summary>
    public async Task<IReadOnlyList<EventType>> GetReportEvents(
        Guid reportId,
        [Service] SecureBootDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var events = await dbContext.Events
            .AsNoTracking()
            .Where(e => e.ReportId == reportId)
            .OrderBy(e => e.TimestampUtc)
            .ToListAsync(cancellationToken);

        return events.Select(EventType.FromEntity).ToList();
    }

    /// <summary>
    /// Clamps the limit parameter to valid range (1-200).
    /// </summary>
    private static int ClampLimit(int limit) => Math.Clamp(limit, MinLimit, MaxLimit);
}
