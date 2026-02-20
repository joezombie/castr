using System.Xml.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Castr.Data.Entities;

namespace Castr.Services;

public class PodcastFeedService
{
    private readonly IPodcastDataService _dataService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PodcastFeedService> _logger;

    private static readonly XNamespace Itunes = "http://www.itunes.com/dtds/podcast-1.0.dtd";

    public PodcastFeedService(
        IPodcastDataService dataService,
        IMemoryCache cache,
        ILogger<PodcastFeedService> logger)
    {
        _dataService = dataService;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IEnumerable<string>> GetFeedNamesAsync()
    {
        var feeds = await _dataService.GetAllFeedsAsync();
        return feeds.Select(f => f.Name);
    }

    public async Task<bool> FeedExistsAsync(string feedName)
    {
        var feed = await _dataService.GetFeedByNameAsync(feedName);
        return feed != null;
    }

    public async Task<string?> GenerateFeedAsync(string feedName, string baseUrl)
    {
        var feed = await _dataService.GetFeedByNameAsync(feedName);
        if (feed == null)
        {
            return null;
        }

        // Generate cache key using hash to handle special characters in feed name and base URL
        var cacheKey = GenerateCacheKey(feedName, baseUrl);

        // Try to get cached feed
        if (_cache.TryGetValue<string>(cacheKey, out var cachedFeed))
        {
            _logger.LogDebug("Cache hit for feed {FeedName}", feedName);
            return cachedFeed;
        }

        _logger.LogDebug("Cache miss for feed {FeedName}, generating new feed", feedName);

        // Generate new feed XML
        var feedXml = await GenerateFeedXmlAsync(feedName, feed, baseUrl);

        // Only cache valid, non-empty feeds
        if (!string.IsNullOrWhiteSpace(feedXml))
        {
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(feed.CacheDurationMinutes)
            };

            _cache.Set(cacheKey, feedXml, cacheOptions);
            _logger.LogDebug("Cached feed {FeedName} for {Minutes} minutes", feedName, feed.CacheDurationMinutes);
        }
        else
        {
            _logger.LogWarning("Feed {FeedName} generated empty or null content, not caching", feedName);
        }

        return feedXml;
    }

    private static string GenerateCacheKey(string feedName, string baseUrl)
    {
        // Use SHA256 hash to create a consistent cache key that handles special characters
        var input = $"{feedName}|{baseUrl}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return $"feed_{Convert.ToHexString(hashBytes)}";
    }

    private async Task<string> GenerateFeedXmlAsync(string feedName, Feed feed, string baseUrl)
    {
        _logger.LogDebug("Generating RSS XML for feed {FeedName} with base URL {BaseUrl}", feedName, baseUrl);
        var episodes = await GetEpisodesAsync(feed);
        _logger.LogDebug("Found {Count} episodes for feed {FeedName}", episodes.Count, feedName);

        var channel = new XElement("channel",
            new XElement("title", feed.Title),
            new XElement("description", feed.Description),
            new XElement("link", feed.Link ?? baseUrl),
            new XElement("language", feed.Language),
            new XElement("generator", "PodcastFeedApi"),
            new XElement("lastBuildDate", DateTime.UtcNow.ToString("R")),
            new XElement(Itunes + "author", feed.Author ?? feed.Title),
            new XElement(Itunes + "summary", feed.Description),
            new XElement(Itunes + "explicit", "no")
        );

        // Determine effective image URL: use feed's own, or fall back to latest episode's thumbnail
        var imageUrl = feed.ImageUrl;
        if (string.IsNullOrEmpty(imageUrl))
        {
            var latestWithThumbnail = episodes
                .OrderByDescending(e => e.PublishDate)
                .FirstOrDefault(e => !string.IsNullOrWhiteSpace(e.ThumbnailUrl));
            if (latestWithThumbnail != null)
            {
                imageUrl = latestWithThumbnail.ThumbnailUrl!; // non-null guaranteed by predicate
                _logger.LogInformation("Feed {FeedName} has no image URL, using latest episode thumbnail as fallback", feedName);
            }
            else
            {
                var latestWithArt = episodes
                    .OrderByDescending(e => e.PublishDate)
                    .FirstOrDefault(e => e.HasEmbeddedArt);
                if (latestWithArt != null)
                {
                    var encodedPath = string.Join("/", latestWithArt.FileName.Split('/').Select(Uri.EscapeDataString));
                    imageUrl = $"{baseUrl.TrimEnd('/')}/feed/{Uri.EscapeDataString(feedName)}/artwork/{encodedPath}";
                    _logger.LogInformation("Feed {FeedName} has no image URL, using latest episode embedded art as fallback", feedName);
                }
                else
                {
                    _logger.LogWarning("Feed {FeedName} has no configured image URL and no episodes with thumbnail or embedded art; channel will have no image", feedName);
                }
            }
        }

        if (!string.IsNullOrEmpty(imageUrl))
        {
            channel.Add(new XElement("image",
                new XElement("url", imageUrl),
                new XElement("title", feed.Title),
                new XElement("link", feed.Link ?? baseUrl)
            ));
            channel.Add(new XElement(Itunes + "image", new XAttribute("href", imageUrl)));
        }

        if (!string.IsNullOrEmpty(feed.Category))
        {
            channel.Add(new XElement(Itunes + "category", new XAttribute("text", feed.Category)));
        }

        foreach (var episode in episodes)
        {
            var encodedPath = string.Join("/", episode.FileName.Split('/').Select(Uri.EscapeDataString));
            var enclosureUrl = $"{baseUrl.TrimEnd('/')}/feed/{feedName}/media/{encodedPath}";

            // Build description with YouTube link if available
            var description = episode.Description ?? episode.Title;
            if (!string.IsNullOrWhiteSpace(episode.VideoId))
            {
                var videoUrl = $"https://www.youtube.com/watch?v={episode.VideoId}";
                description = $"{description}\n\nWatch on YouTube: {videoUrl}";
            }

            var item = new XElement("item",
                new XElement("title", episode.Title),
                new XElement("description", description),
                new XElement("pubDate", episode.PublishDate.ToString("R")),
                new XElement("enclosure",
                    new XAttribute("url", enclosureUrl),
                    new XAttribute("length", episode.FileSize),
                    new XAttribute("type", GetMimeType(episode.FileName))
                ),
                new XElement("guid", new XAttribute("isPermaLink", "false"), episode.FileName),
                new XElement(Itunes + "duration", FormatDuration(episode.Duration)),
                new XElement(Itunes + "explicit", "no")
            );

            // Add episode thumbnail if available, falling back to embedded artwork
            if (!string.IsNullOrWhiteSpace(episode.ThumbnailUrl))
            {
                item.Add(new XElement(Itunes + "image", new XAttribute("href", episode.ThumbnailUrl)));
                _logger.LogDebug("Episode {FileName} using thumbnail URL for artwork", episode.FileName);
            }
            else if (episode.HasEmbeddedArt)
            {
                var artworkUrl = $"{baseUrl.TrimEnd('/')}/feed/{feedName}/artwork/{encodedPath}";
                item.Add(new XElement(Itunes + "image", new XAttribute("href", artworkUrl)));
                _logger.LogDebug("Episode {FileName} using embedded artwork URL (no thumbnail available)", episode.FileName);
            }

            if (!string.IsNullOrWhiteSpace(episode.Artist))
            {
                item.Add(new XElement(Itunes + "author", episode.Artist));
            }

            if (!string.IsNullOrWhiteSpace(episode.Subtitle))
            {
                item.Add(new XElement(Itunes + "subtitle", episode.Subtitle));
            }

            channel.Add(item);
        }

        var rss = new XElement("rss",
            new XAttribute("version", "2.0"),
            new XAttribute(XNamespace.Xmlns + "itunes", Itunes),
            channel
        );

        var doc = new XDocument(new XDeclaration("1.0", "UTF-8", null), rss);
        return doc.ToString();
    }

    private async Task<List<EpisodeInfo>> GetEpisodesAsync(Feed feed)
    {
        var dbEpisodes = await _dataService.GetEpisodesAsync(feed.Id);

        var episodes = new List<EpisodeInfo>();
        foreach (var ep in dbEpisodes)
        {
            try
            {
                var filePath = Path.Combine(feed.Directory, ep.Filename);
                if (!System.IO.File.Exists(filePath))
                {
                    _logger.LogWarning("Skipping episode {Filename} for feed {FeedId}: file not found on disk",
                        ep.Filename, feed.Id);
                    continue;
                }

                var needsFallback = ep.FileSize == null || ep.PublishDate == null;
                var fileInfo = needsFallback ? new FileInfo(filePath) : null;

                episodes.Add(new EpisodeInfo
                {
                    FileName = ep.Filename,
                    Title = ep.Title ?? Path.GetFileNameWithoutExtension(ep.Filename),
                    Description = ep.Description,
                    VideoId = ep.VideoId,
                    ThumbnailUrl = ep.ThumbnailUrl,
                    Artist = ep.Artist,
                    Subtitle = ep.Subtitle,
                    HasEmbeddedArt = ep.HasEmbeddedArt,
                    FileSize = ep.FileSize ?? fileInfo!.Length,
                    PublishDate = ep.PublishDate ?? fileInfo!.LastWriteTimeUtc,
                    Duration = ep.Duration,
                    DisplayOrder = ep.DisplayOrder
                });
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "I/O error reading file info for episode {Filename}, skipping from feed", ep.Filename);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Permission denied reading file info for episode {Filename}, skipping from feed", ep.Filename);
            }
        }

        return episodes.OrderBy(e => e.DisplayOrder).ToList();
    }

    public async Task<string?> GetMediaFilePathAsync(string feedName, string fileName)
    {
        var feed = await _dataService.GetFeedByNameAsync(feedName);
        if (feed == null)
        {
            return null;
        }

        var filePath = Path.Combine(feed.Directory, fileName);

        if (!System.IO.File.Exists(filePath))
        {
            return null;
        }

        // Security check: ensure the resolved path is within the configured directory
        try
        {
            var fullPath = Path.GetFullPath(filePath);
            var directoryPath = Path.GetFullPath(feed.Directory);

            if (!fullPath.StartsWith(directoryPath, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Path traversal attempt detected for feed {FeedName}: {FileName}", feedName, fileName);
                return null;
            }

            return fullPath;
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid path configuration for feed {FeedName}: directory={Directory}", feedName, feed.Directory);
            return null;
        }
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

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
        {
            return $"{(int)duration.TotalHours}:{duration.Minutes:D2}:{duration.Seconds:D2}";
        }
        return $"{duration.Minutes}:{duration.Seconds:D2}";
    }

    private class EpisodeInfo
    {
        public required string FileName { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? VideoId { get; set; }
        public string? ThumbnailUrl { get; set; }
        public string? Artist { get; set; }
        public string? Subtitle { get; set; }
        public bool HasEmbeddedArt { get; set; }
        public long FileSize { get; set; }
        public DateTime PublishDate { get; set; }
        public TimeSpan Duration { get; set; }
        public int DisplayOrder { get; set; }
    }
}
