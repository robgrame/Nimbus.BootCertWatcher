namespace SecureBootDashboard.Api.Data;

/// <summary>
/// API configuration stored in database for dynamic runtime configuration.
/// Allows modifying queue processor, file share, and other API settings without redeployment.
/// </summary>
public sealed class ApiConfigurationEntity
{
    /// <summary>
    /// Unique identifier for the configuration
    /// </summary>
    public int Id { get; set; }

    // === Queue Processor Settings ===

    /// <summary>
    /// Enable Azure Queue processor background service
    /// </summary>
    public bool QueueProcessorEnabled { get; set; } = true;

    /// <summary>
    /// Azure Queue service URI (e.g., https://account.queue.core.windows.net)
    /// </summary>
    public string? QueueServiceUri { get; set; }

    /// <summary>
    /// Queue name to process messages from
    /// </summary>
    public string QueueName { get; set; } = "secureboot-reports";

    /// <summary>
    /// Authentication method: ManagedIdentity, AppRegistration, Certificate, DefaultAzureCredential, ConnectionString
    /// </summary>
    public string QueueAuthenticationMethod { get; set; } = "ManagedIdentity";

    /// <summary>
    /// Connection string (only for ConnectionString auth method)
    /// </summary>
    public string? QueueConnectionString { get; set; }

    /// <summary>
    /// Client ID for AppRegistration/Certificate auth
    /// </summary>
    public string? QueueClientId { get; set; }

    /// <summary>
    /// Tenant ID for AppRegistration/Certificate auth
    /// </summary>
    public string? QueueTenantId { get; set; }

    /// <summary>
    /// Client Secret for AppRegistration auth
    /// </summary>
    public string? QueueClientSecret { get; set; }

    /// <summary>
    /// Certificate path for Certificate auth
    /// </summary>
    public string? QueueCertificatePath { get; set; }

    /// <summary>
    /// Certificate password
    /// </summary>
    public string? QueueCertificatePassword { get; set; }

    /// <summary>
    /// Certificate thumbprint for store-based auth
    /// </summary>
    public string? QueueCertificateThumbprint { get; set; }

    /// <summary>
    /// Certificate store location (CurrentUser or LocalMachine)
    /// </summary>
    public string QueueCertificateStoreLocation { get; set; } = "LocalMachine";

    /// <summary>
    /// Certificate store name (My, Root, CA, etc.)
    /// </summary>
    public string QueueCertificateStoreName { get; set; } = "My";

    /// <summary>
    /// Number of messages to retrieve per batch
    /// </summary>
    public int QueueMaxMessages { get; set; } = 10;

    /// <summary>
    /// Processing interval in seconds
    /// </summary>
    public int QueueProcessingIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// Empty queue poll interval in seconds
    /// </summary>
    public int QueueEmptyQueuePollIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Visibility timeout in seconds
    /// </summary>
    public int QueueVisibilityTimeoutSeconds { get; set; } = 300; // 5 minutes

    /// <summary>
    /// Maximum dequeue count before moving to poison queue
    /// </summary>
    public int QueueMaxDequeueCount { get; set; } = 5;

    // === File Report Store Settings ===

    /// <summary>
    /// Enable file-based report storage
    /// </summary>
    public bool FileReportStoreEnabled { get; set; } = false;

    /// <summary>
    /// Base path for file storage
    /// </summary>
    public string? FileReportStoreBasePath { get; set; }

    /// <summary>
    /// File extension for reports (default: .json)
    /// </summary>
    public string FileReportStoreExtension { get; set; } = ".json";

    /// <summary>
    /// Append timestamp to filename
    /// </summary>
    public bool FileReportStoreAppendTimestamp { get; set; } = true;

    // === Device Cleanup Settings ===

    /// <summary>
    /// Enable automatic device cleanup
    /// </summary>
    public bool DeviceCleanupEnabled { get; set; } = true;

    /// <summary>
    /// Cleanup schedule (cron expression)
    /// </summary>
    public string DeviceCleanupSchedule { get; set; } = "0 2 * * 0"; // Every Sunday at 2 AM

    /// <summary>
    /// Days threshold for inactive devices
    /// </summary>
    public int DeviceCleanupDaysThreshold { get; set; } = 90;

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
