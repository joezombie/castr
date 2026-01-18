namespace Castr.Models;

public class FeedRecord
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public required string Directory { get; set; }
    public string? Author { get; set; }
    public string? ImageUrl { get; set; }
    public string? Link { get; set; }
    public string Language { get; set; } = "en-us";
    public string? Category { get; set; }
    public string FileExtensions { get; set; } = ".mp3";
    public string? YoutubePlaylistUrl { get; set; }
    public int YoutubePollIntervalMinutes { get; set; } = 60;
    public bool YoutubeEnabled { get; set; }
    public int YoutubeMaxConcurrentDownloads { get; set; } = 1;
    public string YoutubeAudioQuality { get; set; } = "highest";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsActive { get; set; } = true;
}
