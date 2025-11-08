using System;

namespace SecureBootDashboard.Api.Configuration
{
    /// <summary>
    /// Configuration options for the Certificate Update Service.
    /// </summary>
    public sealed class CertificateUpdateServiceOptions
    {
        /// <summary>
        /// Whether the certificate update service is enabled.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Storage account queue service URI (e.g., https://mystorageaccount.queue.core.windows.net).
        /// </summary>
        public Uri? QueueServiceUri { get; set; }

        /// <summary>
        /// Queue name for certificate update commands.
        /// </summary>
        public string CommandQueueName { get; set; } = "secureboot-update-commands";

        /// <summary>
        /// Authentication method: "ManagedIdentity", "AppRegistration", "Certificate", "DefaultAzureCredential", or "ConnectionString".
        /// </summary>
        public string AuthenticationMethod { get; set; } = "DefaultAzureCredential";

        /// <summary>
        /// Connection string (only used if AuthenticationMethod is "ConnectionString").
        /// </summary>
        public string? ConnectionString { get; set; }

        /// <summary>
        /// Application (Client) ID from Azure App Registration.
        /// </summary>
        public string? ClientId { get; set; }

        /// <summary>
        /// Directory (Tenant) ID.
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// Client Secret from App Registration.
        /// </summary>
        public string? ClientSecret { get; set; }

        /// <summary>
        /// Path to certificate file (.pfx or .pem).
        /// </summary>
        public string? CertificatePath { get; set; }

        /// <summary>
        /// Password for the certificate file.
        /// </summary>
        public string? CertificatePassword { get; set; }

        /// <summary>
        /// Certificate thumbprint for certificate store.
        /// </summary>
        public string? CertificateThumbprint { get; set; }

        /// <summary>
        /// Certificate store location.
        /// </summary>
        public string CertificateStoreLocation { get; set; } = "CurrentUser";

        /// <summary>
        /// Certificate store name.
        /// </summary>
        public string CertificateStoreName { get; set; } = "My";
    }
}
