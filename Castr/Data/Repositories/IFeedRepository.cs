using Castr.Data.Entities;

namespace Castr.Data.Repositories;

public interface IFeedRepository
{
    Task<List<Feed>> GetAllAsync();
    Task<Feed?> GetByIdAsync(int id);
    Task<Feed?> GetByNameAsync(string name);
    Task<int> AddAsync(Feed feed);
    Task UpdateAsync(Feed feed);
    Task DeleteAsync(int id);
}
