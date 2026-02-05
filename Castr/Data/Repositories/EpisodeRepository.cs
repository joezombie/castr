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
        var now = DateTime.UtcNow;
        foreach (var ep in episodes) ep.AddedAt = now;
        _context.Episodes.AddRange(episodes);
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
}
