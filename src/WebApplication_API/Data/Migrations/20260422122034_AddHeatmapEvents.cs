using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication_API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHeatmapEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HeatmapEvents",
                columns: table => new
                {
                    HeatmapEventId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DeviceId = table.Column<string>(type: "TEXT", nullable: false),
                    SessionId = table.Column<string>(type: "TEXT", nullable: true),
                    LocationId = table.Column<int>(type: "INTEGER", nullable: true),
                    TourId = table.Column<int>(type: "INTEGER", nullable: true),
                    EventType = table.Column<string>(type: "TEXT", nullable: false),
                    Weight = table.Column<int>(type: "INTEGER", nullable: false),
                    TriggerSource = table.Column<string>(type: "TEXT", nullable: false),
                    Latitude = table.Column<double>(type: "REAL", nullable: false),
                    Longitude = table.Column<double>(type: "REAL", nullable: false),
                    AccuracyMeters = table.Column<double>(type: "REAL", nullable: true),
                    SpeedMetersPerSecond = table.Column<double>(type: "REAL", nullable: true),
                    BatteryPercent = table.Column<int>(type: "INTEGER", nullable: true),
                    IsForeground = table.Column<bool>(type: "INTEGER", nullable: false),
                    Context = table.Column<string>(type: "TEXT", nullable: true),
                    CapturedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HeatmapEvents", x => x.HeatmapEventId);
                    table.ForeignKey(
                        name: "FK_HeatmapEvents_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "LocationId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HeatmapEvents_CapturedAt",
                table: "HeatmapEvents",
                column: "CapturedAt");

            migrationBuilder.CreateIndex(
                name: "IX_HeatmapEvents_EventType_CapturedAt",
                table: "HeatmapEvents",
                columns: new[] { "EventType", "CapturedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_HeatmapEvents_LocationId_CapturedAt",
                table: "HeatmapEvents",
                columns: new[] { "LocationId", "CapturedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_HeatmapEvents_LocationId_TourId_CapturedAt",
                table: "HeatmapEvents",
                columns: new[] { "LocationId", "TourId", "CapturedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HeatmapEvents");
        }
    }
}
