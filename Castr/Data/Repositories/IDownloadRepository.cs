using Castr.Data.Entities;

namespace Castr.Data.Repositories;

public interface IDownloadRepository
{
    Task<bool> IsVideoDownloadedAsync(int feedId, string videoId);
    Task<HashSet<string>> GetDownloadedVideoIdsAsync(int feedId);
    Task MarkVideoDownloadedAsync(int feedId, string videoId, string? filename);
    Task RemoveDownloadedVideoAsync(int feedId, string videoId);

    /// <summary>
    /// Bulk-deletes all downloaded-video tracking rows belonging to the given feed in a single
    /// database operation. Returns the number of rows deleted.
    /// </summary>
    Task<int> DeleteDownloadedVideosByFeedIdAsync(int feedId);

    Task<DownloadQueueItem> AddToQueueAsync(int feedId, string videoId, string? title);
    Task UpdateQueueProgressAsync(int queueItemId, string status, int progressPercent, string? errorMessage);
    Task<List<DownloadQueueItem>> GetQueueAsync(int? feedId = null);
    Task<DownloadQueueItem?> GetQueueItemAsync(int feedId, string videoId);
    Task RemoveFromQueueAsync(int queueItemId);
}
