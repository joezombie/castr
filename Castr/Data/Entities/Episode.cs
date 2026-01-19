using System.ComponentModel.DataAnnotations;

namespace Castr.Data.Entities;

/// <summary>
/// Represents an episode entity for Entity Framework Core.
/// Replaces EpisodeRecord with proper EF Core configuration.
/// </summary>
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
    
    public string? Description { get; set; }
    
    [MaxLength(1000)]
    public string? ThumbnailUrl { get; set; }
    
    public int DisplayOrder { get; set; }
    
    public DateTime AddedAt { get; set; }
    
    public DateTime? PublishDate { get; set; }
    
    public double? MatchScore { get; set; }
    
    // Navigation property
    public Feed Feed { get; set; } = null!;
}
