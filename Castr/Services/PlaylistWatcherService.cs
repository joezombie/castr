using System.Collections.Concurrent;
using Castr.Data.Entities;
using Castr.Models;

namespace Castr.Services;

public class PlaylistWatcherService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PlaylistWatcherService> _logger;
    private readonly IPlaylistWatcherTrigger _trigger;
    private readonly ConcurrentDictionary<string, DateTime> _lastPollTimes = new();
    // Feeds that requested a metadata-enriching run (e.g. Clear & Resync). The flag is consumed
    // once when the feed is next processed, so subsequent interval polls stay fast.
    private readonly ConcurrentDictionary<string, byte> _pendingEnrichFeeds = new();

    public PlaylistWatcherService(
        IServiceProvider serviceProvider,
        ILogger<PlaylistWatcherService> logger,
        IPlaylistWatcherTrigger trigger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _trigger = trigger;
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

            // Wait up to 1 minute, but wake up early if a feed is triggered
            _logger.LogDebug("Waiting 1 minute before next poll cycle (or until triggered)");
            using var delayCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var waitTask = WaitForTriggerAsync(delayCts);
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), delayCts.Token);
            }
            catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogDebug("Poll cycle interrupted by trigger");
            }
            finally
            {
                // Always clean up WaitForTriggerAsync before delayCts is disposed
                await delayCts.CancelAsync();
                await waitTask;
            }
        }

        _logger.LogInformation("Playlist Watcher Service stopping");
    }

    private async Task WaitForTriggerAsync(CancellationTokenSource delayCts)
    {
        try
        {
            await foreach (var trigger in _trigger.ReadTriggersAsync(delayCts.Token))
            {
                var feedName = trigger.FeedName;
                _logger.LogInformation(
                    "Received immediate processing trigger for feed: {FeedName} (enrichMetadata: {Enrich})",
                    feedName, trigger.EnrichMetadata);
                if (trigger.EnrichMetadata)
                {
                    _pendingEnrichFeeds[feedName] = 0;
                }
                // Clear last poll time so the feed is picked up in the next cycle
                _lastPollTimes.TryRemove(feedName, out _);
                // Cancel the delay to process immediately
                await delayCts.CancelAsync();
                return;
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when delay completes normally or app shuts down
        }
    }

    private async Task PollAllFeedsAsync(CancellationToken stoppingToken)
    {
        // Sync directories for all active feeds to discover new files
        try
        {
            await SyncAllFeedDirectoriesAsync();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to sync feed directories. Continuing with feed polling.");
        }

        var feedsToProcess = await GetFeedsDueForPollingAsync();

        if (feedsToProcess.Count == 0)
        {
            _logger.LogDebug("No YouTube feeds due for polling this cycle");
            return;
        }

        _logger.LogInformation("Processing {Count} feed(s) due for polling: {Feeds}",
            feedsToProcess.Count,
            string.Join(", ", feedsToProcess.Select(f => f.Name)));

        foreach (var feed in feedsToProcess)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning("Cancellation requested, stopping feed processing");
                break;
            }

            try
            {
                // Consume any pending enrich request for this feed (so only this run enriches).
                var enrichMetadata = _pendingEnrichFeeds.TryRemove(feed.Name, out _);

                _logger.LogInformation("Processing feed: {FeedName} (enrichMetadata: {Enrich})",
                    feed.Name, enrichMetadata);
                var processingStart = DateTime.UtcNow;

                await ProcessFeedAsync(feed, enrichMetadata, stoppingToken);

                var elapsed = DateTime.UtcNow - processingStart;
                _lastPollTimes[feed.Name] = DateTime.UtcNow;

                _logger.LogInformation("Completed processing feed {FeedName} in {ElapsedSec:N1}s",
                    feed.Name, elapsed.TotalSeconds);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex,
                    "Error processing feed {FeedName}, will retry next interval",
                    feed.Name);
            }
        }
    }

    private async Task SyncAllFeedDirectoriesAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var dataService = scope.ServiceProvider.GetRequiredService<IPodcastDataService>();
        var allFeeds = await dataService.GetAllFeedsAsync();

        foreach (var feed in allFeeds.Where(f => f.IsActive))
        {
            try
            {
                await dataService.SyncDirectoryAsync(feed.Id, feed.Directory, feed.FileExtensions, feed.DirectorySearchDepth);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing directory for feed {FeedName}", feed.Name);
            }
        }
    }

    private async Task<List<Feed>> GetFeedsDueForPollingAsync()
    {
        var now = DateTime.UtcNow;
        _logger.LogTrace("Checking which feeds are due for polling at {Time}", now);

        using var scope = _serviceProvider.CreateScope();
        var dataService = scope.ServiceProvider.GetRequiredService<IPodcastDataService>();
        var allFeeds = await dataService.GetAllFeedsAsync();

        var dueFeeds = new List<Feed>();
        foreach (var feed in allFeeds)
        {
            if (!feed.YouTubeEnabled)
            {
                _logger.LogTrace("Skipping feed {FeedName}: YouTube config not enabled", feed.Name);
                continue;
            }

            if (string.IsNullOrWhiteSpace(feed.YouTubePlaylistUrl))
            {
                _logger.LogWarning("Feed {FeedName} has YouTube enabled but no playlist URL configured, skipping", feed.Name);
                continue;
            }

            var interval = TimeSpan.FromMinutes(feed.YouTubePollIntervalMinutes);

            if (!_lastPollTimes.TryGetValue(feed.Name, out var lastPoll))
            {
                _logger.LogDebug("Feed {FeedName} has never been polled, adding to queue", feed.Name);
                dueFeeds.Add(feed);
            }
            else
            {
                var timeSinceLastPoll = now - lastPoll;
                if (timeSinceLastPoll >= interval)
                {
                    _logger.LogDebug("Feed {FeedName} due for polling (last poll: {LastPoll}, interval: {Interval}min)",
                        feed.Name, lastPoll, feed.YouTubePollIntervalMinutes);
                    dueFeeds.Add(feed);
                }
                else
                {
                    var timeUntilNext = interval - timeSinceLastPoll;
                    _logger.LogTrace("Feed {FeedName} not due yet (next poll in {NextPoll}min)",
                        feed.Name, timeUntilNext.TotalMinutes);
                }
            }
        }

        return dueFeeds;
    }

    // Maximum number of new videos to download in a single poll cycle. Bounding this keeps each
    // cycle short so it completes (and persists progress) before the process is restarted; the
    // backlog drains across subsequent polls.
    internal const int MaxDownloadsPerPoll = 5;

    /// <summary>
    /// Selects the videos to download this poll cycle: drops already-downloaded videos, orders
    /// oldest-first (playlists arrive newest-first), and caps the result at <paramref name="maxPerPoll"/>.
    /// </summary>
    internal static List<T> SelectNewVideosToDownload<T>(
        IReadOnlyList<T> playlistVideos,
        Func<T, string> getVideoId,
        ISet<string> downloadedVideoIds,
        int maxPerPoll)
    {
        return playlistVideos
            .Where(v => !downloadedVideoIds.Contains(getVideoId(v)))
            .Reverse() // Download oldest first (playlists are typically newest-first)
            .Take(maxPerPoll)
            .ToList();
    }

    // Delay between per-video metadata fetches during an enriching resync, to be polite to YouTube.
    // Lighter than the 5s download cap since these are cheap metadata-only calls.
    private static readonly TimeSpan MetadataEnrichDelay = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Fetches the real upload date for each playlist video (one network call per video) and writes
    /// it onto the corresponding <see cref="PlaylistVideoInfo"/>. Used only during Clear &amp; Resync,
    /// where the DB rows were wiped and there is no existing publish_date to preserve.
    /// Failures for an individual video are logged and skipped (that one's date stays null) rather
    /// than aborting the whole resync. Respects cancellation.
    /// </summary>
    private async Task EnrichUploadDatesAsync(
        Feed feed,
        IReadOnlyList<PlaylistVideoInfo> playlistInfos,
        IYouTubeDownloadService youtubeService,
        CancellationToken stoppingToken)
    {
        var total = playlistInfos.Count;
        _logger.LogInformation(
            "Enriching upload dates for {Count} videos in {FeedName} (resync)", total, feed.Name);

        var enriched = 0;
        var index = 0;
        foreach (var info in playlistInfos)
        {
            stoppingToken.ThrowIfCancellationRequested();
            index++;

            try
            {
                var uploadDate = await youtubeService.GetVideoUploadDateAsync(info.VideoId, stoppingToken);
                if (uploadDate.HasValue)
                {
                    info.UploadDate = uploadDate;
                    enriched++;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex,
                    "Failed to fetch upload date for video {VideoId} ('{Title}') during resync; leaving date null",
                    info.VideoId, info.Title);
            }

            if (index % 25 == 0 || index == total)
            {
                _logger.LogInformation(
                    "Enriching metadata {Index}/{Total} for {FeedName}", index, total, feed.Name);
            }

            // Rate-limit between metadata fetches (skip the trailing delay after the last one).
            if (index < total)
            {
                await Task.Delay(MetadataEnrichDelay, stoppingToken);
            }
        }

        _logger.LogInformation(
            "Upload date enrichment complete for {FeedName}: {Enriched}/{Total} dates resolved",
            feed.Name, enriched, total);
    }

    private async Task ProcessFeedAsync(
        Feed feed,
        bool enrichMetadata,
        CancellationToken stoppingToken)
    {
        _logger.LogInformation("Checking playlist for feed: {FeedName}", feed.Name);
        _logger.LogDebug("Playlist URL: {PlaylistUrl}", feed.YouTubePlaylistUrl);

        using var scope = _serviceProvider.CreateScope();
        var youtubeService = scope.ServiceProvider.GetRequiredService<IYouTubeDownloadService>();
        var dataService = scope.ServiceProvider.GetRequiredService<IPodcastDataService>();

        var feedId = feed.Id;

        // Step 1: Fetch playlist videos
        _logger.LogDebug("Fetching playlist videos for {FeedName}", feed.Name);
        // YouTubePlaylistUrl is guaranteed non-null here — filtered in GetFeedsDueForPollingAsync
        var videos = await youtubeService.GetPlaylistVideosAsync(
            feed.YouTubePlaylistUrl!,
            stoppingToken);

        _logger.LogInformation(
            "Found {Count} videos in playlist for {FeedName}",
            videos.Count,
            feed.Name);

        // Step 2: Build lightweight playlist info (title only) for fuzzy matching.
        // We deliberately do NOT fetch per-video details here: with a large backlog that meant
        // hundreds of sequential YouTube calls before any download could start, so the cycle never
        // finished. SyncPlaylistInfoAsync only needs the title to match files and preserves existing
        // metadata when details are null; full details are fetched in Step 4 for videos we download.
        var downloadedIds = await dataService.GetDownloadedVideoIdsAsync(feedId);
        _logger.LogDebug("Found {Count} already downloaded videos in database", downloadedIds.Count);

        var playlistInfos = videos
            .Select((v, index) => new PlaylistVideoInfo
            {
                VideoId = v.Id.Value,
                Title = v.Title,
                Description = null, // Fetched on download; preserved from existing DB record otherwise
                // Thumbnails come free with the playlist fetch (no extra network call), so always
                // populate them. This restores thumbnails after Clear & Resync wipes the rows.
                ThumbnailUrl = v.Thumbnails
                    .OrderByDescending(t => t.Resolution.Width)
                    .FirstOrDefault()?.Url,
                UploadDate = null,
                PlaylistIndex = index + 1
            })
            .ToList();

        // Step 2b (resync only): enrich real upload dates via per-video fetches. Normal interval
        // polls skip this entirely to stay fast — only an explicit Clear & Resync sets enrichMetadata.
        if (enrichMetadata)
        {
            await EnrichUploadDatesAsync(feed, playlistInfos, youtubeService, stoppingToken);
        }

        // Step 3: Sync playlist info with database using fuzzy matching
        _logger.LogInformation("Syncing {Count} playlist videos to database with fuzzy matching", playlistInfos.Count);
        await dataService.SyncPlaylistInfoAsync(feedId, playlistInfos, feed.Directory, feed.DirectorySearchDepth);

        // Step 4: Check for new videos to download (capped per poll so the cycle completes promptly)
        _logger.LogDebug("Re-checking downloaded video list after sync");
        downloadedIds = await dataService.GetDownloadedVideoIdsAsync(feedId);
        _logger.LogDebug("Found {Count} downloaded videos in database after sync", downloadedIds.Count);

        var newVideos = SelectNewVideosToDownload(
            videos, v => v.Id.Value, downloadedIds, MaxDownloadsPerPoll);

        if (newVideos.Count == 0)
        {
            _logger.LogInformation("No new videos to download for feed {FeedName}", feed.Name);
            _logger.LogDebug("Syncing directory to catch any manually added files");
            await dataService.SyncDirectoryAsync(feedId, feed.Directory, feed.FileExtensions, feed.DirectorySearchDepth);
            return;
        }

        _logger.LogInformation(
            "Found {Count} new videos to download for {FeedName} (downloading oldest first)",
            newVideos.Count,
            feed.Name);

        _logger.LogDebug("New videos: {VideoIds}",
            string.Join(", ", newVideos.Select(v => $"{v.Id.Value} ({v.Title})")));

        using var semaphore = new SemaphoreSlim(feed.YouTubeMaxConcurrentDownloads);
        var newEpisodes = new ConcurrentBag<Episode>();

        // Track filenames already claimed as episodes during this poll cycle, seeded with
        // every filename Step 3 (SyncPlaylistInfoAsync) already turned into an episode. The
        // matcher used below (GetExistingFilePath) is independent of Step 3's matcher and can
        // resolve to a file Step 3 already claimed; without this guard two videos produce two
        // Episode rows with the same (feed_id, filename), violating the unique index. Concurrent
        // because the download loop may run with parallelism (newEpisodes is a ConcurrentBag).
        var claimedFilenames = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        foreach (var existingEpisode in await dataService.GetEpisodesAsync(feedId))
        {
            claimedFilenames.TryAdd(existingEpisode.Filename, 0);
        }

        _logger.LogDebug("Starting downloads with max concurrency: {MaxConcurrent}",
            feed.YouTubeMaxConcurrentDownloads);

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
            var existingPath = youtubeService.GetExistingFilePath(video.Title, feed.Directory);
            if (existingPath != null)
            {
                _logger.LogInformation(
                    "File already exists for '{Title}', marking as downloaded: {Path}",
                    video.Title,
                    existingPath);

                var existingFilename = Path.GetFileName(existingPath);
                await dataService.MarkVideoDownloadedAsync(feedId, video.Id.Value, existingFilename);

                // Skip if another video (or Step 3) already claimed this filename this cycle:
                // adding a second episode for the same (feed_id, filename) would violate the unique index.
                if (!claimedFilenames.TryAdd(existingFilename, 0))
                {
                    _logger.LogInformation(
                        "Filename '{Filename}' already claimed this cycle, skipping duplicate episode for '{Title}'",
                        existingFilename, video.Title);
                    continue;
                }

                // Get video details for existing file
                _logger.LogDebug("Fetching metadata for existing file");
                var details = await youtubeService.GetVideoDetailsAsync(video.Id.Value, stoppingToken);

                newEpisodes.Add(new Episode
                {
                    FeedId = feedId,
                    Filename = existingFilename,
                    VideoId = video.Id.Value,
                    YoutubeTitle = video.Title,
                    Title = details?.Title ?? video.Title,
                    Description = details?.Description,
                    ThumbnailUrl = details?.ThumbnailUrl,
                    PublishDate = details?.UploadDate,
                    DisplayOrder = 0,
                    AddedAt = DateTime.UtcNow
                });
                continue;
            }

            _logger.LogDebug("Waiting for download slot (max concurrent: {Max})", feed.YouTubeMaxConcurrentDownloads);
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
                    feed.Directory,
                    feed.YouTubeAudioQuality,
                    stoppingToken);

                if (outputPath != null)
                {
                    var downloadElapsed = DateTime.UtcNow - downloadStart;
                    var filename = Path.GetFileName(outputPath);

                    _logger.LogInformation("Download completed in {ElapsedSec:N1}s: {Filename}",
                        downloadElapsed.TotalSeconds, filename);

                    await dataService.MarkVideoDownloadedAsync(feedId, video.Id.Value, filename);

                    // Skip if this filename was already claimed this cycle (e.g. Step 3 matched it
                    // or another download resolved to the same file), to avoid a duplicate episode.
                    if (!claimedFilenames.TryAdd(filename, 0))
                    {
                        _logger.LogInformation(
                            "Filename '{Filename}' already claimed this cycle, skipping duplicate episode for '{Title}'",
                            filename, video.Title);
                        continue;
                    }

                    newEpisodes.Add(new Episode
                    {
                        FeedId = feedId,
                        Filename = filename,
                        VideoId = video.Id.Value,
                        YoutubeTitle = video.Title,
                        Title = details?.Title ?? video.Title,
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
        await dataService.SyncDirectoryAsync(feedId, feed.Directory, feed.FileExtensions, feed.DirectorySearchDepth);

        _logger.LogInformation(
            "Completed processing {FeedName}: {ProcessedCount} videos processed, {NewCount} new episodes added",
            feed.Name,
            processedCount,
            newEpisodes.Count);
    }
}
