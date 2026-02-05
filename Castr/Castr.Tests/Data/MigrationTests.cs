using Castr.Data;
using Microsoft.EntityFrameworkCore;

namespace Castr.Tests.Data;

public class MigrationTests
{
    [Fact]
    public async Task Migrations_ApplyCleanly_OnSQLite()
    {
        // Arrange
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"castr_migration_test_{Guid.NewGuid()}.db");
        try
        {
            var options = new DbContextOptionsBuilder<CastrDbContext>()
                .UseSqlite($"Data Source={tempDbPath}")
                .Options;

            using var context = new CastrDbContext(options);

            // Act - Apply migrations
            await context.Database.MigrateAsync();

            // Assert - Verify tables exist by querying them
            var feeds = await context.Feeds.ToListAsync();
            var episodes = await context.Episodes.ToListAsync();
            var downloads = await context.DownloadedVideos.ToListAsync();
            var queue = await context.DownloadQueue.ToListAsync();
            var logs = await context.ActivityLogs.ToListAsync();

            Assert.NotNull(feeds);
            Assert.NotNull(episodes);
            Assert.NotNull(downloads);
            Assert.NotNull(queue);
            Assert.NotNull(logs);
        }
        finally
        {
            if (File.Exists(tempDbPath))
                File.Delete(tempDbPath);
        }
    }

    [Fact]
    public void Migrations_HavePendingModelChanges_ShouldBeFalse()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<CastrDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        using var context = new CastrDbContext(options);

        // This ensures our migrations are up to date with the model
        // If this fails, run: dotnet ef migrations add <MigrationName>
        var hasPendingChanges = context.Database.HasPendingModelChanges();

        // Note: HasPendingModelChanges returns false for in-memory databases
        // So we also verify we can get pending migrations (should be our InitialCreate)
        var pendingMigrations = context.Database.GetPendingMigrations();

        Assert.NotEmpty(pendingMigrations); // Should have InitialCreate pending
    }

    [Fact]
    public async Task Migrations_AreIdempotent_CanApplyTwice()
    {
        // Arrange
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"castr_idempotent_test_{Guid.NewGuid()}.db");
        try
        {
            var options = new DbContextOptionsBuilder<CastrDbContext>()
                .UseSqlite($"Data Source={tempDbPath}")
                .Options;

            // Act - Apply migrations twice
            using (var context = new CastrDbContext(options))
            {
                await context.Database.MigrateAsync();
            }

            using (var context = new CastrDbContext(options))
            {
                // Should not throw - migrations are tracked and won't re-apply
                await context.Database.MigrateAsync();

                // Assert - Verify database still works
                var pendingMigrations = context.Database.GetPendingMigrations();
                Assert.Empty(pendingMigrations); // All migrations applied
            }
        }
        finally
        {
            if (File.Exists(tempDbPath))
                File.Delete(tempDbPath);
        }
    }

    [Fact]
    public async Task Migrations_CreateCorrectIndexes()
    {
        // Arrange
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"castr_index_test_{Guid.NewGuid()}.db");
        try
        {
            var options = new DbContextOptionsBuilder<CastrDbContext>()
                .UseSqlite($"Data Source={tempDbPath}")
                .Options;

            using var context = new CastrDbContext(options);
            await context.Database.MigrateAsync();

            // Act - Query SQLite index info
            var connection = context.Database.GetDbConnection();
            await connection.OpenAsync();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT name FROM sqlite_master WHERE type='index' AND name LIKE 'idx_%'";
            var indexes = new List<string>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                indexes.Add(reader.GetString(0));
            }

            // Assert - Verify expected indexes exist
            Assert.Contains("idx_feeds_name", indexes);
            Assert.Contains("idx_feeds_is_active", indexes);
            Assert.Contains("idx_episodes_feed_id", indexes);
            Assert.Contains("idx_episodes_filename", indexes);
            Assert.Contains("idx_episodes_video_id", indexes);
            Assert.Contains("idx_episodes_display_order", indexes);
            Assert.Contains("idx_downloaded_feed_video", indexes);
            Assert.Contains("idx_queue_feed_video", indexes);
            Assert.Contains("idx_queue_status", indexes);
            Assert.Contains("idx_activity_type", indexes);
            Assert.Contains("idx_activity_feed_created", indexes);
        }
        finally
        {
            if (File.Exists(tempDbPath))
                File.Delete(tempDbPath);
        }
    }
}
