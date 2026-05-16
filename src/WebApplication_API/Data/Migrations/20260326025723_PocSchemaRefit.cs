using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication_API.Data.Migrations
{
    /// <inheritdoc />
    public partial class PocSchemaRefit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Locations_Categories_CategoryId",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "ImgURL",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "OwnerName",
                table: "Locations");

            migrationBuilder.RenameColumn(
                name: "Phone",
                table: "Locations",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Locations",
                newName: "LocationId");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Categories",
                newName: "CategoryId");

            migrationBuilder.RenameColumn(
                name: "Language",
                table: "AudioContents",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "Duration",
                table: "AudioContents",
                newName: "FileSizeBytes");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "AudioContents",
                newName: "AudioId");

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "Locations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "EstablishedYear",
                table: "Locations",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "CategoryId",
                table: "Locations",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Locations",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Locations",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "DebounceSeconds",
                table: "Locations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 300);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Locations",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsGpsTriggerEnabled",
                table: "Locations",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "OwnerId",
                table: "Locations",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneContact",
                table: "Locations",
                type: "TEXT",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "Locations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "Radius",
                table: "Locations",
                type: "REAL",
                nullable: false,
                defaultValue: 30.0);

            migrationBuilder.AddColumn<double>(
                name: "StandbyRadius",
                table: "Locations",
                type: "REAL",
                nullable: false,
                defaultValue: 12.0);

            migrationBuilder.AddColumn<string>(
                name: "Ward",
                table: "Locations",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "Categories",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Categories",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Categories",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "AudioContents",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<string>(
                name: "FilePath",
                table: "AudioContents",
                type: "TEXT",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "AudioContents",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "DurationSeconds",
                table: "AudioContents",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InterruptPolicy",
                table: "AudioContents",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "NotificationFirst");

            migrationBuilder.AddColumn<bool>(
                name: "IsDownloadable",
                table: "AudioContents",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "LanguageCode",
                table: "AudioContents",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "vi-VN");

            migrationBuilder.AddColumn<string>(
                name: "PlaybackMode",
                table: "AudioContents",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "Auto");

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "AudioContents",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SourceType",
                table: "AudioContents",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "TTS");

            migrationBuilder.AddColumn<string>(
                name: "VoiceName",
                table: "AudioContents",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DashboardUsers",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Username = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    FullName = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    Role = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false, defaultValue: "User"),
                    Email = table.Column<string>(type: "TEXT", nullable: true),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DashboardUsers", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "LocationImages",
                columns: table => new
                {
                    ImageId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LocationId = table.Column<int>(type: "INTEGER", nullable: false),
                    ImageUrl = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationImages", x => x.ImageId);
                    table.ForeignKey(
                        name: "FK_LocationImages_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "LocationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LocationTrackingEvents",
                columns: table => new
                {
                    TrackingEventId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DeviceId = table.Column<string>(type: "TEXT", nullable: false),
                    SessionId = table.Column<string>(type: "TEXT", nullable: true),
                    Latitude = table.Column<double>(type: "REAL", nullable: false),
                    Longitude = table.Column<double>(type: "REAL", nullable: false),
                    AccuracyMeters = table.Column<double>(type: "REAL", nullable: true),
                    SpeedMetersPerSecond = table.Column<double>(type: "REAL", nullable: true),
                    BatteryPercent = table.Column<int>(type: "INTEGER", nullable: true),
                    IsForeground = table.Column<bool>(type: "INTEGER", nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationTrackingEvents", x => x.TrackingEventId);
                });

            migrationBuilder.CreateTable(
                name: "PlaybackEvents",
                columns: table => new
                {
                    PlaybackEventId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DeviceId = table.Column<string>(type: "TEXT", nullable: true),
                    LocationId = table.Column<int>(type: "INTEGER", nullable: true),
                    AudioId = table.Column<int>(type: "INTEGER", nullable: true),
                    TriggerSource = table.Column<string>(type: "TEXT", nullable: false),
                    EventType = table.Column<string>(type: "TEXT", nullable: false),
                    EventAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ListeningSeconds = table.Column<int>(type: "INTEGER", nullable: true),
                    QueuePosition = table.Column<int>(type: "INTEGER", nullable: true),
                    BatteryPercent = table.Column<int>(type: "INTEGER", nullable: true),
                    NetworkType = table.Column<string>(type: "TEXT", nullable: true),
                    SessionId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaybackEvents", x => x.PlaybackEventId);
                    table.ForeignKey(
                        name: "FK_PlaybackEvents_AudioContents_AudioId",
                        column: x => x.AudioId,
                        principalTable: "AudioContents",
                        principalColumn: "AudioId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PlaybackEvents_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "LocationId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Locations_Latitude_Longitude",
                table: "Locations",
                columns: new[] { "Latitude", "Longitude" });

            migrationBuilder.CreateIndex(
                name: "IX_Locations_OwnerId",
                table: "Locations",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_Status",
                table: "Locations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name",
                table: "Categories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AudioContents_LocationId",
                table: "AudioContents",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_DashboardUsers_Email",
                table: "DashboardUsers",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DashboardUsers_Phone",
                table: "DashboardUsers",
                column: "Phone",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DashboardUsers_Username",
                table: "DashboardUsers",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LocationImages_LocationId",
                table: "LocationImages",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_LocationTrackingEvents_DeviceId_CapturedAt",
                table: "LocationTrackingEvents",
                columns: new[] { "DeviceId", "CapturedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PlaybackEvents_AudioId",
                table: "PlaybackEvents",
                column: "AudioId");

            migrationBuilder.CreateIndex(
                name: "IX_PlaybackEvents_LocationId_EventAt",
                table: "PlaybackEvents",
                columns: new[] { "LocationId", "EventAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_AudioContents_Locations_LocationId",
                table: "AudioContents",
                column: "LocationId",
                principalTable: "Locations",
                principalColumn: "LocationId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Locations_Categories_CategoryId",
                table: "Locations",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "CategoryId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Locations_DashboardUsers_OwnerId",
                table: "Locations",
                column: "OwnerId",
                principalTable: "DashboardUsers",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AudioContents_Locations_LocationId",
                table: "AudioContents");

            migrationBuilder.DropForeignKey(
                name: "FK_Locations_Categories_CategoryId",
                table: "Locations");

            migrationBuilder.DropForeignKey(
                name: "FK_Locations_DashboardUsers_OwnerId",
                table: "Locations");

            migrationBuilder.DropTable(
                name: "DashboardUsers");

            migrationBuilder.DropTable(
                name: "LocationImages");

            migrationBuilder.DropTable(
                name: "LocationTrackingEvents");

            migrationBuilder.DropTable(
                name: "PlaybackEvents");

            migrationBuilder.DropIndex(
                name: "IX_Locations_Latitude_Longitude",
                table: "Locations");

            migrationBuilder.DropIndex(
                name: "IX_Locations_OwnerId",
                table: "Locations");

            migrationBuilder.DropIndex(
                name: "IX_Locations_Status",
                table: "Locations");

            migrationBuilder.DropIndex(
                name: "IX_Categories_Name",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_AudioContents_LocationId",
                table: "AudioContents");

            migrationBuilder.DropColumn(
                name: "City",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "DebounceSeconds",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "IsGpsTriggerEnabled",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "PhoneContact",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "Radius",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "StandbyRadius",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "Ward",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "AudioContents");

            migrationBuilder.DropColumn(
                name: "DurationSeconds",
                table: "AudioContents");

            migrationBuilder.DropColumn(
                name: "InterruptPolicy",
                table: "AudioContents");

            migrationBuilder.DropColumn(
                name: "IsDownloadable",
                table: "AudioContents");

            migrationBuilder.DropColumn(
                name: "LanguageCode",
                table: "AudioContents");

            migrationBuilder.DropColumn(
                name: "PlaybackMode",
                table: "AudioContents");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "AudioContents");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "AudioContents");

            migrationBuilder.DropColumn(
                name: "VoiceName",
                table: "AudioContents");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "Locations",
                newName: "Phone");

            migrationBuilder.RenameColumn(
                name: "LocationId",
                table: "Locations",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "CategoryId",
                table: "Categories",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "AudioContents",
                newName: "Language");

            migrationBuilder.RenameColumn(
                name: "FileSizeBytes",
                table: "AudioContents",
                newName: "Duration");

            migrationBuilder.RenameColumn(
                name: "AudioId",
                table: "AudioContents",
                newName: "Id");

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "Locations",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldDefaultValue: 1);

            migrationBuilder.AlterColumn<int>(
                name: "EstablishedYear",
                table: "Locations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "CategoryId",
                table: "Locations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImgURL",
                table: "Locations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerName",
                table: "Locations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "Categories",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldDefaultValue: 1);

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "AudioContents",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldDefaultValue: 1);

            migrationBuilder.AlterColumn<string>(
                name: "FilePath",
                table: "AudioContents",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Locations_Categories_CategoryId",
                table: "Locations",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
