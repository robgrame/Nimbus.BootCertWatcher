using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SecureBootDashboard.Api.Data;

namespace SecureBootDashboard.Api.Services
{
    /// <summary>
    /// Background service that automatically cleans up inactive devices based on configuration.
    /// </summary>
    public sealed class DeviceCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DeviceCleanupService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1); // Check config every hour

        public DeviceCleanupService(
            IServiceProvider serviceProvider,
            ILogger<DeviceCleanupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[DeviceCleanup] Service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessCleanupAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[DeviceCleanup] Error during cleanup processing");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }

            _logger.LogInformation("[DeviceCleanup] Service stopped");
        }

        private async Task ProcessCleanupAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SecureBootDbContext>();

            // Get cleanup configuration
            var config = await dbContext.DeviceCleanupConfig
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken);

            if (config == null)
            {
                _logger.LogWarning("[DeviceCleanup] No configuration found, skipping cleanup");
                return;
            }

            if (!config.Enabled)
            {
                _logger.LogDebug("[DeviceCleanup] Cleanup is disabled");
                return;
            }

            // Check if we should run cleanup based on schedule
            var shouldRun = ShouldRunCleanup(config);
            if (!shouldRun)
            {
                _logger.LogDebug("[DeviceCleanup] Not yet time for cleanup run");
                return;
            }

            _logger.LogInformation(
                "[DeviceCleanup] Starting cleanup run. Threshold: {Days} days",
                config.InactiveDaysThreshold);

            // Calculate cutoff date
            var cutoffDate = DateTimeOffset.UtcNow.AddDays(-config.InactiveDaysThreshold);

            // Find inactive devices
            var inactiveDevices = await dbContext.Devices
                .Where(d => d.LastSeenUtc < cutoffDate)
                .ToListAsync(cancellationToken);

            if (inactiveDevices.Count == 0)
            {
                _logger.LogInformation("[DeviceCleanup] No inactive devices found");
                await UpdateCleanupConfigAsync(dbContext, config, 0, cancellationToken);
                return;
            }

            _logger.LogInformation(
                "[DeviceCleanup] Found {Count} inactive devices to delete",
                inactiveDevices.Count);

            // Delete devices (cascade will handle reports, events, commands)
            dbContext.Devices.RemoveRange(inactiveDevices);
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "[DeviceCleanup] Successfully deleted {Count} inactive devices",
                inactiveDevices.Count);

            // Update cleanup config
            await UpdateCleanupConfigAsync(dbContext, config, inactiveDevices.Count, cancellationToken);

            // TODO: Send notification email if configured
            if (config.NotifyOnCleanup && !string.IsNullOrEmpty(config.NotificationEmail))
            {
                _logger.LogInformation(
                    "[DeviceCleanup] Notification enabled but email sending not yet implemented. " +
                    "Would send to: {Email}",
                    config.NotificationEmail);
            }
        }

        private bool ShouldRunCleanup(DeviceCleanupConfigEntity config)
        {
            // If never run, run now
            if (!config.LastCleanupRunUtc.HasValue)
            {
                return true;
            }

            // Simple daily check: run if last run was more than 23 hours ago
            // In a production system, you would parse the cron schedule
            var timeSinceLastRun = DateTimeOffset.UtcNow - config.LastCleanupRunUtc.Value;
            return timeSinceLastRun.TotalHours >= 23;
        }

        private async Task UpdateCleanupConfigAsync(
            SecureBootDbContext dbContext,
            DeviceCleanupConfigEntity config,
            int deviceCount,
            CancellationToken cancellationToken)
        {
            // Load the entity for tracking
            var trackedConfig = await dbContext.DeviceCleanupConfig
                .FirstOrDefaultAsync(c => c.Id == config.Id, cancellationToken);

            if (trackedConfig != null)
            {
                trackedConfig.LastCleanupRunUtc = DateTimeOffset.UtcNow;
                trackedConfig.LastCleanupDeviceCount = deviceCount;
                trackedConfig.UpdatedAtUtc = DateTimeOffset.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
