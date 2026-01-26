using Castr.Tests.TestHelpers;

namespace Castr.Tests.Services;

public class PodcastFeedServiceTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly Mock<IOptions<PodcastFeedsConfig>> _mockConfig;
    private readonly Mock<IPodcastDatabaseService> _mockDbService;
    private readonly IMemoryCache _cache;
    private readonly Mock<ILogger<PodcastFeedService>> _mockLogger;
    private readonly PodcastFeedsConfig _config;

    public PodcastFeedServiceTests()
    {
        _testDirectory = TestDatabaseHelper.CreateTempDirectory();

        _config = new PodcastFeedsConfig
        {
            Feeds = new Dictionary<string, PodcastFeedConfig>
            {
                ["feed1"] = new PodcastFeedConfig
                {
                    Title = "Feed 1",
                    Description = "Description 1",
                    Directory = _testDirectory
                },
                ["feed2"] = new PodcastFeedConfig
                {
                    Title = "Feed 2",
                    Description = "Description 2",
                    Directory = _testDirectory
                }
            },
            CacheDurationMinutes = 5
        };

        _mockConfig = new Mock<IOptions<PodcastFeedsConfig>>();
        _mockConfig.Setup(x => x.Value).Returns(_config);

        _mockDbService = new Mock<IPodcastDatabaseService>();
        _mockDbService.Setup(x => x.GetEpisodesAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<EpisodeRecord>());

        _cache = new MemoryCache(new MemoryCacheOptions());
        _mockLogger = new Mock<ILogger<PodcastFeedService>>();
    }

    public void Dispose()
    {
        _cache.Dispose();
        TestDatabaseHelper.DeleteDirectory(_testDirectory);
    }

    private PodcastFeedService CreateService()
    {
        return new PodcastFeedService(
            _mockConfig.Object,
            _mockDbService.Object,
            _cache,
            _mockLogger.Object);
    }

    [Fact]
    public void GetFeedNames_ReturnsAllConfiguredFeeds()
    {
        // Arrange
        var service = CreateService();

        // Act
        var names = service.GetFeedNames().ToList();

        // Assert
        Assert.Equal(2, names.Count);
        Assert.Contains("feed1", names);
        Assert.Contains("feed2", names);
    }

    [Fact]
    public void FeedExists_WithValidFeed_ReturnsTrue()
    {
        // Arrange
        var service = CreateService();

        // Act
        var exists = service.FeedExists("feed1");

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public void FeedExists_WithInvalidFeed_ReturnsFalse()
    {
        // Arrange
        var service = CreateService();

        // Act
        var exists = service.FeedExists("nonexistent");

        // Assert
        Assert.False(exists);
    }

    [Fact]
    public void FeedExists_WithNullFeed_ThrowsArgumentNullException()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => service.FeedExists(null!));
    }

    [Fact]
    public void GetMediaFilePath_WithPathTraversal_ReturnsNull()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = service.GetMediaFilePath("feed1", "../../../etc/passwd");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetMediaFilePath_WithNonExistentFeed_ReturnsNull()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = service.GetMediaFilePath("nonexistent", "file.mp3");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetMediaFilePath_WithNonExistentFile_ReturnsNull()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = service.GetMediaFilePath("feed1", "nonexistent.mp3");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetMediaFilePath_WithValidFile_ReturnsPath()
    {
        // Arrange
        var service = CreateService();
        var testFile = "valid.mp3";
        File.WriteAllBytes(Path.Combine(_testDirectory, testFile), new byte[] { 0xFF, 0xFB, 0x90 });

        // Act
        var result = service.GetMediaFilePath("feed1", testFile);

        // Assert
        Assert.NotNull(result);
        Assert.Contains(testFile, result);
    }

    [Fact]
    public async Task GenerateFeedAsync_WithValidFeed_ReturnsXml()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.GenerateFeedAsync("feed1", "https://example.com");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("<rss", result);
        Assert.Contains("Feed 1", result);
    }

    [Fact]
    public async Task GenerateFeedAsync_WithNonExistentFeed_ReturnsNull()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.GenerateFeedAsync("nonexistent", "https://example.com");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GenerateFeedAsync_UsesCaching()
    {
        // Arrange
        var service = CreateService();

        // Act - generate twice
        var result1 = await service.GenerateFeedAsync("feed1", "https://example.com");
        var result2 = await service.GenerateFeedAsync("feed1", "https://example.com");

        // Assert - both should be identical (from cache)
        Assert.Equal(result1, result2);
    }

    [Fact]
    public async Task GenerateFeedAsync_WithEpisodes_IncludesItems()
    {
        // Arrange
        var testFile = "episode1.mp3";
        File.WriteAllBytes(Path.Combine(_testDirectory, testFile), new byte[] { 0xFF, 0xFB, 0x90, 0x00 });

        var service = CreateService();

        // Act
        var result = await service.GenerateFeedAsync("feed1", "https://example.com");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("<item>", result);
    }

    [Fact]
    public async Task GenerateFeedAsync_IncludesItunesNamespace()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.GenerateFeedAsync("feed1", "https://example.com");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("xmlns:itunes", result);
    }
}
