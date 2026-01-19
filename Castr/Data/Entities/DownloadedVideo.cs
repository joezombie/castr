using System.ComponentModel.DataAnnotations;

namespace Castr.Data.Entities;

/// <summary>
/// Represents a downloaded video entity for Entity Framework Core.
/// Tracks videos downloaded from YouTube to prevent duplicate downloads.
/// </summary>
public class DownloadedVideo
{
    public int Id { get; set; }
    
    public int FeedId { get; set; }
    
    [Required, MaxLength(50)]
    public string VideoId { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string? Filename { get; set; }
    
    public DateTime DownloadedAt { get; set; }
    
    // Navigation property
    public Feed Feed { get; set; } = null!;
}
