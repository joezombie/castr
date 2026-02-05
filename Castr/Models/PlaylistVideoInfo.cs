namespace Castr.Models;

/// <summary>
/// Represents metadata for a video from a YouTube playlist.
/// Used during playlist sync to match videos to local files.
/// </summary>
public class PlaylistVideoInfo
{
    public required string VideoId { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public string? ThumbnailUrl { get; set; }
    public DateTime? UploadDate { get; set; }
    public int PlaylistIndex { get; set; }
}
