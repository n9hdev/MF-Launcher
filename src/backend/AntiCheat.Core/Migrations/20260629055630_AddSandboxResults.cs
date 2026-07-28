using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AntiCheat.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddSandboxResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SandboxResults",
                columns: table => new
                {
                    Sha256 = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Verdict = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProcessCount = table.Column<int>(type: "int", nullable: false),
                    FileWriteCount = table.Column<int>(type: "int", nullable: false),
                    RegistryWriteCount = table.Column<int>(type: "int", nullable: false),
                    NetworkConnectionCount = table.Column<int>(type: "int", nullable: false),
                    MutexCount = table.Column<int>(type: "int", nullable: false),
                    DllLoadCount = table.Column<int>(type: "int", nullable: false),
                    ServiceCount = table.Column<int>(type: "int", nullable: false),
                    CreatedSuspiciousProcess = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    WroteExecutableFile = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ConnectedToNetwork = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ModifiedRegistry = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SuspicionScore = table.Column<double>(type: "double", nullable: false),
                    DetailsJson = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AnalysedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SandboxResults", x => x.Sha256);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_SandboxResults_AnalysedAt",
                table: "SandboxResults",
                column: "AnalysedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SandboxResults");
        }
    }
}
