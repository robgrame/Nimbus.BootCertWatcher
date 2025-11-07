using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SecureBootDashboard.Api.Services
{
    /// <summary>
    /// Background service that periodically runs anomaly detection
    /// </summary>
    public sealed class AnomalyDetectionBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AnomalyDetectionBackgroundService> _logger;
        private readonly TimeSpan _interval = TimeSpan.FromHours(1); // Run every hour

        public AnomalyDetectionBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<AnomalyDetectionBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Anomaly Detection Background Service started");

            // Wait a bit before first run to allow system to stabilize
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Running anomaly detection scan");

                    using var scope = _serviceProvider.CreateScope();
                    var anomalyService = scope.ServiceProvider.GetRequiredService<IAnomalyDetectionService>();

                    var anomalies = await anomalyService.DetectAnomaliesAsync(stoppingToken);
                    
                    _logger.LogInformation("Anomaly detection completed. Found {Count} new anomalies", anomalies.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during anomaly detection scan");
                }

                await Task.Delay(_interval, stoppingToken);
            }

            _logger.LogInformation("Anomaly Detection Background Service stopped");
        }
    }
}
