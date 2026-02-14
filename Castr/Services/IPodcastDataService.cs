using Castr.Data.Entities;
using Castr.Models;

namespace Castr.Services;

/// <summary>
/// High-level data service that provides business logic on top of EF Core repositories.
/// Replaces legacy PodcastDatabaseService and CentralDatabaseService.
/// </summary>
public interface IPodcastDataService
{
    // Feed operations
    Task<List<Feed>> GetAllFeedsAsync();
    Task<Feed?> GetFeedByNameAsync(string name);
    Task<Feed?> GetFeedByIdAsync(int id);
    Task<int> AddFeedAsync(Feed feed);
    Task UpdateFeedAsync(Feed feed);
    Task DeleteFeedAsync(int id);

    // Episode operations
    Task<List<Episode>> GetEpisodesAsync(int feedId);
    Task<Episode?> GetEpisodeByIdAsync(int id);
    Task<Episode?> GetEpisodeByFilenameAsync(int feedId, string filename);
    Task<Episode?> GetEpisodeByVideoIdAsync(int feedId, string videoId);
    Task AddEpisodeAsync(Episode episode);
    Task AddEpisodesAsync(IEnumerable<Episode> episodes);
    Task UpdateEpisodeAsync(Episode episode);
    Task DeleteEpisodeAsync(int id);
    Task<int> SyncDirectoryAsync(int feedId, string directory, string[] extensions, int searchDepth = 0);
    Task SyncPlaylistInfoAsync(int feedId, IEnumerable<PlaylistVideoInfo> videos, string directory, int searchDepth = 0);

    // Download tracking
    Task<bool> IsVideoDownloadedAsync(int feedId, string videoId);
    Task<HashSet<string>> GetDownloadedVideoIdsAsync(int feedId);
    Task MarkVideoDownloadedAsync(int feedId, string videoId, string? filename);
    Task RemoveDownloadedVideoAsync(int feedId, string videoId);

    // Download queue
    Task<Data.Entities.DownloadQueueItem> AddToQueueAsync(int feedId, string videoId, string? title);
    Task UpdateQueueProgressAsync(int queueItemId, string status, int progressPercent, string? errorMessage);
    Task<List<Data.Entities.DownloadQueueItem>> GetQueueAsync(int? feedId = null);
    Task<Data.Entities.DownloadQueueItem?> GetQueueItemAsync(int feedId, string videoId);
    Task RemoveFromQueueAsync(int queueItemId);

    // Activity logging
    Task LogActivityAsync(int? feedId, string activityType, string message, string? details = null);
    Task<List<ActivityLog>> GetRecentActivityAsync(int? feedId = null, int count = 100);
    Task ClearActivityLogAsync();
}
