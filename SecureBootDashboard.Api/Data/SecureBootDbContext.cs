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
        }
    }
}
