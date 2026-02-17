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
    public async Task<IActionResult> GetFeeds()
    {
        _logger.LogDebug("Listing all available feeds");
        var feedNames = await _feedService.GetFeedNamesAsync();
        var feeds = feedNames
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
        // Input validation
        if (string.IsNullOrWhiteSpace(feedName) || feedName.Length > 100)
            return BadRequest("Feed name cannot be empty or exceed 100 characters");

        _logger.LogDebug("Generating RSS feed for {FeedName}", feedName);

        var baseUrl = GetBaseUrl();
        _logger.LogDebug("Using base URL: {BaseUrl}", baseUrl);

        var feedXml = await _feedService.GenerateFeedAsync(feedName, baseUrl);

        if (feedXml == null)
        {
            _logger.LogWarning("Feed {FeedName} not found", feedName);
            return NotFound(new { error = $"Feed '{feedName}' not found" });
        }

        _logger.LogInformation("Successfully generated RSS feed for {FeedName}, size: {Size} bytes",
            feedName, feedXml.Length);
        return Content(feedXml, "application/rss+xml; charset=utf-8");
    }

    /// <summary>
    /// Serve media files for a podcast episode
    /// </summary>
    [HttpGet("{feedName}/media/{**filePath}")]
    public async Task<IActionResult> GetMedia(string feedName, string filePath)
    {
        // Input validation
        if (string.IsNullOrWhiteSpace(feedName) || feedName.Length > 100)
            return BadRequest("Feed name cannot be empty or exceed 100 characters");

        if (string.IsNullOrWhiteSpace(filePath) || filePath.Length > 500)
            return BadRequest("File path cannot be empty or exceed 500 characters");

        if (filePath.Contains("..") || filePath.Contains("\\"))
            return BadRequest("File path contains invalid characters or path traversal patterns");

        _logger.LogDebug("Serving media file {FilePath} for feed {FeedName}", filePath, feedName);

        var resolvedPath = await _feedService.GetMediaFilePathAsync(feedName, filePath);

        if (resolvedPath == null)
        {
            _logger.LogWarning("Media file {FilePath} not found for feed {FeedName}", filePath, feedName);
            return NotFound(new { error = "Media file not found" });
        }

        var fileInfo = new FileInfo(resolvedPath);
        var mimeType = GetMimeType(filePath);

        _logger.LogInformation("Streaming media file {FilePath} ({Size} bytes, {MimeType})",
            filePath, fileInfo.Length, mimeType);

        return PhysicalFile(resolvedPath, mimeType, enableRangeProcessing: true);
    }

    /// <summary>
    /// Serve embedded album art from a media file
    /// </summary>
    [HttpGet("{feedName}/artwork/{**filePath}")]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> GetArtwork(string feedName, string filePath)
    {
        if (string.IsNullOrWhiteSpace(feedName) || feedName.Length > 100)
            return BadRequest("Feed name cannot be empty or exceed 100 characters");

        if (string.IsNullOrWhiteSpace(filePath) || filePath.Length > 500)
            return BadRequest("File path cannot be empty or exceed 500 characters");

        if (filePath.Contains("..") || filePath.Contains("\\"))
            return BadRequest("File path contains invalid characters or path traversal patterns");

        _logger.LogDebug("Extracting embedded artwork from {FilePath} for feed {FeedName}", filePath, feedName);

        var resolvedPath = await _feedService.GetMediaFilePathAsync(feedName, filePath);
        if (resolvedPath == null)
        {
            _logger.LogWarning("Artwork request rejected for {FilePath} in feed {FeedName}: media file not found or not permitted", filePath, feedName);
            return NotFound(new { error = "Media file not found" });
        }

        try
        {
            using var tagFile = TagLib.File.Create(resolvedPath);
            if (tagFile.Tag.Pictures == null || tagFile.Tag.Pictures.Length == 0)
            {
                _logger.LogDebug("No embedded artwork in {FilePath} for feed {FeedName}", filePath, feedName);
                return NotFound(new { error = "No embedded artwork found" });
            }

            var picture = tagFile.Tag.Pictures[0];
            if (picture.Data == null || picture.Data.IsEmpty)
            {
                _logger.LogDebug("Embedded artwork in {FilePath} has null or empty data", filePath);
                return NotFound(new { error = "No embedded artwork found" });
            }

            var mimeType = !string.IsNullOrWhiteSpace(picture.MimeType)
                ? picture.MimeType
                : "image/jpeg";

            _logger.LogDebug("Serving embedded artwork ({MimeType}) from {FilePath} for feed {FeedName}", mimeType, filePath, feedName);
            return File(picture.Data.Data, mimeType);
        }
        catch (TagLib.CorruptFileException ex)
        {
            _logger.LogWarning(ex, "Corrupt or unreadable media file when extracting artwork: {FeedName}/{FilePath}", feedName, filePath);
            return NotFound(new { error = "Could not read media file" });
        }
        catch (TagLib.UnsupportedFormatException ex)
        {
            _logger.LogWarning(ex, "Unsupported format when extracting artwork: {FeedName}/{FilePath}", feedName, filePath);
            return NotFound(new { error = "Unsupported media format" });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "I/O error extracting artwork: {FeedName}/{FilePath}", feedName, filePath);
            return StatusCode(500, new { error = "Could not read media file due to a server error" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error extracting artwork from {FeedName}/{FilePath}", feedName, filePath);
            return StatusCode(500, new { error = "An unexpected error occurred reading the media file" });
        }
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
