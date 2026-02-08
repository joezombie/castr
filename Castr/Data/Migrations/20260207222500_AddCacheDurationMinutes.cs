using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Castr.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCacheDurationMinutes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "file_extensions",
                table: "feeds",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "'.mp3'",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 100,
                oldDefaultValue: ".mp3");

            migrationBuilder.AddColumn<int>(
                name: "cache_duration_minutes",
                table: "feeds",
                type: "INTEGER",
                nullable: false,
                defaultValue: 5);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cache_duration_minutes",
                table: "feeds");

            migrationBuilder.AlterColumn<string>(
                name: "file_extensions",
                table: "feeds",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: ".mp3",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldDefaultValueSql: "'.mp3'");
        }
    }
}
