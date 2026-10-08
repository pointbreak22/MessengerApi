using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIpTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastIpAddress",
                table: "Users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IpAddress",
                table: "ModerationViolations",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "IpBans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsAutomatic = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IpBans", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModerationViolations_IpAddress_CreatedAt",
                table: "ModerationViolations",
                columns: new[] { "IpAddress", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IpBans_IpAddress",
                table: "IpBans",
                column: "IpAddress");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IpBans");

            migrationBuilder.DropIndex(
                name: "IX_ModerationViolations_IpAddress_CreatedAt",
                table: "ModerationViolations");

            migrationBuilder.DropColumn(
                name: "LastIpAddress",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IpAddress",
                table: "ModerationViolations");
        }
    }
}
