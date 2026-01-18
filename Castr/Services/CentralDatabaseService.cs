using Microsoft.Data.Sqlite;
using System.Text.Json;
using System.Text.RegularExpressions;
using Castr.Models;

namespace Castr.Services;

/// <summary>
/// Central database service for managing all feeds, episodes, and downloads in a single database.
/// Replaces the per-feed database approach with a unified central database.
/// </summary>
public interface ICentralDatabaseService : IDisposable
{
    // Database initialization
    Task InitializeDatabaseAsync();
    Task<bool> IsDatabaseInitializedAsync();
    
    // Feed management
    Task<int> AddOrUpdateFeedAsync(FeedRecord feed);
    Task<FeedRecord?> GetFeedAsync(int feedId);
    Task<FeedRecord?> GetFeedByNameAsync(string feedName);
    Task<List<FeedRecord>> GetAllFeedsAsync();
    
    // Episode management
    Task<List<EpisodeRecord>> GetEpisodesAsync(int feedId);
    Task AddEpisodeAsync(int feedId, EpisodeRecord episode);
    Task AddEpisodesAsync(int feedId, IEnumerable<EpisodeRecord> episodes);
    Task UpdateEpisodeAsync(int feedId, EpisodeRecord episode);
    Task SyncDirectoryAsync(int feedId, string directory, string[] extensions);
    Task SyncPlaylistInfoAsync(int feedId, IEnumerable<PlaylistVideoInfo> videos, string directory);
    
    // Downloaded videos tracking
    Task<bool> IsVideoDownloadedAsync(int feedId, string videoId);
    Task<HashSet<string>> GetDownloadedVideoIdsAsync(int feedId);
    Task MarkVideoDownloadedAsync(int feedId, string videoId, string filename);
    
    // Download queue (for real-time updates)
    Task<int> AddToDownloadQueueAsync(int feedId, string videoId, string videoTitle);
    Task UpdateDownloadQueueProgressAsync(int queueId, double progress, string status);
    Task UpdateDownloadQueueStatusAsync(int queueId, string status, string? errorMessage = null);
    Task<List<DownloadQueueItem>> GetPendingDownloadsAsync(int feedId);
    
    // Activity logging
    Task LogActivityAsync(int feedId, string activityType, string message, string? metadata = null);
    Task<List<ActivityLogItem>> GetRecentActivityAsync(int feedId, int limit = 50);
    
    // Migration from legacy per-feed databases
    Task MigrateLegacyDatabaseAsync(string feedName, string legacyDatabasePath, int feedId);
}

public class FeedRecord
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public required string Directory { get; set; }
    public string? Author { get; set; }
    public string? ImageUrl { get; set; }
    public string? Link { get; set; }
    public string Language { get; set; } = "en-us";
    public string? Category { get; set; }
    public string[]? FileExtensions { get; set; } = [".mp3"];
    public string? DatabasePath { get; set; }
    public string? YoutubePlaylistUrl { get; set; }
    public int YoutubePollIntervalMinutes { get; set; } = 60;
    public bool YoutubeEnabled { get; set; } = true;
    public int YoutubeMaxConcurrentDownloads { get; set; } = 1;
    public string YoutubeAudioQuality { get; set; } = "highest";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class DownloadQueueItem
{
    public int Id { get; set; }
    public int FeedId { get; set; }
    public required string VideoId { get; set; }
    public required string VideoTitle { get; set; }
    public string Status { get; set; } = "pending"; // pending, downloading, completed, failed
    public double Progress { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class ActivityLogItem
{
    public int Id { get; set; }
    public int FeedId { get; set; }
    public required string ActivityType { get; set; } // sync, download, error
    public required string Message { get; set; }
    public string? Metadata { get; set; }
    public DateTime CreatedAt { get; set; }
}

public partial class CentralDatabaseService : ICentralDatabaseService
{
    private static readonly TimeSpan DatabaseLockTimeout = TimeSpan.FromSeconds(30);
    private readonly string _databasePath;
    private readonly ILogger<CentralDatabaseService> _logger;
    private readonly SemaphoreSlim _dbLock = new(1, 1);
    private bool _initialized;
    private int _disposed;

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    public CentralDatabaseService(ILogger<CentralDatabaseService> logger, string? databasePath = null)
    {
        _logger = logger;
        _databasePath = databasePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Castr",
            "central.db");
    }

    private string GetConnectionString() => $"Data Source={_databasePath}";

    private async Task AcquireDatabaseLockAsync()
    {
        if (!await _dbLock.WaitAsync(DatabaseLockTimeout))
        {
            _logger.LogError("Timeout waiting for central database lock");
            throw new TimeoutException("Central database lock timeout");
        }
    }

    public async Task<bool> IsDatabaseInitializedAsync()
    {
        if (_initialized) return true;
        
        if (!File.Exists(_databasePath)) return false;
        
        try
        {
            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();
            
            var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='feeds'";
            var count = Convert.ToInt32(await command.ExecuteScalarAsync());
            
            return count > 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task InitializeDatabaseAsync()
    {
        if (_initialized)
        {
            _logger.LogTrace("Central database already initialized in this session");
            return;
        }

        await AcquireDatabaseLockAsync();
        try
        {
            if (_initialized) return;

            var dbDir = Path.GetDirectoryName(_databasePath);
            if (!string.IsNullOrEmpty(dbDir) && !Directory.Exists(dbDir))
            {
                _logger.LogInformation("Creating directory for central database: {Directory}", dbDir);
                Directory.CreateDirectory(dbDir);
            }

            var dbExisted = File.Exists(_databasePath);
            _logger.LogDebug("Initializing central database at: {Path} (exists: {Exists})", _databasePath, dbExisted);

            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                -- Feeds table
                CREATE TABLE IF NOT EXISTS feeds (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    name TEXT UNIQUE NOT NULL,
                    title TEXT NOT NULL,
                    description TEXT NOT NULL,
                    directory TEXT NOT NULL,
                    author TEXT,
                    image_url TEXT,
                    link TEXT,
                    language TEXT DEFAULT 'en-us',
                    category TEXT,
                    file_extensions TEXT,
                    database_path TEXT,
                    youtube_playlist_url TEXT,
                    youtube_poll_interval_minutes INTEGER DEFAULT 60,
                    youtube_enabled INTEGER DEFAULT 1,
                    youtube_max_concurrent_downloads INTEGER DEFAULT 1,
                    youtube_audio_quality TEXT DEFAULT 'highest',
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL
                );

                CREATE INDEX IF NOT EXISTS idx_feeds_name ON feeds(name);

                -- Episodes table
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
                );

                CREATE INDEX IF NOT EXISTS idx_episodes_feed_id ON episodes(feed_id);
                CREATE INDEX IF NOT EXISTS idx_episodes_video_id ON episodes(video_id);
                CREATE INDEX IF NOT EXISTS idx_episodes_display_order ON episodes(display_order);

                -- Downloaded videos table
                CREATE TABLE IF NOT EXISTS downloaded_videos (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    feed_id INTEGER NOT NULL,
                    video_id TEXT NOT NULL,
                    filename TEXT,
                    downloaded_at TEXT NOT NULL,
                    FOREIGN KEY (feed_id) REFERENCES feeds(id) ON DELETE CASCADE,
                    UNIQUE(feed_id, video_id)
                );

                CREATE INDEX IF NOT EXISTS idx_downloaded_videos_feed_id ON downloaded_videos(feed_id);
                CREATE INDEX IF NOT EXISTS idx_downloaded_videos_video_id ON downloaded_videos(video_id);

                -- Download queue table
                CREATE TABLE IF NOT EXISTS download_queue (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    feed_id INTEGER NOT NULL,
                    video_id TEXT NOT NULL,
                    video_title TEXT NOT NULL,
                    status TEXT NOT NULL DEFAULT 'pending',
                    progress REAL DEFAULT 0.0,
                    error_message TEXT,
                    created_at TEXT NOT NULL,
                    started_at TEXT,
                    completed_at TEXT,
                    FOREIGN KEY (feed_id) REFERENCES feeds(id) ON DELETE CASCADE
                );

                CREATE INDEX IF NOT EXISTS idx_download_queue_feed_id ON download_queue(feed_id);
                CREATE INDEX IF NOT EXISTS idx_download_queue_status ON download_queue(status);

                -- Activity log table
                CREATE TABLE IF NOT EXISTS activity_log (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    feed_id INTEGER NOT NULL,
                    activity_type TEXT NOT NULL,
                    message TEXT NOT NULL,
                    metadata TEXT,
                    created_at TEXT NOT NULL,
                    FOREIGN KEY (feed_id) REFERENCES feeds(id) ON DELETE CASCADE
                );

                CREATE INDEX IF NOT EXISTS idx_activity_log_feed_id ON activity_log(feed_id);
                CREATE INDEX IF NOT EXISTS idx_activity_log_type ON activity_log(activity_type);
                CREATE INDEX IF NOT EXISTS idx_activity_log_created_at ON activity_log(created_at);
            ";

            await command.ExecuteNonQueryAsync();

            _initialized = true;

            if (dbExisted)
            {
                _logger.LogInformation("Initialized existing central database at {Path}", _databasePath);
            }
            else
            {
                _logger.LogInformation("Created and initialized new central database at {Path}", _databasePath);
            }
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<int> AddOrUpdateFeedAsync(FeedRecord feed)
    {
        await InitializeDatabaseAsync();
        await AcquireDatabaseLockAsync();
        
        try
        {
            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO feeds (
                    name, title, description, directory, author, image_url, link, language, category,
                    file_extensions, database_path, youtube_playlist_url, youtube_poll_interval_minutes,
                    youtube_enabled, youtube_max_concurrent_downloads, youtube_audio_quality,
                    created_at, updated_at
                ) VALUES (
                    @name, @title, @description, @directory, @author, @imageUrl, @link, @language, @category,
                    @fileExtensions, @databasePath, @youtubePlaylistUrl, @youtubePollIntervalMinutes,
                    @youtubeEnabled, @youtubeMaxConcurrentDownloads, @youtubeAudioQuality,
                    @createdAt, @updatedAt
                )
                ON CONFLICT(name) DO UPDATE SET
                    title = @title,
                    description = @description,
                    directory = @directory,
                    author = @author,
                    image_url = @imageUrl,
                    link = @link,
                    language = @language,
                    category = @category,
                    file_extensions = @fileExtensions,
                    database_path = @databasePath,
                    youtube_playlist_url = @youtubePlaylistUrl,
                    youtube_poll_interval_minutes = @youtubePollIntervalMinutes,
                    youtube_enabled = @youtubeEnabled,
                    youtube_max_concurrent_downloads = @youtubeMaxConcurrentDownloads,
                    youtube_audio_quality = @youtubeAudioQuality,
                    updated_at = @updatedAt
                RETURNING id;
            ";

            command.Parameters.AddWithValue("@name", feed.Name);
            command.Parameters.AddWithValue("@title", feed.Title);
            command.Parameters.AddWithValue("@description", feed.Description);
            command.Parameters.AddWithValue("@directory", feed.Directory);
            command.Parameters.AddWithValue("@author", (object?)feed.Author ?? DBNull.Value);
            command.Parameters.AddWithValue("@imageUrl", (object?)feed.ImageUrl ?? DBNull.Value);
            command.Parameters.AddWithValue("@link", (object?)feed.Link ?? DBNull.Value);
            command.Parameters.AddWithValue("@language", feed.Language);
            command.Parameters.AddWithValue("@category", (object?)feed.Category ?? DBNull.Value);
            command.Parameters.AddWithValue("@fileExtensions", 
                feed.FileExtensions != null ? JsonSerializer.Serialize(feed.FileExtensions) : DBNull.Value);
            command.Parameters.AddWithValue("@databasePath", (object?)feed.DatabasePath ?? DBNull.Value);
            command.Parameters.AddWithValue("@youtubePlaylistUrl", (object?)feed.YoutubePlaylistUrl ?? DBNull.Value);
            command.Parameters.AddWithValue("@youtubePollIntervalMinutes", feed.YoutubePollIntervalMinutes);
            command.Parameters.AddWithValue("@youtubeEnabled", feed.YoutubeEnabled ? 1 : 0);
            command.Parameters.AddWithValue("@youtubeMaxConcurrentDownloads", feed.YoutubeMaxConcurrentDownloads);
            command.Parameters.AddWithValue("@youtubeAudioQuality", feed.YoutubeAudioQuality);
            command.Parameters.AddWithValue("@createdAt", feed.CreatedAt != default ? feed.CreatedAt.ToString("O") : DateTime.UtcNow.ToString("O"));
            command.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow.ToString("O"));

            var result = await command.ExecuteScalarAsync();
            var feedId = Convert.ToInt32(result);
            
            _logger.LogInformation("Added/updated feed {FeedName} with ID {FeedId}", feed.Name, feedId);
            return feedId;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<FeedRecord?> GetFeedAsync(int feedId)
    {
        await InitializeDatabaseAsync();

        await using var connection = new SqliteConnection(GetConnectionString());
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT id, name, title, description, directory, author, image_url, link, language, category,
                   file_extensions, database_path, youtube_playlist_url, youtube_poll_interval_minutes,
                   youtube_enabled, youtube_max_concurrent_downloads, youtube_audio_quality,
                   created_at, updated_at
            FROM feeds
            WHERE id = @feedId
        ";
        command.Parameters.AddWithValue("@feedId", feedId);

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return ReadFeedRecord(reader);
        }

        return null;
    }

    public async Task<FeedRecord?> GetFeedByNameAsync(string feedName)
    {
        await InitializeDatabaseAsync();

        await using var connection = new SqliteConnection(GetConnectionString());
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT id, name, title, description, directory, author, image_url, link, language, category,
                   file_extensions, database_path, youtube_playlist_url, youtube_poll_interval_minutes,
                   youtube_enabled, youtube_max_concurrent_downloads, youtube_audio_quality,
                   created_at, updated_at
            FROM feeds
            WHERE name = @feedName
        ";
        command.Parameters.AddWithValue("@feedName", feedName);

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return ReadFeedRecord(reader);
        }

        return null;
    }

    public async Task<List<FeedRecord>> GetAllFeedsAsync()
    {
        await InitializeDatabaseAsync();

        var feeds = new List<FeedRecord>();

        await using var connection = new SqliteConnection(GetConnectionString());
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT id, name, title, description, directory, author, image_url, link, language, category,
                   file_extensions, database_path, youtube_playlist_url, youtube_poll_interval_minutes,
                   youtube_enabled, youtube_max_concurrent_downloads, youtube_audio_quality,
                   created_at, updated_at
            FROM feeds
            ORDER BY name
        ";

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            feeds.Add(ReadFeedRecord(reader));
        }

        return feeds;
    }

    private static FeedRecord ReadFeedRecord(SqliteDataReader reader)
    {
        var fileExtensionsJson = reader.IsDBNull(10) ? null : reader.GetString(10);
        var fileExtensions = fileExtensionsJson != null 
            ? JsonSerializer.Deserialize<string[]>(fileExtensionsJson)
            : null;

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
            FileExtensions = fileExtensions,
            DatabasePath = reader.IsDBNull(11) ? null : reader.GetString(11),
            YoutubePlaylistUrl = reader.IsDBNull(12) ? null : reader.GetString(12),
            YoutubePollIntervalMinutes = reader.GetInt32(13),
            YoutubeEnabled = reader.GetInt32(14) == 1,
            YoutubeMaxConcurrentDownloads = reader.GetInt32(15),
            YoutubeAudioQuality = reader.GetString(16),
            CreatedAt = DateTime.Parse(reader.GetString(17)),
            UpdatedAt = DateTime.Parse(reader.GetString(18))
        };
    }

    // Episode management methods will continue in next part...
    // (Due to length, I'll implement the remaining methods)

    public async Task<List<EpisodeRecord>> GetEpisodesAsync(int feedId)
    {
        await InitializeDatabaseAsync();

        var episodes = new List<EpisodeRecord>();

        await using var connection = new SqliteConnection(GetConnectionString());
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT id, filename, video_id, youtube_title, description, thumbnail_url,
                   display_order, added_at, publish_date, match_score
            FROM episodes
            WHERE feed_id = @feedId
            ORDER BY display_order ASC
        ";
        command.Parameters.AddWithValue("@feedId", feedId);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            episodes.Add(new EpisodeRecord
            {
                Id = reader.GetInt32(0),
                Filename = reader.GetString(1),
                VideoId = reader.IsDBNull(2) ? null : reader.GetString(2),
                YoutubeTitle = reader.IsDBNull(3) ? null : reader.GetString(3),
                Description = reader.IsDBNull(4) ? null : reader.GetString(4),
                ThumbnailUrl = reader.IsDBNull(5) ? null : reader.GetString(5),
                DisplayOrder = reader.GetInt32(6),
                AddedAt = DateTime.Parse(reader.GetString(7)),
                PublishDate = reader.IsDBNull(8) ? null : DateTime.Parse(reader.GetString(8)),
                MatchScore = reader.IsDBNull(9) ? null : reader.GetDouble(9)
            });
        }

        return episodes;
    }

    public async Task AddEpisodeAsync(int feedId, EpisodeRecord episode)
    {
        await AddEpisodesAsync(feedId, new[] { episode });
    }

    public async Task AddEpisodesAsync(int feedId, IEnumerable<EpisodeRecord> episodes)
    {
        await InitializeDatabaseAsync();

        var episodeList = episodes.ToList();
        if (episodeList.Count == 0) return;

        await AcquireDatabaseLockAsync();
        try
        {
            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();

            // Get next display order (prepend to top, so use negative/lower numbers)
            var orderCommand = connection.CreateCommand();
            orderCommand.CommandText = "SELECT COALESCE(MIN(display_order), 1) FROM episodes WHERE feed_id = @feedId";
            orderCommand.Parameters.AddWithValue("@feedId", feedId);
            var minOrder = Convert.ToInt32(await orderCommand.ExecuteScalarAsync());

            await using var transaction = await connection.BeginTransactionAsync();
            try
            {
                foreach (var episode in episodeList)
                {
                    minOrder--;
                    var command = connection.CreateCommand();
                    command.CommandText = @"
                        INSERT OR REPLACE INTO episodes
                        (feed_id, filename, video_id, youtube_title, description, thumbnail_url, display_order, added_at, publish_date, match_score)
                        VALUES (@feedId, @filename, @videoId, @youtubeTitle, @description, @thumbnailUrl, @displayOrder, @addedAt, @publishDate, @matchScore)
                    ";
                    command.Parameters.AddWithValue("@feedId", feedId);
                    command.Parameters.AddWithValue("@filename", episode.Filename);
                    command.Parameters.AddWithValue("@videoId", (object?)episode.VideoId ?? DBNull.Value);
                    command.Parameters.AddWithValue("@youtubeTitle", (object?)episode.YoutubeTitle ?? DBNull.Value);
                    command.Parameters.AddWithValue("@description", (object?)episode.Description ?? DBNull.Value);
                    command.Parameters.AddWithValue("@thumbnailUrl", (object?)episode.ThumbnailUrl ?? DBNull.Value);
                    command.Parameters.AddWithValue("@displayOrder", episode.DisplayOrder != 0 ? episode.DisplayOrder : minOrder);
                    command.Parameters.AddWithValue("@addedAt", episode.AddedAt != default ? episode.AddedAt.ToString("O") : DateTime.UtcNow.ToString("O"));
                    command.Parameters.AddWithValue("@publishDate", episode.PublishDate.HasValue ? episode.PublishDate.Value.ToString("O") : DBNull.Value);
                    command.Parameters.AddWithValue("@matchScore", (object?)episode.MatchScore ?? DBNull.Value);

                    await command.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
                _logger.LogInformation("Added {Count} episodes to central database for feed ID {FeedId}", episodeList.Count, feedId);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task UpdateEpisodeAsync(int feedId, EpisodeRecord episode)
    {
        await InitializeDatabaseAsync();
        await AcquireDatabaseLockAsync();
        
        try
        {
            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE episodes SET
                    video_id = @videoId,
                    youtube_title = @youtubeTitle,
                    description = @description,
                    thumbnail_url = @thumbnailUrl,
                    publish_date = @publishDate,
                    match_score = @matchScore
                WHERE feed_id = @feedId AND filename = @filename
            ";
            command.Parameters.AddWithValue("@feedId", feedId);
            command.Parameters.AddWithValue("@filename", episode.Filename);
            command.Parameters.AddWithValue("@videoId", (object?)episode.VideoId ?? DBNull.Value);
            command.Parameters.AddWithValue("@youtubeTitle", (object?)episode.YoutubeTitle ?? DBNull.Value);
            command.Parameters.AddWithValue("@description", (object?)episode.Description ?? DBNull.Value);
            command.Parameters.AddWithValue("@thumbnailUrl", (object?)episode.ThumbnailUrl ?? DBNull.Value);
            command.Parameters.AddWithValue("@publishDate", episode.PublishDate.HasValue ? episode.PublishDate.Value.ToString("O") : DBNull.Value);
            command.Parameters.AddWithValue("@matchScore", (object?)episode.MatchScore ?? DBNull.Value);

            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task SyncDirectoryAsync(int feedId, string directory, string[] extensions)
    {
        if (!Directory.Exists(directory))
            return;

        await InitializeDatabaseAsync();
        await AcquireDatabaseLockAsync();
        
        try
        {
            var filesInDirectory = extensions
                .SelectMany(ext => Directory.GetFiles(directory, $"*{ext}"))
                .Select(Path.GetFileName)
                .Where(f => f != null)
                .Cast<string>()
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();

            var existingFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var selectCommand = connection.CreateCommand();
            selectCommand.CommandText = "SELECT filename FROM episodes WHERE feed_id = @feedId";
            selectCommand.Parameters.AddWithValue("@feedId", feedId);

            await using (var reader = await selectCommand.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    existingFiles.Add(reader.GetString(0));
                }
            }

            var newFiles = filesInDirectory.Where(f => !existingFiles.Contains(f)).ToList();
            if (newFiles.Count == 0) return;

            var orderCommand = connection.CreateCommand();
            orderCommand.CommandText = "SELECT COALESCE(MIN(display_order), 1) FROM episodes WHERE feed_id = @feedId";
            orderCommand.Parameters.AddWithValue("@feedId", feedId);
            var minOrder = Convert.ToInt32(await orderCommand.ExecuteScalarAsync());

            await using var transaction = await connection.BeginTransactionAsync();
            try
            {
                foreach (var filename in newFiles.OrderBy(f => f))
                {
                    minOrder--;
                    var command = connection.CreateCommand();
                    command.CommandText = @"
                        INSERT INTO episodes (feed_id, filename, display_order, added_at)
                        VALUES (@feedId, @filename, @displayOrder, @addedAt)
                    ";
                    command.Parameters.AddWithValue("@feedId", feedId);
                    command.Parameters.AddWithValue("@filename", filename);
                    command.Parameters.AddWithValue("@displayOrder", minOrder);
                    command.Parameters.AddWithValue("@addedAt", DateTime.UtcNow.ToString("O"));

                    await command.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
                _logger.LogInformation("Synced {Count} new files from directory to central database for feed ID {FeedId}",
                    newFiles.Count, feedId);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task SyncPlaylistInfoAsync(int feedId, IEnumerable<PlaylistVideoInfo> videos, string directory)
    {
        _logger.LogDebug("Starting playlist sync for feed ID {FeedId}", feedId);
        await InitializeDatabaseAsync();

        var videoList = videos.ToList();
        if (videoList.Count == 0)
        {
            _logger.LogWarning("No videos provided for playlist sync");
            return;
        }

        _logger.LogInformation("Syncing {Count} playlist videos to local files in {Directory}",
            videoList.Count, directory);

        var filesInDirectory = Directory.Exists(directory)
            ? Directory.GetFiles(directory, "*.mp3")
                .Select(Path.GetFileName)
                .Where(f => f != null)
                .Cast<string>()
                .ToList()
            : new List<string>();

        _logger.LogInformation("Found {Count} MP3 files in directory to match against", filesInDirectory.Count);

        if (filesInDirectory.Count == 0)
        {
            _logger.LogWarning("No MP3 files found in directory for playlist sync: {Directory}", directory);
            return;
        }

        await AcquireDatabaseLockAsync();
        try
        {
            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();

            _logger.LogDebug("Loading existing episodes from central database");
            var existingEpisodes = new Dictionary<string, (int Id, string? VideoId)>(StringComparer.OrdinalIgnoreCase);
            var selectCommand = connection.CreateCommand();
            selectCommand.CommandText = "SELECT id, filename, video_id FROM episodes WHERE feed_id = @feedId";
            selectCommand.Parameters.AddWithValue("@feedId", feedId);

            await using (var reader = await selectCommand.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var filename = reader.GetString(1);
                    var videoId = reader.IsDBNull(2) ? null : reader.GetString(2);
                    existingEpisodes[filename] = (reader.GetInt32(0), videoId);
                }
            }

            _logger.LogDebug("Loaded {Count} existing episodes from central database", existingEpisodes.Count);

            var matchedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var updatedCount = 0;
            var addedCount = 0;
            var skippedCount = 0;

            _logger.LogInformation("Starting fuzzy matching for {Count} videos", videoList.Count);
            await using var transaction = await connection.BeginTransactionAsync();
            try
            {
                var videoIndex = 0;
                foreach (var video in videoList)
                {
                    videoIndex++;
                    _logger.LogDebug("Matching video {Index}/{Total}: '{Title}' (ID: {VideoId}, PlaylistIndex: {PlaylistIndex})",
                        videoIndex, videoList.Count, video.Title, video.VideoId, video.PlaylistIndex);

                    var (bestMatch, bestScore) = FindBestMatch(video.Title, filesInDirectory, matchedFiles);

                    if (bestMatch == null || bestScore < 0.6)
                    {
                        _logger.LogWarning("No fuzzy match found for video '{Title}' (best score: {Score:P1}, threshold: 60%)",
                            video.Title, bestScore);
                        skippedCount++;
                        continue;
                    }

                    _logger.LogInformation("Fuzzy matched '{VideoTitle}' -> '{Filename}' (score: {Score:P1})",
                        video.Title, bestMatch, bestScore);

                    matchedFiles.Add(bestMatch);

                    if (existingEpisodes.TryGetValue(bestMatch, out var existing))
                    {
                        var hasNewMetadata = video.Description != null || video.ThumbnailUrl != null || video.UploadDate.HasValue;

                        if (existing.VideoId != video.VideoId || hasNewMetadata)
                        {
                            _logger.LogDebug("Updating existing episode {Filename} with YouTube metadata", bestMatch);

                            var updateCommand = connection.CreateCommand();
                            if (hasNewMetadata)
                            {
                                updateCommand.CommandText = @"
                                    UPDATE episodes SET
                                        video_id = @videoId,
                                        youtube_title = @youtubeTitle,
                                        description = COALESCE(@description, description),
                                        thumbnail_url = COALESCE(@thumbnailUrl, thumbnail_url),
                                        publish_date = COALESCE(@publishDate, publish_date),
                                        display_order = @displayOrder,
                                        match_score = @matchScore
                                    WHERE id = @id
                                ";
                            }
                            else
                            {
                                updateCommand.CommandText = @"
                                    UPDATE episodes SET
                                        video_id = @videoId,
                                        youtube_title = @youtubeTitle,
                                        display_order = @displayOrder,
                                        match_score = @matchScore
                                    WHERE id = @id
                                ";
                            }

                            updateCommand.Parameters.AddWithValue("@id", existing.Id);
                            updateCommand.Parameters.AddWithValue("@videoId", video.VideoId);
                            updateCommand.Parameters.AddWithValue("@youtubeTitle", video.Title);
                            if (hasNewMetadata)
                            {
                                updateCommand.Parameters.AddWithValue("@description", (object?)video.Description ?? DBNull.Value);
                                updateCommand.Parameters.AddWithValue("@thumbnailUrl", (object?)video.ThumbnailUrl ?? DBNull.Value);
                                updateCommand.Parameters.AddWithValue("@publishDate",
                                    video.UploadDate.HasValue ? video.UploadDate.Value.ToString("O") : DBNull.Value);
                            }
                            updateCommand.Parameters.AddWithValue("@displayOrder", video.PlaylistIndex);
                            updateCommand.Parameters.AddWithValue("@matchScore", bestScore);

                            await updateCommand.ExecuteNonQueryAsync();
                            updatedCount++;
                        }
                    }
                    else
                    {
                        _logger.LogDebug("Adding new episode {Filename} to central database", bestMatch);

                        var insertCommand = connection.CreateCommand();
                        insertCommand.CommandText = @"
                            INSERT INTO episodes
                            (feed_id, filename, video_id, youtube_title, description, thumbnail_url, display_order, added_at, publish_date, match_score)
                            VALUES (@feedId, @filename, @videoId, @youtubeTitle, @description, @thumbnailUrl, @displayOrder, @addedAt, @publishDate, @matchScore)
                        ";
                        insertCommand.Parameters.AddWithValue("@feedId", feedId);
                        insertCommand.Parameters.AddWithValue("@filename", bestMatch);
                        insertCommand.Parameters.AddWithValue("@videoId", video.VideoId);
                        insertCommand.Parameters.AddWithValue("@youtubeTitle", video.Title);
                        insertCommand.Parameters.AddWithValue("@description", (object?)video.Description ?? DBNull.Value);
                        insertCommand.Parameters.AddWithValue("@thumbnailUrl", (object?)video.ThumbnailUrl ?? DBNull.Value);
                        insertCommand.Parameters.AddWithValue("@displayOrder", video.PlaylistIndex);
                        insertCommand.Parameters.AddWithValue("@addedAt", DateTime.UtcNow.ToString("O"));
                        insertCommand.Parameters.AddWithValue("@publishDate",
                            video.UploadDate.HasValue ? video.UploadDate.Value.ToString("O") : DBNull.Value);
                        insertCommand.Parameters.AddWithValue("@matchScore", bestScore);

                        await insertCommand.ExecuteNonQueryAsync();
                        addedCount++;
                    }
                }

                await transaction.CommitAsync();
                _logger.LogInformation(
                    "Playlist sync completed for feed ID {FeedId}: {Updated} updated, {Added} added, {Skipped} skipped, {Matched}/{Total} matched successfully",
                    feedId, updatedCount, addedCount, skippedCount, matchedFiles.Count, videoList.Count);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        finally
        {
            _dbLock.Release();
        }
    }

    private static (string? Match, double Score) FindBestMatch(
        string videoTitle,
        IEnumerable<string> files,
        HashSet<string> excludeFiles)
    {
        string? bestMatch = null;
        double bestScore = 0;

        var normalizedTitle = NormalizeForComparison(videoTitle);

        foreach (var file in files)
        {
            if (excludeFiles.Contains(file))
                continue;

            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(file);
            var normalizedFileName = NormalizeForComparison(fileNameWithoutExt);

            var score = CalculateSimilarity(normalizedTitle, normalizedFileName);

            if (score > bestScore)
            {
                bestScore = score;
                bestMatch = file;
            }
        }

        return (bestMatch, bestScore);
    }

    private static string NormalizeForComparison(string text)
    {
        var normalized = text
            .Replace("｜", "|")
            .Replace("：", ":")
            .Replace("？", "?")
            .Replace(" | BEHIND THE BASTARDS", "", StringComparison.OrdinalIgnoreCase)
            .Replace("| BEHIND THE BASTARDS", "", StringComparison.OrdinalIgnoreCase)
            .ToLowerInvariant()
            .Trim();

        normalized = WhitespaceRegex().Replace(normalized, " ");

        return normalized;
    }

    private static double CalculateSimilarity(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            return 0;

        var lcsLength = LongestCommonSubsequenceLength(a, b);
        return (2.0 * lcsLength) / (a.Length + b.Length);
    }

    private static int LongestCommonSubsequenceLength(string a, string b)
    {
        var m = a.Length;
        var n = b.Length;
        var dp = new int[m + 1, n + 1];

        for (var i = 1; i <= m; i++)
        {
            for (var j = 1; j <= n; j++)
            {
                if (a[i - 1] == b[j - 1])
                    dp[i, j] = dp[i - 1, j - 1] + 1;
                else
                    dp[i, j] = Math.Max(dp[i - 1, j], dp[i, j - 1]);
            }
        }

        return dp[m, n];
    }

    // Downloaded videos tracking
    public async Task<bool> IsVideoDownloadedAsync(int feedId, string videoId)
    {
        await InitializeDatabaseAsync();

        await using var connection = new SqliteConnection(GetConnectionString());
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM downloaded_videos WHERE feed_id = @feedId AND video_id = @videoId";
        command.Parameters.AddWithValue("@feedId", feedId);
        command.Parameters.AddWithValue("@videoId", videoId);

        var count = Convert.ToInt32(await command.ExecuteScalarAsync());
        return count > 0;
    }

    public async Task<HashSet<string>> GetDownloadedVideoIdsAsync(int feedId)
    {
        await InitializeDatabaseAsync();

        var ids = new HashSet<string>();

        await using var connection = new SqliteConnection(GetConnectionString());
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT video_id FROM downloaded_videos WHERE feed_id = @feedId";
        command.Parameters.AddWithValue("@feedId", feedId);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            ids.Add(reader.GetString(0));
        }

        return ids;
    }

    public async Task MarkVideoDownloadedAsync(int feedId, string videoId, string filename)
    {
        await InitializeDatabaseAsync();
        await AcquireDatabaseLockAsync();
        
        try
        {
            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT OR IGNORE INTO downloaded_videos (feed_id, video_id, filename, downloaded_at)
                VALUES (@feedId, @videoId, @filename, @downloadedAt)
            ";
            command.Parameters.AddWithValue("@feedId", feedId);
            command.Parameters.AddWithValue("@videoId", videoId);
            command.Parameters.AddWithValue("@filename", filename);
            command.Parameters.AddWithValue("@downloadedAt", DateTime.UtcNow.ToString("O"));

            await command.ExecuteNonQueryAsync();

            _logger.LogDebug("Marked video {VideoId} as downloaded for feed ID {FeedId}", videoId, feedId);
        }
        finally
        {
            _dbLock.Release();
        }
    }

    // Download queue management
    public async Task<int> AddToDownloadQueueAsync(int feedId, string videoId, string videoTitle)
    {
        await InitializeDatabaseAsync();
        await AcquireDatabaseLockAsync();
        
        try
        {
            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO download_queue (feed_id, video_id, video_title, status, progress, created_at)
                VALUES (@feedId, @videoId, @videoTitle, 'pending', 0.0, @createdAt)
                RETURNING id
            ";
            command.Parameters.AddWithValue("@feedId", feedId);
            command.Parameters.AddWithValue("@videoId", videoId);
            command.Parameters.AddWithValue("@videoTitle", videoTitle);
            command.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("O"));

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task UpdateDownloadQueueProgressAsync(int queueId, double progress, string status)
    {
        await InitializeDatabaseAsync();

        await using var connection = new SqliteConnection(GetConnectionString());
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE download_queue 
            SET progress = @progress, status = @status, 
                started_at = COALESCE(started_at, @startedAt)
            WHERE id = @queueId
        ";
        command.Parameters.AddWithValue("@queueId", queueId);
        command.Parameters.AddWithValue("@progress", progress);
        command.Parameters.AddWithValue("@status", status);
        command.Parameters.AddWithValue("@startedAt", DateTime.UtcNow.ToString("O"));

        await command.ExecuteNonQueryAsync();
    }

    public async Task UpdateDownloadQueueStatusAsync(int queueId, string status, string? errorMessage = null)
    {
        await InitializeDatabaseAsync();

        await using var connection = new SqliteConnection(GetConnectionString());
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE download_queue 
            SET status = @status, error_message = @errorMessage,
                completed_at = CASE WHEN @status IN ('completed', 'failed') THEN @completedAt ELSE completed_at END
            WHERE id = @queueId
        ";
        command.Parameters.AddWithValue("@queueId", queueId);
        command.Parameters.AddWithValue("@status", status);
        command.Parameters.AddWithValue("@errorMessage", (object?)errorMessage ?? DBNull.Value);
        command.Parameters.AddWithValue("@completedAt", DateTime.UtcNow.ToString("O"));

        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<DownloadQueueItem>> GetPendingDownloadsAsync(int feedId)
    {
        await InitializeDatabaseAsync();

        var items = new List<DownloadQueueItem>();

        await using var connection = new SqliteConnection(GetConnectionString());
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT id, feed_id, video_id, video_title, status, progress, error_message,
                   created_at, started_at, completed_at
            FROM download_queue
            WHERE feed_id = @feedId AND status IN ('pending', 'downloading')
            ORDER BY created_at ASC
        ";
        command.Parameters.AddWithValue("@feedId", feedId);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new DownloadQueueItem
            {
                Id = reader.GetInt32(0),
                FeedId = reader.GetInt32(1),
                VideoId = reader.GetString(2),
                VideoTitle = reader.GetString(3),
                Status = reader.GetString(4),
                Progress = reader.GetDouble(5),
                ErrorMessage = reader.IsDBNull(6) ? null : reader.GetString(6),
                CreatedAt = DateTime.Parse(reader.GetString(7)),
                StartedAt = reader.IsDBNull(8) ? null : DateTime.Parse(reader.GetString(8)),
                CompletedAt = reader.IsDBNull(9) ? null : DateTime.Parse(reader.GetString(9))
            });
        }

        return items;
    }

    // Activity logging
    public async Task LogActivityAsync(int feedId, string activityType, string message, string? metadata = null)
    {
        await InitializeDatabaseAsync();

        await using var connection = new SqliteConnection(GetConnectionString());
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO activity_log (feed_id, activity_type, message, metadata, created_at)
            VALUES (@feedId, @activityType, @message, @metadata, @createdAt)
        ";
        command.Parameters.AddWithValue("@feedId", feedId);
        command.Parameters.AddWithValue("@activityType", activityType);
        command.Parameters.AddWithValue("@message", message);
        command.Parameters.AddWithValue("@metadata", (object?)metadata ?? DBNull.Value);
        command.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("O"));

        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<ActivityLogItem>> GetRecentActivityAsync(int feedId, int limit = 50)
    {
        await InitializeDatabaseAsync();

        var items = new List<ActivityLogItem>();

        await using var connection = new SqliteConnection(GetConnectionString());
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT id, feed_id, activity_type, message, metadata, created_at
            FROM activity_log
            WHERE feed_id = @feedId
            ORDER BY created_at DESC
            LIMIT @limit
        ";
        command.Parameters.AddWithValue("@feedId", feedId);
        command.Parameters.AddWithValue("@limit", limit);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new ActivityLogItem
            {
                Id = reader.GetInt32(0),
                FeedId = reader.GetInt32(1),
                ActivityType = reader.GetString(2),
                Message = reader.GetString(3),
                Metadata = reader.IsDBNull(4) ? null : reader.GetString(4),
                CreatedAt = DateTime.Parse(reader.GetString(5))
            });
        }

        return items;
    }

    // Migration from legacy per-feed databases
    public async Task MigrateLegacyDatabaseAsync(string feedName, string legacyDatabasePath, int feedId)
    {
        if (!File.Exists(legacyDatabasePath))
        {
            _logger.LogDebug("No legacy database found at {Path}, skipping migration", legacyDatabasePath);
            return;
        }

        _logger.LogInformation("Migrating legacy database for feed {FeedName} from {Path}", feedName, legacyDatabasePath);

        await InitializeDatabaseAsync();
        await AcquireDatabaseLockAsync();

        try
        {
            // Read from legacy database
            await using var legacyConnection = new SqliteConnection($"Data Source={legacyDatabasePath}");
            await legacyConnection.OpenAsync();

            // Migrate episodes
            var episodesCommand = legacyConnection.CreateCommand();
            episodesCommand.CommandText = @"
                SELECT filename, video_id, youtube_title, description, thumbnail_url,
                       display_order, added_at, publish_date, match_score
                FROM episodes
            ";

            var episodes = new List<EpisodeRecord>();
            await using (var reader = await episodesCommand.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    episodes.Add(new EpisodeRecord
                    {
                        Filename = reader.GetString(0),
                        VideoId = reader.IsDBNull(1) ? null : reader.GetString(1),
                        YoutubeTitle = reader.IsDBNull(2) ? null : reader.GetString(2),
                        Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                        ThumbnailUrl = reader.IsDBNull(4) ? null : reader.GetString(4),
                        DisplayOrder = reader.GetInt32(5),
                        AddedAt = DateTime.Parse(reader.GetString(6)),
                        PublishDate = reader.IsDBNull(7) ? null : DateTime.Parse(reader.GetString(7)),
                        MatchScore = reader.IsDBNull(8) ? null : reader.GetDouble(8)
                    });
                }
            }

            _logger.LogInformation("Found {Count} episodes in legacy database", episodes.Count);

            // Migrate downloaded videos
            var downloadsCommand = legacyConnection.CreateCommand();
            downloadsCommand.CommandText = "SELECT video_id, filename, downloaded_at FROM downloaded_videos";

            var downloads = new List<(string VideoId, string Filename, string DownloadedAt)>();
            await using (var reader = await downloadsCommand.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    downloads.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));
                }
            }

            _logger.LogInformation("Found {Count} downloaded videos in legacy database", downloads.Count);

            // Write to central database
            await using var centralConnection = new SqliteConnection(GetConnectionString());
            await centralConnection.OpenAsync();
            await using var transaction = await centralConnection.BeginTransactionAsync();

            try
            {
                // Insert episodes
                foreach (var episode in episodes)
                {
                    var command = centralConnection.CreateCommand();
                    command.CommandText = @"
                        INSERT OR IGNORE INTO episodes
                        (feed_id, filename, video_id, youtube_title, description, thumbnail_url, display_order, added_at, publish_date, match_score)
                        VALUES (@feedId, @filename, @videoId, @youtubeTitle, @description, @thumbnailUrl, @displayOrder, @addedAt, @publishDate, @matchScore)
                    ";
                    command.Parameters.AddWithValue("@feedId", feedId);
                    command.Parameters.AddWithValue("@filename", episode.Filename);
                    command.Parameters.AddWithValue("@videoId", (object?)episode.VideoId ?? DBNull.Value);
                    command.Parameters.AddWithValue("@youtubeTitle", (object?)episode.YoutubeTitle ?? DBNull.Value);
                    command.Parameters.AddWithValue("@description", (object?)episode.Description ?? DBNull.Value);
                    command.Parameters.AddWithValue("@thumbnailUrl", (object?)episode.ThumbnailUrl ?? DBNull.Value);
                    command.Parameters.AddWithValue("@displayOrder", episode.DisplayOrder);
                    command.Parameters.AddWithValue("@addedAt", episode.AddedAt.ToString("O"));
                    command.Parameters.AddWithValue("@publishDate", episode.PublishDate.HasValue ? episode.PublishDate.Value.ToString("O") : DBNull.Value);
                    command.Parameters.AddWithValue("@matchScore", (object?)episode.MatchScore ?? DBNull.Value);

                    await command.ExecuteNonQueryAsync();
                }

                // Insert downloaded videos
                foreach (var (videoId, filename, downloadedAt) in downloads)
                {
                    var command = centralConnection.CreateCommand();
                    command.CommandText = @"
                        INSERT OR IGNORE INTO downloaded_videos (feed_id, video_id, filename, downloaded_at)
                        VALUES (@feedId, @videoId, @filename, @downloadedAt)
                    ";
                    command.Parameters.AddWithValue("@feedId", feedId);
                    command.Parameters.AddWithValue("@videoId", videoId);
                    command.Parameters.AddWithValue("@filename", filename);
                    command.Parameters.AddWithValue("@downloadedAt", downloadedAt);

                    await command.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
                _logger.LogInformation(
                    "Successfully migrated legacy database for {FeedName}: {EpisodeCount} episodes, {DownloadCount} downloads",
                    feedName, episodes.Count, downloads.Count);

                // Log migration activity
                await LogActivityAsync(feedId, "migration", 
                    $"Migrated {episodes.Count} episodes and {downloads.Count} downloads from legacy database");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            return;

        _dbLock?.Dispose();
        GC.SuppressFinalize(this);
    }
}
