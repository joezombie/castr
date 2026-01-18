namespace Castr.Models;

/// <summary>
/// Represents a podcast feed stored in the central database.
/// </summary>
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
    
    // YouTube configuration
    public string? YoutubePlaylistUrl { get; set; }
    public int YoutubePollinIntervalMinutes { get; set; } = 60;
    public bool YoutubeEnabled { get; set; } = false;
    public int YoutubeMaxConcurrentDownloads { get; set; } = 1;
    public string YoutubeAudioQuality { get; set; } = "highest";
    
    // Timestamps
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsActive { get; set; } = true;
    
    // Helper property to get file extensions as array
    public string[] GetFileExtensions() => 
        FileExtensions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    
    // Helper method to set file extensions from array
    public void SetFileExtensions(string[] extensions) => 
        FileExtensions = string.Join(",", extensions);
}
