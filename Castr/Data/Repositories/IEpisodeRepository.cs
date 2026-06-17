using Castr.Data.Entities;

namespace Castr.Data.Repositories;

public interface IEpisodeRepository
{
    Task<List<Episode>> GetByFeedIdAsync(int feedId);
    Task<Episode?> GetByIdAsync(int id);
    Task<Episode?> GetByFilenameAsync(int feedId, string filename);
    Task<Episode?> GetByVideoIdAsync(int feedId, string videoId);
    Task AddAsync(Episode episode);
    Task AddRangeAsync(IEnumerable<Episode> episodes);
    Task UpdateAsync(Episode episode);
    Task DeleteAsync(int id);

    /// <summary>
    /// Bulk-deletes all episodes belonging to the given feed in a single database operation.
    /// Returns the number of rows deleted.
    /// </summary>
    Task<int> DeleteByFeedIdAsync(int feedId);
}
