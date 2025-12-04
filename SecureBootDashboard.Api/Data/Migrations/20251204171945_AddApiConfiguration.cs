using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureBootDashboard.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddApiConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApiConfiguration",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QueueProcessorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    QueueServiceUri = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    QueueName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    QueueAuthenticationMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    QueueConnectionString = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QueueClientId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    QueueTenantId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    QueueClientSecret = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QueueCertificatePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    QueueCertificatePassword = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    QueueCertificateThumbprint = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    QueueCertificateStoreLocation = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    QueueCertificateStoreName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    QueueMaxMessages = table.Column<int>(type: "int", nullable: false),
                    QueueProcessingIntervalSeconds = table.Column<int>(type: "int", nullable: false),
                    QueueEmptyQueuePollIntervalSeconds = table.Column<int>(type: "int", nullable: false),
                    QueueVisibilityTimeoutSeconds = table.Column<int>(type: "int", nullable: false),
                    QueueMaxDequeueCount = table.Column<int>(type: "int", nullable: false),
                    FileReportStoreEnabled = table.Column<bool>(type: "bit", nullable: false),
                    FileReportStoreBasePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FileReportStoreExtension = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FileReportStoreAppendTimestamp = table.Column<bool>(type: "bit", nullable: false),
                    DeviceCleanupEnabled = table.Column<bool>(type: "bit", nullable: false),
                    DeviceCleanupSchedule = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeviceCleanupDaysThreshold = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiConfiguration", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "ApiConfiguration",
                columns: new[] { "Id", "CreatedAtUtc", "CreatedBy", "Description", "DeviceCleanupDaysThreshold", "DeviceCleanupEnabled", "DeviceCleanupSchedule", "FileReportStoreAppendTimestamp", "FileReportStoreBasePath", "FileReportStoreEnabled", "FileReportStoreExtension", "IsActive", "QueueAuthenticationMethod", "QueueCertificatePassword", "QueueCertificatePath", "QueueCertificateStoreLocation", "QueueCertificateStoreName", "QueueCertificateThumbprint", "QueueClientId", "QueueClientSecret", "QueueConnectionString", "QueueEmptyQueuePollIntervalSeconds", "QueueMaxDequeueCount", "QueueMaxMessages", "QueueName", "QueueProcessingIntervalSeconds", "QueueProcessorEnabled", "QueueServiceUri", "QueueTenantId", "QueueVisibilityTimeoutSeconds", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { 1, new DateTimeOffset(new DateTime(2025, 1, 14, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "System", "Default API configuration. Update via Admin Settings.", 90, true, "0 2 * * 0", true, null, false, ".json", true, "Certificate", null, null, "LocalMachine", "My", "522172C364D58BB50EA08C60055ACC095A161D12", "c8034569-4990-4823-9f1d-b46223789c35", null, null, 30, 5, 10, "secureboot-reports", 5, true, "https://secbootcert.queue.core.windows.net", "d6dbad84-5922-4700-a049-c7068c37c884", 300, new DateTimeOffset(new DateTime(2025, 1, 14, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "System" });

            migrationBuilder.CreateIndex(
                name: "IX_ApiConfiguration_IsActive",
                table: "ApiConfiguration",
                column: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiConfiguration");
        }
    }
}
