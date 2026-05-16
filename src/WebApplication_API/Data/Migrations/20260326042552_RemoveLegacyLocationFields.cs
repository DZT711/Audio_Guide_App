using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication_API.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacyLocationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "City",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "Ward",
                table: "Locations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Locations",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Locations",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ward",
                table: "Locations",
                type: "TEXT",
                maxLength: 100,
                nullable: true);
        }
    }
}
