using System.ComponentModel.DataAnnotations;

namespace Castr.Data.Entities;

/// <summary>
/// Represents a download queue item for Entity Framework Core.
/// Tracks active and queued downloads for the dashboard.
/// </summary>
public class DownloadQueueItem
{
    public int Id { get; set; }
    
    public int FeedId { get; set; }
    
    [Required, MaxLength(50)]
    public string VideoId { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string? VideoTitle { get; set; }
    
    /// <summary>
    /// Current status: "queued", "downloading", "completed", "failed".
    /// </summary>
    [Required, MaxLength(20)]
    public string Status { get; set; } = string.Empty;
    
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
