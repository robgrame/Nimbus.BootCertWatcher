using System;

namespace SecureBootDashboard.Api.Configuration
{
    /// <summary>
    /// Configuration options for the Azure Queue processor background service.
 /// </summary>
    public sealed class QueueProcessorOptions
    {
        /// <summary>
   /// Enables or disables the queue processor background service.
/// </summary>
  public bool Enabled { get; set; } = false;

        /// <summary>
   /// Storage account queue service URI (e.g., https://mystorageaccount.queue.core.windows.net).
        /// </summary>
public Uri? QueueServiceUri { get; set; }

   /// <summary>
     /// Queue name to process messages from.
        /// </summary>
        public string QueueName { get; set; } = "secureboot-reports";

      /// <summary>
   /// Authentication method: "ManagedIdentity", "AppRegistration", "Certificate", "DefaultAzureCredential", or "ConnectionString".
        /// Default is "ManagedIdentity" (recommended for Azure App Services).
        /// </summary>
        public string AuthenticationMethod { get; set; } = "ManagedIdentity";

     /// <summary>
        /// Connection string (only used if AuthenticationMethod is "ConnectionString").
        /// NOT RECOMMENDED for production - use Managed Identity or App Registration instead.
        /// </summary>
        public string? ConnectionString { get; set; }

        /// <summary>
        /// Application (Client) ID from Azure App Registration.
        /// Required for "AppRegistration" and "Certificate" authentication methods.
        /// </summary>
        public string? ClientId { get; set; }

        /// <summary>
        /// Directory (Tenant) ID where the App Registration exists.
        /// Required for "AppRegistration" and "Certificate" authentication methods.
        /// </summary>
   public string? TenantId { get; set; }

  /// <summary>
  /// Client Secret from App Registration (for "AppRegistration" method).
    /// Should be stored in Azure Key Vault or environment variables, NOT in config files.
        /// </summary>
        public string? ClientSecret { get; set; }

   /// <summary>
/// Path to certificate file (.pfx) for certificate-based authentication.
        /// </summary>
  public string? CertificatePath { get; set; }

        /// <summary>
 /// Password for the certificate file (if password-protected).
   /// </summary>
   public string? CertificatePassword { get; set; }

        /// <summary>
     /// Certificate thumbprint for certificate-based authentication from Windows Certificate Store.
      /// </summary>
        public string? CertificateThumbprint { get; set; }

        /// <summary>
   /// Certificate store location when using CertificateThumbprint.
        /// Values: "CurrentUser" or "LocalMachine". Default: "LocalMachine".
        /// </summary>
        public string CertificateStoreLocation { get; set; } = "LocalMachine";

   /// <summary>
        /// Certificate store name when using CertificateThumbprint.
        /// Values: "My" (Personal), "Root", "CA", etc. Default: "My".
    /// </summary>
        public string CertificateStoreName { get; set; } = "My";

      /// <summary>
        /// Number of messages to retrieve per batch.
      /// </summary>
        public int MaxMessages { get; set; } = 10;

    /// <summary>
      /// Interval between queue polling cycles when messages are found.
   /// </summary>
    public TimeSpan ProcessingInterval { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
  /// Interval between queue polling cycles when queue is empty.
        /// </summary>
        public TimeSpan EmptyQueuePollInterval { get; set; } = TimeSpan.FromSeconds(30);

 /// <summary>
        /// Visibility timeout for dequeued messages (how long before message becomes visible again if not deleted).
  /// </summary>
        public TimeSpan VisibilityTimeout { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Maximum number of times a message can be dequeued before being moved to poison queue.
    /// </summary>
        public int MaxDequeueCount { get; set; } = 5;
}
}
