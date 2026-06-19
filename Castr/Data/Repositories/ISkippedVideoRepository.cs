namespace Castr.Data.Repositories;

public interface ISkippedVideoRepository
{
    /// <summary>Returns the set of video IDs currently recorded as skipped for the feed.</summary>
    Task<HashSet<string>> GetSkippedVideoIdsAsync(int feedId);

    /// <summary>Records a video as skipped (idempotent upsert keyed on feed + video).</summary>
    Task MarkVideoSkippedAsync(int feedId, string videoId, string skipReason, string filterHash);

    /// <summary>
    /// Bulk-records skips in a single SaveChanges. Callers must pass only videos not already skipped.
    /// On a unique-constraint race it falls back to the per-row idempotent upsert so every row still
    /// lands. Returns the number of skips processed.
    /// </summary>
    Task<int> MarkVideosSkippedAsync(int feedId, IEnumerable<(string videoId, string reason)> skips, string filterHash);

    /// <summary>
    /// Deletes all skip rows for the feed whose FilterHash differs from <paramref name="currentFilterHash"/>,
    /// re-admitting those videos for re-evaluation. Returns the number of rows deleted.
    /// </summary>
    Task<int> DeleteStaleSkipsAsync(int feedId, string currentFilterHash);

    /// <summary>Bulk-deletes all skip rows for the feed. Returns the number of rows deleted.</summary>
    Task<int> DeleteSkippedVideosByFeedIdAsync(int feedId);
}
