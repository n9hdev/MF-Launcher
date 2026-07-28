using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AntiCheat.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddFileReputation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FileReputation",
                columns: table => new
                {
                    Sha256 = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Md5 = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FileSize = table.Column<long>(type: "bigint", nullable: true),
                    ProductName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FileVersion = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Signer = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FirstSeen = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastSeen = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    TimesSeen = table.Column<int>(type: "int", nullable: false),
                    TimesFlagged = table.Column<int>(type: "int", nullable: false),
                    UniquePlayers = table.Column<int>(type: "int", nullable: false),
                    Verdict = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, defaultValue: "unknown")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LastAnalysisTime = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    AnalysisNotes = table.Column<string>(type: "varchar(1024)", maxLength: 1024, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ConfidenceScore = table.Column<double>(type: "double", nullable: false),
                    IsLocalOverride = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileReputation", x => x.Sha256);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_FileReputation_LastSeen",
                table: "FileReputation",
                column: "LastSeen");

            migrationBuilder.CreateIndex(
                name: "IX_FileReputation_Verdict",
                table: "FileReputation",
                column: "Verdict");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FileReputation");
        }
    }
}
