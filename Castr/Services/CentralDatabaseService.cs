using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using Castr.Models;
using System.Text.RegularExpressions;

namespace Castr.Services;

/// <summary>
/// Central database service implementation.
/// Manages all feeds, episodes, and activity in a single SQLite database.
/// </summary>
public partial class CentralDatabaseService : ICentralDatabaseService
{
    private static readonly TimeSpan DatabaseLockTimeout = TimeSpan.FromSeconds(30);
    
    private readonly ILogger<CentralDatabaseService> _logger;
    private readonly string _databasePath;
    private readonly SemaphoreSlim _dbLock = new(1, 1);
    private bool _initialized;
    private int _disposed;

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    public CentralDatabaseService(
        IConfiguration configuration,
        ILogger<CentralDatabaseService> logger)
    {
        _logger = logger;
        
        // Get central database path from configuration, or default to /data/podcast_central.db
        _databasePath = configuration["PodcastFeeds:CentralDatabasePath"]
            ?? "/data/podcast_central.db";
            
        _logger.LogInformation("Central database path: {Path}", _databasePath);
    }

    private string GetConnectionString()
    {
        return $"Data Source={_databasePath}";
    }

    private async Task AcquireDatabaseLockAsync()
    {
        if (!await _dbLock.WaitAsync(DatabaseLockTimeout))
        {
            _logger.LogError("Timeout waiting for central database lock");
            throw new TimeoutException("Central database lock timeout");
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
            if (_initialized)
                return;

            var dbExisted = File.Exists(_databasePath);
            _logger.LogDebug("Central database path: {Path} (exists: {Exists})", _databasePath, dbExisted);

            var dbDir = Path.GetDirectoryName(_databasePath);
            if (!string.IsNullOrEmpty(dbDir) && !Directory.Exists(dbDir))
            {
                _logger.LogInformation("Creating directory for central database: {Directory}", dbDir);
                Directory.CreateDirectory(dbDir);
            }

            _logger.LogDebug("Opening central database connection");
            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();

            _logger.LogDebug("Creating central database schema if not exists");
            var command = connection.CreateCommand();
            command.CommandText = @"
                -- Feeds table (replaces appsettings.json configuration)
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
                );

                CREATE INDEX IF NOT EXISTS idx_feeds_name ON feeds(name);
                CREATE INDEX IF NOT EXISTS idx_feeds_is_active ON feeds(is_active);

                -- Episodes table (references feeds)
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
                );

                CREATE INDEX IF NOT EXISTS idx_episodes_feed_id ON episodes(feed_id);
                CREATE INDEX IF NOT EXISTS idx_episodes_filename ON episodes(feed_id, filename);
                CREATE INDEX IF NOT EXISTS idx_episodes_video_id ON episodes(video_id);
                CREATE INDEX IF NOT EXISTS idx_episodes_display_order ON episodes(feed_id, display_order);

                -- Downloaded videos table (references feeds)
                CREATE TABLE IF NOT EXISTS downloaded_videos (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    feed_id INTEGER NOT NULL,
                    video_id TEXT NOT NULL,
                    filename TEXT,
                    downloaded_at TEXT NOT NULL,
                    FOREIGN KEY (feed_id) REFERENCES feeds(id),
                    UNIQUE(feed_id, video_id)
                );

                CREATE INDEX IF NOT EXISTS idx_downloaded_feed_video ON downloaded_videos(feed_id, video_id);

                -- Activity log for dashboard monitoring
                CREATE TABLE IF NOT EXISTS activity_log (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    feed_id INTEGER,
                    activity_type TEXT NOT NULL,
                    message TEXT NOT NULL,
                    details TEXT,
                    created_at TEXT NOT NULL,
                    FOREIGN KEY (feed_id) REFERENCES feeds(id)
                );

                CREATE INDEX IF NOT EXISTS idx_activity_feed_created ON activity_log(feed_id, created_at DESC);
                CREATE INDEX IF NOT EXISTS idx_activity_type ON activity_log(activity_type);

                -- Download queue for active downloads
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
                );

                CREATE INDEX IF NOT EXISTS idx_queue_feed_status ON download_queue(feed_id, status);
                CREATE INDEX IF NOT EXISTS idx_queue_status ON download_queue(status);
                
                -- User settings table (single row)
                CREATE TABLE IF NOT EXISTS user_settings (
                    id INTEGER PRIMARY KEY CHECK (id = 1),
                    dark_mode INTEGER DEFAULT 1,
                    default_polling_interval_minutes INTEGER DEFAULT 60,
                    default_audio_quality TEXT DEFAULT 'highest',
                    default_language TEXT DEFAULT 'en-us',
                    default_file_extensions TEXT DEFAULT '.mp3',
                    default_category TEXT DEFAULT 'Society & Culture',
                    updated_at TEXT NOT NULL
                );
                
                -- Insert default settings if not exists
                INSERT OR IGNORE INTO user_settings (id, dark_mode, default_polling_interval_minutes, 
                    default_audio_quality, default_language, default_file_extensions, 
                    default_category, updated_at)
                VALUES (1, 1, 60, 'highest', 'en-us', '.mp3', 'Society & Culture', @now);
            ";

            command.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));
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

    #region Feed Management

    public async Task<List<FeedRecord>> GetAllFeedsAsync()
    {
        await InitializeDatabaseAsync();

        var feeds = new List<FeedRecord>();

        await using var connection = new SqliteConnection(GetConnectionString());
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT id, name, title, description, directory, author, image_url, link,
                   language, category, file_extensions, youtube_playlist_url,
                   youtube_poll_interval_minutes, youtube_enabled,
                   youtube_max_concurrent_downloads, youtube_audio_quality,
                   created_at, updated_at, is_active
            FROM feeds
            ORDER BY name ASC
        ";

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            feeds.Add(ReadFeedFromReader(reader));
        }

        return feeds;
    }

    public async Task<FeedRecord?> GetFeedByNameAsync(string name)
    {
        await InitializeDatabaseAsync();

        await using var connection = new SqliteConnection(GetConnectionString());
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT id, name, title, description, directory, author, image_url, link,
                   language, category, file_extensions, youtube_playlist_url,
                   youtube_poll_interval_minutes, youtube_enabled,
                   youtube_max_concurrent_downloads, youtube_audio_quality,
                   created_at, updated_at, is_active
            FROM feeds
            WHERE name = @name
        ";
        command.Parameters.AddWithValue("@name", name);

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return ReadFeedFromReader(reader);
        }

        return null;
    }

    public async Task<FeedRecord?> GetFeedByIdAsync(int id)
    {
        await InitializeDatabaseAsync();

        await using var connection = new SqliteConnection(GetConnectionString());
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT id, name, title, description, directory, author, image_url, link,
                   language, category, file_extensions, youtube_playlist_url,
                   youtube_poll_interval_minutes, youtube_enabled,
                   youtube_max_concurrent_downloads, youtube_audio_quality,
                   created_at, updated_at, is_active
            FROM feeds
            WHERE id = @id
        ";
        command.Parameters.AddWithValue("@id", id);

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return ReadFeedFromReader(reader);
        }

        return null;
    }

    private static FeedRecord ReadFeedFromReader(SqliteDataReader reader)
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
            Language = reader.IsDBNull(8) ? null : reader.GetString(8),
            Category = reader.IsDBNull(9) ? null : reader.GetString(9),
            FileExtensions = reader.IsDBNull(10) ? null : reader.GetString(10),
            YouTubePlaylistUrl = reader.IsDBNull(11) ? null : reader.GetString(11),
            YouTubePollIntervalMinutes = reader.IsDBNull(12) ? null : reader.GetInt32(12),
            YouTubeEnabled = reader.GetInt32(13) != 0,
            YouTubeMaxConcurrentDownloads = reader.IsDBNull(14) ? null : reader.GetInt32(14),
            YouTubeAudioQuality = reader.IsDBNull(15) ? null : reader.GetString(15),
            CreatedAt = DateTime.Parse(reader.GetString(16)),
            UpdatedAt = DateTime.Parse(reader.GetString(17)),
            IsActive = reader.GetInt32(18) != 0
        };
    }

    public async Task<int> AddFeedAsync(FeedRecord feed)
    {
        await InitializeDatabaseAsync();

        await AcquireDatabaseLockAsync();
        try
        {
            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO feeds
                (name, title, description, directory, author, image_url, link,
                 language, category, file_extensions, youtube_playlist_url,
                 youtube_poll_interval_minutes, youtube_enabled,
                 youtube_max_concurrent_downloads, youtube_audio_quality,
                 created_at, updated_at, is_active)
                VALUES
                (@name, @title, @description, @directory, @author, @imageUrl, @link,
                 @language, @category, @fileExtensions, @youtubePlaylistUrl,
                 @youtubePollIntervalMinutes, @youtubeEnabled,
                 @youtubeMaxConcurrentDownloads, @youtubeAudioQuality,
                 @createdAt, @updatedAt, @isActive);
                SELECT last_insert_rowid();
            ";

            AddFeedParameters(command, feed);
            feed.CreatedAt = DateTime.UtcNow;
            feed.UpdatedAt = feed.CreatedAt;
            command.Parameters.AddWithValue("@createdAt", feed.CreatedAt.ToString("O"));
            command.Parameters.AddWithValue("@updatedAt", feed.UpdatedAt.ToString("O"));

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
        await InitializeDatabaseAsync();

        await AcquireDatabaseLockAsync();
        try
        {
            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();

            var command = connection.CreateCommand();
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
            feed.UpdatedAt = DateTime.UtcNow;
            command.Parameters.AddWithValue("@updatedAt", feed.UpdatedAt.ToString("O"));

            await command.ExecuteNonQueryAsync();
            _logger.LogInformation("Updated feed {FeedName} (ID: {FeedId})", feed.Name, feed.Id);
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task DeleteFeedAsync(int id)
    {
        await InitializeDatabaseAsync();

        await AcquireDatabaseLockAsync();
        try
        {
            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();

            // Delete in order due to foreign key constraints
            var commands = new[]
            {
                "DELETE FROM download_queue WHERE feed_id = @id",
                "DELETE FROM activity_log WHERE feed_id = @id",
                "DELETE FROM downloaded_videos WHERE feed_id = @id",
                "DELETE FROM episodes WHERE feed_id = @id",
                "DELETE FROM feeds WHERE id = @id"
            };

            foreach (var sql in commands)
            {
                var command = connection.CreateCommand();
                command.CommandText = sql;
                command.Parameters.AddWithValue("@id", id);
                await command.ExecuteNonQueryAsync();
            }

            _logger.LogInformation("Deleted feed with ID {FeedId}", id);
        }
        finally
        {
            _dbLock.Release();
        }
    }

    private static void AddFeedParameters(SqliteCommand command, FeedRecord feed)
    {
        command.Parameters.AddWithValue("@name", feed.Name);
        command.Parameters.AddWithValue("@title", feed.Title);
        command.Parameters.AddWithValue("@description", feed.Description);
        command.Parameters.AddWithValue("@directory", feed.Directory);
        command.Parameters.AddWithValue("@author", (object?)feed.Author ?? DBNull.Value);
        command.Parameters.AddWithValue("@imageUrl", (object?)feed.ImageUrl ?? DBNull.Value);
        command.Parameters.AddWithValue("@link", (object?)feed.Link ?? DBNull.Value);
        command.Parameters.AddWithValue("@language", (object?)feed.Language ?? DBNull.Value);
        command.Parameters.AddWithValue("@category", (object?)feed.Category ?? DBNull.Value);
        command.Parameters.AddWithValue("@fileExtensions", (object?)feed.FileExtensions ?? DBNull.Value);
        command.Parameters.AddWithValue("@youtubePlaylistUrl", (object?)feed.YouTubePlaylistUrl ?? DBNull.Value);
        command.Parameters.AddWithValue("@youtubePollIntervalMinutes", (object?)feed.YouTubePollIntervalMinutes ?? DBNull.Value);
        command.Parameters.AddWithValue("@youtubeEnabled", feed.YouTubeEnabled ? 1 : 0);
        command.Parameters.AddWithValue("@youtubeMaxConcurrentDownloads", (object?)feed.YouTubeMaxConcurrentDownloads ?? DBNull.Value);
        command.Parameters.AddWithValue("@youtubeAudioQuality", (object?)feed.YouTubeAudioQuality ?? DBNull.Value);
        command.Parameters.AddWithValue("@isActive", feed.IsActive ? 1 : 0);
    }

    #endregion

    #region Episode Management

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
            episodes.Add(ReadEpisodeFromReader(reader));
        }

        return episodes;
    }

    public async Task<EpisodeRecord?> GetEpisodeByIdAsync(int episodeId)
    {
        await InitializeDatabaseAsync();

        await using var connection = new SqliteConnection(GetConnectionString());
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT id, filename, video_id, youtube_title, description, thumbnail_url,
                   display_order, added_at, publish_date, match_score
            FROM episodes
            WHERE id = @id
        ";
        command.Parameters.AddWithValue("@id", episodeId);

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return ReadEpisodeFromReader(reader);
        }

        return null;
    }

    public async Task<EpisodeRecord?> GetEpisodeByFilenameAsync(int feedId, string filename)
    {
        await InitializeDatabaseAsync();

        await using var connection = new SqliteConnection(GetConnectionString());
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT id, filename, video_id, youtube_title, description, thumbnail_url,
                   display_order, added_at, publish_date, match_score
            FROM episodes
            WHERE feed_id = @feedId AND filename = @filename
        ";
        command.Parameters.AddWithValue("@feedId", feedId);
        command.Parameters.AddWithValue("@filename", filename);

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return ReadEpisodeFromReader(reader);
        }

        return null;
    }

    private static EpisodeRecord ReadEpisodeFromReader(SqliteDataReader reader)
    {
        return new EpisodeRecord
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
        };
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
                        (feed_id, filename, video_id, youtube_title, description, thumbnail_url, 
                         display_order, added_at, publish_date, match_score)
                        VALUES (@feedId, @filename, @videoId, @youtubeTitle, @description, 
                                @thumbnailUrl, @displayOrder, @addedAt, @publishDate, @matchScore)
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
                _logger.LogInformation("Added {Count} episodes to feed {FeedId}", episodeList.Count, feedId);
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
            // Get all files in directory
            var filesInDirectory = extensions
                .SelectMany(ext => Directory.GetFiles(directory, $"*{ext}"))
                .Select(Path.GetFileName)
                .Where(f => f != null)
                .Cast<string>()
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Get files already in database
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

            // Find new files
            var newFiles = filesInDirectory
                .Where(f => !existingFiles.Contains(f))
                .ToList();

            if (newFiles.Count == 0)
                return;

            // Get min display order for prepending
            var orderCommand = connection.CreateCommand();
            orderCommand.CommandText = "SELECT COALESCE(MIN(display_order), 1) FROM episodes WHERE feed_id = @feedId";
            orderCommand.Parameters.AddWithValue("@feedId", feedId);
            var minOrder = Convert.ToInt32(await orderCommand.ExecuteScalarAsync());

            // Add new files
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
                _logger.LogInformation("Synced {Count} new files from directory to feed {FeedId}",
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
        _logger.LogDebug("Starting playlist sync for feed {FeedId}", feedId);
        await InitializeDatabaseAsync();

        var videoList = videos.ToList();
        if (videoList.Count == 0)
        {
            _logger.LogWarning("No videos provided for playlist sync");
            return;
        }

        _logger.LogInformation("Syncing {Count} playlist videos to local files in {Directory}",
            videoList.Count, directory);

        // Get all files in directory
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

            // Get existing episodes
            _logger.LogDebug("Loading existing episodes from database");
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

            _logger.LogDebug("Loaded {Count} existing episodes from database", existingEpisodes.Count);

            // Track which files have been matched
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

                    // Find best matching file using fuzzy matching
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
                        // Update existing episode with YouTube info (only if video ID changed or we have new metadata)
                        var hasNewMetadata = video.Description != null || video.ThumbnailUrl != null || video.UploadDate.HasValue;

                        if (existing.VideoId != video.VideoId || hasNewMetadata)
                        {
                            _logger.LogDebug("Updating existing episode {Filename} with YouTube metadata", bestMatch);

                            // Only update fields that have new data (preserve existing metadata if new data is null)
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
                                // No new metadata, just update video_id and ordering
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

                            _logger.LogDebug("Updated episode {Filename} with video {VideoId}", bestMatch, video.VideoId);
                        }
                        else
                        {
                            _logger.LogTrace("Episode {Filename} already linked to video {VideoId}, skipping update",
                                bestMatch, existing.VideoId);
                        }
                    }
                    else
                    {
                        // Add new episode
                        _logger.LogDebug("Adding new episode {Filename} to database", bestMatch);

                        var insertCommand = connection.CreateCommand();
                        insertCommand.CommandText = @"
                            INSERT INTO episodes
                            (feed_id, filename, video_id, youtube_title, description, thumbnail_url, 
                             display_order, added_at, publish_date, match_score)
                            VALUES (@feedId, @filename, @videoId, @youtubeTitle, @description, 
                                    @thumbnailUrl, @displayOrder, @addedAt, @publishDate, @matchScore)
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
                        _logger.LogDebug("Added new episode {Filename}", bestMatch);
                    }
                }

                await transaction.CommitAsync();
                _logger.LogInformation(
                    "Playlist sync completed for feed {FeedId}: {Updated} updated, {Added} added, {Skipped} skipped, {Matched}/{Total} matched successfully",
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

    #endregion

    #region Download Tracking

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

            _logger.LogDebug("Marked video {VideoId} as downloaded for feed {FeedId}", videoId, feedId);
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task RemoveDownloadedVideoAsync(int feedId, string videoId)
    {
        await InitializeDatabaseAsync();

        await AcquireDatabaseLockAsync();
        try
        {
            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                DELETE FROM downloaded_videos
                WHERE feed_id = @feedId AND video_id = @videoId
            ";
            command.Parameters.AddWithValue("@feedId", feedId);
            command.Parameters.AddWithValue("@videoId", videoId);

            var rowsAffected = await command.ExecuteNonQueryAsync();

            _logger.LogInformation("Removed downloaded video {VideoId} from feed {FeedId} (rows affected: {Rows})",
                videoId, feedId, rowsAffected);
        }
        finally
        {
            _dbLock.Release();
        }
    }

    #endregion

    #region Activity Logging

    public async Task LogActivityAsync(int? feedId, string activityType, string message, string? details = null)
    {
        await InitializeDatabaseAsync();

        await AcquireDatabaseLockAsync();
        try
        {
            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO activity_log (feed_id, activity_type, message, details, created_at)
                VALUES (@feedId, @activityType, @message, @details, @createdAt)
            ";
            command.Parameters.AddWithValue("@feedId", (object?)feedId ?? DBNull.Value);
            command.Parameters.AddWithValue("@activityType", activityType);
            command.Parameters.AddWithValue("@message", message);
            command.Parameters.AddWithValue("@details", (object?)details ?? DBNull.Value);
            command.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("O"));

            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<List<ActivityLogRecord>> GetRecentActivityAsync(int? feedId = null, int count = 100)
    {
        await InitializeDatabaseAsync();

        var activities = new List<ActivityLogRecord>();

        await using var connection = new SqliteConnection(GetConnectionString());
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        if (feedId.HasValue)
        {
            command.CommandText = @"
                SELECT id, feed_id, activity_type, message, details, created_at
                FROM activity_log
                WHERE feed_id = @feedId
                ORDER BY created_at DESC
                LIMIT @count
            ";
            command.Parameters.AddWithValue("@feedId", feedId.Value);
        }
        else
        {
            command.CommandText = @"
                SELECT id, feed_id, activity_type, message, details, created_at
                FROM activity_log
                ORDER BY created_at DESC
                LIMIT @count
            ";
        }
        command.Parameters.AddWithValue("@count", count);

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
                CreatedAt = DateTime.Parse(reader.GetString(5))
            });
        }

        return activities;
    }

    #endregion

    #region Download Queue Management

    public async Task<DownloadQueueItem> AddToDownloadQueueAsync(int feedId, string videoId, string? videoTitle = null)
    {
        await InitializeDatabaseAsync();

        await AcquireDatabaseLockAsync();
        try
        {
            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT OR IGNORE INTO download_queue 
                (feed_id, video_id, video_title, status, progress_percent, queued_at)
                VALUES (@feedId, @videoId, @videoTitle, @status, 0, @queuedAt);
                SELECT id FROM download_queue WHERE feed_id = @feedId AND video_id = @videoId;
            ";
            command.Parameters.AddWithValue("@feedId", feedId);
            command.Parameters.AddWithValue("@videoId", videoId);
            command.Parameters.AddWithValue("@videoTitle", (object?)videoTitle ?? DBNull.Value);
            command.Parameters.AddWithValue("@status", "queued");
            command.Parameters.AddWithValue("@queuedAt", DateTime.UtcNow.ToString("O"));

            var id = Convert.ToInt32(await command.ExecuteScalarAsync());

            return new DownloadQueueItem
            {
                Id = id,
                FeedId = feedId,
                VideoId = videoId,
                VideoTitle = videoTitle,
                Status = "queued",
                ProgressPercent = 0,
                QueuedAt = DateTime.UtcNow
            };
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task UpdateDownloadProgressAsync(int queueItemId, string status, int progressPercent, string? errorMessage = null)
    {
        await InitializeDatabaseAsync();

        await AcquireDatabaseLockAsync();
        try
        {
            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE download_queue SET
                    status = @status,
                    progress_percent = @progressPercent,
                    error_message = @errorMessage,
                    started_at = COALESCE(started_at, @startedAt),
                    completed_at = CASE WHEN @status IN ('completed', 'failed') THEN @completedAt ELSE completed_at END
                WHERE id = @id
            ";
            command.Parameters.AddWithValue("@id", queueItemId);
            command.Parameters.AddWithValue("@status", status);
            command.Parameters.AddWithValue("@progressPercent", progressPercent);
            command.Parameters.AddWithValue("@errorMessage", (object?)errorMessage ?? DBNull.Value);
            command.Parameters.AddWithValue("@startedAt", DateTime.UtcNow.ToString("O"));
            command.Parameters.AddWithValue("@completedAt", DateTime.UtcNow.ToString("O"));

            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<List<DownloadQueueItem>> GetDownloadQueueAsync(int? feedId = null)
    {
        await InitializeDatabaseAsync();

        var items = new List<DownloadQueueItem>();

        await using var connection = new SqliteConnection(GetConnectionString());
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        if (feedId.HasValue)
        {
            command.CommandText = @"
                SELECT id, feed_id, video_id, video_title, status, progress_percent,
                       error_message, queued_at, started_at, completed_at
                FROM download_queue
                WHERE feed_id = @feedId
                ORDER BY queued_at DESC
            ";
            command.Parameters.AddWithValue("@feedId", feedId.Value);
        }
        else
        {
            command.CommandText = @"
                SELECT id, feed_id, video_id, video_title, status, progress_percent,
                       error_message, queued_at, started_at, completed_at
                FROM download_queue
                ORDER BY queued_at DESC
            ";
        }

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new DownloadQueueItem
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
            });
        }

        return items;
    }

    public async Task<DownloadQueueItem?> GetQueueItemAsync(int feedId, string videoId)
    {
        await InitializeDatabaseAsync();

        await using var connection = new SqliteConnection(GetConnectionString());
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT id, feed_id, video_id, video_title, status, progress_percent,
                   error_message, queued_at, started_at, completed_at
            FROM download_queue
            WHERE feed_id = @feedId AND video_id = @videoId
        ";
        command.Parameters.AddWithValue("@feedId", feedId);
        command.Parameters.AddWithValue("@videoId", videoId);

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
                QueuedAt = DateTime.Parse(reader.GetString(7)),
                StartedAt = reader.IsDBNull(8) ? null : DateTime.Parse(reader.GetString(8)),
                CompletedAt = reader.IsDBNull(9) ? null : DateTime.Parse(reader.GetString(9))
            };
        }

        return null;
    }

    public async Task RemoveFromDownloadQueueAsync(int queueItemId)
    {
        await InitializeDatabaseAsync();

        await AcquireDatabaseLockAsync();
        try
        {
            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM download_queue WHERE id = @id";
            command.Parameters.AddWithValue("@id", queueItemId);

            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            _dbLock.Release();
        }
    }

    #endregion

    #region Fuzzy Matching Helpers

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

    #endregion

    #region Migration

    /// <summary>
    /// Migrates data from per-feed SQLite databases and appsettings.json configuration
    /// into the central database.
    /// </summary>
    public async Task MigrateFromPerFeedDatabasesAsync(Dictionary<string, PodcastFeedConfig> feeds)
    {
        _logger.LogInformation("Starting migration from per-feed databases to central database");
        await InitializeDatabaseAsync();

        await AcquireDatabaseLockAsync();
        try
        {
            foreach (var (feedName, feedConfig) in feeds)
            {
                _logger.LogInformation("Migrating feed: {FeedName}", feedName);

                // Check if feed already exists in central database
                var existingFeed = await GetFeedByNameAsync(feedName);
                if (existingFeed != null)
                {
                    _logger.LogInformation("Feed {FeedName} already exists in central database, skipping", feedName);
                    continue;
                }

                // Create feed record from configuration
                var feedRecord = new FeedRecord
                {
                    Name = feedName,
                    Title = feedConfig.Title,
                    Description = feedConfig.Description,
                    Directory = feedConfig.Directory,
                    Author = feedConfig.Author,
                    ImageUrl = feedConfig.ImageUrl,
                    Link = feedConfig.Link,
                    Language = feedConfig.Language ?? "en-us",
                    Category = feedConfig.Category,
                    FileExtensions = feedConfig.FileExtensions != null 
                        ? string.Join(",", feedConfig.FileExtensions) 
                        : ".mp3",
                    YouTubePlaylistUrl = feedConfig.YouTube?.PlaylistUrl,
                    YouTubePollIntervalMinutes = feedConfig.YouTube?.PollIntervalMinutes,
                    YouTubeEnabled = feedConfig.YouTube?.Enabled ?? false,
                    YouTubeMaxConcurrentDownloads = feedConfig.YouTube?.MaxConcurrentDownloads,
                    YouTubeAudioQuality = feedConfig.YouTube?.AudioQuality,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                var feedId = await AddFeedAsync(feedRecord);
                _logger.LogInformation("Created feed {FeedName} with ID {FeedId}", feedName, feedId);

                // Migrate episodes from per-feed database if it exists
                var perFeedDbPath = feedConfig.DatabasePath 
                    ?? Path.Combine(feedConfig.Directory, "podcast.db");

                if (File.Exists(perFeedDbPath))
                {
                    _logger.LogInformation("Migrating episodes from per-feed database: {Path}", perFeedDbPath);
                    await MigrateEpisodesFromPerFeedDatabase(feedId, perFeedDbPath);
                    await MigrateDownloadedVideosFromPerFeedDatabase(feedId, perFeedDbPath);
                }
                else
                {
                    _logger.LogWarning("Per-feed database not found at {Path}, skipping episode migration", perFeedDbPath);
                }
            }

            _logger.LogInformation("Migration completed successfully");
        }
        finally
        {
            _dbLock.Release();
        }
    }

    private async Task MigrateEpisodesFromPerFeedDatabase(int feedId, string perFeedDbPath)
    {
        var connectionString = $"Data Source={perFeedDbPath}";
        
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT filename, video_id, youtube_title, description, thumbnail_url,
                   display_order, added_at, publish_date, match_score
            FROM episodes
            ORDER BY display_order ASC
        ";

        var episodes = new List<EpisodeRecord>();
        await using (var reader = await command.ExecuteReaderAsync())
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

        if (episodes.Count > 0)
        {
            _logger.LogInformation("Migrating {Count} episodes from per-feed database", episodes.Count);
            
            await using var centralConnection = new SqliteConnection(GetConnectionString());
            await centralConnection.OpenAsync();

            await using var transaction = await centralConnection.BeginTransactionAsync();
            try
            {
                foreach (var episode in episodes)
                {
                    var insertCommand = centralConnection.CreateCommand();
                    insertCommand.CommandText = @"
                        INSERT INTO episodes
                        (feed_id, filename, video_id, youtube_title, description, thumbnail_url,
                         display_order, added_at, publish_date, match_score)
                        VALUES (@feedId, @filename, @videoId, @youtubeTitle, @description,
                                @thumbnailUrl, @displayOrder, @addedAt, @publishDate, @matchScore)
                    ";
                    insertCommand.Parameters.AddWithValue("@feedId", feedId);
                    insertCommand.Parameters.AddWithValue("@filename", episode.Filename);
                    insertCommand.Parameters.AddWithValue("@videoId", (object?)episode.VideoId ?? DBNull.Value);
                    insertCommand.Parameters.AddWithValue("@youtubeTitle", (object?)episode.YoutubeTitle ?? DBNull.Value);
                    insertCommand.Parameters.AddWithValue("@description", (object?)episode.Description ?? DBNull.Value);
                    insertCommand.Parameters.AddWithValue("@thumbnailUrl", (object?)episode.ThumbnailUrl ?? DBNull.Value);
                    insertCommand.Parameters.AddWithValue("@displayOrder", episode.DisplayOrder);
                    insertCommand.Parameters.AddWithValue("@addedAt", episode.AddedAt.ToString("O"));
                    insertCommand.Parameters.AddWithValue("@publishDate", episode.PublishDate.HasValue ? episode.PublishDate.Value.ToString("O") : DBNull.Value);
                    insertCommand.Parameters.AddWithValue("@matchScore", (object?)episode.MatchScore ?? DBNull.Value);

                    await insertCommand.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
                _logger.LogInformation("Successfully migrated {Count} episodes", episodes.Count);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to migrate episodes");
                throw;
            }
        }
        else
        {
            _logger.LogInformation("No episodes found in per-feed database");
        }
    }

    private async Task MigrateDownloadedVideosFromPerFeedDatabase(int feedId, string perFeedDbPath)
    {
        var connectionString = $"Data Source={perFeedDbPath}";
        
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT video_id, filename, downloaded_at
            FROM downloaded_videos
        ";

        var downloadedVideos = new List<(string VideoId, string? Filename, DateTime DownloadedAt)>();
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                downloadedVideos.Add((
                    reader.GetString(0),
                    reader.IsDBNull(1) ? null : reader.GetString(1),
                    DateTime.Parse(reader.GetString(2))
                ));
            }
        }

        if (downloadedVideos.Count > 0)
        {
            _logger.LogInformation("Migrating {Count} downloaded videos from per-feed database", downloadedVideos.Count);
            
            await using var centralConnection = new SqliteConnection(GetConnectionString());
            await centralConnection.OpenAsync();

            await using var transaction = await centralConnection.BeginTransactionAsync();
            try
            {
                foreach (var (videoId, filename, downloadedAt) in downloadedVideos)
                {
                    var insertCommand = centralConnection.CreateCommand();
                    insertCommand.CommandText = @"
                        INSERT OR IGNORE INTO downloaded_videos
                        (feed_id, video_id, filename, downloaded_at)
                        VALUES (@feedId, @videoId, @filename, @downloadedAt)
                    ";
                    insertCommand.Parameters.AddWithValue("@feedId", feedId);
                    insertCommand.Parameters.AddWithValue("@videoId", videoId);
                    insertCommand.Parameters.AddWithValue("@filename", (object?)filename ?? DBNull.Value);
                    insertCommand.Parameters.AddWithValue("@downloadedAt", downloadedAt.ToString("O"));

                    await insertCommand.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
                _logger.LogInformation("Successfully migrated {Count} downloaded videos", downloadedVideos.Count);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to migrate downloaded videos");
                throw;
            }
        }
        else
        {
            _logger.LogInformation("No downloaded videos found in per-feed database");
        }
    }

    #endregion

    #region User Settings Management
    
    public async Task<UserSettings> GetUserSettingsAsync()
    {
        await AcquireDatabaseLockAsync();
        try
        {
            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT dark_mode, default_polling_interval_minutes, default_audio_quality,
                       default_language, default_file_extensions, default_category, updated_at
                FROM user_settings WHERE id = 1
            ";

            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new UserSettings
                {
                    Id = 1,
                    DarkMode = reader.GetInt32(0) == 1,
                    DefaultPollingIntervalMinutes = reader.GetInt32(1),
                    DefaultAudioQuality = reader.GetString(2),
                    DefaultLanguage = reader.GetString(3),
                    DefaultFileExtensions = reader.GetString(4),
                    DefaultCategory = reader.GetString(5),
                    UpdatedAt = DateTime.Parse(reader.GetString(6), null, System.Globalization.DateTimeStyles.RoundtripKind)
                };
            }
            
            // Return default settings if not found (should not happen due to INSERT OR IGNORE)
            return new UserSettings
            {
                Id = 1,
                DarkMode = true,
                DefaultPollingIntervalMinutes = 60,
                DefaultAudioQuality = "highest",
                DefaultLanguage = "en-us",
                DefaultFileExtensions = ".mp3",
                DefaultCategory = "Society & Culture",
                UpdatedAt = DateTime.UtcNow
            };
        }
        finally
        {
            _dbLock.Release();
        }
    }
    
    public async Task SaveUserSettingsAsync(UserSettings settings)
    {
        await AcquireDatabaseLockAsync();
        try
        {
            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE user_settings SET
                    dark_mode = @darkMode,
                    default_polling_interval_minutes = @pollingInterval,
                    default_audio_quality = @audioQuality,
                    default_language = @language,
                    default_file_extensions = @fileExtensions,
                    default_category = @category,
                    updated_at = @updatedAt
                WHERE id = 1
            ";
            
            command.Parameters.AddWithValue("@darkMode", settings.DarkMode ? 1 : 0);
            command.Parameters.AddWithValue("@pollingInterval", settings.DefaultPollingIntervalMinutes);
            command.Parameters.AddWithValue("@audioQuality", settings.DefaultAudioQuality);
            command.Parameters.AddWithValue("@language", settings.DefaultLanguage);
            command.Parameters.AddWithValue("@fileExtensions", settings.DefaultFileExtensions);
            command.Parameters.AddWithValue("@category", settings.DefaultCategory);
            command.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow.ToString("O"));

            await command.ExecuteNonQueryAsync();
            _logger.LogInformation("User settings saved successfully");
        }
        finally
        {
            _dbLock.Release();
        }
    }
    
    public async Task<long> GetDatabaseSizeAsync()
    {
        await AcquireDatabaseLockAsync();
        try
        {
            var fileInfo = new FileInfo(_databasePath);
            return fileInfo.Exists ? fileInfo.Length : 0;
        }
        finally
        {
            _dbLock.Release();
        }
    }
    
    public async Task ClearActivityLogAsync()
    {
        await AcquireDatabaseLockAsync();
        try
        {
            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM activity_log";
            
            var deletedCount = await command.ExecuteNonQueryAsync();
            _logger.LogInformation("Cleared {Count} activity log entries", deletedCount);
            
            // Vacuum database to reclaim space
            var vacuumCommand = connection.CreateCommand();
            vacuumCommand.CommandText = "VACUUM";
            await vacuumCommand.ExecuteNonQueryAsync();
        }
        finally
        {
            _dbLock.Release();
        }
    }
    
    #endregion

    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            return;

        _dbLock?.Dispose();
        GC.SuppressFinalize(this);
    }
}
