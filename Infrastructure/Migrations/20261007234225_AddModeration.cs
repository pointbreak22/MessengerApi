using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddModeration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastStrikeAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ModerationStrikes",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ModerationViolations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Target = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    MatchedWords = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    StrikeNumber = table.Column<int>(type: "integer", nullable: false),
                    ResultedInBan = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModerationViolations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModerationViolations_UserId_CreatedAt",
                table: "ModerationViolations",
                columns: new[] { "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModerationViolations");

            migrationBuilder.DropColumn(
                name: "LastStrikeAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ModerationStrikes",
                table: "Users");
        }
    }
}
