using System.Xml.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Castr.Data.Entities;
using TagLib;

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
        var episodes = await GetEpisodesAsync(feedName, feed);
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

        if (!string.IsNullOrEmpty(feed.ImageUrl))
        {
            channel.Add(new XElement("image",
                new XElement("url", feed.ImageUrl),
                new XElement("title", feed.Title),
                new XElement("link", feed.Link ?? baseUrl)
            ));
            channel.Add(new XElement(Itunes + "image", new XAttribute("href", feed.ImageUrl)));
        }

        if (!string.IsNullOrEmpty(feed.Category))
        {
            channel.Add(new XElement(Itunes + "category", new XAttribute("text", feed.Category)));
        }

        foreach (var episode in episodes)
        {
            var enclosureUrl = $"{baseUrl.TrimEnd('/')}/feed/{feedName}/media/{Uri.EscapeDataString(episode.FileName)}";

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

            // Add episode thumbnail if available
            if (!string.IsNullOrWhiteSpace(episode.ThumbnailUrl))
            {
                item.Add(new XElement(Itunes + "image", new XAttribute("href", episode.ThumbnailUrl)));
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

    private async Task<List<EpisodeInfo>> GetEpisodesAsync(string feedName, Feed feed)
    {
        _logger.LogDebug("Scanning directory for episodes: {Directory}", feed.Directory);
        var episodes = new List<EpisodeInfo>();
        var extensions = feed.FileExtensions;
        _logger.LogDebug("Looking for file extensions: {Extensions}", string.Join(", ", extensions));

        if (!System.IO.Directory.Exists(feed.Directory))
        {
            _logger.LogWarning("Directory not found: {Directory}", feed.Directory);
            return episodes;
        }

        var files = System.IO.Directory.GetFiles(feed.Directory)
            .Where(f => extensions.Any(ext => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        _logger.LogDebug("Found {Count} media files in directory", files.Count);

        foreach (var filePath in files)
        {
            try
            {
                _logger.LogTrace("Processing file: {FilePath}", filePath);
                var fileInfo = new FileInfo(filePath);
                var episode = new EpisodeInfo
                {
                    FileName = fileInfo.Name,
                    FilePath = filePath,
                    FileSize = fileInfo.Length,
                    PublishDate = fileInfo.LastWriteTimeUtc
                };

                // Try to read ID3 tags for additional metadata
                try
                {
                    using var tagFile = TagLib.File.Create(filePath);
                    episode.Title = !string.IsNullOrWhiteSpace(tagFile.Tag.Title)
                        ? tagFile.Tag.Title
                        : Path.GetFileNameWithoutExtension(fileInfo.Name);
                    episode.Description = tagFile.Tag.Comment;
                    episode.Duration = tagFile.Properties.Duration;
                    _logger.LogTrace("Read ID3 tags from {FileName}: title='{Title}', duration={Duration}",
                        fileInfo.Name, episode.Title, episode.Duration);
                }
                catch (Exception tagEx)
                {
                    _logger.LogDebug(tagEx, "Could not read ID3 tags from {FileName}, using filename", fileInfo.Name);
                    episode.Title = Path.GetFileNameWithoutExtension(fileInfo.Name);
                    episode.Duration = TimeSpan.Zero;
                }

                episodes.Add(episode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing file: {FilePath}", filePath);
            }
        }

        return await SortEpisodesAsync(feed, episodes);
    }

    private async Task<List<EpisodeInfo>> SortEpisodesAsync(Feed feed, List<EpisodeInfo> episodes)
    {
        // Get episode order from database
        var dbEpisodes = await _dataService.GetEpisodesAsync(feed.Id);
        var episodeMap = dbEpisodes
            .ToDictionary(e => e.Filename, e => e, StringComparer.OrdinalIgnoreCase);

        if (episodeMap.Count == 0)
        {
            // No database entries - sort by filename (alphabetical)
            _logger.LogDebug("No episodes in database, using alphabetical order");
            return episodes.OrderBy(e => e.FileName).ToList();
        }

        _logger.LogDebug("Loaded {Count} episodes from database", episodeMap.Count);

        // Apply YouTube metadata from database where available
        var youtubeMetadataApplied = 0;
        foreach (var episode in episodes)
        {
            if (episodeMap.TryGetValue(episode.FileName, out var dbEpisode))
            {
                var hasMetadata = false;
                if (dbEpisode.PublishDate.HasValue)
                {
                    episode.PublishDate = dbEpisode.PublishDate.Value;
                    hasMetadata = true;
                }
                if (!string.IsNullOrWhiteSpace(dbEpisode.Description))
                {
                    episode.Description = dbEpisode.Description;
                    hasMetadata = true;
                }
                if (!string.IsNullOrWhiteSpace(dbEpisode.VideoId))
                {
                    episode.VideoId = dbEpisode.VideoId;
                    hasMetadata = true;
                }
                if (!string.IsNullOrWhiteSpace(dbEpisode.ThumbnailUrl))
                {
                    episode.ThumbnailUrl = dbEpisode.ThumbnailUrl;
                    hasMetadata = true;
                }
                if (hasMetadata)
                {
                    youtubeMetadataApplied++;
                    _logger.LogTrace("Applied YouTube metadata to {FileName}", episode.FileName);
                }
            }
        }
        if (youtubeMetadataApplied > 0)
        {
            _logger.LogDebug("Applied YouTube metadata to {Count} episodes", youtubeMetadataApplied);
        }

        // Split into mapped and unmapped episodes
        var mapped = episodes.Where(e => episodeMap.ContainsKey(e.FileName)).ToList();
        var unmapped = episodes.Where(e => !episodeMap.ContainsKey(e.FileName)).ToList();

        _logger.LogDebug("Sorting episodes: {Mapped} mapped in database, {Unmapped} unmapped",
            mapped.Count, unmapped.Count);

        // Sort mapped by database order, unmapped by date (newest first)
        var sortedMapped = mapped.OrderBy(e => episodeMap[e.FileName].DisplayOrder).ToList();
        var sortedUnmapped = unmapped.OrderByDescending(e => e.PublishDate).ToList();

        // Unmapped (new) files go to the top
        sortedUnmapped.AddRange(sortedMapped);
        _logger.LogDebug("Final episode order: {Total} episodes ({Unmapped} new, {Mapped} tracked)",
            sortedUnmapped.Count, unmapped.Count, mapped.Count);
        return sortedUnmapped;
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
        var fullPath = Path.GetFullPath(filePath);
        var directoryPath = Path.GetFullPath(feed.Directory);

        if (!fullPath.StartsWith(directoryPath, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Path traversal attempt detected for feed {FeedName}: {FileName}", feedName, fileName);
            return null;
        }

        return fullPath;
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
        public required string FilePath { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? VideoId { get; set; }
        public string? ThumbnailUrl { get; set; }
        public long FileSize { get; set; }
        public DateTime PublishDate { get; set; }
        public TimeSpan Duration { get; set; }
    }
}
