using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SecureBootDashboard.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationSettingsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApplicationSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ValueType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsSensitive = table.Column<bool>(type: "bit", nullable: false),
                    RequiresRestart = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationSettings", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "ApplicationSettings",
                columns: new[] { "Id", "Category", "CreatedAtUtc", "Description", "IsSensitive", "Key", "RequiresRestart", "UpdatedAtUtc", "UpdatedBy", "Value", "ValueType" },
                values: new object[,]
                {
                    { 1, "QueueProcessor", new DateTimeOffset(new DateTime(2025, 1, 14, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Enable or disable the queue processor background service", false, "QueueProcessor:Enabled", true, new DateTimeOffset(new DateTime(2025, 1, 14, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "true", "bool" },
                    { 2, "QueueProcessor", new DateTimeOffset(new DateTime(2025, 1, 14, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Maximum number of messages to process in each batch", false, "QueueProcessor:MaxMessages", false, new DateTimeOffset(new DateTime(2025, 1, 14, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "10", "int" },
                    { 3, "QueueProcessor", new DateTimeOffset(new DateTime(2025, 1, 14, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Interval between queue processing cycles when messages are present", false, "QueueProcessor:ProcessingInterval", false, new DateTimeOffset(new DateTime(2025, 1, 14, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "00:00:02", "timespan" },
                    { 4, "QueueProcessor", new DateTimeOffset(new DateTime(2025, 1, 14, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Interval to check queue when it was previously empty", false, "QueueProcessor:EmptyQueuePollInterval", false, new DateTimeOffset(new DateTime(2025, 1, 14, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "00:00:10", "timespan" },
                    { 5, "ClientUpdate", new DateTimeOffset(new DateTime(2025, 1, 14, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Latest available client version", false, "ClientUpdate:LatestVersion", false, new DateTimeOffset(new DateTime(2025, 1, 14, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "\"1.5.0.0\"", "string" },
                    { 6, "ClientUpdate", new DateTimeOffset(new DateTime(2025, 1, 14, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Minimum supported client version", false, "ClientUpdate:MinimumVersion", false, new DateTimeOffset(new DateTime(2025, 1, 14, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "\"1.3.0.0\"", "string" },
                    { 7, "ClientUpdate", new DateTimeOffset(new DateTime(2025, 1, 14, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Whether client update is mandatory", false, "ClientUpdate:IsUpdateRequired", false, new DateTimeOffset(new DateTime(2025, 1, 14, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "false", "bool" },
                    { 8, "ClientUpdate", new DateTimeOffset(new DateTime(2025, 1, 14, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "URL to download the latest client package", false, "ClientUpdate:DownloadUrl", false, new DateTimeOffset(new DateTime(2025, 1, 14, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "\"https://secbootcert.queue.core.windows.net/client-packages/SecureBootWatcher-Client-latest.zip\"", "string" },
                    { 9, "SecureBootReadiness", new DateTimeOffset(new DateTime(2025, 1, 14, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Days before expiration to show warning (3 years)", false, "SecureBootReadiness:CertificateExpirationWarningDays", false, new DateTimeOffset(new DateTime(2025, 1, 14, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "1095", "int" },
                    { 10, "SecureBootReadiness", new DateTimeOffset(new DateTime(2025, 1, 14, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Days before expiration to show critical alert (1 year)", false, "SecureBootReadiness:CertificateExpirationCriticalDays", false, new DateTimeOffset(new DateTime(2025, 1, 14, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "365", "int" },
                    { 11, "SecureBootReadiness", new DateTimeOffset(new DateTime(2025, 1, 14, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Require Windows UEFI CA 2023 certificate for readiness", false, "SecureBootReadiness:RequireWindowsUEFICA2023", false, new DateTimeOffset(new DateTime(2025, 1, 14, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "true", "bool" },
                    { 12, "SecureBootReadiness", new DateTimeOffset(new DateTime(2025, 1, 14, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Require OEM certificates to be valid (not expired)", false, "SecureBootReadiness:RequireOemCertificatesValid", false, new DateTimeOffset(new DateTime(2025, 1, 14, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "true", "bool" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationSettings_Category",
                table: "ApplicationSettings",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationSettings_Key",
                table: "ApplicationSettings",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationSettings");
        }
    }
}
