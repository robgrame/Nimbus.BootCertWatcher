using WindowsVersionsCore.Services;

namespace WindowsVersionsCore.Services.BackgroundTasks
{
    /// <summary>
    /// Background service that periodically scrapes Windows version data
    /// </summary>
    public class WindowsVersionScraper : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<WindowsVersionScraper> _logger;
        private readonly IConfiguration _configuration;

        public WindowsVersionScraper(
            IServiceProvider serviceProvider,
            ILogger<WindowsVersionScraper> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var intervalMinutes = _configuration.GetValue<int>("WindowsScraper:intervalMinutes", 12);
            var initialDelayMinutes = _configuration.GetValue<int>("WindowsScraper:InitialDelayMinutes", 5);

            _logger.LogInformation("Windows Version Scraper starting with {intervalMinutes} hour interval", intervalMinutes);

            // Initial delay to allow the application to fully start
            await Task.Delay(TimeSpan.FromMinutes(initialDelayMinutes), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Starting Windows version data scraping");

                    using var scope = _serviceProvider.CreateScope();
                    var windowsService = scope.ServiceProvider.GetRequiredService<IWindowsService>();

                    var success = await windowsService.RefreshDataAsync();

                    if (success)
                    {
                        _logger.LogInformation("Windows version data scraping completed successfully");
                    }
                    else
                    {
                        _logger.LogWarning("Windows version data scraping completed with errors");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during Windows version data scraping");
                }

                // Wait for the next interval
                await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
            }

            _logger.LogInformation("Windows Version Scraper stopping");
        }
    }
}