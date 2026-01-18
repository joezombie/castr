namespace Castr.Models;

/// <summary>
/// Represents a download queue item in the central database.
/// </summary>
public class DownloadQueueItem
{
    public int Id { get; set; }
    public int FeedId { get; set; }
    public required string VideoId { get; set; }
    public string? VideoTitle { get; set; }
    public required string Status { get; set; } // queued, downloading, completed, failed
    public int ProgressPercent { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime QueuedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
