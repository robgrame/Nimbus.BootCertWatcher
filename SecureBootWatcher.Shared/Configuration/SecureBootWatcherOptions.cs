using System;

namespace SecureBootWatcher.Shared.Configuration
{
    public sealed class SecureBootWatcherOptions
    {
        public string? FleetId { get; set; }

        public TimeSpan RegistryPollInterval { get; set; } = TimeSpan.FromMinutes(30);

        public TimeSpan EventQueryInterval { get; set; } = TimeSpan.FromMinutes(30);

        public TimeSpan EventLookbackPeriod { get; set; } = TimeSpan.FromHours(24);

        public string[] EventChannels { get; set; } = new[]
        {
            "Microsoft-Windows-DeviceManagement-Enterprise-Diagnostics-Provider/Admin",
            "Microsoft-Windows-CodeIntegrity/Operational"
        };

        public SinkOptions Sinks { get; set; } = new SinkOptions();
    }

    public sealed class SinkOptions
    {
        public bool EnableFileShare { get; set; }

        public FileShareSinkOptions FileShare { get; set; } = new FileShareSinkOptions();

        public bool EnableAzureQueue { get; set; }

        public AzureQueueSinkOptions AzureQueue { get; set; } = new AzureQueueSinkOptions();

        public bool EnableWebApi { get; set; }

        public WebApiSinkOptions WebApi { get; set; } = new WebApiSinkOptions();
    }

    public sealed class FileShareSinkOptions
    {
        public string? RootPath { get; set; }

        public string FileExtension { get; set; } = ".json";

        public bool AppendTimestampToFileName { get; set; } = true;
    }

    public sealed class AzureQueueSinkOptions
    {
        /// <summary>
        /// Gets or sets the resource ID or URI of the storage account queue endpoint.
        /// </summary>
        public string? QueueEndpoint { get; set; }

        public string? ConnectionString { get; set; }

        public string QueueName { get; set; } = "secureboot-reports";

        public TimeSpan VisibilityTimeout { get; set; } = TimeSpan.FromMinutes(5);

        public int MaxSendRetryCount { get; set; } = 5;
    }

    public sealed class WebApiSinkOptions
    {
        public Uri? BaseAddress { get; set; }

        public string IngestionRoute { get; set; } = "/api/secureboot/reports";

        public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(30);
    }
}
