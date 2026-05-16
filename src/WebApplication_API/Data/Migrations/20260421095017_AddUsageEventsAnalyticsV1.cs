using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication_API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUsageEventsAnalyticsV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UsageEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ReferenceId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Details = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    DurationSeconds = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsageEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UsageEvents_DeviceId_Timestamp",
                table: "UsageEvents",
                columns: new[] { "DeviceId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_UsageEvents_EventType",
                table: "UsageEvents",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_UsageEvents_ReferenceId_Timestamp",
                table: "UsageEvents",
                columns: new[] { "ReferenceId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_UsageEvents_Timestamp",
                table: "UsageEvents",
                column: "Timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UsageEvents");
        }
    }
}
