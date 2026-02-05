using System.ComponentModel.DataAnnotations;

namespace Castr.Data.Entities;

public class DownloadedVideo
{
    public int Id { get; set; }

    public int FeedId { get; set; }

    [Required, MaxLength(50)]
    public string VideoId { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Filename { get; set; }

    public DateTime DownloadedAt { get; set; } = DateTime.UtcNow;

    public Feed Feed { get; set; } = null!;
}
