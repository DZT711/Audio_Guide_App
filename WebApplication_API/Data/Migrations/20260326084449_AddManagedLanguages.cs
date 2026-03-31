using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication_API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddManagedLanguages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Languages",
                columns: table => new
                {
                    LanguageId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LangCode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    LangName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    NativeName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    PreferNativeVoice = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Languages", x => x.LanguageId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Languages_LangCode",
                table: "Languages",
                column: "LangCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Languages");
        }
    }
}
