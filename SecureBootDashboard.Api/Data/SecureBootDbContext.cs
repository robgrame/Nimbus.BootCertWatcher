using Microsoft.EntityFrameworkCore;

namespace SecureBootDashboard.Api.Data
{
    public sealed class SecureBootDbContext : DbContext
    {
        public SecureBootDbContext(DbContextOptions<SecureBootDbContext> options)
            : base(options)
        {
        }

        public DbSet<DeviceEntity> Devices => Set<DeviceEntity>();

        public DbSet<SecureBootReportEntity> Reports => Set<SecureBootReportEntity>();

        public DbSet<SecureBootEventEntity> Events => Set<SecureBootEventEntity>();

        public DbSet<PendingCommandEntity> PendingCommands => Set<PendingCommandEntity>();

        // Windows Version tracking
        public DbSet<WindowsVersionEntity> WindowsVersions => Set<WindowsVersionEntity>();

        public DbSet<WindowsBuildEntity> WindowsBuilds => Set<WindowsBuildEntity>();

        // Device Cleanup Configuration
        public DbSet<DeviceCleanupConfigEntity> DeviceCleanupConfig => Set<DeviceCleanupConfigEntity>();

        // Application Settings
        public DbSet<ApplicationSettingEntity> ApplicationSettings => Set<ApplicationSettingEntity>();

        // Mutual TLS Configuration
        public DbSet<MutualTlsConfigEntity> MutualTlsConfig => Set<MutualTlsConfigEntity>();
        
        public DbSet<TrustedCertificateAuthorityEntity> TrustedCertificateAuthorities => Set<TrustedCertificateAuthorityEntity>();

    // Client Sink Configuration
    public DbSet<ClientSinkConfigEntity> ClientSinkConfig => Set<ClientSinkConfigEntity>();

    // API Configuration
    public DbSet<ApiConfigurationEntity> ApiConfiguration => Set<ApiConfigurationEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<DeviceEntity>(entity =>
            {
                entity.ToTable("Devices");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.MachineName).HasMaxLength(256).IsRequired();
                entity.Property(e => e.DomainName).HasMaxLength(256);
                entity.Property(e => e.UserPrincipalName).HasMaxLength(256);
                entity.Property(e => e.Manufacturer).HasMaxLength(256);
                entity.Property(e => e.Model).HasMaxLength(256);
                entity.Property(e => e.FirmwareVersion).HasMaxLength(256);
                entity.Property(e => e.FleetId).HasMaxLength(128);
                entity.Property(e => e.TagsJson).HasColumnType("nvarchar(max)");
                entity.HasIndex(e => e.MachineName);
            });

            modelBuilder.Entity<SecureBootReportEntity>(entity =>
            {
                entity.ToTable("SecureBootReports");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.RegistryStateJson).HasColumnType("nvarchar(max)");
                entity.Property(e => e.CertificatesJson).HasColumnType("nvarchar(max)");
                entity.Property(e => e.AlertsJson).HasColumnType("nvarchar(max)");
                entity.Property(e => e.DeploymentState).HasMaxLength(64);
                entity.Property(e => e.ClientVersion).HasMaxLength(64);
                entity.Property(e => e.CorrelationId).HasMaxLength(128);
                entity.HasIndex(e => e.CreatedAtUtc);
                entity.HasOne(e => e.Device)
                    .WithMany(d => d.Reports)
                    .HasForeignKey(e => e.DeviceId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<SecureBootEventEntity>(entity =>
            {
                entity.ToTable("SecureBootEvents");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ProviderName).HasMaxLength(256).IsRequired();
                entity.Property(e => e.Level).HasMaxLength(64);
                entity.Property(e => e.Message).HasColumnType("nvarchar(max)");
                entity.Property(e => e.RawXml).HasColumnType("nvarchar(max)");
                entity.HasIndex(e => e.TimestampUtc);
                entity.HasOne(e => e.Report)
                    .WithMany(r => r.Events)
                    .HasForeignKey(e => e.ReportId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<PendingCommandEntity>(entity =>
            {
                entity.ToTable("PendingCommands");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CommandType).HasMaxLength(100).IsRequired();
                entity.Property(e => e.CommandJson).HasColumnType("nvarchar(max)").IsRequired();
                entity.Property(e => e.Status).HasMaxLength(50).IsRequired();
                entity.Property(e => e.CreatedBy).HasMaxLength(256);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.ResultJson).HasColumnType("nvarchar(max)");
                entity.HasIndex(e => e.DeviceId);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.CreatedAtUtc);
                entity.HasIndex(e => new { e.DeviceId, e.Status });
                entity.HasOne(e => e.Device)
                    .WithMany()
                    .HasForeignKey(e => e.DeviceId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Windows Version configuration
            modelBuilder.Entity<WindowsVersionEntity>(entity =>
            {
                entity.ToTable("WindowsVersions");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Version).HasMaxLength(50).IsRequired();
                entity.Property(e => e.Name).HasMaxLength(256).IsRequired();
                entity.HasIndex(e => e.Version).IsUnique();
            });

            modelBuilder.Entity<WindowsBuildEntity>(entity =>
            {
                entity.ToTable("WindowsBuilds");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.BuildNumber).HasMaxLength(100).IsRequired();
                entity.Property(e => e.KbArticle).HasMaxLength(50);
                entity.Property(e => e.SecurityNotes).HasColumnType("nvarchar(max)");
                entity.HasIndex(e => e.BuildNumber);
                entity.HasIndex(e => new { e.WindowsVersionId, e.BuildNumber }).IsUnique();
                entity.HasIndex(e => e.IsLatest);
                entity.HasOne(e => e.Version)
                    .WithMany(v => v.Builds)
                    .HasForeignKey(e => e.WindowsVersionId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Device Cleanup Configuration
            modelBuilder.Entity<DeviceCleanupConfigEntity>(entity =>
            {
                entity.ToTable("DeviceCleanupConfig");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CleanupSchedule).HasMaxLength(100);
                entity.Property(e => e.NotificationEmail).HasMaxLength(256);
                
                // Seed default configuration with static timestamp
                entity.HasData(new DeviceCleanupConfigEntity
                {
                    Id = 1,
                    Enabled = false,
                    InactiveDaysThreshold = 90,
                    CleanupSchedule = "0 2 * * *", // Daily at 2 AM
                    DeleteAssociatedData = true,
                    NotifyOnCleanup = false,
                    NotificationEmail = null,
                    LastCleanupRunUtc = null,
                    LastCleanupDeviceCount = 0,
                    CreatedAtUtc = new DateTimeOffset(2025, 1, 14, 0, 0, 0, TimeSpan.Zero),
                    UpdatedAtUtc = new DateTimeOffset(2025, 1, 14, 0, 0, 0, TimeSpan.Zero)
                });
            });

            // Application Settings
            modelBuilder.Entity<ApplicationSettingEntity>(entity =>
            {
                entity.ToTable("ApplicationSettings");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Key).HasMaxLength(200).IsRequired();
                entity.Property(e => e.Value).HasColumnType("nvarchar(max)").IsRequired();
                entity.Property(e => e.Category).HasMaxLength(100).IsRequired();
                entity.Property(e => e.ValueType).HasMaxLength(50).IsRequired();
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.UpdatedBy).HasMaxLength(256);
                entity.HasIndex(e => e.Key).IsUnique();
                entity.HasIndex(e => e.Category);
                
                // Seed default settings from appsettings.json - static timestamp
                var settingsNow = new DateTimeOffset(2025, 1, 14, 12, 0, 0, TimeSpan.Zero);
                
                entity.HasData(
                    // QueueProcessor settings
                    new ApplicationSettingEntity
                    {
                        Id = 1,
                        Key = "QueueProcessor:Enabled",
                        Value = "true",
                        Category = "QueueProcessor",
                        ValueType = "bool",
                        Description = "Enable or disable the queue processor background service",
                        IsSensitive = false,
                        RequiresRestart = true,
                        CreatedAtUtc = settingsNow,
                        UpdatedAtUtc = settingsNow
                    },
                    new ApplicationSettingEntity
                    {
                        Id = 2,
                        Key = "QueueProcessor:MaxMessages",
                        Value = "10",
                        Category = "QueueProcessor",
                        ValueType = "int",
                        Description = "Maximum number of messages to process in each batch",
                        IsSensitive = false,
                        RequiresRestart = false,
                        CreatedAtUtc = settingsNow,
                        UpdatedAtUtc = settingsNow
                    },
                    new ApplicationSettingEntity
                    {
                        Id = 3,
                        Key = "QueueProcessor:ProcessingInterval",
                        Value = "00:00:02",
                        Category = "QueueProcessor",
                        ValueType = "timespan",
                        Description = "Interval between queue processing cycles when messages are present",
                        IsSensitive = false,
                        RequiresRestart = false,
                        CreatedAtUtc = settingsNow,
                        UpdatedAtUtc = settingsNow
                    },
                    new ApplicationSettingEntity
                    {
                        Id = 4,
                        Key = "QueueProcessor:EmptyQueuePollInterval",
                        Value = "00:00:10",
                        Category = "QueueProcessor",
                        ValueType = "timespan",
                        Description = "Interval to check queue when it was previously empty",
                        IsSensitive = false,
                        RequiresRestart = false,
                        CreatedAtUtc = settingsNow,
                        UpdatedAtUtc = settingsNow
                    },
                    
                    // ClientUpdate settings
                    new ApplicationSettingEntity
                    {
                        Id = 5,
                        Key = "ClientUpdate:LatestVersion",
                        Value = "\"1.5.0.0\"",
                        Category = "ClientUpdate",
                        ValueType = "string",
                        Description = "Latest available client version",
                        IsSensitive = false,
                        RequiresRestart = false,
                        CreatedAtUtc = settingsNow,
                        UpdatedAtUtc = settingsNow
                    },
                    new ApplicationSettingEntity
                    {
                        Id = 6,
                        Key = "ClientUpdate:MinimumVersion",
                        Value = "\"1.3.0.0\"",
                        Category = "ClientUpdate",
                        ValueType = "string",
                        Description = "Minimum supported client version",
                        IsSensitive = false,
                        RequiresRestart = false,
                        CreatedAtUtc = settingsNow,
                        UpdatedAtUtc = settingsNow
                    },
                    new ApplicationSettingEntity
                    {
                        Id = 7,
                        Key = "ClientUpdate:IsUpdateRequired",
                        Value = "false",
                        Category = "ClientUpdate",
                        ValueType = "bool",
                        Description = "Whether client update is mandatory",
                        IsSensitive = false,
                        RequiresRestart = false,
                        CreatedAtUtc = settingsNow,
                        UpdatedAtUtc = settingsNow
                    },
                    new ApplicationSettingEntity
                    {
                        Id = 8,
                        Key = "ClientUpdate:DownloadUrl",
                        Value = "\"https://secbootcert.queue.core.windows.net/client-packages/SecureBootWatcher-Client-latest.zip\"",
                        Category = "ClientUpdate",
                        ValueType = "string",
                        Description = "URL to download the latest client package",
                        IsSensitive = false,
                        RequiresRestart = false,
                        CreatedAtUtc = settingsNow,
                        UpdatedAtUtc = settingsNow
                    },
                    
                    // SecureBootReadiness settings
                    new ApplicationSettingEntity
                    {
                        Id = 9,
                        Key = "SecureBootReadiness:CertificateExpirationWarningDays",
                        Value = "1095",
                        Category = "SecureBootReadiness",
                        ValueType = "int",
                        Description = "Days before expiration to show warning (3 years)",
                        IsSensitive = false,
                        RequiresRestart = false,
                        CreatedAtUtc = settingsNow,
                        UpdatedAtUtc = settingsNow
                    },
                    new ApplicationSettingEntity
                    {
                        Id = 10,
                        Key = "SecureBootReadiness:CertificateExpirationCriticalDays",
                        Value = "365",
                        Category = "SecureBootReadiness",
                        ValueType = "int",
                        Description = "Days before expiration to show critical alert (1 year)",
                        IsSensitive = false,
                        RequiresRestart = false,
                        CreatedAtUtc = settingsNow,
                        UpdatedAtUtc = settingsNow
                    },
                    new ApplicationSettingEntity
                    {
                        Id = 11,
                        Key = "SecureBootReadiness:RequireWindowsUEFICA2023",
                        Value = "true",
                        Category = "SecureBootReadiness",
                        ValueType = "bool",
                        Description = "Require Windows UEFI CA 2023 certificate for readiness",
                        IsSensitive = false,
                        RequiresRestart = false,
                        CreatedAtUtc = settingsNow,
                        UpdatedAtUtc = settingsNow
                    },
                    new ApplicationSettingEntity
                    {
                        Id = 12,
                        Key = "SecureBootReadiness:RequireOemCertificatesValid",
                        Value = "true",
                        Category = "SecureBootReadiness",
                        ValueType = "bool",
                        Description = "Require OEM certificates to be valid (not expired)",
                        IsSensitive = false,
                        RequiresRestart = false,
                        CreatedAtUtc = settingsNow,
                        UpdatedAtUtc = settingsNow
                    }
                );
            });

            // Mutual TLS Configuration
            modelBuilder.Entity<MutualTlsConfigEntity>(entity =>
            {
                entity.ToTable("MutualTlsConfig");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.AllowedThumbprints).HasMaxLength(4000);
                entity.Property(e => e.ValidationNotes).HasMaxLength(2000);
                entity.Property(e => e.CreatedBy).HasMaxLength(256);
                entity.Property(e => e.UpdatedBy).HasMaxLength(256);
                
                // Seed default mTLS configuration - static timestamp
                var mtlsNow = new DateTimeOffset(2025, 1, 14, 12, 0, 0, TimeSpan.Zero);
                
                // Seed default mTLS configuration
                entity.HasData(new MutualTlsConfigEntity
                {
                    Id = 1,
                    Enabled = false,
                    AllowSelfSignedCertificates = false,
                    CheckCertificateRevocation = true,
                    ValidateCertificateChain = true,
                    RequireClientAuthEku = true,
                    ValidateCertificateValidity = true,
                    ExpirationGracePeriodDays = 0,
                    EnableThumbprintAllowlist = false,
                    AllowedThumbprints = null,
                    EnableIssuerAllowlist = true,
                    EnableDetailedLogging = false,
                    RevocationCheckTimeoutSeconds = 10,
                    ValidationNotes = "Default mutual TLS configuration. Update via Admin Settings.",
                    CreatedAtUtc = mtlsNow,
                    CreatedBy = "System",
                    UpdatedAtUtc = mtlsNow,
                    UpdatedBy = "System"
                });
            });

            // Trusted Certificate Authorities
            modelBuilder.Entity<TrustedCertificateAuthorityEntity>(entity =>
            {
                entity.ToTable("TrustedCertificateAuthorities");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CommonName).HasMaxLength(256).IsRequired();
                entity.Property(e => e.Thumbprint).HasMaxLength(40).IsRequired();
                entity.Property(e => e.Thumbprint256).HasMaxLength(64);
                entity.Property(e => e.Subject).HasMaxLength(500).IsRequired();
                entity.Property(e => e.Issuer).HasMaxLength(500).IsRequired();
                entity.Property(e => e.SerialNumber).HasMaxLength(100);
                entity.Property(e => e.CertificateDataBase64).IsRequired();
                entity.Property(e => e.Description).HasMaxLength(1000);
                entity.Property(e => e.CreatedBy).HasMaxLength(256);
                entity.Property(e => e.UpdatedBy).HasMaxLength(256);
                
                // Indexes for fast lookups
                entity.HasIndex(e => e.Thumbprint).IsUnique();
                entity.HasIndex(e => e.CommonName);
                entity.HasIndex(e => e.IsEnabled);
                entity.HasIndex(e => e.IsRootCa);
                entity.HasIndex(e => e.NotAfter);
            });

            // Client Sink Configuration
            modelBuilder.Entity<ClientSinkConfigEntity>(entity =>
            {
                entity.ToTable("ClientSinkConfig");
                entity.HasKey(e => e.Id);
                
                // General settings
                entity.Property(e => e.ExecutionStrategy).HasMaxLength(50).IsRequired();
                entity.Property(e => e.SinkPriority).HasMaxLength(200).IsRequired();
                
                // FileShare settings
                entity.Property(e => e.FileShareRootPath).HasMaxLength(500);
                entity.Property(e => e.FileShareExtension).HasMaxLength(20).IsRequired();
                
                // Azure Queue settings
                entity.Property(e => e.AzureQueueServiceUri).HasMaxLength(500);
                entity.Property(e => e.AzureQueueName).HasMaxLength(100).IsRequired();
                entity.Property(e => e.AzureQueueAuthMethod).HasMaxLength(50).IsRequired();
                entity.Property(e => e.AzureQueueConnectionString).HasColumnType("nvarchar(max)");
                entity.Property(e => e.AzureQueueClientId).HasMaxLength(100);
                entity.Property(e => e.AzureQueueTenantId).HasMaxLength(100);
                entity.Property(e => e.AzureQueueClientSecret).HasColumnType("nvarchar(max)");
                entity.Property(e => e.AzureQueueCertPath).HasMaxLength(500);
                entity.Property(e => e.AzureQueueCertPassword).HasMaxLength(500);
                entity.Property(e => e.AzureQueueCertThumbprint).HasMaxLength(100);
                entity.Property(e => e.AzureQueueCertStoreLocation).HasMaxLength(50).IsRequired();
                entity.Property(e => e.AzureQueueCertStoreName).HasMaxLength(50).IsRequired();
                
                // Web API settings
                entity.Property(e => e.WebApiBaseAddress).HasMaxLength(500);
                entity.Property(e => e.WebApiIngestionRoute).HasMaxLength(200).IsRequired();
                entity.Property(e => e.WebApiCertPath).HasMaxLength(500);
                entity.Property(e => e.WebApiCertPassword).HasMaxLength(500);
                entity.Property(e => e.WebApiCertThumbprint).HasMaxLength(100);
                entity.Property(e => e.WebApiCertStoreLocation).HasMaxLength(50).IsRequired();
                entity.Property(e => e.WebApiCertStoreName).HasMaxLength(50).IsRequired();
                
                // Metadata
                entity.Property(e => e.Description).HasMaxLength(1000);
                entity.Property(e => e.CreatedBy).HasMaxLength(256);
                entity.Property(e => e.UpdatedBy).HasMaxLength(256);
                
                // Indexes
                entity.HasIndex(e => e.IsActive);
                
                // Seed default configuration
                var now = new DateTimeOffset(2025, 1, 14, 12, 0, 0, TimeSpan.Zero);
                
                entity.HasData(new ClientSinkConfigEntity
                {
                    Id = 1,
                    EnableFileShare = false,
                    EnableAzureQueue = false,
                    EnableWebApi = true,
                    ExecutionStrategy = "StopOnFirstSuccess",
                    SinkPriority = "WebApi,AzureQueue,FileShare",
                    MaxRetryAttempts = 3,
                    RetryDelaySeconds = 300,
                    UseExponentialBackoff = false,
                    FileShareRootPath = null,
                    FileShareExtension = ".json",
                    FileShareAppendTimestamp = true,
                    AzureQueueServiceUri = null,
                    AzureQueueName = "secureboot-reports",
                    AzureQueueAuthMethod = "DefaultAzureCredential",
                    AzureQueueConnectionString = null,
                    AzureQueueClientId = null,
                    AzureQueueTenantId = null,
                    AzureQueueClientSecret = null,
                    AzureQueueCertPath = null,
                    AzureQueueCertPassword = null,
                    AzureQueueCertThumbprint = null,
                    AzureQueueCertStoreLocation = "CurrentUser",
                    AzureQueueCertStoreName = "My",
                    AzureQueueVisibilityTimeoutSeconds = 300,
                    AzureQueueMaxSendRetryCount = 5,
                    WebApiBaseAddress = null,
                    WebApiIngestionRoute = "/api/SecureBootReports",
                    WebApiTimeoutSeconds = 30,
                    WebApiUseCertAuth = false,
                    WebApiCertPath = null,
                    WebApiCertPassword = null,
                    WebApiCertThumbprint = null,
                    WebApiCertStoreLocation = "LocalMachine",
                    WebApiCertStoreName = "My",
                    Description = "Default client sink configuration. Configure via Admin Settings.",
                    IsActive = true,
                    CreatedAtUtc = now,
                    CreatedBy = "System",
                    UpdatedAtUtc = now,
                    UpdatedBy = "System"
                });
            });

            // API Configuration
            modelBuilder.Entity<ApiConfigurationEntity>(entity =>
            {
                entity.ToTable("ApiConfiguration");
                entity.HasKey(e => e.Id);

                // Queue settings
                entity.Property(e => e.QueueServiceUri).HasMaxLength(500);
                entity.Property(e => e.QueueName).HasMaxLength(100).IsRequired();
                entity.Property(e => e.QueueAuthenticationMethod).HasMaxLength(50).IsRequired();
                entity.Property(e => e.QueueConnectionString).HasColumnType("nvarchar(max)");
                entity.Property(e => e.QueueClientId).HasMaxLength(100);
                entity.Property(e => e.QueueTenantId).HasMaxLength(100);
                entity.Property(e => e.QueueClientSecret).HasColumnType("nvarchar(max)");
                entity.Property(e => e.QueueCertificatePath).HasMaxLength(500);
                entity.Property(e => e.QueueCertificatePassword).HasMaxLength(500);
                entity.Property(e => e.QueueCertificateThumbprint).HasMaxLength(100);
                entity.Property(e => e.QueueCertificateStoreLocation).HasMaxLength(50).IsRequired();
                entity.Property(e => e.QueueCertificateStoreName).HasMaxLength(50).IsRequired();

                // File store settings
                entity.Property(e => e.FileReportStoreBasePath).HasMaxLength(1000);
                entity.Property(e => e.FileReportStoreExtension).HasMaxLength(20).IsRequired();

                // Device cleanup settings
                entity.Property(e => e.DeviceCleanupSchedule).HasMaxLength(100).IsRequired();

                // Metadata
                entity.Property(e => e.Description).HasMaxLength(1000);
                entity.Property(e => e.CreatedBy).HasMaxLength(256);
                entity.Property(e => e.UpdatedBy).HasMaxLength(256);

                // Indexes
                entity.HasIndex(e => e.IsActive);

                // Seed default configuration
                var apiConfigNow = new DateTimeOffset(2025, 1, 14, 12, 0, 0, TimeSpan.Zero);

                entity.HasData(new ApiConfigurationEntity
                {
                    Id = 1,
                    QueueProcessorEnabled = true,
                    QueueServiceUri = "https://secbootcert.queue.core.windows.net",
                    QueueName = "secureboot-reports",
                    QueueAuthenticationMethod = "Certificate",
                    QueueConnectionString = null,
                    QueueClientId = "c8034569-4990-4823-9f1d-b46223789c35",
                    QueueTenantId = "d6dbad84-5922-4700-a049-c7068c37c884",
                    QueueClientSecret = null,
                    QueueCertificatePath = null,
                    QueueCertificatePassword = null,
                    QueueCertificateThumbprint = "522172C364D58BB50EA08C60055ACC095A161D12",
                    QueueCertificateStoreLocation = "LocalMachine",
                    QueueCertificateStoreName = "My",
                    QueueMaxMessages = 10,
                    QueueProcessingIntervalSeconds = 5,
                    QueueEmptyQueuePollIntervalSeconds = 30,
                    QueueVisibilityTimeoutSeconds = 300,
                    QueueMaxDequeueCount = 5,
                    FileReportStoreEnabled = false,
                    FileReportStoreBasePath = null,
                    FileReportStoreExtension = ".json",
                    FileReportStoreAppendTimestamp = true,
                    DeviceCleanupEnabled = true,
                    DeviceCleanupSchedule = "0 2 * * 0",
                    DeviceCleanupDaysThreshold = 90,
                    Description = "Default API configuration. Update via Admin Settings.",
                    IsActive = true,
                    CreatedAtUtc = apiConfigNow,
                    CreatedBy = "System",
                    UpdatedAtUtc = apiConfigNow,
                    UpdatedBy = "System"
                });
            });
        }
    }
}
