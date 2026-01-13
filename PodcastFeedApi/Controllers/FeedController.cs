using Microsoft.AspNetCore.Mvc;
using PodcastFeedApi.Services;

namespace PodcastFeedApi.Controllers;

[ApiController]
[Route("feed")]
public class FeedController : ControllerBase
{
    private readonly PodcastFeedService _feedService;

    public FeedController(PodcastFeedService feedService)
    {
        _feedService = feedService;
    }

    /// <summary>
    /// List all available podcast feeds
    /// </summary>
    [HttpGet]
    public IActionResult GetFeeds()
    {
        var feeds = _feedService.GetFeedNames()
            .Select(name => new
            {
                Name = name,
                FeedUrl = $"{GetBaseUrl()}/feed/{name}"
            });

        return Ok(feeds);
    }

    /// <summary>
    /// Get the RSS feed for a specific podcast
    /// </summary>
    [HttpGet("{feedName}")]
    [Produces("application/rss+xml")]
    public IActionResult GetFeed(string feedName)
    {
        if (!_feedService.FeedExists(feedName))
        {
            return NotFound(new { error = $"Feed '{feedName}' not found" });
        }

        var baseUrl = GetBaseUrl();
        var feedXml = _feedService.GenerateFeed(feedName, baseUrl);

        if (feedXml == null)
        {
            return NotFound(new { error = $"Feed '{feedName}' not found" });
        }

        return Content(feedXml, "application/rss+xml; charset=utf-8");
    }

    /// <summary>
    /// Serve media files for a podcast episode
    /// </summary>
    [HttpGet("{feedName}/media/{fileName}")]
    public IActionResult GetMedia(string feedName, string fileName)
    {
        var filePath = _feedService.GetMediaFilePath(feedName, fileName);

        if (filePath == null)
        {
            return NotFound(new { error = "Media file not found" });
        }

        var mimeType = GetMimeType(fileName);
        return PhysicalFile(filePath, mimeType, enableRangeProcessing: true);
    }

    private string GetBaseUrl()
    {
        var request = HttpContext.Request;
        return $"{request.Scheme}://{request.Host}";
    }

    private static string GetMimeType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".mp3" => "audio/mpeg",
            ".m4a" => "audio/mp4",
            ".aac" => "audio/aac",
            ".ogg" => "audio/ogg",
            ".wav" => "audio/wav",
            ".flac" => "audio/flac",
            _ => "application/octet-stream"
        };
    }
}
