namespace Castr.Data.Entities;

/// <summary>
/// Entity representing a podcast feed.
/// Replaces per-feed configuration in appsettings.json.
/// </summary>
public class Feed
{
    public int Id { get; set; }
    
    /// <summary>
    /// Unique identifier for the feed (e.g., "btb", "btbc").
    /// </summary>
    public required string Name { get; set; }
    
    public required string Title { get; set; }
    public required string Description { get; set; }
    public required string Directory { get; set; }
    
    public string? Author { get; set; }
    public string? ImageUrl { get; set; }
    public string? Link { get; set; }
    public string? Language { get; set; }
    public string? Category { get; set; }
    
    /// <summary>
    /// Comma-separated list of file extensions (e.g., ".mp3,.m4a").
    /// </summary>
    public string? FileExtensions { get; set; }
    
    // YouTube configuration
    public string? YouTubePlaylistUrl { get; set; }
    public int? YouTubePollIntervalMinutes { get; set; }
    public bool YouTubeEnabled { get; set; }
    public int? YouTubeMaxConcurrentDownloads { get; set; }
    public string? YouTubeAudioQuality { get; set; }
    
    // Metadata
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsActive { get; set; }
    
    // Navigation properties
    public ICollection<Episode> Episodes { get; set; } = new List<Episode>();
    public ICollection<DownloadedVideo> DownloadedVideos { get; set; } = new List<DownloadedVideo>();
    public ICollection<ActivityLog> ActivityLogs { get; set; } = new List<ActivityLog>();
    public ICollection<DownloadQueueItem> DownloadQueue { get; set; } = new List<DownloadQueueItem>();
}
