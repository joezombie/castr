using Microsoft.AspNetCore.Mvc;
using Castr.Services;

namespace Castr.Controllers;

[ApiController]
[Route("feed")]
public class FeedController : ControllerBase
{
    private readonly PodcastFeedService _feedService;
    private readonly ILogger<FeedController> _logger;

    public FeedController(PodcastFeedService feedService, ILogger<FeedController> logger)
    {
        _feedService = feedService;
        _logger = logger;
    }

    /// <summary>
    /// List all available podcast feeds
    /// </summary>
    [HttpGet]
    public IActionResult GetFeeds()
    {
        _logger.LogDebug("Listing all available feeds");
        var feeds = _feedService.GetFeedNames()
            .Select(name => new
            {
                Name = name,
                FeedUrl = $"{GetBaseUrl()}/feed/{name}"
            })
            .ToList();

        _logger.LogInformation("Returning {Count} feeds", feeds.Count);
        return Ok(feeds);
    }

    /// <summary>
    /// Get the RSS feed for a specific podcast
    /// </summary>
    [HttpGet("{feedName}")]
    [Produces("application/rss+xml")]
    public async Task<IActionResult> GetFeed(string feedName)
    {
        _logger.LogDebug("Generating RSS feed for {FeedName}", feedName);

        if (!_feedService.FeedExists(feedName))
        {
            _logger.LogWarning("Feed {FeedName} not found", feedName);
            return NotFound(new { error = $"Feed '{feedName}' not found" });
        }

        var baseUrl = GetBaseUrl();
        _logger.LogDebug("Using base URL: {BaseUrl}", baseUrl);

        var feedXml = await _feedService.GenerateFeedAsync(feedName, baseUrl);

        if (feedXml == null)
        {
            _logger.LogWarning("Failed to generate feed for {FeedName}", feedName);
            return NotFound(new { error = $"Feed '{feedName}' not found" });
        }

        _logger.LogInformation("Successfully generated RSS feed for {FeedName}, size: {Size} bytes",
            feedName, feedXml.Length);
        return Content(feedXml, "application/rss+xml; charset=utf-8");
    }

    /// <summary>
    /// Serve media files for a podcast episode
    /// </summary>
    [HttpGet("{feedName}/media/{fileName}")]
    public IActionResult GetMedia(string feedName, string fileName)
    {
        _logger.LogDebug("Serving media file {FileName} for feed {FeedName}", fileName, feedName);

        var filePath = _feedService.GetMediaFilePath(feedName, fileName);

        if (filePath == null)
        {
            _logger.LogWarning("Media file {FileName} not found for feed {FeedName}", fileName, feedName);
            return NotFound(new { error = "Media file not found" });
        }

        var fileInfo = new FileInfo(filePath);
        var mimeType = GetMimeType(fileName);

        _logger.LogInformation("Streaming media file {FileName} ({Size} bytes, {MimeType})",
            fileName, fileInfo.Length, mimeType);

        return PhysicalFile(filePath, mimeType, enableRangeProcessing: true);
    }

    private string GetBaseUrl()
    {
        var request = HttpContext.Request;
        // Uses forwarded headers (X-Forwarded-Proto, X-Forwarded-Host) when behind reverse proxy
        var baseUrl = $"{request.Scheme}://{request.Host}";
        _logger.LogDebug("Generated base URL: {BaseUrl} (Scheme: {Scheme}, Host: {Host})",
            baseUrl, request.Scheme, request.Host);
        return baseUrl;
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
