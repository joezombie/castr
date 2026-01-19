using System.ComponentModel.DataAnnotations;

namespace Castr.Data.Entities;

/// <summary>
/// Represents an activity log entry for Entity Framework Core.
/// Tracks important events like downloads, syncs, errors for monitoring.
/// </summary>
public class ActivityLog
{
    public int Id { get; set; }
    
    /// <summary>
    /// Foreign key to the feeds table. Null for system-wide activities.
    /// </summary>
    public int? FeedId { get; set; }
    
    [Required, MaxLength(50)]
    public string ActivityType { get; set; } = string.Empty;
    
    [Required]
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// Optional JSON or text details about the activity.
    /// </summary>
    public string? Details { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    // Navigation property
    public Feed? Feed { get; set; }
}
