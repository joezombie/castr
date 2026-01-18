namespace Castr.Models;

/// <summary>
/// Represents an activity log entry for dashboard monitoring.
/// Tracks important events like downloads, syncs, errors, etc.
/// </summary>
public class ActivityLogRecord
{
    public int Id { get; set; }
    
    /// <summary>
    /// Foreign key to the feeds table. Null for system-wide activities.
    /// </summary>
    public int? FeedId { get; set; }
    
    /// <summary>
    /// Type of activity (e.g., "download", "sync", "error", "startup").
    /// </summary>
    public required string ActivityType { get; set; }
    
    /// <summary>
    /// Human-readable message describing the activity.
    /// </summary>
    public required string Message { get; set; }
    
    /// <summary>
    /// Optional JSON or text details about the activity.
    /// </summary>
    public string? Details { get; set; }
    
    public DateTime CreatedAt { get; set; }
}
