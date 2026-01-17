using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using Castr.Models;

namespace Castr.Services;

public interface IPodcastDatabaseService
{
    Task InitializeDatabaseAsync(string feedName);
    Task<List<EpisodeRecord>> GetEpisodesAsync(string feedName);
    Task<bool> IsVideoDownloadedAsync(string feedName, string videoId);
    Task AddEpisodeAsync(string feedName, EpisodeRecord episode);
    Task AddEpisodesAsync(string feedName, IEnumerable<EpisodeRecord> episodes);
    Task MarkVideoDownloadedAsync(string feedName, string videoId, string filename);
    Task SyncDirectoryAsync(string feedName, string directory, string[] extensions);
    Task<HashSet<string>> GetDownloadedVideoIdsAsync(string feedName);
    Task SyncPlaylistInfoAsync(string feedName, IEnumerable<PlaylistVideoInfo> videos, string directory);
    Task UpdateEpisodeAsync(string feedName, EpisodeRecord episode);
}

public class EpisodeRecord
{
    public int Id { get; set; }
    public required string Filename { get; set; }
    public string? VideoId { get; set; }
    public string? YoutubeTitle { get; set; }
    public string? Description { get; set; }
    public string? ThumbnailUrl { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime AddedAt { get; set; }
    public DateTime? PublishDate { get; set; }
    public double? MatchScore { get; set; }
}

public class PlaylistVideoInfo
{
    public required string VideoId { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public string? ThumbnailUrl { get; set; }
    public DateTime? UploadDate { get; set; }
    public int PlaylistIndex { get; set; }
}

public class PodcastDatabaseService : IPodcastDatabaseService
{
    private readonly IOptions<PodcastFeedsConfig> _config;
    private readonly ILogger<PodcastDatabaseService> _logger;
    private readonly SemaphoreSlim _dbLock = new(1, 1);
    private readonly Dictionary<string, bool> _initialized = new();

    public PodcastDatabaseService(
        IOptions<PodcastFeedsConfig> config,
        ILogger<PodcastDatabaseService> logger)
    {
        _config = config;
        _logger = logger;
    }

    private string GetDatabasePath(string feedName)
    {
        var feedConfig = _config.Value.Feeds[feedName];
        return feedConfig.DatabasePath
            ?? Path.Combine(feedConfig.Directory, "podcast.db");
    }

    private string GetConnectionString(string feedName)
    {
        return $"Data Source={GetDatabasePath(feedName)}";
    }

    private async Task MigrateColumnIfMissingAsync(SqliteConnection connection, string columnName, string columnType)
    {
        var checkCommand = connection.CreateCommand();
        checkCommand.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('episodes') WHERE name='{columnName}'";
        var hasColumn = Convert.ToInt32(await checkCommand.ExecuteScalarAsync()) > 0;
        if (!hasColumn)
        {
            var alterCommand = connection.CreateCommand();
            alterCommand.CommandText = $"ALTER TABLE episodes ADD COLUMN {columnName} {columnType}";
            await alterCommand.ExecuteNonQueryAsync();
            _logger.LogInformation("Migrated database: added {ColumnName} column", columnName);
        }
    }

    public async Task InitializeDatabaseAsync(string feedName)
    {
        if (_initialized.TryGetValue(feedName, out var isInit) && isInit)
        {
            _logger.LogTrace("Database for {FeedName} already initialized in this session", feedName);
            return;
        }

        await _dbLock.WaitAsync();
        try
        {
            if (_initialized.TryGetValue(feedName, out isInit) && isInit)
                return;

            var dbPath = GetDatabasePath(feedName);
            var dbExisted = File.Exists(dbPath);

            _logger.LogDebug("Database path for {FeedName}: {Path} (exists: {Exists})", feedName, dbPath, dbExisted);

            var dbDir = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dbDir) && !Directory.Exists(dbDir))
            {
                _logger.LogInformation("Creating directory for database: {Directory}", dbDir);
                Directory.CreateDirectory(dbDir);
            }

            _logger.LogDebug("Opening database connection for {FeedName}", feedName);
            await using var connection = new SqliteConnection(GetConnectionString(feedName));
            await connection.OpenAsync();

            _logger.LogDebug("Creating database schema if not exists");
            var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS episodes (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    filename TEXT NOT NULL UNIQUE,
                    video_id TEXT,
                    youtube_title TEXT,
                    description TEXT,
                    thumbnail_url TEXT,
                    display_order INTEGER NOT NULL,
                    added_at TEXT NOT NULL,
                    publish_date TEXT,
                    match_score REAL
                );

                CREATE INDEX IF NOT EXISTS idx_episodes_filename ON episodes(filename);
                CREATE INDEX IF NOT EXISTS idx_episodes_video_id ON episodes(video_id);
                CREATE INDEX IF NOT EXISTS idx_episodes_display_order ON episodes(display_order);

                CREATE TABLE IF NOT EXISTS downloaded_videos (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    video_id TEXT NOT NULL UNIQUE,
                    filename TEXT,
                    downloaded_at TEXT NOT NULL
                );

                CREATE INDEX IF NOT EXISTS idx_downloaded_video_id ON downloaded_videos(video_id);
            ";

            await command.ExecuteNonQueryAsync();

            // Migrations for existing databases
            _logger.LogDebug("Running database migrations");
            await MigrateColumnIfMissingAsync(connection, "publish_date", "TEXT");
            await MigrateColumnIfMissingAsync(connection, "description", "TEXT");
            await MigrateColumnIfMissingAsync(connection, "thumbnail_url", "TEXT");

            _initialized[feedName] = true;

            if (dbExisted)
            {
                _logger.LogInformation("Initialized existing database for feed {FeedName} at {Path}", feedName, dbPath);
            }
            else
            {
                _logger.LogInformation("Created and initialized new database for feed {FeedName} at {Path}", feedName, dbPath);
            }
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<List<EpisodeRecord>> GetEpisodesAsync(string feedName)
    {
        await InitializeDatabaseAsync(feedName);

        var episodes = new List<EpisodeRecord>();

        await using var connection = new SqliteConnection(GetConnectionString(feedName));
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT id, filename, video_id, youtube_title, description, thumbnail_url,
                   display_order, added_at, publish_date, match_score
            FROM episodes
            ORDER BY display_order ASC
        ";

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

    public async Task<bool> IsVideoDownloadedAsync(string feedName, string videoId)
    {
        await InitializeDatabaseAsync(feedName);

        await using var connection = new SqliteConnection(GetConnectionString(feedName));
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM downloaded_videos WHERE video_id = @videoId";
        command.Parameters.AddWithValue("@videoId", videoId);

        var count = Convert.ToInt32(await command.ExecuteScalarAsync());
        return count > 0;
    }

    public async Task<HashSet<string>> GetDownloadedVideoIdsAsync(string feedName)
    {
        await InitializeDatabaseAsync(feedName);

        var ids = new HashSet<string>();

        await using var connection = new SqliteConnection(GetConnectionString(feedName));
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT video_id FROM downloaded_videos";

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            ids.Add(reader.GetString(0));
        }

        return ids;
    }

    public async Task MarkVideoDownloadedAsync(string feedName, string videoId, string filename)
    {
        await InitializeDatabaseAsync(feedName);

        await _dbLock.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection(GetConnectionString(feedName));
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT OR IGNORE INTO downloaded_videos (video_id, filename, downloaded_at)
                VALUES (@videoId, @filename, @downloadedAt)
            ";
            command.Parameters.AddWithValue("@videoId", videoId);
            command.Parameters.AddWithValue("@filename", filename);
            command.Parameters.AddWithValue("@downloadedAt", DateTime.UtcNow.ToString("O"));

            await command.ExecuteNonQueryAsync();

            _logger.LogDebug("Marked video {VideoId} as downloaded for feed {FeedName}", videoId, feedName);
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task AddEpisodeAsync(string feedName, EpisodeRecord episode)
    {
        await AddEpisodesAsync(feedName, new[] { episode });
    }

    public async Task AddEpisodesAsync(string feedName, IEnumerable<EpisodeRecord> episodes)
    {
        await InitializeDatabaseAsync(feedName);

        var episodeList = episodes.ToList();
        if (episodeList.Count == 0) return;

        await _dbLock.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection(GetConnectionString(feedName));
            await connection.OpenAsync();

            // Get next display order (prepend to top, so use negative/lower numbers)
            var orderCommand = connection.CreateCommand();
            orderCommand.CommandText = "SELECT COALESCE(MIN(display_order), 1) FROM episodes";
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
                        (filename, video_id, youtube_title, description, thumbnail_url, display_order, added_at, publish_date, match_score)
                        VALUES (@filename, @videoId, @youtubeTitle, @description, @thumbnailUrl, @displayOrder, @addedAt, @publishDate, @matchScore)
                    ";
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
                _logger.LogInformation("Added {Count} episodes to database for feed {FeedName}", episodeList.Count, feedName);
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

    public async Task UpdateEpisodeAsync(string feedName, EpisodeRecord episode)
    {
        await InitializeDatabaseAsync(feedName);

        await _dbLock.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection(GetConnectionString(feedName));
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
                WHERE filename = @filename
            ";
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

    public async Task SyncDirectoryAsync(string feedName, string directory, string[] extensions)
    {
        if (!Directory.Exists(directory))
            return;

        await InitializeDatabaseAsync(feedName);

        await _dbLock.WaitAsync();
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
            await using var connection = new SqliteConnection(GetConnectionString(feedName));
            await connection.OpenAsync();

            var existingFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var selectCommand = connection.CreateCommand();
            selectCommand.CommandText = "SELECT filename FROM episodes";

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
            orderCommand.CommandText = "SELECT COALESCE(MIN(display_order), 1) FROM episodes";
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
                        INSERT INTO episodes (filename, display_order, added_at)
                        VALUES (@filename, @displayOrder, @addedAt)
                    ";
                    command.Parameters.AddWithValue("@filename", filename);
                    command.Parameters.AddWithValue("@displayOrder", minOrder);
                    command.Parameters.AddWithValue("@addedAt", DateTime.UtcNow.ToString("O"));

                    await command.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
                _logger.LogInformation("Synced {Count} new files from directory to database for feed {FeedName}",
                    newFiles.Count, feedName);
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

    public async Task SyncPlaylistInfoAsync(string feedName, IEnumerable<PlaylistVideoInfo> videos, string directory)
    {
        _logger.LogDebug("Starting playlist sync for feed {FeedName}", feedName);
        await InitializeDatabaseAsync(feedName);

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

        await _dbLock.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection(GetConnectionString(feedName));
            await connection.OpenAsync();

            // Get existing episodes
            _logger.LogDebug("Loading existing episodes from database");
            var existingEpisodes = new Dictionary<string, (int Id, string? VideoId)>(StringComparer.OrdinalIgnoreCase);
            var selectCommand = connection.CreateCommand();
            selectCommand.CommandText = "SELECT id, filename, video_id FROM episodes";

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
                            (filename, video_id, youtube_title, description, thumbnail_url, display_order, added_at, publish_date, match_score)
                            VALUES (@filename, @videoId, @youtubeTitle, @description, @thumbnailUrl, @displayOrder, @addedAt, @publishDate, @matchScore)
                        ";
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
                    "Playlist sync completed for {FeedName}: {Updated} updated, {Added} added, {Skipped} skipped, {Matched}/{Total} matched successfully",
                    feedName, updatedCount, addedCount, skippedCount, matchedFiles.Count, videoList.Count);
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

        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+", " ");

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
}
