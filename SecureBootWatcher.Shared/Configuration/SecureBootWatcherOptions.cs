using System;
using System.Collections.Generic;

namespace SecureBootWatcher.Shared.Configuration
{
    /// <summary>
    /// Configuration for a Certificate Authority (CA) certificate.
    /// Used to validate certificate chains against expected CA certificates.
    /// </summary>
    public sealed class CertificateAuthorityConfig
    {
        /// <summary>
        /// Name or Subject of the CA certificate (e.g., "CN=Contoso Root CA, O=Contoso, C=US").
        /// Used for identification and logging purposes.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// SHA-1 thumbprint of the CA certificate.
        /// Format: "ABC123DEF456..." (no spaces or colons).
        /// Used to validate the certificate chain against expected CAs.
        /// </summary>
        public string? Thumbprint { get; set; }
    }

    public sealed class SecureBootWatcherOptions
    {
        public string? FleetId { get; set; }

        /// <summary>
        /// Run mode: "Once" or "Continuous".
        /// - "Once": Execute a single report generation cycle and exit (for scheduled tasks).
        /// - "Continuous": Run indefinitely with periodic polling (default, for services).
        /// </summary>
        public string RunMode { get; set; } = "Once";

        public TimeSpan RegistryPollInterval { get; set; } = TimeSpan.FromMinutes(30);

        public TimeSpan EventQueryInterval { get; set; } = TimeSpan.FromMinutes(30);

        public TimeSpan EventLookbackPeriod { get; set; } = TimeSpan.FromHours(24);

        public string[] EventChannels { get; set; } = new[]
        {
            "Microsoft-Windows-DeviceManagement-Enterprise-Diagnostics-Provider/Admin",
            "Microsoft-Windows-CodeIntegrity/Operational"
        };

        public SinkOptions Sinks { get; set; } = new SinkOptions();

        public ClientUpdateOptions ClientUpdate { get; set; } = new ClientUpdateOptions();

        public CommandProcessingOptions Commands { get; set; } = new CommandProcessingOptions();
    }

    public sealed class CommandProcessingOptions
    {
        /// <summary>
        /// Enable processing of configuration commands from the API.
        /// When enabled, client will fetch pending commands, execute them, verify results, and report back.
        /// Default: false (opt-in feature)
        /// </summary>
        public bool EnableCommandProcessing { get; set; } = false;

        /// <summary>
        /// Process commands before inventory collection (true) or after (false).
        /// Recommended: true (apply configuration changes before capturing state)
        /// Default: true
        /// </summary>
        public bool ProcessBeforeInventory { get; set; } = true;

        /// <summary>
        /// Maximum number of commands to process in a single execution cycle.
        /// Prevents runaway processing if many commands are queued.
        /// Default: 10
        /// </summary>
        public int MaxCommandsPerCycle { get; set; } = 10;

        /// <summary>
        /// Delay between command executions to allow registry changes to propagate.
        /// Default: 2 seconds
        /// </summary>
        public TimeSpan CommandExecutionDelay { get; set; } = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Whether to continue inventory collection if command processing fails.
        /// true = Always send inventory even if commands fail
        /// false = Skip inventory if commands fail
        /// Default: true (resilient mode)
        /// </summary>
        public bool ContinueOnCommandFailure { get; set; } = true;
    }

    public sealed class ClientUpdateOptions
    {
        /// <summary>
        /// Enable checking for client updates.
        /// Default: true
        /// </summary>
        public bool CheckForUpdates { get; set; } = true;

        /// <summary>
        /// Automatically download updates when available.
        /// Default: false (notify only)
        /// </summary>
        public bool AutoDownloadEnabled { get; set; } = false;

        /// <summary>
        /// Automatically install downloaded updates.
        /// Requires AutoDownloadEnabled = true.
        /// Default: false
        /// </summary>
        public bool AutoInstallEnabled { get; set; } = false;

        /// <summary>
        /// Add alert to report when update is available.
        /// Default: true
        /// </summary>
        public bool NotifyOnUpdateAvailable { get; set; } = true;
    }

    public sealed class SinkOptions
    {
        public bool EnableFileShare { get; set; }

        public FileShareSinkOptions FileShare { get; set; } = new FileShareSinkOptions();

        public bool EnableAzureQueue { get; set; }

        public AzureQueueSinkOptions AzureQueue { get; set; } = new AzureQueueSinkOptions();

        public bool EnableWebApi { get; set; }

        public WebApiSinkOptions WebApi { get; set; } = new WebApiSinkOptions();

        public bool EnableAzureFunction { get; set; }

        public AzureFunctionSinkOptions AzureFunction { get; set; } = new AzureFunctionSinkOptions();

        /// <summary>
        /// Sink execution strategy: "StopOnFirstSuccess" or "TryAll".
        /// - "StopOnFirstSuccess": Stops after the first sink succeeds (default, faster).
        /// - "TryAll": Sends to all enabled sinks regardless of success (redundancy, slower).
        /// </summary>
        public string ExecutionStrategy { get; set; } = "StopOnFirstSuccess";

        /// <summary>
        /// Priority order for sinks. Format: "AzureFunction,AzureQueue,WebApi,FileShare".
        /// Sinks are tried in this order. If not specified, default order is: AzureFunction, AzureQueue, WebApi, FileShare.
        /// Only enabled sinks are executed.
        /// </summary>
        public string SinkPriority { get; set; } = "AzureFunction,AzureQueue,WebApi,FileShare";

        /// <summary>
        /// Maximum number of retry attempts for each sink before moving to the next one.
        /// Default: 3 retries
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// Delay between retry attempts for each sink.
        /// Default: 5 minutes
        /// </summary>
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Whether to use exponential backoff for retries (delay doubles each attempt).
        /// Default: false (fixed delay)
        /// </summary>
        public bool UseExponentialBackoff { get; set; } = false;
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

        public string IngestionRoute { get; set; } = "/api/SecureBootReports";

        public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Enable client certificate authentication for mutual TLS.
        /// Default: false
        /// </summary>
        public bool UseCertificateAuth { get; set; }

        /// <summary>
        /// Path to client certificate file (.pfx or .p12) for certificate-based authentication.
        /// </summary>
        public string? CertificatePath { get; set; }

        /// <summary>
        /// Password for the certificate file (if the .pfx is password-protected).
        /// Should be stored securely, not in config files.
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
        /// Default: "LocalMachine"
        /// </summary>
        public string CertificateStoreLocation { get; set; } = "LocalMachine";

        /// <summary>
        /// Certificate store name when using CertificateThumbprint.
        /// Values: "My" (Personal), "Root", "CA", etc.
        /// Default: "My" (Personal certificates)
        /// </summary>
        public string CertificateStoreName { get; set; } = "My";

        /// <summary>
        /// Validate certificate chain when using certificate authentication.
        /// Default: true (recommended for production)
        /// </summary>
        public bool ValidateCertificateChain { get; set; } = true;

        /// <summary>
        /// Check Certificate Revocation List (CRL) for certificate revocation.
        /// Default: false (can cause delays if CRL server is unavailable)
        /// </summary>
        public bool CheckCertificateRevocation { get; set; } = false;

        /// <summary>
        /// Expected CA Root certificate name (Subject).
        /// If specified, the certificate chain will be validated to ensure it's signed by this CA Root.
        /// Example: "CN=Contoso Root CA, O=Contoso, C=US"
        /// </summary>
        public string? ExpectedCARootName { get; set; }

        /// <summary>
        /// Expected CA Root certificate thumbprint.
        /// If specified, the root certificate in the chain must match this thumbprint.
        /// Format: "ABC123DEF456..." (SHA-1 thumbprint, no spaces or colons)
        /// </summary>
        public string? ExpectedCARootThumbprint { get; set; }

        /// <summary>
        /// Expected Subordinate (Intermediate) CA certificates.
        /// List of subordinate CAs that should be present in the certificate chain.
        /// Each entry should contain the CA name and thumbprint for validation.
        /// </summary>
        public List<CertificateAuthorityConfig> ExpectedSubordinateCAs { get; set; } = new List<CertificateAuthorityConfig>();
    }

    public sealed class AzureFunctionSinkOptions
    {
        /// <summary>
        /// Azure Function URL for report ingestion.
        /// Example: https://your-function-app.azurewebsites.net/api/reports
        /// </summary>
        public Uri? FunctionUrl { get; set; }

        /// <summary>
        /// API Key for authenticating with the Azure Function.
        /// This key should be stored securely (e.g., Azure Key Vault).
        /// Can be used alone or in combination with certificate authentication.
        /// </summary>
        public string? ApiKey { get; set; }

        /// <summary>
        /// HTTP timeout for requests to the Azure Function.
        /// Default: 30 seconds
        /// </summary>
        public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Whether to send the API key as a query parameter (true) or header (false).
        /// Default: false (send as X-API-Key header - more secure)
        /// </summary>
        public bool UseApiKeyAsQueryParameter { get; set; } = false;

        /// <summary>
        /// Enable client certificate authentication for mutual TLS with the Azure Function.
        /// Can be used alone or in combination with API key for defense-in-depth.
        /// Default: false
        /// </summary>
        public bool UseCertificateAuth { get; set; }

        /// <summary>
        /// Path to client certificate file (.pfx or .p12) for certificate-based authentication.
        /// More secure than API key alone - recommended for production environments.
        /// </summary>
        public string? CertificatePath { get; set; }

        /// <summary>
        /// Password for the certificate file (if the .pfx is password-protected).
        /// Should be stored securely, not in config files.
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
        /// Default: "LocalMachine"
        /// </summary>
        public string CertificateStoreLocation { get; set; } = "LocalMachine";

        /// <summary>
        /// Certificate store name when using CertificateThumbprint.
        /// Values: "My" (Personal), "Root", "CA", etc.
        /// Default: "My" (Personal certificates)
        /// </summary>
        public string CertificateStoreName { get; set; } = "My";

        /// <summary>
        /// Validate certificate chain when using certificate authentication.
        /// Default: true (recommended for production)
        /// </summary>
        public bool ValidateCertificateChain { get; set; } = true;

        /// <summary>
        /// Check Certificate Revocation List (CRL) for certificate revocation.
        /// Default: false (can cause delays if CRL server is unavailable)
        /// </summary>
        public bool CheckCertificateRevocation { get; set; } = false;

        /// <summary>
        /// Expected CA Root certificate name (Subject).
        /// If specified, the certificate chain will be validated to ensure it's signed by this CA Root.
        /// Example: "CN=Contoso Root CA, O=Contoso, C=US"
        /// </summary>
        public string? ExpectedCARootName { get; set; }

        /// <summary>
        /// Expected CA Root certificate thumbprint.
        /// If specified, the root certificate in the chain must match this thumbprint.
        /// Format: "ABC123DEF456..." (SHA-1 thumbprint, no spaces or colons)
        /// </summary>
        public string? ExpectedCARootThumbprint { get; set; }

        /// <summary>
        /// Expected Subordinate (Intermediate) CA certificates.
        /// List of subordinate CAs that should be present in the certificate chain.
        /// Each entry should contain the CA name and thumbprint for validation.
        /// </summary>
        public List<CertificateAuthorityConfig> ExpectedSubordinateCAs { get; set; } = new List<CertificateAuthorityConfig>();
    }
}
