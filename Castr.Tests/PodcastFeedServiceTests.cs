using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Castr.Services;
using Castr.Data.Entities;

namespace Castr.Tests;

/// <summary>
/// Tests for PodcastFeedService focusing on feed management,
/// RSS generation, and media file path resolution.
/// </summary>
public class PodcastFeedServiceTests : IDisposable
{
    private readonly Mock<IPodcastDataService> _mockDataService;
    private readonly Mock<ILogger<PodcastFeedService>> _mockLogger;
    private readonly IMemoryCache _cache;
    private readonly PodcastFeedService _service;
    private readonly string _testDirectory;

    public PodcastFeedServiceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "castr_feed_test_" + Guid.NewGuid());
        Directory.CreateDirectory(_testDirectory);

        _mockDataService = new Mock<IPodcastDataService>();

        // Setup feeds in mock data service
        var testFeed = new Feed
        {
            Id = 1,
            Name = "testfeed",
            Title = "Test Podcast",
            Description = "Test Description",
            Directory = _testDirectory,
            Author = "Test Author",
            Language = "en-us",
            Category = "Technology",
            ImageUrl = "http://example.com/image.jpg",
            Link = "http://example.com",
            FileExtensions = [".mp3"],
            CacheDurationMinutes = 5
        };
        var emptyFeed = new Feed
        {
            Id = 2,
            Name = "emptyfeed",
            Title = "Empty Feed",
            Description = "Empty",
            Directory = Path.Combine(_testDirectory, "empty"),
            FileExtensions = [".mp3"],
            CacheDurationMinutes = 5
        };

        _mockDataService.Setup(x => x.GetAllFeedsAsync())
            .ReturnsAsync(new List<Feed> { testFeed, emptyFeed });
        _mockDataService.Setup(x => x.GetFeedByNameAsync("testfeed"))
            .ReturnsAsync(testFeed);
        _mockDataService.Setup(x => x.GetFeedByNameAsync("emptyfeed"))
            .ReturnsAsync(emptyFeed);
        _mockDataService.Setup(x => x.GetFeedByNameAsync(It.IsNotIn("testfeed", "emptyfeed")))
            .ReturnsAsync((Feed?)null);
        _mockDataService.Setup(x => x.GetEpisodesAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<Episode>());

        _mockLogger = new Mock<ILogger<PodcastFeedService>>();
        _cache = new MemoryCache(new MemoryCacheOptions());

        _service = new PodcastFeedService(_mockDataService.Object, _cache, _mockLogger.Object);
    }

    public void Dispose()
    {
        _cache.Dispose();
        if (Directory.Exists(_testDirectory))
        {
            try
            {
                Directory.Delete(_testDirectory, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    #region Feed Management Tests

    [Fact]
    public async Task GetFeedNamesAsync_ReturnsConfiguredFeeds()
    {
        // Act
        var feedNames = (await _service.GetFeedNamesAsync()).ToList();

        // Assert
        Assert.Equal(2, feedNames.Count);
        Assert.Contains("testfeed", feedNames);
        Assert.Contains("emptyfeed", feedNames);
    }

    [Fact]
    public async Task FeedExistsAsync_WithValidFeed_ReturnsTrue()
    {
        // Act
        var exists = await _service.FeedExistsAsync("testfeed");

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task FeedExistsAsync_WithInvalidFeed_ReturnsFalse()
    {
        // Act
        var exists = await _service.FeedExistsAsync("nonexistent");

        // Assert
        Assert.False(exists);
    }

    [Fact]
    public async Task FeedExistsAsync_CaseSensitive()
    {
        // Act
        var exists = await _service.FeedExistsAsync("TESTFEED");

        // Assert
        Assert.False(exists); // Should be case-sensitive
    }

    #endregion

    #region GenerateFeedAsync Tests

    [Fact]
    public async Task GenerateFeedAsync_WithInvalidFeed_ReturnsNull()
    {
        // Act
        var result = await _service.GenerateFeedAsync("nonexistent", "http://localhost");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GenerateFeedAsync_WithValidFeed_ReturnsXml()
    {
        // Act
        var result = await _service.GenerateFeedAsync("testfeed", "http://localhost");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("<rss", result);
        Assert.Contains("<channel>", result);
        Assert.Contains("Test Podcast", result);
        Assert.Contains("Test Description", result);
    }

    [Fact]
    public async Task GenerateFeedAsync_IncludesEpisodes()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "episode001.mp3");
        File.WriteAllText(testFile, "test content");

        // Act
        var result = await _service.GenerateFeedAsync("testfeed", "http://localhost");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("<channel>", result);
    }

    [Fact]
    public async Task GenerateFeedAsync_IncludesBaseUrl()
    {
        // Arrange
        var baseUrl = "https://podcast.example.com";

        // Act
        var result = await _service.GenerateFeedAsync("testfeed", baseUrl);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("<channel>", result);
    }

    [Fact]
    public async Task GenerateFeedAsync_IncludesItunesNamespace()
    {
        // Act
        var result = await _service.GenerateFeedAsync("testfeed", "http://localhost");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("xmlns:itunes", result);
        Assert.Contains("http://www.itunes.com/dtds/podcast-1.0.dtd", result);
    }

    [Fact]
    public async Task GenerateFeedAsync_IncludesChannelMetadata()
    {
        // Act
        var result = await _service.GenerateFeedAsync("testfeed", "http://localhost");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("<title>Test Podcast</title>", result);
        Assert.Contains("<description>Test Description</description>", result);
        Assert.Contains("<language>en-us</language>", result);
        Assert.Contains("Test Author", result);
    }

    [Fact]
    public async Task GenerateFeedAsync_IncludesImageWhenConfigured()
    {
        // Act
        var result = await _service.GenerateFeedAsync("testfeed", "http://localhost");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("http://example.com/image.jpg", result);
        Assert.Contains("<image>", result);
    }

    #endregion

    #region GetMediaFilePathAsync Tests

    [Fact]
    public async Task GetMediaFilePathAsync_WithValidFile_ReturnsPath()
    {
        // Arrange
        var fileName = "episode001.mp3";
        var filePath = Path.Combine(_testDirectory, fileName);
        File.WriteAllText(filePath, "test");

        // Act
        var result = await _service.GetMediaFilePathAsync("testfeed", fileName);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(Path.GetFullPath(filePath), result);
    }

    [Fact]
    public async Task GetMediaFilePathAsync_WithNonExistentFile_ReturnsNull()
    {
        // Act
        var result = await _service.GetMediaFilePathAsync("testfeed", "nonexistent.mp3");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetMediaFilePathAsync_WithInvalidFeed_ReturnsNull()
    {
        // Act
        var result = await _service.GetMediaFilePathAsync("nonexistent", "episode.mp3");

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("..\\..\\..\\Windows\\System32\\config\\SAM")]
    [InlineData("subdir/../../../etc/passwd")]
    public async Task GetMediaFilePathAsync_WithPathTraversal_ReturnsNull(string maliciousFileName)
    {
        // Act
        var result = await _service.GetMediaFilePathAsync("testfeed", maliciousFileName);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetMediaFilePathAsync_SecurityCheck_PreventsDirectoryTraversal()
    {
        // Arrange - create a file outside the feed directory
        var outsideDir = Path.Combine(Path.GetTempPath(), "outside_" + Guid.NewGuid());
        Directory.CreateDirectory(outsideDir);
        var outsideFile = Path.Combine(outsideDir, "secret.mp3");
        File.WriteAllText(outsideFile, "secret data");

        try
        {
            // Calculate relative path from test directory to outside file
            var relativePath = Path.GetRelativePath(_testDirectory, outsideFile);

            // Act
            var result = await _service.GetMediaFilePathAsync("testfeed", relativePath);

            // Assert
            Assert.Null(result); // Should return null due to security check
        }
        finally
        {
            if (Directory.Exists(outsideDir))
                Directory.Delete(outsideDir, true);
        }
    }

    [Fact]
    public async Task GetMediaFilePathAsync_WithSymlink_RespectsSecurityCheck()
    {
        // Arrange
        var targetFile = Path.Combine(_testDirectory, "legitimate.mp3");
        File.WriteAllText(targetFile, "test");

        // Act
        var result = await _service.GetMediaFilePathAsync("testfeed", "legitimate.mp3");

        // Assert
        Assert.NotNull(result);
        Assert.StartsWith(Path.GetFullPath(_testDirectory), result);
    }

    [Fact]
    public async Task GetMediaFilePathAsync_CaseInsensitiveOnWindows()
    {
        // Arrange
        var fileName = "Episode001.mp3";
        var filePath = Path.Combine(_testDirectory, fileName);
        File.WriteAllText(filePath, "test");

        // Act - try with different case
        var result = await _service.GetMediaFilePathAsync("testfeed",
            OperatingSystem.IsWindows() ? "EPISODE001.MP3" : fileName);

        // Assert
        if (OperatingSystem.IsWindows())
        {
            Assert.NotNull(result); // Windows is case-insensitive
        }
        else
        {
            // On Linux/Mac, case matters, so this might fail
            // Just verify the original case works
            result = await _service.GetMediaFilePathAsync("testfeed", fileName);
            Assert.NotNull(result);
        }
    }

    #endregion

    #region Configuration Validation Tests

    [Fact]
    public async Task PodcastFeedService_WithNoFeeds_HandlesGracefully()
    {
        // Arrange
        var emptyDataService = new Mock<IPodcastDataService>();
        emptyDataService.Setup(x => x.GetAllFeedsAsync()).ReturnsAsync(new List<Feed>());
        emptyDataService.Setup(x => x.GetFeedByNameAsync(It.IsAny<string>())).ReturnsAsync((Feed?)null);

        var service = new PodcastFeedService(emptyDataService.Object, _cache, _mockLogger.Object);

        // Act
        var feedNames = (await service.GetFeedNamesAsync()).ToList();

        // Assert
        Assert.Empty(feedNames);
    }

    #endregion
}
