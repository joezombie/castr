using System.Xml.Linq;
using Microsoft.Extensions.Options;
using PodcastFeedApi.Models;
using TagLib;

namespace PodcastFeedApi.Services;

public class PodcastFeedService
{
    private readonly PodcastFeedsConfig _config;
    private readonly ILogger<PodcastFeedService> _logger;

    private static readonly XNamespace Itunes = "http://www.itunes.com/dtds/podcast-1.0.dtd";

    public PodcastFeedService(IOptions<PodcastFeedsConfig> config, ILogger<PodcastFeedService> logger)
    {
        _config = config.Value;
        _logger = logger;
    }

    public IEnumerable<string> GetFeedNames() => _config.Feeds.Keys;

    public bool FeedExists(string feedName) => _config.Feeds.ContainsKey(feedName);

    public string? GenerateFeed(string feedName, string baseUrl)
    {
        if (!_config.Feeds.TryGetValue(feedName, out var feedConfig))
        {
            return null;
        }

        return GenerateFeedXml(feedName, feedConfig, baseUrl);
    }

    private string GenerateFeedXml(string feedName, PodcastFeedConfig config, string baseUrl)
    {
        var episodes = GetEpisodes(config);

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

            var item = new XElement("item",
                new XElement("title", episode.Title),
                new XElement("description", episode.Description ?? episode.Title),
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

    private List<EpisodeInfo> GetEpisodes(PodcastFeedConfig config)
    {
        var episodes = new List<EpisodeInfo>();
        var extensions = config.FileExtensions ?? [".mp3"];

        if (!System.IO.Directory.Exists(config.Directory))
        {
            _logger.LogWarning("Directory not found: {Directory}", config.Directory);
            return episodes;
        }

        var files = System.IO.Directory.GetFiles(config.Directory)
            .Where(f => extensions.Any(ext => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)));

        foreach (var filePath in files)
        {
            try
            {
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
                }
                catch
                {
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

        return SortEpisodes(episodes, config.MapFile);
    }

    private List<EpisodeInfo> SortEpisodes(List<EpisodeInfo> episodes, string? mapFilePath)
    {
        // Load map file if specified
        var orderMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrEmpty(mapFilePath) && System.IO.File.Exists(mapFilePath))
        {
            try
            {
                var lines = System.IO.File.ReadAllLines(mapFilePath);
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i].Trim();
                    if (!string.IsNullOrEmpty(line))
                    {
                        orderMap[line] = i;
                    }
                }
                _logger.LogInformation("Loaded map file with {Count} entries", orderMap.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading map file: {MapFile}", mapFilePath);
            }
        }

        if (orderMap.Count == 0)
        {
            // No map file - sort by filename (alphabetical)
            return episodes.OrderBy(e => e.FileName).ToList();
        }

        // Split into mapped and unmapped episodes
        var mapped = episodes.Where(e => orderMap.ContainsKey(e.FileName)).ToList();
        var unmapped = episodes.Where(e => !orderMap.ContainsKey(e.FileName)).ToList();

        // Sort mapped by map order, unmapped by date (newest first)
        var sortedMapped = mapped.OrderBy(e => orderMap[e.FileName]).ToList();
        var sortedUnmapped = unmapped.OrderByDescending(e => e.PublishDate).ToList();

        // Unmapped (new) files go to the top
        sortedUnmapped.AddRange(sortedMapped);
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
        public long FileSize { get; set; }
        public DateTime PublishDate { get; set; }
        public TimeSpan Duration { get; set; }
    }
}
