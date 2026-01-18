using Castr.Models;

namespace Castr.Services;

public interface ICentralDatabaseService
{
    // Database initialization
    Task InitializeDatabaseAsync();
    
    // Feed operations
    Task<List<FeedRecord>> GetFeedsAsync();
    Task<FeedRecord?> GetFeedByNameAsync(string name);
    Task<FeedRecord?> GetFeedByIdAsync(int id);
    Task<int> AddFeedAsync(FeedRecord feed);
    Task UpdateFeedAsync(FeedRecord feed);
    Task DeleteFeedAsync(int id);
    
    // Activity log operations
    Task LogActivityAsync(int? feedId, string activityType, string message, string? details = null);
    Task<List<ActivityLogRecord>> GetRecentActivitiesAsync(int limit = 20);
    
    // Download queue operations
    Task<List<DownloadQueueItem>> GetActiveDownloadsAsync();
    Task<DownloadQueueItem?> GetDownloadAsync(int id);
    Task<int> AddDownloadAsync(DownloadQueueItem download);
    Task UpdateDownloadProgressAsync(int id, int progressPercent);
    Task UpdateDownloadStatusAsync(int id, string status, string? errorMessage = null);
    Task CompleteDownloadAsync(int id);
    
    // Statistics
    Task<int> GetTotalFeedsCountAsync();
    Task<int> GetTotalEpisodesCountAsync();
    Task<int> GetActiveDownloadsCountAsync();
}
