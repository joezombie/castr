using Castr.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Castr.Data.Repositories;

public class SkippedVideoRepository : ISkippedVideoRepository
{
    private readonly CastrDbContext _context;
    private readonly ILogger<SkippedVideoRepository> _logger;

    public SkippedVideoRepository(CastrDbContext context, ILogger<SkippedVideoRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<HashSet<string>> GetSkippedVideoIdsAsync(int feedId)
    {
        var ids = await _context.SkippedVideos.Where(s => s.FeedId == feedId).Select(s => s.VideoId).ToListAsync();
        return ids.ToHashSet();
    }

    public async Task MarkVideoSkippedAsync(int feedId, string videoId, string skipReason, string filterHash)
    {
        var existing = await _context.SkippedVideos.FirstOrDefaultAsync(s => s.FeedId == feedId && s.VideoId == videoId);
        if (existing != null)
        {
            existing.SkipReason = skipReason;
            existing.FilterHash = filterHash;
            existing.SkippedAt = DateTime.UtcNow;
        }
        else
        {
            _context.SkippedVideos.Add(new SkippedVideo
            {
                FeedId = feedId,
                VideoId = videoId,
                SkipReason = skipReason,
                FilterHash = filterHash,
                SkippedAt = DateTime.UtcNow
            });
        }

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex,
                "DbUpdateException recording skip for FeedId={FeedId}, VideoId={VideoId}; attempting upsert fallback",
                feedId, videoId);

            // Detach only the failed Added entity to avoid corrupting other tracked entities
            var failedEntry = _context.ChangeTracker.Entries<SkippedVideo>()
                .FirstOrDefault(e => e.State == EntityState.Added
                    && e.Entity.FeedId == feedId && e.Entity.VideoId == videoId);
            if (failedEntry != null)
                failedEntry.State = EntityState.Detached;

            var conflict = await _context.SkippedVideos
                .FirstOrDefaultAsync(s => s.FeedId == feedId && s.VideoId == videoId);
            if (conflict != null)
            {
                conflict.SkipReason = skipReason;
                conflict.FilterHash = filterHash;
                conflict.SkippedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
            else
            {
                // Not a unique constraint race — rethrow the original error
                throw;
            }
        }
    }

    public async Task<int> MarkVideosSkippedAsync(int feedId, IEnumerable<(string videoId, string reason)> skips, string filterHash)
    {
        var skipList = skips.ToList();
        if (skipList.Count == 0)
            return 0;

        var now = DateTime.UtcNow;
        var rows = skipList.Select(s => new SkippedVideo
        {
            FeedId = feedId,
            VideoId = s.videoId,
            SkipReason = s.reason,
            FilterHash = filterHash,
            SkippedAt = now
        }).ToList();

        _context.SkippedVideos.AddRange(rows);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex,
                "DbUpdateException bulk-recording {Count} skip(s) for FeedId={FeedId}; falling back to per-row upsert",
                rows.Count, feedId);

            // Detach the failed batch so we can re-insert idempotently one row at a time.
            foreach (var entry in _context.ChangeTracker.Entries<SkippedVideo>()
                         .Where(e => e.State == EntityState.Added && e.Entity.FeedId == feedId)
                         .ToList())
            {
                entry.State = EntityState.Detached;
            }

            foreach (var s in skipList)
            {
                await MarkVideoSkippedAsync(feedId, s.videoId, s.reason, filterHash);
            }
        }

        return skipList.Count;
    }

    public Task<int> DeleteStaleSkipsAsync(int feedId, string currentFilterHash)
    {
        return _context.SkippedVideos
            .Where(s => s.FeedId == feedId && s.FilterHash != currentFilterHash)
            .ExecuteDeleteAsync();
    }

    public Task<int> DeleteSkippedVideosByFeedIdAsync(int feedId)
    {
        return _context.SkippedVideos.Where(s => s.FeedId == feedId).ExecuteDeleteAsync();
    }
}
