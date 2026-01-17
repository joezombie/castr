using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Converter;
using YoutubeExplode.Playlists;
using YoutubeExplode.Videos.Streams;
using System.Text.RegularExpressions;

namespace Castr.Services;

public interface IYouTubeDownloadService
{
    Task<IReadOnlyList<PlaylistVideo>> GetPlaylistVideosAsync(
        string playlistUrl,
        CancellationToken cancellationToken = default);

    Task<string?> DownloadAudioAsync(
        string videoId,
        string outputDirectory,
        string? preferredQuality = null,
        CancellationToken cancellationToken = default);

    Task<DateTime?> GetVideoUploadDateAsync(
        string videoId,
        CancellationToken cancellationToken = default);

    Task<VideoDetails?> GetVideoDetailsAsync(
        string videoId,
        CancellationToken cancellationToken = default);

    string? GetExistingFilePath(string videoTitle, string outputDirectory);
}

public class VideoDetails
{
    public required string VideoId { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public string? ThumbnailUrl { get; set; }
    public DateTime? UploadDate { get; set; }
}

public partial class YouTubeDownloadService : IYouTubeDownloadService
{
    private readonly YoutubeClient _youtube;
    private readonly ILogger<YouTubeDownloadService> _logger;

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    public YouTubeDownloadService(ILogger<YouTubeDownloadService> logger)
    {
        _youtube = new YoutubeClient();
        _logger = logger;
    }

    public async Task<IReadOnlyList<PlaylistVideo>> GetPlaylistVideosAsync(
        string playlistUrl,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching playlist videos from: {PlaylistUrl}", playlistUrl);
        try
        {
            var startTime = DateTime.UtcNow;
            var videos = await _youtube.Playlists
                .GetVideosAsync(playlistUrl, cancellationToken)
                .ToListAsync(cancellationToken);

            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogInformation("Successfully fetched {Count} videos from playlist in {ElapsedMs}ms",
                videos.Count, elapsed.TotalMilliseconds);

            return videos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch playlist: {PlaylistUrl}", playlistUrl);
            throw;
        }
    }

    public async Task<string?> DownloadAudioAsync(
        string videoId,
        string outputDirectory,
        string? preferredQuality = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting download for video: {VideoId}", videoId);
        try
        {
            _logger.LogDebug("Fetching video metadata for: {VideoId}", videoId);
            var video = await _youtube.Videos.GetAsync(videoId, cancellationToken);
            _logger.LogDebug("Video metadata retrieved: Title='{Title}', Duration={Duration}",
                video.Title, video.Duration);

            // Check for existing file using fuzzy matching
            _logger.LogDebug("Checking for existing file matching: {Title}", video.Title);
            var existingFile = GetExistingFilePath(video.Title, outputDirectory);
            if (existingFile != null)
            {
                _logger.LogInformation("File already exists, skipping download: {Path}", existingFile);
                return existingFile;
            }

            var safeTitle = SanitizeFileName(video.Title);
            var outputPath = Path.Combine(outputDirectory, $"{safeTitle}.mp3");

            _logger.LogInformation("Downloading video '{Title}' (ID: {VideoId}) to {Path}",
                video.Title, videoId, outputPath);

            var downloadStart = DateTime.UtcNow;
            
            // Add timeout to prevent indefinitely hung downloads
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromMinutes(30)); // 30 min timeout
            
            await _youtube.Videos.DownloadAsync(
                videoId,
                outputPath,
                o => o.SetContainer("mp3").SetPreset(ConversionPreset.Medium),
                cancellationToken: cts.Token);

            var downloadElapsed = DateTime.UtcNow - downloadStart;
            var fileInfo = new FileInfo(outputPath);
            _logger.LogInformation("Successfully downloaded '{Title}' ({Size:N0} bytes) in {ElapsedSec:N1}s",
                video.Title, fileInfo.Length, downloadElapsed.TotalSeconds);
            return outputPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download audio for video: {VideoId}", videoId);
            return null;
        }
    }

    public async Task<DateTime?> GetVideoUploadDateAsync(
        string videoId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var video = await _youtube.Videos.GetAsync(videoId, cancellationToken);
            return video.UploadDate.DateTime;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get upload date for video: {VideoId}", videoId);
            return null;
        }
    }

    public async Task<VideoDetails?> GetVideoDetailsAsync(
        string videoId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Fetching full details for video: {VideoId}", videoId);
        try
        {
            var video = await _youtube.Videos.GetAsync(videoId, cancellationToken);
            var thumbnailUrl = video.Thumbnails.OrderByDescending(t => t.Resolution.Width).FirstOrDefault()?.Url;

            _logger.LogDebug("Retrieved video details: Title='{Title}', UploadDate={UploadDate}, HasThumbnail={HasThumbnail}",
                video.Title, video.UploadDate.DateTime, thumbnailUrl != null);

            return new VideoDetails
            {
                VideoId = video.Id.Value,
                Title = video.Title,
                Description = video.Description,
                ThumbnailUrl = thumbnailUrl,
                UploadDate = video.UploadDate.DateTime
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get details for video: {VideoId}", videoId);
            return null;
        }
    }

    public string? GetExistingFilePath(string videoTitle, string outputDirectory)
    {
        _logger.LogDebug("Checking for existing file matching video title: {Title}", videoTitle);

        // First check for exact match
        var safeTitle = SanitizeFileName(videoTitle);
        var exactPath = Path.Combine(outputDirectory, $"{safeTitle}.mp3");
        if (File.Exists(exactPath))
        {
            _logger.LogDebug("Found exact match: {Path}", exactPath);
            return exactPath;
        }

        // Fuzzy match against existing files
        if (!Directory.Exists(outputDirectory))
        {
            _logger.LogDebug("Output directory does not exist: {Directory}", outputDirectory);
            return null;
        }

        var mp3Files = Directory.GetFiles(outputDirectory, "*.mp3");
        if (mp3Files.Length == 0)
        {
            _logger.LogDebug("No MP3 files found in directory: {Directory}", outputDirectory);
            return null;
        }

        _logger.LogDebug("Starting fuzzy match against {Count} MP3 files", mp3Files.Length);
        var normalizedTitle = NormalizeForComparison(videoTitle);
        _logger.LogTrace("Normalized title for matching: '{NormalizedTitle}'", normalizedTitle);

        string? bestMatch = null;
        double bestScore = 0;
        const double threshold = 0.80;

        foreach (var filePath in mp3Files)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var normalizedFileName = NormalizeForComparison(fileName);

            var score = CalculateSimilarity(normalizedTitle, normalizedFileName);
            _logger.LogTrace("Match score for '{File}': {Score:P1}", fileName, score);

            if (score > bestScore && score >= threshold)
            {
                bestScore = score;
                bestMatch = filePath;
            }
        }

        if (bestMatch != null)
        {
            _logger.LogInformation(
                "Fuzzy matched '{Title}' to existing file '{File}' with score {Score:P1}",
                videoTitle,
                Path.GetFileName(bestMatch),
                bestScore);
        }
        else
        {
            _logger.LogDebug("No fuzzy match found for '{Title}' (best score: {BestScore:P1}, threshold: {Threshold:P0})",
                videoTitle, bestScore, threshold);
        }

        return bestMatch;
    }

    private static string NormalizeForComparison(string text)
    {
        // Remove common suffixes and normalize characters (similar to Python script)
        var normalized = text
            .Replace("｜", "|")
            .Replace("：", ":")
            .Replace("？", "?")
            .Replace(" | BEHIND THE BASTARDS", "", StringComparison.OrdinalIgnoreCase)
            .Replace("| BEHIND THE BASTARDS", "", StringComparison.OrdinalIgnoreCase)
            .ToLowerInvariant()
            .Trim();

        // Remove extra whitespace
        normalized = WhitespaceRegex().Replace(normalized, " ");

        return normalized;
    }

    private static double CalculateSimilarity(string a, string b)
    {
        // Longest Common Subsequence ratio (similar to difflib.SequenceMatcher)
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            return 0;

        var lcsLength = LongestCommonSubsequenceLength(a, b);
        return (2.0 * lcsLength) / (a.Length + b.Length);
    }

    private static int LongestCommonSubsequenceLength(string a, string b)
    {
        var m = a.Length;
        var n = b.Length;
        var dp = new int[m + 1, n + 1];

        for (var i = 1; i <= m; i++)
        {
            for (var j = 1; j <= n; j++)
            {
                if (a[i - 1] == b[j - 1])
                    dp[i, j] = dp[i - 1, j - 1] + 1;
                else
                    dp[i, j] = Math.Max(dp[i - 1, j], dp[i, j - 1]);
            }
        }

        return dp[m, n];
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName
            .Where(c => !invalidChars.Contains(c))
            .ToArray());

        if (sanitized.Length > 200)
            sanitized = sanitized[..200];

        return sanitized.Trim();
    }
}
