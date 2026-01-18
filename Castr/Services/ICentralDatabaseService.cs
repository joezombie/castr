using Castr.Models;

namespace Castr.Services;

/// <summary>
/// Interface for central database operations managing feeds, episodes, and activities.
/// </summary>
public interface ICentralDatabaseService
{
    /// <summary>
    /// Initialize the central database schema and perform migrations.
    /// </summary>
    Task InitializeDatabaseAsync(CancellationToken cancellationToken = default);
    
    // Feed operations
    Task<List<FeedRecord>> GetAllFeedsAsync(CancellationToken cancellationToken = default);
    Task<FeedRecord?> GetFeedByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<FeedRecord?> GetFeedByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<int> CreateFeedAsync(FeedRecord feed, CancellationToken cancellationToken = default);
    Task UpdateFeedAsync(FeedRecord feed, CancellationToken cancellationToken = default);
    Task DeleteFeedAsync(int id, CancellationToken cancellationToken = default);
    Task<int> GetFeedEpisodeCountAsync(int feedId, CancellationToken cancellationToken = default);
    Task<DateTime?> GetFeedLastSyncTimeAsync(int feedId, CancellationToken cancellationToken = default);
    
    // Activity log operations
    Task LogActivityAsync(int? feedId, string activityType, string message, string? details = null, 
        CancellationToken cancellationToken = default);
    Task<List<ActivityLogRecord>> GetRecentActivitiesAsync(int count = 20, int? feedId = null, 
        CancellationToken cancellationToken = default);
    
    // Download queue operations
    Task<List<DownloadQueueItem>> GetActiveDownloadsAsync(CancellationToken cancellationToken = default);
    Task<DownloadQueueItem?> GetDownloadByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<int> CreateDownloadAsync(DownloadQueueItem item, CancellationToken cancellationToken = default);
    Task UpdateDownloadAsync(DownloadQueueItem item, CancellationToken cancellationToken = default);
    
    // Statistics
    Task<int> GetTotalFeedsCountAsync(CancellationToken cancellationToken = default);
    Task<int> GetTotalEpisodesCountAsync(CancellationToken cancellationToken = default);
    Task<int> GetActiveDownloadsCountAsync(CancellationToken cancellationToken = default);
}
