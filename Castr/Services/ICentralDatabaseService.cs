namespace Castr.Services;

using Castr.Models;

/// <summary>
/// Central database service for managing all feeds, episodes, and activity in a single database.
/// Replaces per-feed SQLite databases with a unified schema.
/// </summary>
public interface ICentralDatabaseService : IDisposable
{
    // Database initialization
    Task InitializeDatabaseAsync();
    Task MigrateFromPerFeedDatabasesAsync(Dictionary<string, PodcastFeedConfig> feeds);
    
    // Feed management
    Task<List<FeedRecord>> GetAllFeedsAsync();
    Task<FeedRecord?> GetFeedByNameAsync(string name);
    Task<FeedRecord?> GetFeedByIdAsync(int id);
    Task<int> AddFeedAsync(FeedRecord feed);
    Task UpdateFeedAsync(FeedRecord feed);
    Task DeleteFeedAsync(int id);
    
    // Episode management (scoped by feed)
    Task<List<EpisodeRecord>> GetEpisodesAsync(int feedId);
    Task<EpisodeRecord?> GetEpisodeByIdAsync(int episodeId);
    Task<EpisodeRecord?> GetEpisodeByFilenameAsync(int feedId, string filename);
    Task AddEpisodeAsync(int feedId, EpisodeRecord episode);
    Task AddEpisodesAsync(int feedId, IEnumerable<EpisodeRecord> episodes);
    Task UpdateEpisodeAsync(int feedId, EpisodeRecord episode);
    Task SyncDirectoryAsync(int feedId, string directory, string[] extensions);
    Task SyncPlaylistInfoAsync(int feedId, IEnumerable<PlaylistVideoInfo> videos, string directory);
    
    // Download tracking (scoped by feed)
    Task<bool> IsVideoDownloadedAsync(int feedId, string videoId);
    Task<HashSet<string>> GetDownloadedVideoIdsAsync(int feedId);
    Task MarkVideoDownloadedAsync(int feedId, string videoId, string filename);
    
    // Activity logging
    Task LogActivityAsync(int? feedId, string activityType, string message, string? details = null);
    Task<List<ActivityLogRecord>> GetRecentActivityAsync(int? feedId = null, int count = 100);
    
    // Download queue management
    Task<DownloadQueueItem> AddToDownloadQueueAsync(int feedId, string videoId, string? videoTitle = null);
    Task UpdateDownloadProgressAsync(int queueItemId, string status, int progressPercent, string? errorMessage = null);
    Task<List<DownloadQueueItem>> GetDownloadQueueAsync(int? feedId = null);
    Task<DownloadQueueItem?> GetQueueItemAsync(int feedId, string videoId);
    Task RemoveFromDownloadQueueAsync(int queueItemId);
}
