using Castr.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Castr.Data.Repositories;

public class EpisodeRepository : IEpisodeRepository
{
    private readonly CastrDbContext _context;

    public EpisodeRepository(CastrDbContext context)
    {
        _context = context;
    }

    public async Task<List<Episode>> GetByFeedIdAsync(int feedId)
    {
        return await _context.Episodes.Where(e => e.FeedId == feedId).OrderBy(e => e.DisplayOrder).ToListAsync();
    }

    public async Task<Episode?> GetByIdAsync(int id)
    {
        return await _context.Episodes.FindAsync(id);
    }

    public async Task<Episode?> GetByFilenameAsync(int feedId, string filename)
    {
        return await _context.Episodes.FirstOrDefaultAsync(e => e.FeedId == feedId && e.Filename == filename);
    }

    public async Task<Episode?> GetByVideoIdAsync(int feedId, string videoId)
    {
        return await _context.Episodes.FirstOrDefaultAsync(e => e.FeedId == feedId && e.VideoId == videoId);
    }

    public async Task AddAsync(Episode episode)
    {
        episode.AddedAt = DateTime.UtcNow;
        _context.Episodes.Add(episode);
        await _context.SaveChangesAsync();
    }

    public async Task AddRangeAsync(IEnumerable<Episode> episodes)
    {
        var incoming = episodes as IList<Episode> ?? episodes.ToList();
        if (incoming.Count == 0) return;

        var feedIds = incoming.Select(e => e.FeedId).Distinct().ToList();

        // Existing (FeedId, Filename) pairs already in the DB for the affected feeds.
        var existing = await _context.Episodes
            .Where(e => feedIds.Contains(e.FeedId))
            .Select(e => new { e.FeedId, e.Filename })
            .ToListAsync();

        // Track (FeedId, Filename) already seen, seeded with existing DB rows, so we
        // skip both in-batch duplicates and duplicates of rows already persisted.
        // This makes the insert idempotent w.r.t. the unique (feed_id, filename) index.
        var seen = new HashSet<(int FeedId, string Filename)>(
            existing.Select(e => (e.FeedId, e.Filename)));

        var now = DateTime.UtcNow;
        var toInsert = new List<Episode>();
        foreach (var ep in incoming)
        {
            if (!seen.Add((ep.FeedId, ep.Filename))) continue; // duplicate (existing or in-batch) -> no-op
            ep.AddedAt = now;
            toInsert.Add(ep);
        }

        if (toInsert.Count == 0) return;
        _context.Episodes.AddRange(toInsert);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Episode episode)
    {
        _context.Entry(episode).State = EntityState.Modified;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var episode = await _context.Episodes.FindAsync(id);
        if (episode != null)
        {
            _context.Episodes.Remove(episode);
            await _context.SaveChangesAsync();
        }
    }

    public Task<int> DeleteByFeedIdAsync(int feedId)
    {
        return _context.Episodes.Where(e => e.FeedId == feedId).ExecuteDeleteAsync();
    }
}
