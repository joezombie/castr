namespace Castr.Models;

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
    /// Path to SQLite database for episode tracking and ordering.
    /// If not specified, defaults to {Directory}/podcast.db
    /// </summary>
    public string? DatabasePath { get; set; }

    /// <summary>
    /// YouTube playlist configuration for automatic downloading.
    /// </summary>
    public YouTubePlaylistConfig? YouTube { get; set; }
}

public class YouTubePlaylistConfig
{
    /// <summary>
    /// YouTube playlist URL or ID (e.g., "PLxxxxxxxxx" or full URL).
    /// </summary>
    public required string PlaylistUrl { get; set; }

    /// <summary>
    /// Poll interval in minutes. Default: 60 (1 hour).
    /// </summary>
    public int PollIntervalMinutes { get; set; } = 60;

    /// <summary>
    /// Whether playlist watching is enabled. Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Maximum concurrent downloads. Default: 1.
    /// </summary>
    public int MaxConcurrentDownloads { get; set; } = 1;

    /// <summary>
    /// Audio quality preference: "highest", "lowest", or bitrate like "128".
    /// </summary>
    public string AudioQuality { get; set; } = "highest";
}
