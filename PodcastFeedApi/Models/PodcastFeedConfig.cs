namespace PodcastFeedApi.Models;

public class PodcastFeedsConfig
{
    public Dictionary<string, PodcastFeedConfig> Feeds { get; set; } = new();
}

public class PodcastFeedConfig
{
    public required string Title { get; set; }
    public required string Description { get; set; }
    public required string Directory { get; set; }
    public string? Author { get; set; }
    public string? ImageUrl { get; set; }
    public string? Link { get; set; }
    public string? Language { get; set; } = "en-us";
    public string? Category { get; set; }
    public string[]? FileExtensions { get; set; } = [".mp3"];
    /// <summary>
    /// Path to a map file that defines episode order (one filename per line).
    /// Files not in the map are added to the top (newest first).
    /// </summary>
    public string? MapFile { get; set; }
}
