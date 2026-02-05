using Castr.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Castr.Data.Repositories;

public class ActivityRepository : IActivityRepository
{
    private readonly CastrDbContext _context;

    public ActivityRepository(CastrDbContext context)
    {
        _context = context;
    }

    public async Task LogAsync(int? feedId, string activityType, string message, string? details = null)
    {
        _context.ActivityLogs.Add(new ActivityLog { FeedId = feedId, ActivityType = activityType, Message = message, Details = details, CreatedAt = DateTime.UtcNow });
        await _context.SaveChangesAsync();
    }

    public async Task<List<ActivityLog>> GetRecentAsync(int? feedId = null, int count = 100)
    {
        var query = _context.ActivityLogs.AsQueryable();
        if (feedId.HasValue) query = query.Where(a => a.FeedId == feedId.Value);
        return await query.OrderByDescending(a => a.CreatedAt).Take(count).ToListAsync();
    }

    public async Task ClearAsync()
    {
        await _context.ActivityLogs.ExecuteDeleteAsync();
    }
}
