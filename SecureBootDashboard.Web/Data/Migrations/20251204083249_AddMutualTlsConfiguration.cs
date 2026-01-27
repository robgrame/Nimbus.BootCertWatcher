using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureBootDashboard.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMutualTlsConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MutualTlsConfig",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    AllowSelfSignedCertificates = table.Column<bool>(type: "bit", nullable: false),
                    CheckCertificateRevocation = table.Column<bool>(type: "bit", nullable: false),
                    ValidateCertificateChain = table.Column<bool>(type: "bit", nullable: false),
                    RequireClientAuthEku = table.Column<bool>(type: "bit", nullable: false),
                    ValidateCertificateValidity = table.Column<bool>(type: "bit", nullable: false),
                    ExpirationGracePeriodDays = table.Column<int>(type: "int", nullable: false),
                    EnableThumbprintAllowlist = table.Column<bool>(type: "bit", nullable: false),
                    AllowedThumbprints = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    EnableIssuerAllowlist = table.Column<bool>(type: "bit", nullable: false),
                    EnableDetailedLogging = table.Column<bool>(type: "bit", nullable: false),
                    RevocationCheckTimeoutSeconds = table.Column<int>(type: "int", nullable: false),
                    ValidationNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MutualTlsConfig", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TrustedCertificateAuthorities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CommonName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Thumbprint = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Thumbprint256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Issuer = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    NotBefore = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    NotAfter = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsRootCa = table.Column<bool>(type: "bit", nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CertificateDataBase64 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrustedCertificateAuthorities", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "MutualTlsConfig",
                columns: new[] { "Id", "AllowSelfSignedCertificates", "AllowedThumbprints", "CheckCertificateRevocation", "CreatedAtUtc", "CreatedBy", "EnableDetailedLogging", "EnableIssuerAllowlist", "EnableThumbprintAllowlist", "Enabled", "ExpirationGracePeriodDays", "RequireClientAuthEku", "RevocationCheckTimeoutSeconds", "UpdatedAtUtc", "UpdatedBy", "ValidateCertificateChain", "ValidateCertificateValidity", "ValidationNotes" },
                values: new object[] { 1, false, null, true, new DateTimeOffset(new DateTime(2025, 1, 14, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "System", false, true, false, false, 0, true, 10, new DateTimeOffset(new DateTime(2025, 1, 14, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "System", true, true, "Default mutual TLS configuration. Update via Admin Settings." });

            migrationBuilder.CreateIndex(
                name: "IX_TrustedCertificateAuthorities_CommonName",
                table: "TrustedCertificateAuthorities",
                column: "CommonName");

            migrationBuilder.CreateIndex(
                name: "IX_TrustedCertificateAuthorities_IsEnabled",
                table: "TrustedCertificateAuthorities",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_TrustedCertificateAuthorities_IsRootCa",
                table: "TrustedCertificateAuthorities",
                column: "IsRootCa");

            migrationBuilder.CreateIndex(
                name: "IX_TrustedCertificateAuthorities_NotAfter",
                table: "TrustedCertificateAuthorities",
                column: "NotAfter");

            migrationBuilder.CreateIndex(
                name: "IX_TrustedCertificateAuthorities_Thumbprint",
                table: "TrustedCertificateAuthorities",
                column: "Thumbprint",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MutualTlsConfig");

            migrationBuilder.DropTable(
                name: "TrustedCertificateAuthorities");
        }
    }
}
