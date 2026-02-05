using System.ComponentModel.DataAnnotations;

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

    public string? Description { get; set; }

    [MaxLength(1000)]
    public string? ThumbnailUrl { get; set; }

    public int DisplayOrder { get; set; }

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    public DateTime? PublishDate { get; set; }

    public double? MatchScore { get; set; }

    public Feed Feed { get; set; } = null!;
}
