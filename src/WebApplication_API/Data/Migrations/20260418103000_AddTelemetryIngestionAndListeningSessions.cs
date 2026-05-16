using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace WebApplication_API.Data.Migrations;

[DbContext(typeof(DBContext))]
[Migration("20260418103000_AddTelemetryIngestionAndListeningSessions")]
public partial class AddTelemetryIngestionAndListeningSessions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Context",
            table: "PlaybackEvents",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "PoiId",
            table: "PlaybackEvents",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "TourId",
            table: "PlaybackEvents",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Context",
            table: "LocationTrackingEvents",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "PoiId",
            table: "LocationTrackingEvents",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "TourId",
            table: "LocationTrackingEvents",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "AudioListeningSessions",
            columns: table => new
            {
                AudioListeningSessionId = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                DeviceId = table.Column<string>(type: "TEXT", nullable: false),
                SessionId = table.Column<string>(type: "TEXT", nullable: true),
                AudioId = table.Column<int>(type: "INTEGER", nullable: true),
                LocationId = table.Column<int>(type: "INTEGER", nullable: true),
                TourId = table.Column<int>(type: "INTEGER", nullable: true),
                PoiId = table.Column<int>(type: "INTEGER", nullable: true),
                StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                EndedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                ListeningSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                IsCompleted = table.Column<bool>(type: "INTEGER", nullable: false),
                InterruptedReason = table.Column<string>(type: "TEXT", nullable: true),
                Context = table.Column<string>(type: "TEXT", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AudioListeningSessions", x => x.AudioListeningSessionId);
                table.ForeignKey(
                    name: "FK_AudioListeningSessions_AudioContents_AudioId",
                    column: x => x.AudioId,
                    principalTable: "AudioContents",
                    principalColumn: "AudioId",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_AudioListeningSessions_Locations_LocationId",
                    column: x => x.LocationId,
                    principalTable: "Locations",
                    principalColumn: "LocationId",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(
            name: "IX_LocationTrackingEvents_CapturedAt",
            table: "LocationTrackingEvents",
            column: "CapturedAt");

        migrationBuilder.CreateIndex(
            name: "IX_LocationTrackingEvents_PoiId_TourId_CapturedAt",
            table: "LocationTrackingEvents",
            columns: new[] { "PoiId", "TourId", "CapturedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_PlaybackEvents_EventAt",
            table: "PlaybackEvents",
            column: "EventAt");

        migrationBuilder.CreateIndex(
            name: "IX_PlaybackEvents_PoiId_TourId_EventAt",
            table: "PlaybackEvents",
            columns: new[] { "PoiId", "TourId", "EventAt" });

        migrationBuilder.CreateIndex(
            name: "IX_AudioListeningSessions_AudioId",
            table: "AudioListeningSessions",
            column: "AudioId");

        migrationBuilder.CreateIndex(
            name: "IX_AudioListeningSessions_LocationId_StartedAt",
            table: "AudioListeningSessions",
            columns: new[] { "LocationId", "StartedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_AudioListeningSessions_PoiId_TourId_StartedAt",
            table: "AudioListeningSessions",
            columns: new[] { "PoiId", "TourId", "StartedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_AudioListeningSessions_StartedAt",
            table: "AudioListeningSessions",
            column: "StartedAt");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AudioListeningSessions");

        migrationBuilder.DropIndex(
            name: "IX_LocationTrackingEvents_CapturedAt",
            table: "LocationTrackingEvents");

        migrationBuilder.DropIndex(
            name: "IX_LocationTrackingEvents_PoiId_TourId_CapturedAt",
            table: "LocationTrackingEvents");

        migrationBuilder.DropIndex(
            name: "IX_PlaybackEvents_EventAt",
            table: "PlaybackEvents");

        migrationBuilder.DropIndex(
            name: "IX_PlaybackEvents_PoiId_TourId_EventAt",
            table: "PlaybackEvents");

        migrationBuilder.DropColumn(
            name: "Context",
            table: "PlaybackEvents");

        migrationBuilder.DropColumn(
            name: "PoiId",
            table: "PlaybackEvents");

        migrationBuilder.DropColumn(
            name: "TourId",
            table: "PlaybackEvents");

        migrationBuilder.DropColumn(
            name: "Context",
            table: "LocationTrackingEvents");

        migrationBuilder.DropColumn(
            name: "PoiId",
            table: "LocationTrackingEvents");

        migrationBuilder.DropColumn(
            name: "TourId",
            table: "LocationTrackingEvents");
    }
}
