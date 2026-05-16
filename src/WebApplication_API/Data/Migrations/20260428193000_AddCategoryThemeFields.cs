using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication_API.Data.Migrations;

[DbContext(typeof(DBContext))]
[Migration("20260428193000_AddCategoryThemeFields")]
public partial class AddCategoryThemeFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "IconEmoji",
            table: "Categories",
            type: "TEXT",
            maxLength: 16,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PrimaryColor",
            table: "Categories",
            type: "TEXT",
            maxLength: 7,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SecondaryColor",
            table: "Categories",
            type: "TEXT",
            maxLength: 7,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ThemeName",
            table: "Categories",
            type: "TEXT",
            maxLength: 32,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Categories_ThemeName",
            table: "Categories",
            column: "ThemeName",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Categories_ThemeName",
            table: "Categories");

        migrationBuilder.DropColumn(
            name: "IconEmoji",
            table: "Categories");

        migrationBuilder.DropColumn(
            name: "PrimaryColor",
            table: "Categories");

        migrationBuilder.DropColumn(
            name: "SecondaryColor",
            table: "Categories");

        migrationBuilder.DropColumn(
            name: "ThemeName",
            table: "Categories");
    }
}
