using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication_API.Data.Migrations;

[DbContext(typeof(DBContext))]
[Migration("20260418091500_AddLocationQrSettings")]
public partial class AddLocationQrSettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "QrAutoplay",
            table: "Locations",
            type: "INTEGER",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<int?>(
            name: "QrAudioTrackId",
            table: "Locations",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "QrFormat",
            table: "Locations",
            type: "TEXT",
            maxLength: 8,
            nullable: false,
            defaultValue: "png");

        migrationBuilder.AddColumn<int>(
            name: "QrSize",
            table: "Locations",
            type: "INTEGER",
            nullable: false,
            defaultValue: 512);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "QrAutoplay",
            table: "Locations");

        migrationBuilder.DropColumn(
            name: "QrAudioTrackId",
            table: "Locations");

        migrationBuilder.DropColumn(
            name: "QrFormat",
            table: "Locations");

        migrationBuilder.DropColumn(
            name: "QrSize",
            table: "Locations");
    }
}
