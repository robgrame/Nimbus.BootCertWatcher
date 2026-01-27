using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureBootDashboard.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOSBuildNumberToDevice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OSBuildNumber",
                table: "Devices",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OSBuildNumber",
                table: "Devices");
        }
    }
}
