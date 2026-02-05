using Castr.Data.Entities;

namespace Castr.Data.Repositories;

public interface IActivityRepository
{
    Task LogAsync(int? feedId, string activityType, string message, string? details = null);
    Task<List<ActivityLog>> GetRecentAsync(int? feedId = null, int count = 100);
    Task ClearAsync();
}
