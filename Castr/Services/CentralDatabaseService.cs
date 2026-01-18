using Microsoft.Data.Sqlite;
using Castr.Models;

namespace Castr.Services;

public class CentralDatabaseService : ICentralDatabaseService, IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<CentralDatabaseService> _logger;
    private readonly string _databasePath;
    private readonly SemaphoreSlim _dbLock = new(1, 1);
    private bool _disposed;

    public CentralDatabaseService(
        IConfiguration configuration,
        ILogger<CentralDatabaseService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        
        // Get database path from configuration or use default
        _databasePath = configuration.GetValue<string>("CentralDatabasePath") 
            ?? "/data/castr.db";
        
        _logger.LogInformation("Central database path: {Path}", _databasePath);
    }

    private static DateTime ParseIso8601DateTime(string dateTimeString)
    {
        return DateTime.ParseExact(dateTimeString, "O", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind);
    }

    private string GetConnectionString()
    {
        return $"Data Source={_databasePath}";
    }

    public async Task InitializeDatabaseAsync()
    {
        _logger.LogInformation("Initializing central database at {Path}", _databasePath);
        
        // Ensure directory exists
        var directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            _logger.LogInformation("Created directory: {Directory}", directory);
        }

        await using var connection = new SqliteConnection(GetConnectionString());
        await connection.OpenAsync();

        // Create feeds table
        var createFeedsTable = @"
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

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = createFeedsTable;
            await command.ExecuteNonQueryAsync();
        }

        // Create episodes table (references feeds)
        var createEpisodesTable = @"
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
                FOREIGN KEY (feed_id) REFERENCES feeds(id),
                UNIQUE(feed_id, filename)
            )";

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = createEpisodesTable;
            await command.ExecuteNonQueryAsync();
        }

        // Create downloaded_videos table
        var createDownloadedVideosTable = @"
            CREATE TABLE IF NOT EXISTS downloaded_videos (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                feed_id INTEGER NOT NULL,
                video_id TEXT NOT NULL,
                filename TEXT,
                downloaded_at TEXT NOT NULL,
                FOREIGN KEY (feed_id) REFERENCES feeds(id),
                UNIQUE(feed_id, video_id)
            )";

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = createDownloadedVideosTable;
            await command.ExecuteNonQueryAsync();
        }

        // Create activity_log table
        var createActivityLogTable = @"
            CREATE TABLE IF NOT EXISTS activity_log (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                feed_id INTEGER,
                activity_type TEXT NOT NULL,
                message TEXT NOT NULL,
                details TEXT,
                created_at TEXT NOT NULL,
                FOREIGN KEY (feed_id) REFERENCES feeds(id)
            )";

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = createActivityLogTable;
            await command.ExecuteNonQueryAsync();
        }

        // Create download_queue table
        var createDownloadQueueTable = @"
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
                FOREIGN KEY (feed_id) REFERENCES feeds(id),
                UNIQUE(feed_id, video_id)
            )";

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = createDownloadQueueTable;
            await command.ExecuteNonQueryAsync();
        }

        // Create indexes for performance
        var indexes = new[]
        {
            "CREATE INDEX IF NOT EXISTS idx_episodes_feed_id ON episodes(feed_id)",
            "CREATE INDEX IF NOT EXISTS idx_episodes_video_id ON episodes(video_id)",
            "CREATE INDEX IF NOT EXISTS idx_activity_log_created_at ON activity_log(created_at DESC)",
            "CREATE INDEX IF NOT EXISTS idx_download_queue_status ON download_queue(status)"
        };

        foreach (var indexSql in indexes)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = indexSql;
            await command.ExecuteNonQueryAsync();
        }

        _logger.LogInformation("Central database initialized successfully");
    }

    // Feed operations
    public async Task<List<FeedRecord>> GetFeedsAsync()
    {
        await _dbLock.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT id, name, title, description, directory, author, image_url, link, 
                       language, category, file_extensions, youtube_playlist_url, 
                       youtube_poll_interval_minutes, youtube_enabled, 
                       youtube_max_concurrent_downloads, youtube_audio_quality,
                       created_at, updated_at, is_active
                FROM feeds 
                WHERE is_active = 1
                ORDER BY name";

            var feeds = new List<FeedRecord>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                feeds.Add(new FeedRecord
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
                    CreatedAt = ParseIso8601DateTime(reader.GetString(16)),
                    UpdatedAt = ParseIso8601DateTime(reader.GetString(17)),
                    IsActive = reader.GetInt32(18) == 1
                });
            }

            return feeds;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<FeedRecord?> GetFeedByNameAsync(string name)
    {
        await _dbLock.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT id, name, title, description, directory, author, image_url, link, 
                       language, category, file_extensions, youtube_playlist_url, 
                       youtube_poll_interval_minutes, youtube_enabled, 
                       youtube_max_concurrent_downloads, youtube_audio_quality,
                       created_at, updated_at, is_active
                FROM feeds 
                WHERE name = @name";
            command.Parameters.AddWithValue("@name", name);

            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
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
                    CreatedAt = ParseIso8601DateTime(reader.GetString(16)),
                    UpdatedAt = ParseIso8601DateTime(reader.GetString(17)),
                    IsActive = reader.GetInt32(18) == 1
                };
            }

            return null;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<FeedRecord?> GetFeedByIdAsync(int id)
    {
        await _dbLock.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT id, name, title, description, directory, author, image_url, link, 
                       language, category, file_extensions, youtube_playlist_url, 
                       youtube_poll_interval_minutes, youtube_enabled, 
                       youtube_max_concurrent_downloads, youtube_audio_quality,
                       created_at, updated_at, is_active
                FROM feeds 
                WHERE id = @id";
            command.Parameters.AddWithValue("@id", id);

            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
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
                    CreatedAt = ParseIso8601DateTime(reader.GetString(16)),
                    UpdatedAt = ParseIso8601DateTime(reader.GetString(17)),
                    IsActive = reader.GetInt32(18) == 1
                };
            }

            return null;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<int> AddFeedAsync(FeedRecord feed)
    {
        await _dbLock.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO feeds (name, title, description, directory, author, image_url, link,
                                  language, category, file_extensions, youtube_playlist_url,
                                  youtube_poll_interval_minutes, youtube_enabled,
                                  youtube_max_concurrent_downloads, youtube_audio_quality,
                                  created_at, updated_at, is_active)
                VALUES (@name, @title, @description, @directory, @author, @imageUrl, @link,
                       @language, @category, @fileExtensions, @youtubePlaylistUrl,
                       @youtubePollIntervalMinutes, @youtubeEnabled,
                       @youtubeMaxConcurrentDownloads, @youtubeAudioQuality,
                       @createdAt, @updatedAt, @isActive);
                SELECT last_insert_rowid()";

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
            command.Parameters.AddWithValue("@createdAt", feed.CreatedAt.ToString("O"));
            command.Parameters.AddWithValue("@updatedAt", feed.UpdatedAt.ToString("O"));
            command.Parameters.AddWithValue("@isActive", feed.IsActive ? 1 : 0);

            var id = Convert.ToInt32(await command.ExecuteScalarAsync());
            _logger.LogInformation("Added feed {FeedName} with ID {FeedId}", feed.Name, id);
            return id;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task UpdateFeedAsync(FeedRecord feed)
    {
        await _dbLock.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE feeds
                SET name = @name, title = @title, description = @description, 
                    directory = @directory, author = @author, image_url = @imageUrl, 
                    link = @link, language = @language, category = @category, 
                    file_extensions = @fileExtensions, youtube_playlist_url = @youtubePlaylistUrl,
                    youtube_poll_interval_minutes = @youtubePollIntervalMinutes,
                    youtube_enabled = @youtubeEnabled,
                    youtube_max_concurrent_downloads = @youtubeMaxConcurrentDownloads,
                    youtube_audio_quality = @youtubeAudioQuality,
                    updated_at = @updatedAt, is_active = @isActive
                WHERE id = @id";

            command.Parameters.AddWithValue("@id", feed.Id);
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
            command.Parameters.AddWithValue("@updatedAt", feed.UpdatedAt.ToString("O"));
            command.Parameters.AddWithValue("@isActive", feed.IsActive ? 1 : 0);

            await command.ExecuteNonQueryAsync();
            _logger.LogInformation("Updated feed {FeedName}", feed.Name);
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task DeleteFeedAsync(int id)
    {
        await _dbLock.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = "UPDATE feeds SET is_active = 0 WHERE id = @id";
            command.Parameters.AddWithValue("@id", id);

            await command.ExecuteNonQueryAsync();
            _logger.LogInformation("Soft deleted feed with ID {FeedId}", id);
        }
        finally
        {
            _dbLock.Release();
        }
    }

    // Activity log operations
    public async Task LogActivityAsync(int? feedId, string activityType, string message, string? details = null)
    {
        await _dbLock.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO activity_log (feed_id, activity_type, message, details, created_at)
                VALUES (@feedId, @activityType, @message, @details, @createdAt)";

            command.Parameters.AddWithValue("@feedId", feedId.HasValue ? feedId.Value : DBNull.Value);
            command.Parameters.AddWithValue("@activityType", activityType);
            command.Parameters.AddWithValue("@message", message);
            command.Parameters.AddWithValue("@details", details ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("O"));

            await command.ExecuteNonQueryAsync();
            _logger.LogDebug("Logged activity: {ActivityType} - {Message}", activityType, message);
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<List<ActivityLogRecord>> GetRecentActivitiesAsync(int limit = 20)
    {
        await _dbLock.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT id, feed_id, activity_type, message, details, created_at
                FROM activity_log
                ORDER BY created_at DESC
                LIMIT @limit";
            command.Parameters.AddWithValue("@limit", limit);

            var activities = new List<ActivityLogRecord>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                activities.Add(new ActivityLogRecord
                {
                    Id = reader.GetInt32(0),
                    FeedId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                    ActivityType = reader.GetString(2),
                    Message = reader.GetString(3),
                    Details = reader.IsDBNull(4) ? null : reader.GetString(4),
                    CreatedAt = DateTime.ParseExact(reader.GetString(5), "O", System.Globalization.CultureInfo.InvariantCulture)
                });
            }

            return activities;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    // Download queue operations
    public async Task<List<DownloadQueueItem>> GetActiveDownloadsAsync()
    {
        await _dbLock.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT id, feed_id, video_id, video_title, status, progress_percent, 
                       error_message, queued_at, started_at, completed_at
                FROM download_queue
                WHERE status IN (@queued, @inProgress)
                ORDER BY queued_at";
            command.Parameters.AddWithValue("@queued", DownloadStatus.Queued);
            command.Parameters.AddWithValue("@inProgress", DownloadStatus.InProgress);

            var downloads = new List<DownloadQueueItem>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                downloads.Add(new DownloadQueueItem
                {
                    Id = reader.GetInt32(0),
                    FeedId = reader.GetInt32(1),
                    VideoId = reader.GetString(2),
                    VideoTitle = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Status = reader.GetString(4),
                    ProgressPercent = reader.GetInt32(5),
                    ErrorMessage = reader.IsDBNull(6) ? null : reader.GetString(6),
                    QueuedAt = ParseIso8601DateTime(reader.GetString(7)),
                    StartedAt = reader.IsDBNull(8) ? null : ParseIso8601DateTime(reader.GetString(8)),
                    CompletedAt = reader.IsDBNull(9) ? null : ParseIso8601DateTime(reader.GetString(9))
                });
            }

            return downloads;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<DownloadQueueItem?> GetDownloadAsync(int id)
    {
        await _dbLock.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT id, feed_id, video_id, video_title, status, progress_percent, 
                       error_message, queued_at, started_at, completed_at
                FROM download_queue
                WHERE id = @id";
            command.Parameters.AddWithValue("@id", id);

            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
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
                    QueuedAt = ParseIso8601DateTime(reader.GetString(7)),
                    StartedAt = reader.IsDBNull(8) ? null : ParseIso8601DateTime(reader.GetString(8)),
                    CompletedAt = reader.IsDBNull(9) ? null : ParseIso8601DateTime(reader.GetString(9))
                };
            }

            return null;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<int> AddDownloadAsync(DownloadQueueItem download)
    {
        await _dbLock.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO download_queue (feed_id, video_id, video_title, status, 
                                           progress_percent, queued_at)
                VALUES (@feedId, @videoId, @videoTitle, @status, @progressPercent, @queuedAt);
                SELECT last_insert_rowid()";

            command.Parameters.AddWithValue("@feedId", download.FeedId);
            command.Parameters.AddWithValue("@videoId", download.VideoId);
            command.Parameters.AddWithValue("@videoTitle", download.VideoTitle ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@status", download.Status);
            command.Parameters.AddWithValue("@progressPercent", download.ProgressPercent);
            command.Parameters.AddWithValue("@queuedAt", download.QueuedAt.ToString("O"));

            var id = Convert.ToInt32(await command.ExecuteScalarAsync());
            _logger.LogDebug("Added download to queue: {VideoId} for feed {FeedId}", download.VideoId, download.FeedId);
            return id;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task UpdateDownloadProgressAsync(int id, int progressPercent)
    {
        await _dbLock.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE download_queue
                SET progress_percent = @progressPercent,
                    status = @status,
                    started_at = COALESCE(started_at, @startedAt)
                WHERE id = @id";

            command.Parameters.AddWithValue("@id", id);
            command.Parameters.AddWithValue("@progressPercent", progressPercent);
            command.Parameters.AddWithValue("@status", DownloadStatus.InProgress);
            command.Parameters.AddWithValue("@startedAt", DateTime.UtcNow.ToString("O"));

            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task UpdateDownloadStatusAsync(int id, string status, string? errorMessage = null)
    {
        await _dbLock.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE download_queue
                SET status = @status,
                    error_message = @errorMessage
                WHERE id = @id";

            command.Parameters.AddWithValue("@id", id);
            command.Parameters.AddWithValue("@status", status);
            command.Parameters.AddWithValue("@errorMessage", errorMessage ?? (object)DBNull.Value);

            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task CompleteDownloadAsync(int id)
    {
        await _dbLock.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE download_queue
                SET status = @status,
                    progress_percent = 100,
                    completed_at = @completedAt
                WHERE id = @id";

            command.Parameters.AddWithValue("@id", id);
            command.Parameters.AddWithValue("@status", DownloadStatus.Completed);
            command.Parameters.AddWithValue("@completedAt", DateTime.UtcNow.ToString("O"));

            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            _dbLock.Release();
        }
    }

    // Statistics
    public async Task<int> GetTotalFeedsCountAsync()
    {
        await _dbLock.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM feeds WHERE is_active = 1";

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<int> GetTotalEpisodesCountAsync()
    {
        await _dbLock.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT COUNT(*) 
                FROM episodes e
                INNER JOIN feeds f ON e.feed_id = f.id
                WHERE f.is_active = 1";

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<int> GetActiveDownloadsCountAsync()
    {
        await _dbLock.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT COUNT(*) 
                FROM download_queue
                WHERE status IN (@queued, @inProgress)";
            command.Parameters.AddWithValue("@queued", DownloadStatus.Queued);
            command.Parameters.AddWithValue("@inProgress", DownloadStatus.InProgress);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _dbLock?.Dispose();
        _disposed = true;
    }
}
