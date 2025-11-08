using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SecureBootDashboard.Api.Data;
using SecureBootWatcher.Shared.Models;

namespace SecureBootDashboard.Api.Services
{
    /// <summary>
    /// Service for detecting anomalies in device behavior using statistical analysis (mean and standard deviation)
    /// </summary>
    public interface IAnomalyDetectionService
    {
        Task<IReadOnlyList<AnomalyDetectionResult>> DetectAnomaliesAsync(CancellationToken cancellationToken = default);
        Task<AnomalyDetectionResult?> GetAnomalyAsync(Guid anomalyId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<AnomalyDetectionResult>> GetActiveAnomaliesAsync(CancellationToken cancellationToken = default);
        Task ResolveAnomalyAsync(Guid anomalyId, string resolvedBy, CancellationToken cancellationToken = default);
    }

    public sealed class AnomalyDetectionService : IAnomalyDetectionService
    {
        private readonly SecureBootDbContext _dbContext;
        private readonly ILogger<AnomalyDetectionService> _logger;

        // Configuration constants
        private const int MinimumDeviceCountForStatistics = 3;
        private const double StatisticalThreshold = 2.5; // Standard deviations
        private const int ReportingFrequencyLookbackDays = 7;
        private const int DeploymentStateLookbackDays = 14;
        private const int InactivityThresholdDays = 14;
        private const int PendingStateAnomalyThreshold = 3; // consecutive reports
        private const int ErrorStateAnomalyThreshold = 2; // reports

        public AnomalyDetectionService(SecureBootDbContext dbContext, ILogger<AnomalyDetectionService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<IReadOnlyList<AnomalyDetectionResult>> DetectAnomaliesAsync(CancellationToken cancellationToken = default)
        {
            var detectedAnomalies = new List<AnomalyDetectionResult>();

            try
            {
                // Detect reporting frequency anomalies
                var frequencyAnomalies = await DetectReportingFrequencyAnomaliesAsync(cancellationToken);
                detectedAnomalies.AddRange(frequencyAnomalies);

                // Detect deployment state anomalies
                var stateAnomalies = await DetectDeploymentStateAnomaliesAsync(cancellationToken);
                detectedAnomalies.AddRange(stateAnomalies);

                // Detect device inactivity anomalies
                var inactivityAnomalies = await DetectInactivityAnomaliesAsync(cancellationToken);
                detectedAnomalies.AddRange(inactivityAnomalies);

                // Save detected anomalies to database
                foreach (var anomaly in detectedAnomalies)
                {
                    // Check if anomaly already exists
                    var exists = await _dbContext.Anomalies.AnyAsync(
                        a => a.DeviceId == anomaly.DeviceId && 
                             a.AnomalyType == anomaly.Type.ToString() && 
                             a.Status == "Active",
                        cancellationToken);

                    if (!exists)
                    {
                        var entity = new AnomalyEntity
                        {
                            Id = Guid.NewGuid(),
                            DeviceId = anomaly.DeviceId,
                            AnomalyType = anomaly.Type.ToString(),
                            Description = anomaly.Description,
                            Severity = anomaly.Severity,
                            DetectedAtUtc = DateTimeOffset.UtcNow,
                            Status = "Active"
                        };
                        _dbContext.Anomalies.Add(entity);
                        anomaly.Id = entity.Id;
                    }
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Detected {Count} new anomalies", detectedAnomalies.Count);
            }
            catch (OperationCanceledException ocex)
            {
                _logger.LogWarning(ocex, "Anomaly detection was canceled");
            }
            catch (DbUpdateException dbex)
            {
                _logger.LogError(dbex, "Database update error while detecting anomalies");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error detecting anomalies");
            }

            return detectedAnomalies;
        }

        private async Task<List<AnomalyDetectionResult>> DetectReportingFrequencyAnomaliesAsync(CancellationToken cancellationToken)
        {
            var anomalies = new List<AnomalyDetectionResult>();
            var lookbackPeriod = DateTimeOffset.UtcNow.AddDays(-ReportingFrequencyLookbackDays);

            // Get devices with their report counts
            var deviceReportStats = await _dbContext.Devices
                .Select(d => new
                {
                    d.Id,
                    d.MachineName,
                    ReportCount = d.Reports.Count(r => r.CreatedAtUtc > lookbackPeriod),
                    TotalReports = d.Reports.Count
                })
                .Where(d => d.TotalReports > 0)
                .ToListAsync(cancellationToken);

            if (deviceReportStats.Count < MinimumDeviceCountForStatistics)
                return anomalies;

            // Calculate statistical thresholds
            var reportCounts = deviceReportStats.Select(d => (double)d.ReportCount).ToArray();
            var mean = reportCounts.Average();
            var stdDev = Math.Sqrt(reportCounts.Select(x => Math.Pow(x - mean, 2)).Average());
            var upperThreshold = mean + (StatisticalThreshold * stdDev);
            var lowerThreshold = Math.Max(0, mean - (StatisticalThreshold * stdDev));

            // Identify anomalies
            foreach (var device in deviceReportStats.Where(d => d.ReportCount > upperThreshold || d.ReportCount < lowerThreshold))
            {
                var severity = Math.Abs(device.ReportCount - mean) / Math.Max(stdDev, 1);
                severity = Math.Min(severity / 3.0, 1.0); // Normalize to 0-1

                string description;
                if (device.ReportCount > upperThreshold)
                {
                    description = $"Excessive reporting detected. Device reported {device.ReportCount} times in {ReportingFrequencyLookbackDays} days (expected: ~{mean:F1})";
                }
                else
                {
                    description = $"Insufficient reporting detected. Device reported {device.ReportCount} times in {ReportingFrequencyLookbackDays} days (expected: ~{mean:F1})";
                }

                anomalies.Add(new AnomalyDetectionResult
                {
                    DeviceId = device.Id,
                    DeviceName = device.MachineName,
                    Type = AnomalyType.ReportingFrequency,
                    Description = description,
                    Severity = severity,
                    DetectedAtUtc = DateTimeOffset.UtcNow,
                    Status = AnomalyStatus.Active
                });
            }

            return anomalies;
        }

        private async Task<List<AnomalyDetectionResult>> DetectDeploymentStateAnomaliesAsync(CancellationToken cancellationToken)
        {
            var anomalies = new List<AnomalyDetectionResult>();
            var lookbackPeriod = DateTimeOffset.UtcNow.AddDays(-DeploymentStateLookbackDays);

            // Find devices stuck in non-deployed states
            var stuckDevices = await _dbContext.Devices
                .Include(d => d.Reports.Where(r => r.CreatedAtUtc > lookbackPeriod).OrderByDescending(r => r.CreatedAtUtc))
                .Where(d => d.Reports.Any(r => r.CreatedAtUtc > lookbackPeriod))
                .ToListAsync(cancellationToken);

            foreach (var device in stuckDevices)
            {
                var recentReports = device.Reports.Take(5).ToList();
                if (recentReports.Count < PendingStateAnomalyThreshold)
                    continue;

                // Check if device has been in Pending or Error state for multiple reports
                var pendingCount = recentReports.Count(r => r.DeploymentState == "Pending");
                var errorCount = recentReports.Count(r => r.DeploymentState == "Error");

                if (pendingCount >= PendingStateAnomalyThreshold)
                {
                    anomalies.Add(new AnomalyDetectionResult
                    {
                        DeviceId = device.Id,
                        DeviceName = device.MachineName,
                        Type = AnomalyType.DeploymentState,
                        Description = $"Device stuck in 'Pending' state for {pendingCount} consecutive reports",
                        Severity = 0.6,
                        DetectedAtUtc = DateTimeOffset.UtcNow,
                        Status = AnomalyStatus.Active
                    });
                }

                if (errorCount >= ErrorStateAnomalyThreshold)
                {
                    anomalies.Add(new AnomalyDetectionResult
                    {
                        DeviceId = device.Id,
                        DeviceName = device.MachineName,
                        Type = AnomalyType.DeploymentState,
                        Description = $"Device in 'Error' state for {errorCount} reports",
                        Severity = 0.8,
                        DetectedAtUtc = DateTimeOffset.UtcNow,
                        Status = AnomalyStatus.Active
                    });
                }
            }

            return anomalies;
        }

        private async Task<List<AnomalyDetectionResult>> DetectInactivityAnomaliesAsync(CancellationToken cancellationToken)
        {
            var anomalies = new List<AnomalyDetectionResult>();
            var inactivityThreshold = DateTimeOffset.UtcNow.AddDays(-InactivityThresholdDays);

            // Find devices that haven't reported recently but were active before
            var inactiveDevices = await _dbContext.Devices
                .Where(d => d.LastSeenUtc < inactivityThreshold && d.Reports.Any())
                .Select(d => new
                {
                    d.Id,
                    d.MachineName,
                    d.LastSeenUtc,
                    ReportCount = d.Reports.Count
                })
                .ToListAsync(cancellationToken);

            foreach (var device in inactiveDevices)
            {
                var daysSinceLastReport = (DateTimeOffset.UtcNow - device.LastSeenUtc).TotalDays;
                var severity = Math.Min(daysSinceLastReport / 30.0, 1.0);

                anomalies.Add(new AnomalyDetectionResult
                {
                    DeviceId = device.Id,
                    DeviceName = device.MachineName,
                    Type = AnomalyType.DeviceBehavior,
                    Description = $"Device inactive for {daysSinceLastReport:F0} days (last seen: {device.LastSeenUtc:yyyy-MM-dd})",
                    Severity = severity,
                    DetectedAtUtc = DateTimeOffset.UtcNow,
                    Status = AnomalyStatus.Active
                });
            }

            return anomalies;
        }

        public async Task<AnomalyDetectionResult?> GetAnomalyAsync(Guid anomalyId, CancellationToken cancellationToken = default)
        {
            var entity = await _dbContext.Anomalies
                .Include(a => a.Device)
                .FirstOrDefaultAsync(a => a.Id == anomalyId, cancellationToken);

            return entity == null ? null : MapToResult(entity);
        }

        public async Task<IReadOnlyList<AnomalyDetectionResult>> GetActiveAnomaliesAsync(CancellationToken cancellationToken = default)
        {
            var entities = await _dbContext.Anomalies
                .Include(a => a.Device)
                .Where(a => a.Status == "Active")
                .OrderByDescending(a => a.Severity)
                .ThenByDescending(a => a.DetectedAtUtc)
                .ToListAsync(cancellationToken);

            return entities.Select(MapToResult).ToList();
        }

        public async Task ResolveAnomalyAsync(Guid anomalyId, string resolvedBy, CancellationToken cancellationToken = default)
        {
            var entity = await _dbContext.Anomalies.FindAsync(new object[] { anomalyId }, cancellationToken);
            if (entity != null)
            {
                entity.Status = "Resolved";
                entity.ResolvedBy = resolvedBy;
                entity.ResolvedAtUtc = DateTimeOffset.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
                
                // Sanitize user input before logging to prevent log forging
                var sanitizedResolvedBy = resolvedBy.Replace("\n", "").Replace("\r", "");
                _logger.LogInformation("Anomaly {AnomalyId} resolved by {ResolvedBy}", anomalyId, sanitizedResolvedBy);
            }
        }

        private static AnomalyDetectionResult MapToResult(AnomalyEntity entity)
        {
            return new AnomalyDetectionResult
            {
                Id = entity.Id,
                DeviceId = entity.DeviceId,
                DeviceName = entity.Device?.MachineName ?? "Unknown",
                Type = Enum.Parse<AnomalyType>(entity.AnomalyType),
                Description = entity.Description,
                Severity = entity.Severity,
                DetectedAtUtc = entity.DetectedAtUtc,
                Status = Enum.Parse<AnomalyStatus>(entity.Status),
                ResolvedBy = entity.ResolvedBy,
                ResolvedAtUtc = entity.ResolvedAtUtc,
                Metadata = entity.Metadata
            };
        }
    }
}
