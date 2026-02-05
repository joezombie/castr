using System.ComponentModel.DataAnnotations;

namespace Castr.Data.Entities;

public class DownloadQueueItem
{
    public int Id { get; set; }

    public int FeedId { get; set; }

    [Required, MaxLength(50)]
    public string VideoId { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? VideoTitle { get; set; }

    [Required, MaxLength(20)]
    public string Status { get; set; } = "queued";

    public int ProgressPercent { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime QueuedAt { get; set; } = DateTime.UtcNow;

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public Feed Feed { get; set; } = null!;
}
