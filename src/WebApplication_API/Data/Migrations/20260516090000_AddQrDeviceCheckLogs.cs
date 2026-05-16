using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication_API.Data.Migrations;

[DbContext(typeof(DBContext))]
[Migration("20260516090000_AddQrDeviceCheckLogs")]
public partial class AddQrDeviceCheckLogs : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "QrDeviceCheckLogs",
            columns: table => new
            {
                QrDeviceCheckLogId = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                LocationId = table.Column<int>(type: "INTEGER", nullable: false),
                OpenedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                DeviceName = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                Platform = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                OsVersion = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                QrCode = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                WeakScore = table.Column<int>(type: "INTEGER", nullable: false),
                UserAgent = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_QrDeviceCheckLogs", x => x.QrDeviceCheckLogId);
                table.ForeignKey(
                    name: "FK_QrDeviceCheckLogs_Locations_LocationId",
                    column: x => x.LocationId,
                    principalTable: "Locations",
                    principalColumn: "LocationId",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_QrDeviceCheckLogs_LocationId_OpenedAt",
            table: "QrDeviceCheckLogs",
            columns: new[] { "LocationId", "OpenedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_QrDeviceCheckLogs_OpenedAt",
            table: "QrDeviceCheckLogs",
            column: "OpenedAt");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "QrDeviceCheckLogs");
    }
}
