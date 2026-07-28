using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AntiCheat.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddBanEntryFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "BannedAt",
                table: "BanEntries",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<double>(
                name: "DurationHours",
                table: "BanEntries",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "IpAddress",
                table: "BanEntries",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PlayerId",
                table: "BanEntries",
                type: "varchar(255)",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ProofUrl",
                table: "BanEntries",
                type: "varchar(2048)",
                maxLength: 2048,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "SerialNumber",
                table: "BanEntries",
                type: "varchar(128)",
                maxLength: 128,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_BanEntries_PlayerId",
                table: "BanEntries",
                column: "PlayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BanEntries_PlayerId",
                table: "BanEntries");

            migrationBuilder.DropColumn(
                name: "BannedAt",
                table: "BanEntries");

            migrationBuilder.DropColumn(
                name: "DurationHours",
                table: "BanEntries");

            migrationBuilder.DropColumn(
                name: "IpAddress",
                table: "BanEntries");

            migrationBuilder.DropColumn(
                name: "PlayerId",
                table: "BanEntries");

            migrationBuilder.DropColumn(
                name: "ProofUrl",
                table: "BanEntries");

            migrationBuilder.DropColumn(
                name: "SerialNumber",
                table: "BanEntries");
        }
    }
}
