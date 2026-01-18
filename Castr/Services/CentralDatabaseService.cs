using Castr.Models;
using Microsoft.Data.Sqlite;

namespace Castr.Services;

/// <summary>
/// Central database service for managing feeds, episodes, and activities.
/// </summary>
public class CentralDatabaseService : ICentralDatabaseService
{
    private readonly ILogger<CentralDatabaseService> _logger;
    private readonly string _connectionString;
    
    public CentralDatabaseService(ILogger<CentralDatabaseService> logger, IConfiguration configuration)
    {
        _logger = logger;
        var dbPath = configuration["CentralDatabasePath"] ?? "/data/castr.db";
        
        // Ensure directory exists
        var directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            _logger.LogInformation("Created directory for central database: {Directory}", directory);
        }
        
        _connectionString = $"Data Source={dbPath}";
        _logger.LogInformation("Central database connection string: {ConnectionString}", _connectionString);
    }
    
    public async Task InitializeDatabaseAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing central database");
        
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        
        // Create feeds table
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS feeds (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    name TEXT NOT NULL UNIQUE,
                    title TEXT NOT NULL,
                    description TEXT NOT NULL,
                    directory TEXT NOT NULL,
                    author TEXT,
                    image_url TEXT,
                    link TEXT,
                    language TEXT DEFAULT 'en-us',
                    category TEXT,
                    file_extensions TEXT DEFAULT '.mp3',
                    youtube_playlist_url TEXT,
                    youtube_poll_interval_minutes INTEGER DEFAULT 60,
                    youtube_enabled INTEGER DEFAULT 0,
                    youtube_max_concurrent_downloads INTEGER DEFAULT 1,
                    youtube_audio_quality TEXT DEFAULT 'highest',
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL,
                    is_active INTEGER DEFAULT 1
                )";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        
        // Create episodes table
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS episodes (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    feed_id INTEGER NOT NULL,
                    filename TEXT NOT NULL,
                    video_id TEXT,
                    youtube_title TEXT,
                    description TEXT,
                    thumbnail_url TEXT,
                    display_order INTEGER NOT NULL,
                    added_at TEXT NOT NULL,
                    publish_date TEXT,
                    match_score REAL,
                    FOREIGN KEY (feed_id) REFERENCES feeds(id) ON DELETE CASCADE,
                    UNIQUE(feed_id, filename)
                )";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        
        // Create downloaded_videos table
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS downloaded_videos (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    feed_id INTEGER NOT NULL,
                    video_id TEXT NOT NULL,
                    filename TEXT,
                    downloaded_at TEXT NOT NULL,
                    FOREIGN KEY (feed_id) REFERENCES feeds(id) ON DELETE CASCADE,
                    UNIQUE(feed_id, video_id)
                )";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        
        // Create activity_log table
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS activity_log (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    feed_id INTEGER,
                    activity_type TEXT NOT NULL,
                    message TEXT NOT NULL,
                    details TEXT,
                    created_at TEXT NOT NULL,
                    FOREIGN KEY (feed_id) REFERENCES feeds(id) ON DELETE SET NULL
                )";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        
        // Create download_queue table
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS download_queue (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    feed_id INTEGER NOT NULL,
                    video_id TEXT NOT NULL,
                    video_title TEXT,
                    status TEXT NOT NULL,
                    progress_percent INTEGER DEFAULT 0,
                    error_message TEXT,
                    queued_at TEXT NOT NULL,
                    started_at TEXT,
                    completed_at TEXT,
                    FOREIGN KEY (feed_id) REFERENCES feeds(id) ON DELETE CASCADE,
                    UNIQUE(feed_id, video_id)
                )";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        
        // Create indexes for performance
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = @"
                CREATE INDEX IF NOT EXISTS idx_episodes_feed_id ON episodes(feed_id);
                CREATE INDEX IF NOT EXISTS idx_episodes_display_order ON episodes(feed_id, display_order);
                CREATE INDEX IF NOT EXISTS idx_activity_log_feed_id ON activity_log(feed_id);
                CREATE INDEX IF NOT EXISTS idx_activity_log_created_at ON activity_log(created_at DESC);
                CREATE INDEX IF NOT EXISTS idx_download_queue_status ON download_queue(status);
            ";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        
        _logger.LogInformation("Central database initialized successfully");
    }
    
    // Feed operations
    public async Task<List<FeedRecord>> GetAllFeedsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting all feeds");
        
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM feeds WHERE is_active = 1 ORDER BY name";
        
        var feeds = new List<FeedRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        
        while (await reader.ReadAsync(cancellationToken))
        {
            feeds.Add(ReadFeedFromReader(reader));
        }
        
        _logger.LogInformation("Retrieved {Count} feeds", feeds.Count);
        return feeds;
    }
    
    public async Task<FeedRecord?> GetFeedByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting feed by ID: {Id}", id);
        
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM feeds WHERE id = @id AND is_active = 1";
        command.Parameters.AddWithValue("@id", id);
        
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        
        if (await reader.ReadAsync(cancellationToken))
        {
            return ReadFeedFromReader(reader);
        }
        
        return null;
    }
    
    public async Task<FeedRecord?> GetFeedByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting feed by name: {Name}", name);
        
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM feeds WHERE name = @name AND is_active = 1";
        command.Parameters.AddWithValue("@name", name);
        
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        
        if (await reader.ReadAsync(cancellationToken))
        {
            return ReadFeedFromReader(reader);
        }
        
        return null;
    }
    
    public async Task<int> CreateFeedAsync(FeedRecord feed, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating new feed: {Name}", feed.Name);
        
        feed.CreatedAt = DateTime.UtcNow;
        feed.UpdatedAt = DateTime.UtcNow;
        
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO feeds (
                name, title, description, directory, author, image_url, link,
                language, category, file_extensions,
                youtube_playlist_url, youtube_poll_interval_minutes, youtube_enabled,
                youtube_max_concurrent_downloads, youtube_audio_quality,
                created_at, updated_at, is_active
            ) VALUES (
                @name, @title, @description, @directory, @author, @imageUrl, @link,
                @language, @category, @fileExtensions,
                @youtubePlaylistUrl, @youtubePollIntervalMinutes, @youtubeEnabled,
                @youtubeMaxConcurrentDownloads, @youtubeAudioQuality,
                @createdAt, @updatedAt, @isActive
            );
            SELECT last_insert_rowid();
        ";
        
        AddFeedParameters(command, feed);
        
        var id = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        _logger.LogInformation("Created feed with ID: {Id}", id);
        
        await LogActivityAsync(id, "feed_created", $"Feed '{feed.Name}' created", cancellationToken: cancellationToken);
        
        return id;
    }
    
    public async Task UpdateFeedAsync(FeedRecord feed, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating feed: {Name} (ID: {Id})", feed.Name, feed.Id);
        
        feed.UpdatedAt = DateTime.UtcNow;
        
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE feeds SET
                name = @name,
                title = @title,
                description = @description,
                directory = @directory,
                author = @author,
                image_url = @imageUrl,
                link = @link,
                language = @language,
                category = @category,
                file_extensions = @fileExtensions,
                youtube_playlist_url = @youtubePlaylistUrl,
                youtube_poll_interval_minutes = @youtubePollIntervalMinutes,
                youtube_enabled = @youtubeEnabled,
                youtube_max_concurrent_downloads = @youtubeMaxConcurrentDownloads,
                youtube_audio_quality = @youtubeAudioQuality,
                updated_at = @updatedAt,
                is_active = @isActive
            WHERE id = @id
        ";
        
        command.Parameters.AddWithValue("@id", feed.Id);
        AddFeedParameters(command, feed);
        
        await command.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogInformation("Updated feed: {Name}", feed.Name);
        
        await LogActivityAsync(feed.Id, "feed_updated", $"Feed '{feed.Name}' updated", cancellationToken: cancellationToken);
    }
    
    public async Task DeleteFeedAsync(int id, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting feed: {Id}", id);
        
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        
        // Soft delete by setting is_active to 0
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE feeds SET is_active = 0, updated_at = @updatedAt WHERE id = @id";
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow.ToString("o"));
        
        await command.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogInformation("Deleted feed: {Id}", id);
        
        await LogActivityAsync(id, "feed_deleted", $"Feed deleted", cancellationToken: cancellationToken);
    }
    
    public async Task<int> GetFeedEpisodeCountAsync(int feedId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting episode count for feed: {FeedId}", feedId);
        
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM episodes WHERE feed_id = @feedId";
        command.Parameters.AddWithValue("@feedId", feedId);
        
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }
    
    public async Task<DateTime?> GetFeedLastSyncTimeAsync(int feedId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting last sync time for feed: {FeedId}", feedId);
        
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT created_at FROM activity_log 
            WHERE feed_id = @feedId AND activity_type = 'sync_completed'
            ORDER BY created_at DESC LIMIT 1
        ";
        command.Parameters.AddWithValue("@feedId", feedId);
        
        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result != null && DateTime.TryParse(result.ToString(), out var syncTime))
        {
            return syncTime;
        }
        
        return null;
    }
    
    // Activity log operations
    public async Task LogActivityAsync(int? feedId, string activityType, string message, string? details = null, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Logging activity: {ActivityType} - {Message}", activityType, message);
        
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO activity_log (feed_id, activity_type, message, details, created_at)
            VALUES (@feedId, @activityType, @message, @details, @createdAt)
        ";
        command.Parameters.AddWithValue("@feedId", feedId.HasValue ? feedId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@activityType", activityType);
        command.Parameters.AddWithValue("@message", message);
        command.Parameters.AddWithValue("@details", details ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("o"));
        
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
    
    public async Task<List<ActivityLogRecord>> GetRecentActivitiesAsync(int count = 20, int? feedId = null, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting recent activities (count: {Count}, feedId: {FeedId})", count, feedId);
        
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        
        await using var command = connection.CreateCommand();
        if (feedId.HasValue)
        {
            command.CommandText = @"
                SELECT * FROM activity_log 
                WHERE feed_id = @feedId 
                ORDER BY created_at DESC LIMIT @count
            ";
            command.Parameters.AddWithValue("@feedId", feedId.Value);
        }
        else
        {
            command.CommandText = "SELECT * FROM activity_log ORDER BY created_at DESC LIMIT @count";
        }
        command.Parameters.AddWithValue("@count", count);
        
        var activities = new List<ActivityLogRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        
        while (await reader.ReadAsync(cancellationToken))
        {
            activities.Add(new ActivityLogRecord
            {
                Id = reader.GetInt32(0),
                FeedId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                ActivityType = reader.GetString(2),
                Message = reader.GetString(3),
                Details = reader.IsDBNull(4) ? null : reader.GetString(4),
                CreatedAt = DateTime.Parse(reader.GetString(5))
            });
        }
        
        return activities;
    }
    
    // Download queue operations
    public async Task<List<DownloadQueueItem>> GetActiveDownloadsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting active downloads");
        
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT * FROM download_queue 
            WHERE status IN ('queued', 'downloading')
            ORDER BY queued_at
        ";
        
        var downloads = new List<DownloadQueueItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        
        while (await reader.ReadAsync(cancellationToken))
        {
            downloads.Add(ReadDownloadFromReader(reader));
        }
        
        return downloads;
    }
    
    public async Task<DownloadQueueItem?> GetDownloadByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting download by ID: {Id}", id);
        
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM download_queue WHERE id = @id";
        command.Parameters.AddWithValue("@id", id);
        
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        
        if (await reader.ReadAsync(cancellationToken))
        {
            return ReadDownloadFromReader(reader);
        }
        
        return null;
    }
    
    public async Task<int> CreateDownloadAsync(DownloadQueueItem item, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating download for video: {VideoId}", item.VideoId);
        
        item.QueuedAt = DateTime.UtcNow;
        
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO download_queue (
                feed_id, video_id, video_title, status, progress_percent,
                error_message, queued_at, started_at, completed_at
            ) VALUES (
                @feedId, @videoId, @videoTitle, @status, @progressPercent,
                @errorMessage, @queuedAt, @startedAt, @completedAt
            );
            SELECT last_insert_rowid();
        ";
        
        AddDownloadParameters(command, item);
        
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }
    
    public async Task UpdateDownloadAsync(DownloadQueueItem item, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Updating download: {VideoId} - {Status} ({Progress}%)", 
            item.VideoId, item.Status, item.ProgressPercent);
        
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE download_queue SET
                feed_id = @feedId,
                video_id = @videoId,
                video_title = @videoTitle,
                status = @status,
                progress_percent = @progressPercent,
                error_message = @errorMessage,
                queued_at = @queuedAt,
                started_at = @startedAt,
                completed_at = @completedAt
            WHERE id = @id
        ";
        
        command.Parameters.AddWithValue("@id", item.Id);
        AddDownloadParameters(command, item);
        
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
    
    // Statistics
    public async Task<int> GetTotalFeedsCountAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting total feeds count");
        
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM feeds WHERE is_active = 1";
        
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }
    
    public async Task<int> GetTotalEpisodesCountAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting total episodes count");
        
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM episodes";
        
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }
    
    public async Task<int> GetActiveDownloadsCountAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting active downloads count");
        
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM download_queue WHERE status IN ('queued', 'downloading')";
        
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }
    
    // Helper methods
    private FeedRecord ReadFeedFromReader(SqliteDataReader reader)
    {
        return new FeedRecord
        {
            Id = reader.GetInt32(0),
            Name = reader.GetString(1),
            Title = reader.GetString(2),
            Description = reader.GetString(3),
            Directory = reader.GetString(4),
            Author = reader.IsDBNull(5) ? null : reader.GetString(5),
            ImageUrl = reader.IsDBNull(6) ? null : reader.GetString(6),
            Link = reader.IsDBNull(7) ? null : reader.GetString(7),
            Language = reader.GetString(8),
            Category = reader.IsDBNull(9) ? null : reader.GetString(9),
            FileExtensions = reader.GetString(10),
            YoutubePlaylistUrl = reader.IsDBNull(11) ? null : reader.GetString(11),
            YoutubePollIntervalMinutes = reader.GetInt32(12),
            YoutubeEnabled = reader.GetInt32(13) == 1,
            YoutubeMaxConcurrentDownloads = reader.GetInt32(14),
            YoutubeAudioQuality = reader.GetString(15),
            CreatedAt = DateTime.Parse(reader.GetString(16)),
            UpdatedAt = DateTime.Parse(reader.GetString(17)),
            IsActive = reader.GetInt32(18) == 1
        };
    }
    
    private void AddFeedParameters(SqliteCommand command, FeedRecord feed)
    {
        command.Parameters.AddWithValue("@name", feed.Name);
        command.Parameters.AddWithValue("@title", feed.Title);
        command.Parameters.AddWithValue("@description", feed.Description);
        command.Parameters.AddWithValue("@directory", feed.Directory);
        command.Parameters.AddWithValue("@author", feed.Author ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@imageUrl", feed.ImageUrl ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@link", feed.Link ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@language", feed.Language);
        command.Parameters.AddWithValue("@category", feed.Category ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@fileExtensions", feed.FileExtensions);
        command.Parameters.AddWithValue("@youtubePlaylistUrl", feed.YoutubePlaylistUrl ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@youtubePollIntervalMinutes", feed.YoutubePollIntervalMinutes);
        command.Parameters.AddWithValue("@youtubeEnabled", feed.YoutubeEnabled ? 1 : 0);
        command.Parameters.AddWithValue("@youtubeMaxConcurrentDownloads", feed.YoutubeMaxConcurrentDownloads);
        command.Parameters.AddWithValue("@youtubeAudioQuality", feed.YoutubeAudioQuality);
        command.Parameters.AddWithValue("@createdAt", feed.CreatedAt.ToString("o"));
        command.Parameters.AddWithValue("@updatedAt", feed.UpdatedAt.ToString("o"));
        command.Parameters.AddWithValue("@isActive", feed.IsActive ? 1 : 0);
    }
    
    private DownloadQueueItem ReadDownloadFromReader(SqliteDataReader reader)
    {
        return new DownloadQueueItem
        {
            Id = reader.GetInt32(0),
            FeedId = reader.GetInt32(1),
            VideoId = reader.GetString(2),
            VideoTitle = reader.IsDBNull(3) ? null : reader.GetString(3),
            Status = reader.GetString(4),
            ProgressPercent = reader.GetInt32(5),
            ErrorMessage = reader.IsDBNull(6) ? null : reader.GetString(6),
            QueuedAt = DateTime.Parse(reader.GetString(7)),
            StartedAt = reader.IsDBNull(8) ? null : DateTime.Parse(reader.GetString(8)),
            CompletedAt = reader.IsDBNull(9) ? null : DateTime.Parse(reader.GetString(9))
        };
    }
    
    private void AddDownloadParameters(SqliteCommand command, DownloadQueueItem item)
    {
        command.Parameters.AddWithValue("@feedId", item.FeedId);
        command.Parameters.AddWithValue("@videoId", item.VideoId);
        command.Parameters.AddWithValue("@videoTitle", item.VideoTitle ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@status", item.Status);
        command.Parameters.AddWithValue("@progressPercent", item.ProgressPercent);
        command.Parameters.AddWithValue("@errorMessage", item.ErrorMessage ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@queuedAt", item.QueuedAt.ToString("o"));
        command.Parameters.AddWithValue("@startedAt", item.StartedAt?.ToString("o") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@completedAt", item.CompletedAt?.ToString("o") ?? (object)DBNull.Value);
    }
}
