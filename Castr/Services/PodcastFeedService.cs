using System.Xml.Linq;
using Microsoft.Extensions.Options;
using Castr.Models;
using TagLib;

namespace Castr.Services;

public class PodcastFeedService
{
    private readonly PodcastFeedsConfig _config;
    private readonly IPodcastDatabaseService _database;
    private readonly ILogger<PodcastFeedService> _logger;

    private static readonly XNamespace Itunes = "http://www.itunes.com/dtds/podcast-1.0.dtd";

    public PodcastFeedService(
        IOptions<PodcastFeedsConfig> config,
        IPodcastDatabaseService database,
        ILogger<PodcastFeedService> logger)
    {
        _config = config.Value;
        _database = database;
        _logger = logger;
    }

    public IEnumerable<string> GetFeedNames() => _config.Feeds.Keys;

    public bool FeedExists(string feedName) => _config.Feeds.ContainsKey(feedName);

    public async Task<string?> GenerateFeedAsync(string feedName, string baseUrl)
    {
        if (!_config.Feeds.TryGetValue(feedName, out var feedConfig))
        {
            return null;
        }

        return await GenerateFeedXmlAsync(feedName, feedConfig, baseUrl);
    }

    private async Task<string> GenerateFeedXmlAsync(string feedName, PodcastFeedConfig config, string baseUrl)
    {
        _logger.LogDebug("Generating RSS XML for feed {FeedName} with base URL {BaseUrl}", feedName, baseUrl);
        var episodes = await GetEpisodesAsync(feedName, config);
        _logger.LogDebug("Found {Count} episodes for feed {FeedName}", episodes.Count, feedName);

        var channel = new XElement("channel",
            new XElement("title", config.Title),
            new XElement("description", config.Description),
            new XElement("link", config.Link ?? baseUrl),
            new XElement("language", config.Language),
            new XElement("generator", "PodcastFeedApi"),
            new XElement("lastBuildDate", DateTime.UtcNow.ToString("R")),
            new XElement(Itunes + "author", config.Author ?? config.Title),
            new XElement(Itunes + "summary", config.Description),
            new XElement(Itunes + "explicit", "no")
        );

        if (!string.IsNullOrEmpty(config.ImageUrl))
        {
            channel.Add(new XElement("image",
                new XElement("url", config.ImageUrl),
                new XElement("title", config.Title),
                new XElement("link", config.Link ?? baseUrl)
            ));
            channel.Add(new XElement(Itunes + "image", new XAttribute("href", config.ImageUrl)));
        }

        if (!string.IsNullOrEmpty(config.Category))
        {
            channel.Add(new XElement(Itunes + "category", new XAttribute("text", config.Category)));
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

    private async Task<List<EpisodeInfo>> GetEpisodesAsync(string feedName, PodcastFeedConfig config)
    {
        _logger.LogDebug("Scanning directory for episodes: {Directory}", config.Directory);
        var episodes = new List<EpisodeInfo>();
        var extensions = config.FileExtensions ?? [".mp3"];
        _logger.LogDebug("Looking for file extensions: {Extensions}", string.Join(", ", extensions));

        if (!System.IO.Directory.Exists(config.Directory))
        {
            _logger.LogWarning("Directory not found: {Directory}", config.Directory);
            return episodes;
        }

        var files = System.IO.Directory.GetFiles(config.Directory)
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

        return await SortEpisodesAsync(feedName, episodes);
    }

    private async Task<List<EpisodeInfo>> SortEpisodesAsync(string feedName, List<EpisodeInfo> episodes)
    {
        // Get episode order from database
        var dbEpisodes = await _database.GetEpisodesAsync(feedName);
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

    public string? GetMediaFilePath(string feedName, string fileName)
    {
        if (!_config.Feeds.TryGetValue(feedName, out var feedConfig))
        {
            return null;
        }

        var filePath = Path.Combine(feedConfig.Directory, fileName);

        if (!System.IO.File.Exists(filePath))
        {
            return null;
        }

        // Security check: ensure the resolved path is within the configured directory
        var fullPath = Path.GetFullPath(filePath);
        var directoryPath = Path.GetFullPath(feedConfig.Directory);

        if (!fullPath.StartsWith(directoryPath, StringComparison.OrdinalIgnoreCase))
        {
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
