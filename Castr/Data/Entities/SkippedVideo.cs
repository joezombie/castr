using System.ComponentModel.DataAnnotations;

namespace Castr.Data.Entities;

/// <summary>
/// Records a playlist video that was skipped by a feed's sync filters, so it is not re-evaluated
/// (and, for date skips, not re-fetched) on every poll. <see cref="FilterHash"/> captures the
/// filter configuration at skip time; when the feed's filters change the hash differs and the row
/// is deleted, re-admitting the video for re-evaluation.
/// </summary>
public class SkippedVideo
{
    public int Id { get; set; }

    public int FeedId { get; set; }

    [Required, MaxLength(50)]
    public string VideoId { get; set; } = string.Empty;

    /// <summary>"keyword" or "date".</summary>
    [Required, MaxLength(20)]
    public string SkipReason { get; set; } = string.Empty;

    /// <summary>Hash of the three filter fields at the time the video was skipped.</summary>
    [Required, MaxLength(64)]
    public string FilterHash { get; set; } = string.Empty;

    public DateTime SkippedAt { get; set; } = DateTime.UtcNow;

    public Feed Feed { get; set; } = null!;
}
