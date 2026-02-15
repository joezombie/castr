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

    [MaxLength(500)]
    public string? Artist { get; set; }

    [MaxLength(500)]
    public string? Album { get; set; }

    [MaxLength(200)]
    public string? Genre { get; set; }

    public uint? Year { get; set; }

    public uint? TrackNumber { get; set; }

    public int? Bitrate { get; set; }

    [MaxLength(1000)]
    public string? Subtitle { get; set; }

    public bool HasEmbeddedArt { get; set; }

    [NotMapped]
    public TimeSpan Duration => TimeSpan.FromSeconds(DurationSeconds ?? 0);

    public Feed Feed { get; set; } = null!;
}
