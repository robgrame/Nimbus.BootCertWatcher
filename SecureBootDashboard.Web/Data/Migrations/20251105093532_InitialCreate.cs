using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureBootDashboard.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MachineName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DomainName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UserPrincipalName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Manufacturer = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Model = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    FirmwareVersion = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    FleetId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    TagsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastSeenUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SecureBootReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RegistryStateJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AlertsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeploymentState = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ClientVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecureBootReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SecureBootReports_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SecureBootEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    EventId = table.Column<int>(type: "int", nullable: false),
                    Level = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    TimestampUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RawXml = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecureBootEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SecureBootEvents_SecureBootReports_ReportId",
                        column: x => x.ReportId,
                        principalTable: "SecureBootReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Devices_MachineName",
                table: "Devices",
                column: "MachineName");

            migrationBuilder.CreateIndex(
                name: "IX_SecureBootEvents_ReportId",
                table: "SecureBootEvents",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_SecureBootEvents_TimestampUtc",
                table: "SecureBootEvents",
                column: "TimestampUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SecureBootReports_CreatedAtUtc",
                table: "SecureBootReports",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SecureBootReports_DeviceId",
                table: "SecureBootReports",
                column: "DeviceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SecureBootEvents");

            migrationBuilder.DropTable(
                name: "SecureBootReports");

            migrationBuilder.DropTable(
                name: "Devices");
        }
    }
}
