using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Castr.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "feeds",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    description = table.Column<string>(type: "TEXT", nullable: false),
                    directory = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    author = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    image_url = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    link = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    language = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false, defaultValue: "en-us"),
                    category = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    file_extensions = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, defaultValue: ".mp3"),
                    youtube_playlist_url = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    youtube_poll_interval_minutes = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 60),
                    youtube_enabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    youtube_max_concurrent_downloads = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    youtube_audio_quality = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "highest"),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feeds", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "activity_log",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    feed_id = table.Column<int>(type: "INTEGER", nullable: true),
                    activity_type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    message = table.Column<string>(type: "TEXT", nullable: false),
                    details = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_log", x => x.id);
                    table.ForeignKey(
                        name: "FK_activity_log_feeds_feed_id",
                        column: x => x.feed_id,
                        principalTable: "feeds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "download_queue",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    feed_id = table.Column<int>(type: "INTEGER", nullable: false),
                    video_id = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    video_title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "queued"),
                    progress_percent = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    error_message = table.Column<string>(type: "TEXT", nullable: true),
                    queued_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    started_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    completed_at = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_download_queue", x => x.id);
                    table.ForeignKey(
                        name: "FK_download_queue_feeds_feed_id",
                        column: x => x.feed_id,
                        principalTable: "feeds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "downloaded_videos",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    feed_id = table.Column<int>(type: "INTEGER", nullable: false),
                    video_id = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    filename = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    downloaded_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_downloaded_videos", x => x.id);
                    table.ForeignKey(
                        name: "FK_downloaded_videos_feeds_feed_id",
                        column: x => x.feed_id,
                        principalTable: "feeds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "episodes",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    feed_id = table.Column<int>(type: "INTEGER", nullable: false),
                    filename = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    video_id = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    youtube_title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    description = table.Column<string>(type: "TEXT", nullable: true),
                    thumbnail_url = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    display_order = table.Column<int>(type: "INTEGER", nullable: false),
                    added_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    publish_date = table.Column<DateTime>(type: "TEXT", nullable: true),
                    match_score = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_episodes", x => x.id);
                    table.ForeignKey(
                        name: "FK_episodes_feeds_feed_id",
                        column: x => x.feed_id,
                        principalTable: "feeds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_activity_feed_created",
                table: "activity_log",
                columns: new[] { "feed_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "idx_activity_type",
                table: "activity_log",
                column: "activity_type");

            migrationBuilder.CreateIndex(
                name: "idx_queue_feed_status",
                table: "download_queue",
                columns: new[] { "feed_id", "status" });

            migrationBuilder.CreateIndex(
                name: "idx_queue_feed_video",
                table: "download_queue",
                columns: new[] { "feed_id", "video_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_queue_status",
                table: "download_queue",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_downloaded_feed_video",
                table: "downloaded_videos",
                columns: new[] { "feed_id", "video_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_episodes_display_order",
                table: "episodes",
                columns: new[] { "feed_id", "display_order" });

            migrationBuilder.CreateIndex(
                name: "idx_episodes_feed_id",
                table: "episodes",
                column: "feed_id");

            migrationBuilder.CreateIndex(
                name: "idx_episodes_filename",
                table: "episodes",
                columns: new[] { "feed_id", "filename" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_episodes_video_id",
                table: "episodes",
                column: "video_id");

            migrationBuilder.CreateIndex(
                name: "idx_feeds_is_active",
                table: "feeds",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "idx_feeds_name",
                table: "feeds",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "activity_log");

            migrationBuilder.DropTable(
                name: "download_queue");

            migrationBuilder.DropTable(
                name: "downloaded_videos");

            migrationBuilder.DropTable(
                name: "episodes");

            migrationBuilder.DropTable(
                name: "feeds");
        }
    }
}
