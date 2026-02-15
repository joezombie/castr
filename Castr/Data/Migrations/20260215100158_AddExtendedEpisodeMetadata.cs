using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Castr.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExtendedEpisodeMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "album",
                table: "episodes",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "artist",
                table: "episodes",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "bitrate",
                table: "episodes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "genre",
                table: "episodes",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "has_embedded_art",
                table: "episodes",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "subtitle",
                table: "episodes",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "track_number",
                table: "episodes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "year",
                table: "episodes",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "album",
                table: "episodes");

            migrationBuilder.DropColumn(
                name: "artist",
                table: "episodes");

            migrationBuilder.DropColumn(
                name: "bitrate",
                table: "episodes");

            migrationBuilder.DropColumn(
                name: "genre",
                table: "episodes");

            migrationBuilder.DropColumn(
                name: "has_embedded_art",
                table: "episodes");

            migrationBuilder.DropColumn(
                name: "subtitle",
                table: "episodes");

            migrationBuilder.DropColumn(
                name: "track_number",
                table: "episodes");

            migrationBuilder.DropColumn(
                name: "year",
                table: "episodes");
        }
    }
}
