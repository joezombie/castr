using System.Text.RegularExpressions;
using Castr.Data.Entities;
using Castr.Data.Repositories;
using Castr.Models;
using Microsoft.Extensions.Logging;

namespace Castr.Services;

/// <summary>
/// High-level data service that provides business logic on top of EF Core repositories.
/// Replaces legacy PodcastDatabaseService and CentralDatabaseService.
/// </summary>
public partial class PodcastDataService : IPodcastDataService
{
    private readonly IFeedRepository _feedRepository;
    private readonly IEpisodeRepository _episodeRepository;
    private readonly IDownloadRepository _downloadRepository;
    private readonly IActivityRepository _activityRepository;
    private readonly ILogger<PodcastDataService> _logger;

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    public PodcastDataService(
        IFeedRepository feedRepository,
        IEpisodeRepository episodeRepository,
        IDownloadRepository downloadRepository,
        IActivityRepository activityRepository,
        ILogger<PodcastDataService> logger)
    {
        _feedRepository = feedRepository;
        _episodeRepository = episodeRepository;
        _downloadRepository = downloadRepository;
        _activityRepository = activityRepository;
        _logger = logger;
    }

    #region Feed Operations

    public Task<List<Feed>> GetAllFeedsAsync()
        => _feedRepository.GetAllAsync();

    public Task<Feed?> GetFeedByNameAsync(string name)
        => _feedRepository.GetByNameAsync(name);

    public Task<Feed?> GetFeedByIdAsync(int id)
        => _feedRepository.GetByIdAsync(id);

    public Task<int> AddFeedAsync(Feed feed)
        => _feedRepository.AddAsync(feed);

    public Task UpdateFeedAsync(Feed feed)
        => _feedRepository.UpdateAsync(feed);

    public Task DeleteFeedAsync(int id)
        => _feedRepository.DeleteAsync(id);

    #endregion

    #region Episode Operations

    public Task<List<Episode>> GetEpisodesAsync(int feedId)
        => _episodeRepository.GetByFeedIdAsync(feedId);

    public Task<Episode?> GetEpisodeByIdAsync(int id)
        => _episodeRepository.GetByIdAsync(id);

    public Task<Episode?> GetEpisodeByFilenameAsync(int feedId, string filename)
        => _episodeRepository.GetByFilenameAsync(feedId, filename);

    public Task<Episode?> GetEpisodeByVideoIdAsync(int feedId, string videoId)
        => _episodeRepository.GetByVideoIdAsync(feedId, videoId);

    public Task AddEpisodeAsync(Episode episode)
        => _episodeRepository.AddAsync(episode);

    public Task AddEpisodesAsync(IEnumerable<Episode> episodes)
        => _episodeRepository.AddRangeAsync(episodes);

    public Task UpdateEpisodeAsync(Episode episode)
        => _episodeRepository.UpdateAsync(episode);

    public Task DeleteEpisodeAsync(int id)
        => _episodeRepository.DeleteAsync(id);

    /// <summary>
    /// Scans a directory for files with matching extensions and adds any new files to the database.
    /// New files are added with DisplayOrder values below the current minimum (prepended to existing order).
    /// </summary>
    public async Task SyncDirectoryAsync(int feedId, string directory, string[] extensions)
    {
        if (!Directory.Exists(directory))
        {
            _logger.LogWarning("Directory does not exist for sync: {Directory}", directory);
            return;
        }

        // Get all files in directory with matching extensions
        var filesInDirectory = extensions
            .SelectMany(ext => Directory.GetFiles(directory, $"*{ext}"))
            .Select(Path.GetFileName)
            .Where(f => f != null)
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Get existing episodes to determine which files are new
        var existingEpisodes = await _episodeRepository.GetByFeedIdAsync(feedId);
        var existingFilenames = existingEpisodes
            .Select(e => e.Filename)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Find new files not already in database
        var newFiles = filesInDirectory
            .Where(f => !existingFilenames.Contains(f))
            .OrderBy(f => f)
            .ToList();

        if (newFiles.Count == 0)
        {
            _logger.LogDebug("No new files to sync for feed {FeedId}", feedId);
            return;
        }

        // Calculate starting display order (prepend to top, so use negative/lower numbers)
        var minOrder = existingEpisodes.Any()
            ? existingEpisodes.Min(e => e.DisplayOrder)
            : 1;

        // Create episodes for new files
        var newEpisodes = new List<Episode>();
        foreach (var filename in newFiles)
        {
            minOrder--;
            newEpisodes.Add(new Episode
            {
                FeedId = feedId,
                Filename = filename,
                DisplayOrder = minOrder,
                AddedAt = DateTime.UtcNow
            });
        }

        await _episodeRepository.AddRangeAsync(newEpisodes);
        _logger.LogInformation("Synced {Count} new files from directory to database for feed {FeedId}",
            newFiles.Count, feedId);
    }

    /// <summary>
    /// Syncs YouTube playlist information to local episodes using fuzzy matching.
    /// Updates episodes with VideoId, YoutubeTitle, Description, ThumbnailUrl, and PublishDate.
    /// Also marks matched videos as downloaded in the DownloadedVideo table to prevent re-downloading.
    /// </summary>
    public async Task SyncPlaylistInfoAsync(int feedId, IEnumerable<PlaylistVideoInfo> videos, string directory)
    {
        var videoList = videos.ToList();
        if (videoList.Count == 0)
        {
            _logger.LogWarning("No videos provided for playlist sync");
            return;
        }

        _logger.LogInformation("Syncing {Count} playlist videos to local files in {Directory}",
            videoList.Count, directory);

        // Get all MP3 files in directory for fuzzy matching
        // Note: only matches .mp3 files; other extensions configured on the feed are not considered here
        var filesInDirectory = Directory.Exists(directory)
            ? Directory.GetFiles(directory, "*.mp3")
                .Select(Path.GetFileName)
                .Where(f => f != null)
                .Cast<string>()
                .ToList()
            : new List<string>();

        if (filesInDirectory.Count == 0)
        {
            _logger.LogWarning("No MP3 files found in directory for playlist sync: {Directory}", directory);
            return;
        }

        _logger.LogInformation("Found {Count} MP3 files in directory to match against", filesInDirectory.Count);

        // Get existing episodes
        var existingEpisodes = await _episodeRepository.GetByFeedIdAsync(feedId);
        var episodesByFilename = existingEpisodes.ToDictionary(
            e => e.Filename,
            e => e,
            StringComparer.OrdinalIgnoreCase);

        // Track which files have been matched
        var matchedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var updatedCount = 0;
        var addedCount = 0;
        var skippedCount = 0;
        var failedCount = 0;

        foreach (var video in videoList)
        {
            try
            {
                _logger.LogDebug("Matching video: '{Title}' (ID: {VideoId}, PlaylistIndex: {PlaylistIndex})",
                    video.Title, video.VideoId, video.PlaylistIndex);

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

                if (episodesByFilename.TryGetValue(bestMatch, out var existingEpisode))
                {
                    // Update existing episode with YouTube info
                    var hasNewMetadata = video.Description != null || video.ThumbnailUrl != null || video.UploadDate.HasValue;

                    if (existingEpisode.VideoId != video.VideoId || hasNewMetadata)
                    {
                        existingEpisode.VideoId = video.VideoId;
                        existingEpisode.YoutubeTitle = video.Title;
                        existingEpisode.DisplayOrder = video.PlaylistIndex;
                        existingEpisode.MatchScore = bestScore;

                        // Only update metadata fields if we have new data (preserve existing if new is null)
                        if (video.Description != null)
                            existingEpisode.Description = video.Description;
                        if (video.ThumbnailUrl != null)
                            existingEpisode.ThumbnailUrl = video.ThumbnailUrl;
                        if (video.UploadDate.HasValue)
                            existingEpisode.PublishDate = video.UploadDate;

                        await _episodeRepository.UpdateAsync(existingEpisode);
                        updatedCount++;
                        _logger.LogDebug("Updated episode {Filename} with video {VideoId}", bestMatch, video.VideoId);
                    }

                    // Always ensure matched videos are tracked as downloaded (idempotent upsert)
                    await _downloadRepository.MarkVideoDownloadedAsync(feedId, video.VideoId, bestMatch);
                }
                else
                {
                    // Add new episode
                    var newEpisode = new Episode
                    {
                        FeedId = feedId,
                        Filename = bestMatch,
                        VideoId = video.VideoId,
                        YoutubeTitle = video.Title,
                        Description = video.Description,
                        ThumbnailUrl = video.ThumbnailUrl,
                        DisplayOrder = video.PlaylistIndex,
                        AddedAt = DateTime.UtcNow,
                        PublishDate = video.UploadDate,
                        MatchScore = bestScore
                    };

                    await _episodeRepository.AddAsync(newEpisode);
                    await _downloadRepository.MarkVideoDownloadedAsync(feedId, video.VideoId, bestMatch);
                    episodesByFilename[bestMatch] = newEpisode;
                    addedCount++;
                    _logger.LogDebug("Added new episode {Filename}", bestMatch);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex,
                    "Failed to sync video '{VideoId}' ('{Title}') for feed {FeedId}. Continuing with remaining videos.",
                    video.VideoId, video.Title, feedId);
                failedCount++;
            }
        }

        _logger.LogInformation(
            "Playlist sync completed for feed {FeedId}: {Updated} updated, {Added} added, {Skipped} skipped, {Failed} failed, {Matched}/{Total} matched and marked as downloaded",
            feedId, updatedCount, addedCount, skippedCount, failedCount, matchedFiles.Count, videoList.Count);

        if (failedCount > 0)
        {
            try
            {
                await _activityRepository.LogAsync(feedId, "sync_warning",
                    $"Playlist sync completed with {failedCount} failed video(s)",
                    $"Updated: {updatedCount}, Added: {addedCount}, Skipped: {skippedCount}, Failed: {failedCount}, Matched: {matchedFiles.Count}/{videoList.Count}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write activity log for sync warning (feed {FeedId})", feedId);
            }
        }
    }

    #endregion

    #region Download Tracking

    public Task<bool> IsVideoDownloadedAsync(int feedId, string videoId)
        => _downloadRepository.IsVideoDownloadedAsync(feedId, videoId);

    public Task<HashSet<string>> GetDownloadedVideoIdsAsync(int feedId)
        => _downloadRepository.GetDownloadedVideoIdsAsync(feedId);

    public Task MarkVideoDownloadedAsync(int feedId, string videoId, string? filename)
        => _downloadRepository.MarkVideoDownloadedAsync(feedId, videoId, filename);

    public Task RemoveDownloadedVideoAsync(int feedId, string videoId)
        => _downloadRepository.RemoveDownloadedVideoAsync(feedId, videoId);

    #endregion

    #region Download Queue

    public Task<Data.Entities.DownloadQueueItem> AddToQueueAsync(int feedId, string videoId, string? title)
        => _downloadRepository.AddToQueueAsync(feedId, videoId, title);

    public Task UpdateQueueProgressAsync(int queueItemId, string status, int progressPercent, string? errorMessage)
        => _downloadRepository.UpdateQueueProgressAsync(queueItemId, status, progressPercent, errorMessage);

    public Task<List<Data.Entities.DownloadQueueItem>> GetQueueAsync(int? feedId = null)
        => _downloadRepository.GetQueueAsync(feedId);

    public Task<Data.Entities.DownloadQueueItem?> GetQueueItemAsync(int feedId, string videoId)
        => _downloadRepository.GetQueueItemAsync(feedId, videoId);

    public Task RemoveFromQueueAsync(int queueItemId)
        => _downloadRepository.RemoveFromQueueAsync(queueItemId);

    #endregion

    #region Activity Logging

    public Task LogActivityAsync(int? feedId, string activityType, string message, string? details = null)
        => _activityRepository.LogAsync(feedId, activityType, message, details);

    public Task<List<ActivityLog>> GetRecentActivityAsync(int? feedId = null, int count = 100)
        => _activityRepository.GetRecentAsync(feedId, count);

    public Task ClearActivityLogAsync()
        => _activityRepository.ClearAsync();

    #endregion

    #region Fuzzy Matching

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
            .ToLowerInvariant()
            .Trim();

        // Remove trailing YouTube channel name suffix (e.g., " | channel name")
        var pipeIdx = normalized.LastIndexOf('|');
        if (pipeIdx > 0)
            normalized = normalized[..pipeIdx].TrimEnd();

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
}
