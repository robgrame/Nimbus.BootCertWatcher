using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureBootDashboard.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClientSinkConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClientSinkConfig",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EnableFileShare = table.Column<bool>(type: "bit", nullable: false),
                    EnableAzureQueue = table.Column<bool>(type: "bit", nullable: false),
                    EnableWebApi = table.Column<bool>(type: "bit", nullable: false),
                    ExecutionStrategy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SinkPriority = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MaxRetryAttempts = table.Column<int>(type: "int", nullable: false),
                    RetryDelaySeconds = table.Column<int>(type: "int", nullable: false),
                    UseExponentialBackoff = table.Column<bool>(type: "bit", nullable: false),
                    FileShareRootPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FileShareExtension = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FileShareAppendTimestamp = table.Column<bool>(type: "bit", nullable: false),
                    AzureQueueServiceUri = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AzureQueueName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AzureQueueAuthMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AzureQueueConnectionString = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AzureQueueClientId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AzureQueueTenantId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AzureQueueClientSecret = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AzureQueueCertPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AzureQueueCertPassword = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AzureQueueCertThumbprint = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AzureQueueCertStoreLocation = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AzureQueueCertStoreName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AzureQueueVisibilityTimeoutSeconds = table.Column<int>(type: "int", nullable: false),
                    AzureQueueMaxSendRetryCount = table.Column<int>(type: "int", nullable: false),
                    WebApiBaseAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    WebApiIngestionRoute = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    WebApiTimeoutSeconds = table.Column<int>(type: "int", nullable: false),
                    WebApiUseCertAuth = table.Column<bool>(type: "bit", nullable: false),
                    WebApiCertPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    WebApiCertPassword = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    WebApiCertThumbprint = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    WebApiCertStoreLocation = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    WebApiCertStoreName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientSinkConfig", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "ClientSinkConfig",
                columns: new[] { "Id", "AzureQueueAuthMethod", "AzureQueueCertPassword", "AzureQueueCertPath", "AzureQueueCertStoreLocation", "AzureQueueCertStoreName", "AzureQueueCertThumbprint", "AzureQueueClientId", "AzureQueueClientSecret", "AzureQueueConnectionString", "AzureQueueMaxSendRetryCount", "AzureQueueName", "AzureQueueServiceUri", "AzureQueueTenantId", "AzureQueueVisibilityTimeoutSeconds", "CreatedAtUtc", "CreatedBy", "Description", "EnableAzureQueue", "EnableFileShare", "EnableWebApi", "ExecutionStrategy", "FileShareAppendTimestamp", "FileShareExtension", "FileShareRootPath", "IsActive", "MaxRetryAttempts", "RetryDelaySeconds", "SinkPriority", "UpdatedAtUtc", "UpdatedBy", "UseExponentialBackoff", "WebApiBaseAddress", "WebApiCertPassword", "WebApiCertPath", "WebApiCertStoreLocation", "WebApiCertStoreName", "WebApiCertThumbprint", "WebApiIngestionRoute", "WebApiTimeoutSeconds", "WebApiUseCertAuth" },
                values: new object[] { 1, "DefaultAzureCredential", null, null, "CurrentUser", "My", null, null, null, null, 5, "secureboot-reports", null, null, 300, new DateTimeOffset(new DateTime(2025, 1, 14, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "System", "Default client sink configuration. Configure via Admin Settings.", false, false, true, "StopOnFirstSuccess", true, ".json", null, true, 3, 300, "WebApi,AzureQueue,FileShare", new DateTimeOffset(new DateTime(2025, 1, 14, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "System", false, null, null, null, "LocalMachine", "My", null, "/api/SecureBootReports", 30, false });

            migrationBuilder.CreateIndex(
                name: "IX_ClientSinkConfig_IsActive",
                table: "ClientSinkConfig",
                column: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientSinkConfig");
        }
    }
}
