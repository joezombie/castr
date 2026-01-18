namespace Castr.Models;

public class DownloadQueueItem
{
    public int Id { get; set; }
    public int FeedId { get; set; }
    public required string VideoId { get; set; }
    public string? VideoTitle { get; set; }
    public required string Status { get; set; }
    public int ProgressPercent { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime QueuedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public static class DownloadStatus
{
    public const string Queued = "queued";
    public const string InProgress = "in_progress";
    public const string Completed = "completed";
    public const string Failed = "failed";
}
