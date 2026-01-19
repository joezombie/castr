namespace Castr.Data.Entities;

/// <summary>
/// Entity representing a download queue item for tracking active and queued downloads.
/// Used by the dashboard to display download progress and status.
/// </summary>
public class DownloadQueueItem
{
    public int Id { get; set; }
    
    /// <summary>
    /// Foreign key to the feeds table.
    /// </summary>
    public int FeedId { get; set; }
    
    public required string VideoId { get; set; }
    public string? VideoTitle { get; set; }
    
    /// <summary>
    /// Current status: "queued", "downloading", "completed", "failed".
    /// </summary>
    public required string Status { get; set; }
    
    /// <summary>
    /// Download progress percentage (0-100).
    /// </summary>
    public int ProgressPercent { get; set; }
    
    /// <summary>
    /// Error message if status is "failed".
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    // Timestamps
    public DateTime QueuedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    
    // Navigation property
    public Feed Feed { get; set; } = null!;
}
