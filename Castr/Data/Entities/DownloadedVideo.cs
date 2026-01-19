namespace Castr.Data.Entities;

/// <summary>
/// Entity representing a downloaded YouTube video.
/// Tracks which videos have been downloaded for each feed to prevent duplicates.
/// </summary>
public class DownloadedVideo
{
    public int Id { get; set; }
    
    /// <summary>
    /// Foreign key to the feeds table.
    /// </summary>
    public int FeedId { get; set; }
    
    public required string VideoId { get; set; }
    public string? Filename { get; set; }
    public DateTime DownloadedAt { get; set; }
    
    // Navigation property
    public Feed Feed { get; set; } = null!;
}
