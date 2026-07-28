using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AntiCheat.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddCertificateReputation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CertificateReputation",
                columns: table => new
                {
                    Thumbprint = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Subject = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Issuer = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SerialNumber = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NotBefore = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    NotAfter = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    IsRevoked = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsSelfSigned = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsChainTrusted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ChainStatus = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Verdict = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReputationScore = table.Column<double>(type: "double", nullable: false),
                    TotalFilesSeen = table.Column<int>(type: "int", nullable: false),
                    FilesFlagged = table.Column<int>(type: "int", nullable: false),
                    FirstSeen = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastVerified = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CertificateReputation", x => x.Thumbprint);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_CertificateReputation_LastVerified",
                table: "CertificateReputation",
                column: "LastVerified");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CertificateReputation");
        }
    }
}
