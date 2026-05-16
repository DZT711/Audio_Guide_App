using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication_API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddToursAndUsageHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tours",
                columns: table => new
                {
                    TourId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OwnerId = table.Column<int>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    TotalDistanceKm = table.Column<double>(type: "REAL", nullable: false),
                    EstimatedDurationMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    WalkingSpeedKph = table.Column<double>(type: "REAL", nullable: false, defaultValue: 4.5),
                    StartTime = table.Column<string>(type: "TEXT", maxLength: 5, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tours", x => x.TourId);
                    table.ForeignKey(
                        name: "FK_Tours_DashboardUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "DashboardUsers",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TourLocations",
                columns: table => new
                {
                    TourId = table.Column<int>(type: "INTEGER", nullable: false),
                    LocationId = table.Column<int>(type: "INTEGER", nullable: false),
                    SequenceOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TourLocations", x => new { x.TourId, x.LocationId });
                    table.ForeignKey(
                        name: "FK_TourLocations_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "LocationId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TourLocations_Tours_TourId",
                        column: x => x.TourId,
                        principalTable: "Tours",
                        principalColumn: "TourId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TourLocations_LocationId",
                table: "TourLocations",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_TourLocations_TourId_SequenceOrder",
                table: "TourLocations",
                columns: new[] { "TourId", "SequenceOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tours_OwnerId",
                table: "Tours",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Tours_Status",
                table: "Tours",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TourLocations");

            migrationBuilder.DropTable(
                name: "Tours");
        }
    }
}
