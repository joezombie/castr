using Castr.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Castr.Data.Repositories;

public class DownloadRepository : IDownloadRepository
{
    private readonly CastrDbContext _context;

    public DownloadRepository(CastrDbContext context)
    {
        _context = context;
    }

    public async Task<bool> IsVideoDownloadedAsync(int feedId, string videoId)
    {
        return await _context.DownloadedVideos.AnyAsync(d => d.FeedId == feedId && d.VideoId == videoId);
    }

    public async Task<HashSet<string>> GetDownloadedVideoIdsAsync(int feedId)
    {
        var ids = await _context.DownloadedVideos.Where(d => d.FeedId == feedId).Select(d => d.VideoId).ToListAsync();
        return ids.ToHashSet();
    }

    public async Task MarkVideoDownloadedAsync(int feedId, string videoId, string? filename)
    {
        var existing = await _context.DownloadedVideos.FirstOrDefaultAsync(d => d.FeedId == feedId && d.VideoId == videoId);
        if (existing != null)
        {
            existing.Filename = filename;
            existing.DownloadedAt = DateTime.UtcNow;
        }
        else
        {
            _context.DownloadedVideos.Add(new DownloadedVideo { FeedId = feedId, VideoId = videoId, Filename = filename, DownloadedAt = DateTime.UtcNow });
        }
        await _context.SaveChangesAsync();
    }

    public async Task RemoveDownloadedVideoAsync(int feedId, string videoId)
    {
        var video = await _context.DownloadedVideos.FirstOrDefaultAsync(d => d.FeedId == feedId && d.VideoId == videoId);
        if (video != null)
        {
            _context.DownloadedVideos.Remove(video);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<DownloadQueueItem> AddToQueueAsync(int feedId, string videoId, string? title)
    {
        var existing = await _context.DownloadQueue.FirstOrDefaultAsync(q => q.FeedId == feedId && q.VideoId == videoId);
        if (existing != null) return existing;

        var item = new DownloadQueueItem { FeedId = feedId, VideoId = videoId, VideoTitle = title, Status = "queued", QueuedAt = DateTime.UtcNow };
        _context.DownloadQueue.Add(item);
        await _context.SaveChangesAsync();
        return item;
    }

    public async Task UpdateQueueProgressAsync(int queueItemId, string status, int progressPercent, string? errorMessage)
    {
        var item = await _context.DownloadQueue.FindAsync(queueItemId);
        if (item != null)
        {
            item.Status = status;
            item.ProgressPercent = progressPercent;
            item.ErrorMessage = errorMessage;
            if (status == "downloading" && item.StartedAt == null) item.StartedAt = DateTime.UtcNow;
            if (status == "completed" || status == "failed") item.CompletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<List<DownloadQueueItem>> GetQueueAsync(int? feedId = null)
    {
        var query = _context.DownloadQueue.AsQueryable();
        if (feedId.HasValue) query = query.Where(q => q.FeedId == feedId.Value);
        return await query.OrderBy(q => q.QueuedAt).ToListAsync();
    }

    public async Task<DownloadQueueItem?> GetQueueItemAsync(int feedId, string videoId)
    {
        return await _context.DownloadQueue.FirstOrDefaultAsync(q => q.FeedId == feedId && q.VideoId == videoId);
    }

    public async Task RemoveFromQueueAsync(int queueItemId)
    {
        var item = await _context.DownloadQueue.FindAsync(queueItemId);
        if (item != null)
        {
            _context.DownloadQueue.Remove(item);
            await _context.SaveChangesAsync();
        }
    }
}
