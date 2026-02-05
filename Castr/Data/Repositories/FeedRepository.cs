using Castr.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Castr.Data.Repositories;

public class FeedRepository : IFeedRepository
{
    private readonly CastrDbContext _context;

    public FeedRepository(CastrDbContext context)
    {
        _context = context;
    }

    public async Task<List<Feed>> GetAllAsync()
    {
        return await _context.Feeds.OrderBy(f => f.Name).ToListAsync();
    }

    public async Task<Feed?> GetByIdAsync(int id)
    {
        return await _context.Feeds.FindAsync(id);
    }

    public async Task<Feed?> GetByNameAsync(string name)
    {
        return await _context.Feeds.FirstOrDefaultAsync(f => f.Name == name);
    }

    public async Task<int> AddAsync(Feed feed)
    {
        feed.CreatedAt = DateTime.UtcNow;
        feed.UpdatedAt = DateTime.UtcNow;
        _context.Feeds.Add(feed);
        await _context.SaveChangesAsync();
        return feed.Id;
    }

    public async Task UpdateAsync(Feed feed)
    {
        feed.UpdatedAt = DateTime.UtcNow;
        _context.Entry(feed).State = EntityState.Modified;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var feed = await _context.Feeds.FindAsync(id);
        if (feed != null)
        {
            _context.Feeds.Remove(feed);
            await _context.SaveChangesAsync();
        }
    }
}
