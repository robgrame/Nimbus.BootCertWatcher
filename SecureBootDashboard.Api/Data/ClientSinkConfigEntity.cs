namespace SecureBootDashboard.Api.Data;

/// <summary>
/// Client sink configuration stored in database for centralized management.
/// Allows dynamic configuration of client reporting sinks without client-side config changes.
/// </summary>
public sealed class ClientSinkConfigEntity
{
    /// <summary>
    /// Unique identifier for the configuration
    /// </summary>
    public int Id { get; set; }

    // === General Sink Settings ===

    /// <summary>
    /// Enable FileShare sink
    /// </summary>
    public bool EnableFileShare { get; set; }

    /// <summary>
    /// Enable Azure Queue sink
    /// </summary>
    public bool EnableAzureQueue { get; set; }

    /// <summary>
    /// Enable Web API sink
    /// </summary>
    public bool EnableWebApi { get; set; }

    /// <summary>
    /// Sink execution strategy: "StopOnFirstSuccess" or "TryAll"
    /// </summary>
    public string ExecutionStrategy { get; set; } = "StopOnFirstSuccess";

    /// <summary>
    /// Priority order for sinks (e.g., "AzureQueue,WebApi,FileShare")
    /// </summary>
    public string SinkPriority { get; set; } = "AzureQueue,WebApi,FileShare";

    /// <summary>
    /// Maximum retry attempts per sink
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Retry delay in seconds
    /// </summary>
    public int RetryDelaySeconds { get; set; } = 300; // 5 minutes

    /// <summary>
    /// Use exponential backoff for retries
    /// </summary>
    public bool UseExponentialBackoff { get; set; }

    // === FileShare Sink Settings ===

    /// <summary>
    /// FileShare root path (UNC or local path)
    /// </summary>
    public string? FileShareRootPath { get; set; }

    /// <summary>
    /// FileShare file extension (default: .json)
    /// </summary>
    public string FileShareExtension { get; set; } = ".json";

    /// <summary>
    /// Append timestamp to FileShare filename
    /// </summary>
    public bool FileShareAppendTimestamp { get; set; } = true;

    // === Azure Queue Sink Settings ===

    /// <summary>
    /// Azure Queue service URI (e.g., https://account.queue.core.windows.net)
    /// </summary>
    public string? AzureQueueServiceUri { get; set; }

    /// <summary>
    /// Azure Queue name
    /// </summary>
    public string AzureQueueName { get; set; } = "secureboot-reports";

    /// <summary>
    /// Azure Queue authentication method (ManagedIdentity, AppRegistration, Certificate, DefaultAzureCredential, ConnectionString)
    /// </summary>
    public string AzureQueueAuthMethod { get; set; } = "DefaultAzureCredential";

    /// <summary>
    /// Azure Queue connection string (sensitive, only for ConnectionString auth)
    /// </summary>
    public string? AzureQueueConnectionString { get; set; }

    /// <summary>
    /// Azure Queue client ID (for AppRegistration or Certificate auth)
    /// </summary>
    public string? AzureQueueClientId { get; set; }

    /// <summary>
    /// Azure Queue tenant ID (for AppRegistration or Certificate auth)
    /// </summary>
    public string? AzureQueueTenantId { get; set; }

    /// <summary>
    /// Azure Queue client secret (sensitive, for AppRegistration auth)
    /// </summary>
    public string? AzureQueueClientSecret { get; set; }

    /// <summary>
    /// Azure Queue certificate path (for Certificate auth)
    /// </summary>
    public string? AzureQueueCertPath { get; set; }

    /// <summary>
    /// Azure Queue certificate password (sensitive)
    /// </summary>
    public string? AzureQueueCertPassword { get; set; }

    /// <summary>
    /// Azure Queue certificate thumbprint (for Certificate auth from store)
    /// </summary>
    public string? AzureQueueCertThumbprint { get; set; }

    /// <summary>
    /// Azure Queue certificate store location (CurrentUser or LocalMachine)
    /// </summary>
    public string AzureQueueCertStoreLocation { get; set; } = "CurrentUser";

    /// <summary>
    /// Azure Queue certificate store name (My, Root, CA, etc.)
    /// </summary>
    public string AzureQueueCertStoreName { get; set; } = "My";

    /// <summary>
    /// Azure Queue visibility timeout in seconds
    /// </summary>
    public int AzureQueueVisibilityTimeoutSeconds { get; set; } = 300; // 5 minutes

    /// <summary>
    /// Azure Queue max send retry count
    /// </summary>
    public int AzureQueueMaxSendRetryCount { get; set; } = 5;

    // === Web API Sink Settings ===

    /// <summary>
    /// Web API base address (e.g., https://api.example.com)
    /// </summary>
    public string? WebApiBaseAddress { get; set; }

    /// <summary>
    /// Web API ingestion route (default: /api/SecureBootReports)
    /// </summary>
    public string WebApiIngestionRoute { get; set; } = "/api/SecureBootReports";

    /// <summary>
    /// Web API HTTP timeout in seconds
    /// </summary>
    public int WebApiTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Enable client certificate authentication for Web API
    /// </summary>
    public bool WebApiUseCertAuth { get; set; }

    /// <summary>
    /// Web API certificate path (for certificate auth)
    /// </summary>
    public string? WebApiCertPath { get; set; }

    /// <summary>
    /// Web API certificate password (sensitive)
    /// </summary>
    public string? WebApiCertPassword { get; set; }

    /// <summary>
    /// Web API certificate thumbprint (for certificate auth from store)
    /// </summary>
    public string? WebApiCertThumbprint { get; set; }

    /// <summary>
    /// Web API certificate store location (CurrentUser or LocalMachine)
    /// </summary>
    public string WebApiCertStoreLocation { get; set; } = "LocalMachine";

    /// <summary>
    /// Web API certificate store name (My, Root, CA, etc.)
    /// </summary>
    public string WebApiCertStoreName { get; set; } = "My";

    // === Metadata ===

    /// <summary>
    /// Configuration description/notes
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Whether this configuration is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// When the configuration was created
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>
    /// User or system that created this configuration
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// When the configuration was last updated
    /// </summary>
    public DateTimeOffset UpdatedAtUtc { get; set; }

    /// <summary>
    /// User or system that last modified this configuration
    /// </summary>
    public string? UpdatedBy { get; set; }
}
