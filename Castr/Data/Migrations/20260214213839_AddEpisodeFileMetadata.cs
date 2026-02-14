using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Castr.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEpisodeFileMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "duration_seconds",
                table: "episodes",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "file_size",
                table: "episodes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "title",
                table: "episodes",
                type: "TEXT",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "duration_seconds",
                table: "episodes");

            migrationBuilder.DropColumn(
                name: "file_size",
                table: "episodes");

            migrationBuilder.DropColumn(
                name: "title",
                table: "episodes");
        }
    }
}
