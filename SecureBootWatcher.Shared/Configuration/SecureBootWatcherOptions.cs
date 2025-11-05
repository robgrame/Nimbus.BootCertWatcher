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

        /// <summary>
        /// Sink execution strategy: "StopOnFirstSuccess" or "TryAll".
        /// - "StopOnFirstSuccess": Stops after the first sink succeeds (default, faster).
        /// - "TryAll": Sends to all enabled sinks regardless of success (redundancy, slower).
        /// </summary>
        public string ExecutionStrategy { get; set; } = "StopOnFirstSuccess";

        /// <summary>
        /// Priority order for sinks. Format: "AzureQueue,WebApi,FileShare".
        /// Sinks are tried in this order. If not specified, default order is: AzureQueue, WebApi, FileShare.
        /// Only enabled sinks are executed.
        /// </summary>
        public string SinkPriority { get; set; } = "AzureQueue,WebApi,FileShare";
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
        /// Storage account queue service URI (e.g., https://mystorageaccount.queue.core.windows.net).
        /// </summary>
        public Uri? QueueServiceUri { get; set; }

        /// <summary>
        /// Queue name within the storage account.
        /// </summary>
        public string QueueName { get; set; } = "secureboot-reports";

        /// <summary>
        /// Authentication method: "ManagedIdentity", "AppRegistration", "Certificate", "DefaultAzureCredential", or "ConnectionString".
        /// Default is "DefaultAzureCredential" which tries multiple credential sources automatically.
        /// Recommended for production: "AppRegistration" or "Certificate".
        /// </summary>
        public string AuthenticationMethod { get; set; } = "DefaultAzureCredential";

        /// <summary>
        /// Connection string (only used if AuthenticationMethod is "ConnectionString").
        /// NOT RECOMMENDED for production - use App Registration or Managed Identity instead.
        /// </summary>
        public string? ConnectionString { get; set; }

        /// <summary>
        /// Application (Client) ID from Azure App Registration.
        /// Required for "AppRegistration" and "Certificate" authentication methods.
        /// Find in: Azure Portal ? Entra ID ? App registrations ? Your App ? Overview
        /// </summary>
        public string? ClientId { get; set; }

        /// <summary>
        /// Directory (Tenant) ID where the App Registration exists.
        /// Required for "AppRegistration" and "Certificate" authentication methods.
        /// Find in: Azure Portal ? Entra ID ? Overview ? Tenant ID
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// Client Secret from App Registration (for "AppRegistration" method).
        /// Should be stored in Azure Key Vault or environment variables, NOT in config files.
        /// Find in: Azure Portal ? Entra ID ? App registrations ? Your App ? Certificates & secrets
        /// </summary>
        public string? ClientSecret { get; set; }

        /// <summary>
        /// Path to certificate file (.pfx or .pem) for certificate-based authentication.
        /// Used with "Certificate" authentication method.
        /// More secure than Client Secret - recommended for production.
        /// </summary>
        public string? CertificatePath { get; set; }

        /// <summary>
        /// Password for the certificate file (if the .pfx is password-protected).
        /// Should be stored in Azure Key Vault or environment variables.
        /// </summary>
        public string? CertificatePassword { get; set; }

        /// <summary>
        /// Certificate thumbprint for certificate-based authentication from certificate store.
        /// Alternative to CertificatePath - looks for certificate in Windows Certificate Store.
        /// Format: "ABC123DEF456..." (SHA-1 thumbprint, no spaces or colons)
        /// </summary>
        public string? CertificateThumbprint { get; set; }

        /// <summary>
        /// Certificate store location when using CertificateThumbprint.
        /// Values: "CurrentUser" or "LocalMachine"
        /// Default: "CurrentUser"
        /// </summary>
        public string CertificateStoreLocation { get; set; } = "CurrentUser";

        /// <summary>
        /// Certificate store name when using CertificateThumbprint.
        /// Values: "My" (Personal), "Root", "CA", etc.
        /// Default: "My" (Personal certificates)
        /// </summary>
        public string CertificateStoreName { get; set; } = "My";

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
