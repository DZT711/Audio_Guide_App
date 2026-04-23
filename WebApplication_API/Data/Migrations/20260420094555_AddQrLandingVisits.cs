using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication_API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQrLandingVisits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QrLandingVisits",
                columns: table => new
                {
                    QrLandingVisitId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LocationId = table.Column<int>(type: "INTEGER", nullable: false),
                    OpenedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Referrer = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QrLandingVisits", x => x.QrLandingVisitId);
                    table.ForeignKey(
                        name: "FK_QrLandingVisits_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "LocationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QrLandingVisits_LocationId_OpenedAt",
                table: "QrLandingVisits",
                columns: new[] { "LocationId", "OpenedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_QrLandingVisits_OpenedAt",
                table: "QrLandingVisits",
                column: "OpenedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QrLandingVisits");
        }
    }
}
