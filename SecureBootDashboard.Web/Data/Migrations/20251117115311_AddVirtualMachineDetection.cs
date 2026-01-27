using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureBootDashboard.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVirtualMachineDetection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsVirtualMachine",
                table: "Devices",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VirtualizationPlatform",
                table: "Devices",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsVirtualMachine",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "VirtualizationPlatform",
                table: "Devices");
        }
    }
}
