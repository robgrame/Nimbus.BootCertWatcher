using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureBootDashboard.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWindowsVersionTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WindowsVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Version = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ReleaseDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndOfSupportDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSyncedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WindowsVersions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WindowsBuilds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WindowsVersionId = table.Column<int>(type: "int", nullable: false),
                    BuildNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MajorBuild = table.Column<int>(type: "int", nullable: false),
                    MinorBuild = table.Column<int>(type: "int", nullable: true),
                    ReleaseDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    KbArticle = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsSecure = table.Column<bool>(type: "bit", nullable: false),
                    IsLatest = table.Column<bool>(type: "bit", nullable: false),
                    SecurityNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastSyncedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WindowsBuilds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WindowsBuilds_WindowsVersions_WindowsVersionId",
                        column: x => x.WindowsVersionId,
                        principalTable: "WindowsVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WindowsBuilds_BuildNumber",
                table: "WindowsBuilds",
                column: "BuildNumber");

            migrationBuilder.CreateIndex(
                name: "IX_WindowsBuilds_IsLatest",
                table: "WindowsBuilds",
                column: "IsLatest");

            migrationBuilder.CreateIndex(
                name: "IX_WindowsBuilds_WindowsVersionId_BuildNumber",
                table: "WindowsBuilds",
                columns: new[] { "WindowsVersionId", "BuildNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WindowsVersions_Version",
                table: "WindowsVersions",
                column: "Version",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WindowsBuilds");

            migrationBuilder.DropTable(
                name: "WindowsVersions");
        }
    }
}
