using Microsoft.Extensions.Options;
using SecureBootDashboard.Api.Services;

namespace SecureBootDashboard.Api.Configuration;

/// <summary>
/// Provides QueueProcessorOptions from database configuration with fallback to appsettings.json.
/// This provider is invoked at startup and populates QueueProcessorOptions from ApiConfigurationService.
/// </summary>
public class QueueProcessorOptionsProvider : IConfigureOptions<QueueProcessorOptions>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<QueueProcessorOptionsProvider> _logger;

    public QueueProcessorOptionsProvider(
        IServiceProvider serviceProvider,
        ILogger<QueueProcessorOptionsProvider> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public void Configure(QueueProcessorOptions options)
    {
        // This method is called when IOptionsMonitor<QueueProcessorOptions> is first accessed
        // It allows us to override values from appsettings.json with database values
        
        try
        {
            _logger.LogDebug("QueueProcessorOptionsProvider: Attempting to load configuration from database");

            // Create a scope to resolve scoped services (ApiConfigurationService is scoped)
            using var scope = _serviceProvider.CreateScope();
            var apiConfigService = scope.ServiceProvider.GetService<IApiConfigurationService>();

            if (apiConfigService == null)
            {
                _logger.LogWarning(
                    "ApiConfigurationService not available. Queue Processor will use appsettings.json configuration.");
                return;
            }

            // Get configuration from database (synchronous call at startup)
            // Note: Using .GetAwaiter().GetResult() is safe here because this is called during startup
            // before any async operations begin
            var dbOptions = apiConfigService.GetQueueProcessorOptionsAsync().GetAwaiter().GetResult();

            if (dbOptions == null)
            {
                _logger.LogWarning(
                    "No Queue Processor configuration found in database. Using appsettings.json configuration.");
                return;
            }

            // Only override if database config is explicitly enabled
            // This allows admins to temporarily disable queue processing via database
            if (!dbOptions.Enabled)
            {
                _logger.LogInformation(
                    "Queue Processor is DISABLED in database configuration. Using appsettings.json settings.");
                
                // Keep the appsettings.json value for Enabled
                // Don't override other settings if queue is disabled in DB
                return;
            }

            // Database configuration is enabled - override appsettings.json values
            _logger.LogInformation("Loading Queue Processor configuration from DATABASE");

            options.Enabled = dbOptions.Enabled;
            options.QueueServiceUri = dbOptions.QueueServiceUri;
            options.QueueName = dbOptions.QueueName;
            options.AuthenticationMethod = dbOptions.AuthenticationMethod;
            options.ConnectionString = dbOptions.ConnectionString;
            options.ClientId = dbOptions.ClientId;
            options.TenantId = dbOptions.TenantId;
            options.ClientSecret = dbOptions.ClientSecret;
            options.CertificatePath = dbOptions.CertificatePath;
            options.CertificatePassword = dbOptions.CertificatePassword;
            options.CertificateThumbprint = dbOptions.CertificateThumbprint;
            options.CertificateStoreLocation = dbOptions.CertificateStoreLocation;
            options.CertificateStoreName = dbOptions.CertificateStoreName;
            options.MaxMessages = dbOptions.MaxMessages;
            options.ProcessingInterval = dbOptions.ProcessingInterval;
            options.EmptyQueuePollInterval = dbOptions.EmptyQueuePollInterval;
            options.VisibilityTimeout = dbOptions.VisibilityTimeout;
            options.MaxDequeueCount = dbOptions.MaxDequeueCount;

            _logger.LogInformation(
                "? Queue Processor configured from DATABASE: " +
                "Enabled={Enabled}, Queue={QueueName}, Auth={AuthMethod}, Uri={QueueUri}",
                options.Enabled,
                options.QueueName,
                options.AuthenticationMethod,
                options.QueueServiceUri?.ToString() ?? "NULL");

            _logger.LogInformation(
                "Queue Processing Settings: MaxMessages={MaxMessages}, ProcessingInterval={ProcessingInterval}s, EmptyQueuePollInterval={EmptyQueuePollInterval}s",
                options.MaxMessages,
                options.ProcessingInterval.TotalSeconds,
                options.EmptyQueuePollInterval.TotalSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "? Failed to load Queue Processor configuration from database. " +
                "Falling back to appsettings.json configuration.");
            
            // Options will retain their default values from appsettings.json
            // This ensures the service continues to work even if database is unavailable
        }
    }
}
