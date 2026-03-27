using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication_API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTourRouteSegmentsAndDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<double>(
                name: "WalkingSpeedKph",
                table: "Tours",
                type: "REAL",
                nullable: false,
                defaultValue: 5.0,
                oldClrType: typeof(double),
                oldType: "REAL",
                oldDefaultValue: 4.5);

            migrationBuilder.AddColumn<double>(
                name: "SegmentDistanceKm",
                table: "TourLocations",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SegmentDistanceKm",
                table: "TourLocations");

            migrationBuilder.AlterColumn<double>(
                name: "WalkingSpeedKph",
                table: "Tours",
                type: "REAL",
                nullable: false,
                defaultValue: 4.5,
                oldClrType: typeof(double),
                oldType: "REAL",
                oldDefaultValue: 5.0);
        }
    }
}
