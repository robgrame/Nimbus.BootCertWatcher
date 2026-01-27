using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureBootDashboard.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCertificateCollection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CertificatesJson",
                table: "SecureBootReports",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CertificatesJson",
                table: "SecureBootReports");
        }
    }
}
