using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Castr.Data.Entities;

public class Episode
{
    public int Id { get; set; }

    public int FeedId { get; set; }

    [Required, MaxLength(500)]
    public string Filename { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? VideoId { get; set; }

    [MaxLength(500)]
    public string? YoutubeTitle { get; set; }

    [MaxLength(500)]
    public string? Title { get; set; }

    public string? Description { get; set; }

    [MaxLength(1000)]
    public string? ThumbnailUrl { get; set; }

    public int DisplayOrder { get; set; }

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    public DateTime? PublishDate { get; set; }

    public double? MatchScore { get; set; }

    public double? DurationSeconds { get; set; }

    public long? FileSize { get; set; }

    [NotMapped]
    public TimeSpan Duration => TimeSpan.FromSeconds(DurationSeconds ?? 0);

    public Feed Feed { get; set; } = null!;
}
