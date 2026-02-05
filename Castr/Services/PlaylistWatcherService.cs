using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Castr.Models;
using Castr.Data.Entities;

namespace Castr.Services;

public class PlaylistWatcherService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptions<PodcastFeedsConfig> _config;
    private readonly ILogger<PlaylistWatcherService> _logger;
    private readonly ConcurrentDictionary<string, DateTime> _lastPollTimes = new();

    public PlaylistWatcherService(
        IServiceProvider serviceProvider,
        IOptions<PodcastFeedsConfig> config,
        ILogger<PlaylistWatcherService> logger)
    {
        _serviceProvider = serviceProvider;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Playlist Watcher Service starting");
        _logger.LogDebug("Initial delay: 10 seconds before first poll");

        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        _logger.LogInformation("Starting playlist polling loop (checking every 1 minute)");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogDebug("Starting poll cycle");
                await PollAllFeedsAsync(stoppingToken);
                _logger.LogDebug("Poll cycle completed");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error in playlist watcher main loop");
            }

            _logger.LogDebug("Waiting 1 minute before next poll cycle");
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }

        _logger.LogInformation("Playlist Watcher Service stopping");
    }

    private async Task PollAllFeedsAsync(CancellationToken stoppingToken)
    {
        var feedsToProcess = GetFeedsDueForPolling().ToList();

        if (feedsToProcess.Count == 0)
        {
            _logger.LogDebug("No feeds due for polling this cycle");
            return;
        }

        _logger.LogInformation("Processing {Count} feed(s) due for polling: {Feeds}",
            feedsToProcess.Count,
            string.Join(", ", feedsToProcess.Select(f => f.Name)));

        foreach (var (feedName, feedConfig) in feedsToProcess)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning("Cancellation requested, stopping feed processing");
                break;
            }

            try
            {
                _logger.LogInformation("Processing feed: {FeedName}", feedName);
                var processingStart = DateTime.UtcNow;

                await ProcessFeedAsync(feedName, feedConfig, stoppingToken);

                var elapsed = DateTime.UtcNow - processingStart;
                _lastPollTimes[feedName] = DateTime.UtcNow;

                _logger.LogInformation("Completed processing feed {FeedName} in {ElapsedSec:N1}s",
                    feedName, elapsed.TotalSeconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error processing feed {FeedName}, will retry next interval",
                    feedName);
            }
        }
    }

    private IEnumerable<(string Name, PodcastFeedConfig Config)> GetFeedsDueForPolling()
    {
        var now = DateTime.UtcNow;
        _logger.LogTrace("Checking which feeds are due for polling at {Time}", now);

        foreach (var (feedName, feedConfig) in _config.Value.Feeds)
        {
            if (feedConfig.YouTube == null || !feedConfig.YouTube.Enabled)
            {
                _logger.LogTrace("Skipping feed {FeedName}: YouTube config not enabled", feedName);
                continue;
            }

            var interval = TimeSpan.FromMinutes(feedConfig.YouTube.PollIntervalMinutes);

            if (!_lastPollTimes.TryGetValue(feedName, out var lastPoll))
            {
                _logger.LogDebug("Feed {FeedName} has never been polled, adding to queue", feedName);
                yield return (feedName, feedConfig);
            }
            else
            {
                var timeSinceLastPoll = now - lastPoll;
                if (timeSinceLastPoll >= interval)
                {
                    _logger.LogDebug("Feed {FeedName} due for polling (last poll: {LastPoll}, interval: {Interval}min)",
                        feedName, lastPoll, feedConfig.YouTube.PollIntervalMinutes);
                    yield return (feedName, feedConfig);
                }
                else
                {
                    var timeUntilNext = interval - timeSinceLastPoll;
                    _logger.LogTrace("Feed {FeedName} not due yet (next poll in {NextPoll}min)",
                        feedName, timeUntilNext.TotalMinutes);
                }
            }
        }
    }

    private async Task ProcessFeedAsync(
        string feedName,
        PodcastFeedConfig feedConfig,
        CancellationToken stoppingToken)
    {
        _logger.LogInformation("Checking playlist for feed: {FeedName}", feedName);
        _logger.LogDebug("Playlist URL: {PlaylistUrl}", feedConfig.YouTube!.PlaylistUrl);

        using var scope = _serviceProvider.CreateScope();
        var youtubeService = scope.ServiceProvider.GetRequiredService<IYouTubeDownloadService>();
        var dataService = scope.ServiceProvider.GetRequiredService<IPodcastDataService>();

        // Look up feed by name to get feedId
        var feed = await dataService.GetFeedByNameAsync(feedName);
        if (feed == null)
        {
            _logger.LogWarning("Feed {FeedName} not found in database, skipping", feedName);
            return;
        }
        var feedId = feed.Id;

        var youtubeConfig = feedConfig.YouTube!;

        // Step 1: Fetch playlist videos
        _logger.LogDebug("Fetching playlist videos for {FeedName}", feedName);
        var videos = await youtubeService.GetPlaylistVideosAsync(
            youtubeConfig.PlaylistUrl,
            stoppingToken);

        _logger.LogInformation(
            "Found {Count} videos in playlist for {FeedName}",
            videos.Count,
            feedName);

        // Step 2: Check which videos already have files and fetch details only for new ones
        _logger.LogDebug("Checking which videos already have files on disk to optimize metadata fetch");
        var downloadedIds = await dataService.GetDownloadedVideoIdsAsync(feedId);
        _logger.LogDebug("Found {Count} already downloaded videos in database", downloadedIds.Count);

        _logger.LogInformation("Fetching detailed metadata for videos (skipping {SkipCount} already downloaded)",
            downloadedIds.Count);
        var playlistInfos = new List<PlaylistVideoInfo>();
        var detailsFetchStart = DateTime.UtcNow;
        var skippedCount = 0;
        var fetchedCount = 0;

        foreach (var (v, index) in videos.Select((v, i) => (v, i)))
        {
            if (stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning("Cancellation requested while fetching video details");
                break;
            }

            // Check if file exists on disk before fetching detailed metadata
            var existingPath = youtubeService.GetExistingFilePath(v.Title, feedConfig.Directory);
            var isDownloaded = downloadedIds.Contains(v.Id.Value) || existingPath != null;

            if (isDownloaded)
            {
                // Skip fetching details for videos that are already downloaded with existing files
                _logger.LogTrace("Skipping metadata fetch for video {Index}/{Total}: '{Title}' (file exists)",
                    index + 1, videos.Count, v.Title);

                playlistInfos.Add(new PlaylistVideoInfo
                {
                    VideoId = v.Id.Value,
                    Title = v.Title,
                    Description = null, // Will be preserved from existing DB record
                    ThumbnailUrl = null, // Will be preserved from existing DB record
                    UploadDate = null, // Will be preserved from existing DB record
                    PlaylistIndex = index + 1
                });
                skippedCount++;
            }
            else
            {
                // Fetch full details for new videos
                _logger.LogDebug("Fetching details for video {Index}/{Total}: {VideoId} - '{Title}'",
                    index + 1, videos.Count, v.Id.Value, v.Title);

                var details = await youtubeService.GetVideoDetailsAsync(v.Id.Value, stoppingToken);
                playlistInfos.Add(new PlaylistVideoInfo
                {
                    VideoId = v.Id.Value,
                    Title = v.Title,
                    Description = details?.Description,
                    ThumbnailUrl = details?.ThumbnailUrl,
                    UploadDate = details?.UploadDate,
                    PlaylistIndex = index + 1
                });
                fetchedCount++;
            }
        }

        var detailsFetchElapsed = DateTime.UtcNow - detailsFetchStart;
        _logger.LogInformation("Fetched details for {Fetched} videos, skipped {Skipped} existing videos in {ElapsedSec:N1}s",
            fetchedCount, skippedCount, detailsFetchElapsed.TotalSeconds);

        // Step 3: Sync playlist info with database using fuzzy matching
        _logger.LogInformation("Syncing {Count} playlist videos to database with fuzzy matching", playlistInfos.Count);
        await dataService.SyncPlaylistInfoAsync(feedId, playlistInfos, feedConfig.Directory);

        // Step 4: Check for new videos to download
        _logger.LogDebug("Re-checking downloaded video list after sync");
        downloadedIds = await dataService.GetDownloadedVideoIdsAsync(feedId);
        _logger.LogDebug("Found {Count} downloaded videos in database after sync", downloadedIds.Count);

        var newVideos = videos
            .Where(v => !downloadedIds.Contains(v.Id.Value))
            .Reverse() // Download oldest first (playlists are typically newest-first)
            .ToList();

        if (newVideos.Count == 0)
        {
            _logger.LogInformation("No new videos to download for feed {FeedName}", feedName);
            _logger.LogDebug("Syncing directory to catch any manually added files");
            await dataService.SyncDirectoryAsync(feedId, feedConfig.Directory,
                feedConfig.FileExtensions ?? [".mp3"]);
            return;
        }

        _logger.LogInformation(
            "Found {Count} new videos to download for {FeedName} (downloading oldest first)",
            newVideos.Count,
            feedName);

        _logger.LogDebug("New videos: {VideoIds}",
            string.Join(", ", newVideos.Select(v => $"{v.Id.Value} ({v.Title})")));

        var semaphore = new SemaphoreSlim(youtubeConfig.MaxConcurrentDownloads);
        var newEpisodes = new ConcurrentBag<Episode>();

        _logger.LogDebug("Starting downloads with max concurrency: {MaxConcurrent}",
            youtubeConfig.MaxConcurrentDownloads);

        var processedCount = 0;
        foreach (var video in newVideos)
        {
            processedCount++;
            if (stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning("Cancellation requested, stopping downloads at {Processed}/{Total}",
                    processedCount - 1, newVideos.Count);
                break;
            }

            _logger.LogInformation("Processing video {Index}/{Total}: '{Title}' (ID: {VideoId})",
                processedCount, newVideos.Count, video.Title, video.Id.Value);

            // Check if file already exists on disk before downloading
            var existingPath = youtubeService.GetExistingFilePath(video.Title, feedConfig.Directory);
            if (existingPath != null)
            {
                _logger.LogInformation(
                    "File already exists for '{Title}', marking as downloaded: {Path}",
                    video.Title,
                    existingPath);

                await dataService.MarkVideoDownloadedAsync(feedId, video.Id.Value, Path.GetFileName(existingPath));

                // Get video details for existing file
                _logger.LogDebug("Fetching metadata for existing file");
                var details = await youtubeService.GetVideoDetailsAsync(video.Id.Value, stoppingToken);

                newEpisodes.Add(new Episode
                {
                    FeedId = feedId,
                    Filename = Path.GetFileName(existingPath),
                    VideoId = video.Id.Value,
                    YoutubeTitle = video.Title,
                    Description = details?.Description,
                    ThumbnailUrl = details?.ThumbnailUrl,
                    PublishDate = details?.UploadDate,
                    DisplayOrder = 0,
                    AddedAt = DateTime.UtcNow
                });
                continue;
            }

            _logger.LogDebug("Waiting for download slot (max concurrent: {Max})", youtubeConfig.MaxConcurrentDownloads);
            await semaphore.WaitAsync(stoppingToken);
            try
            {
                // Get video details before downloading
                _logger.LogDebug("Fetching video metadata before download");
                var details = await youtubeService.GetVideoDetailsAsync(video.Id.Value, stoppingToken);

                _logger.LogInformation("Downloading video '{Title}'", video.Title);
                var downloadStart = DateTime.UtcNow;

                var outputPath = await youtubeService.DownloadAudioAsync(
                    video.Id.Value,
                    feedConfig.Directory,
                    youtubeConfig.AudioQuality,
                    stoppingToken);

                if (outputPath != null)
                {
                    var downloadElapsed = DateTime.UtcNow - downloadStart;
                    var filename = Path.GetFileName(outputPath);

                    _logger.LogInformation("Download completed in {ElapsedSec:N1}s: {Filename}",
                        downloadElapsed.TotalSeconds, filename);

                    await dataService.MarkVideoDownloadedAsync(feedId, video.Id.Value, filename);
                    newEpisodes.Add(new Episode
                    {
                        FeedId = feedId,
                        Filename = filename,
                        VideoId = video.Id.Value,
                        YoutubeTitle = video.Title,
                        Description = details?.Description,
                        ThumbnailUrl = details?.ThumbnailUrl,
                        PublishDate = details?.UploadDate,
                        DisplayOrder = 0,
                        AddedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    _logger.LogWarning("Download failed for '{Title}' (ID: {VideoId})", video.Title, video.Id.Value);
                }

                // Rate limiting delay between downloads
                _logger.LogDebug("Rate limiting: waiting 5 seconds before next download");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            finally
            {
                semaphore.Release();
            }
        }

        // Step 5: Add all new episodes to database
        if (newEpisodes.Count > 0)
        {
            _logger.LogInformation("Adding {Count} new episodes to database", newEpisodes.Count);
            await dataService.AddEpisodesAsync(newEpisodes);
            _logger.LogDebug("Episodes added to database successfully");
        }
        else
        {
            _logger.LogDebug("No new episodes to add to database");
        }

        // Step 6: Sync any files in directory that aren't in the database
        _logger.LogDebug("Performing final directory sync to catch any manually added files");
        await dataService.SyncDirectoryAsync(feedId, feedConfig.Directory,
            feedConfig.FileExtensions ?? [".mp3"]);

        _logger.LogInformation(
            "Completed processing {FeedName}: {ProcessedCount} videos processed, {NewCount} new episodes added",
            feedName,
            processedCount,
            newEpisodes.Count);
    }
}
