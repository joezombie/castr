namespace Castr.Data.Entities;

/// <summary>
/// Entity representing a podcast episode.
/// Tracks episode metadata and fuzzy matching information.
/// </summary>
public class Episode
{
    public int Id { get; set; }
    
    /// <summary>
    /// Foreign key to the feeds table.
    /// </summary>
    public int FeedId { get; set; }
    
    public required string Filename { get; set; }
    public string? VideoId { get; set; }
    public string? YoutubeTitle { get; set; }
    public string? Description { get; set; }
    public string? ThumbnailUrl { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime AddedAt { get; set; }
    public DateTime? PublishDate { get; set; }
    
    /// <summary>
    /// Fuzzy matching score (0.0 to 1.0) between YouTube title and filename.
    /// </summary>
    public double? MatchScore { get; set; }
    
    // Navigation property
    public Feed Feed { get; set; } = null!;
}
