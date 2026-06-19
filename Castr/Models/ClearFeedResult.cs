namespace Castr.Models;

/// <summary>
/// Result of clearing a feed's episode metadata and download tracking from the database.
/// </summary>
/// <param name="EpisodesCleared">Number of episode rows deleted.</param>
/// <param name="TrackingRowsCleared">Number of downloaded-video tracking rows deleted.</param>
/// <param name="SkipRowsCleared">Number of skipped-video rows deleted.</param>
public record ClearFeedResult(int EpisodesCleared, int TrackingRowsCleared, int SkipRowsCleared = 0);
