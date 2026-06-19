using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Castr.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddYouTubeSyncFilters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "youtube_download_after_date",
                table: "feeds",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "youtube_exclude_keywords",
                table: "feeds",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "youtube_include_keywords",
                table: "feeds",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "skipped_videos",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    feed_id = table.Column<int>(type: "INTEGER", nullable: false),
                    video_id = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    skip_reason = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    filter_hash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    skipped_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_skipped_videos", x => x.id);
                    table.ForeignKey(
                        name: "FK_skipped_videos_feeds_feed_id",
                        column: x => x.feed_id,
                        principalTable: "feeds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_skipped_feed_video",
                table: "skipped_videos",
                columns: new[] { "feed_id", "video_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "skipped_videos");

            migrationBuilder.DropColumn(
                name: "youtube_download_after_date",
                table: "feeds");

            migrationBuilder.DropColumn(
                name: "youtube_exclude_keywords",
                table: "feeds");

            migrationBuilder.DropColumn(
                name: "youtube_include_keywords",
                table: "feeds");
        }
    }
}
