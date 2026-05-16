using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication_API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChangeRequestsAndInbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChangeRequests",
                columns: table => new
                {
                    RequestId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TargetTable = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    TargetId = table.Column<int>(type: "INTEGER", nullable: true),
                    OwnerId = table.Column<int>(type: "INTEGER", nullable: false),
                    RequestType = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false, defaultValue: "CREATE"),
                    NewDataJson = table.Column<string>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    AdminNote = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false, defaultValue: "Pending"),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeRequests", x => x.RequestId);
                    table.ForeignKey(
                        name: "FK_ChangeRequests_DashboardUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "DashboardUsers",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InboxMessages",
                columns: table => new
                {
                    MessageId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    MessageType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false, defaultValue: "Info"),
                    RelatedRequestId = table.Column<int>(type: "INTEGER", nullable: true),
                    IsRead = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxMessages", x => x.MessageId);
                    table.ForeignKey(
                        name: "FK_InboxMessages_ChangeRequests_RelatedRequestId",
                        column: x => x.RelatedRequestId,
                        principalTable: "ChangeRequests",
                        principalColumn: "RequestId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_InboxMessages_DashboardUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "DashboardUsers",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChangeRequests_OwnerId_Status",
                table: "ChangeRequests",
                columns: new[] { "OwnerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ChangeRequests_TargetTable_TargetId",
                table: "ChangeRequests",
                columns: new[] { "TargetTable", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_RelatedRequestId",
                table: "InboxMessages",
                column: "RelatedRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_UserId_IsRead_CreatedAt",
                table: "InboxMessages",
                columns: new[] { "UserId", "IsRead", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InboxMessages");

            migrationBuilder.DropTable(
                name: "ChangeRequests");
        }
    }
}
