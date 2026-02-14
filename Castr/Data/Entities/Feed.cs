using System.ComponentModel.DataAnnotations;

namespace Castr.Data.Entities;

public class Feed
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [Required, MaxLength(1000)]
    public string Directory { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Author { get; set; }

    [MaxLength(1000)]
    public string? ImageUrl { get; set; }

    [MaxLength(1000)]
    public string? Link { get; set; }

    [MaxLength(10)]
    public string Language { get; set; } = "en-us";

    [MaxLength(100)]
    public string? Category { get; set; }

    public string[] FileExtensions { get; set; } = [".mp3"];

    [Range(0, 4)]
    public int DirectorySearchDepth { get; set; } = 0;

    public int CacheDurationMinutes { get; set; } = 5;

    [MaxLength(500)]
    public string? YouTubePlaylistUrl { get; set; }

    public int YouTubePollIntervalMinutes { get; set; } = 60;

    public bool YouTubeEnabled { get; set; }

    public int YouTubeMaxConcurrentDownloads { get; set; } = 1;

    [MaxLength(20)]
    public string YouTubeAudioQuality { get; set; } = "highest";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;

    public ICollection<Episode> Episodes { get; set; } = new List<Episode>();
    public ICollection<DownloadedVideo> DownloadedVideos { get; set; } = new List<DownloadedVideo>();
    public ICollection<ActivityLog> ActivityLogs { get; set; } = new List<ActivityLog>();
    public ICollection<DownloadQueueItem> DownloadQueue { get; set; } = new List<DownloadQueueItem>();
}
