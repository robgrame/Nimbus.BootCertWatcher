using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureBootDashboard.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceCleanupConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeviceCleanupConfig",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    InactiveDaysThreshold = table.Column<int>(type: "int", nullable: false),
                    CleanupSchedule = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DeleteAssociatedData = table.Column<bool>(type: "bit", nullable: false),
                    NotifyOnCleanup = table.Column<bool>(type: "bit", nullable: false),
                    NotificationEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LastCleanupRunUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastCleanupDeviceCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceCleanupConfig", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "DeviceCleanupConfig",
                columns: new[] { "Id", "CleanupSchedule", "CreatedAtUtc", "DeleteAssociatedData", "Enabled", "InactiveDaysThreshold", "LastCleanupDeviceCount", "LastCleanupRunUtc", "NotificationEmail", "NotifyOnCleanup", "UpdatedAtUtc" },
                values: new object[] { 1, "0 2 * * *", new DateTimeOffset(new DateTime(2025, 1, 14, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, false, 90, 0, null, null, false, new DateTimeOffset(new DateTime(2025, 1, 14, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceCleanupConfig");
        }
    }
}
