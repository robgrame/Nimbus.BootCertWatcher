using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureBootDashboard.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFirmwareReleaseDateToDevice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FirmwareReleaseDate",
                table: "Devices",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FirmwareReleaseDate",
                table: "Devices");
        }
    }
}
