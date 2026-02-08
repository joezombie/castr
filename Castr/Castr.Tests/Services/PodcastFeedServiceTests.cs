using Castr.Tests.TestHelpers;
using Castr.Data.Entities;

namespace Castr.Tests.Services;

public class PodcastFeedServiceTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly Mock<IPodcastDataService> _mockDataService;
    private readonly IMemoryCache _cache;
    private readonly Mock<ILogger<PodcastFeedService>> _mockLogger;

    public PodcastFeedServiceTests()
    {
        _testDirectory = TestDatabaseHelper.CreateTempDirectory();

        var feed1 = new Feed
        {
            Id = 1,
            Name = "feed1",
            Title = "Feed 1",
            Description = "Description 1",
            Directory = _testDirectory,
            FileExtensions = [".mp3"],
            CacheDurationMinutes = 5
        };
        var feed2 = new Feed
        {
            Id = 2,
            Name = "feed2",
            Title = "Feed 2",
            Description = "Description 2",
            Directory = _testDirectory,
            FileExtensions = [".mp3"],
            CacheDurationMinutes = 5
        };

        _mockDataService = new Mock<IPodcastDataService>();
        _mockDataService.Setup(x => x.GetAllFeedsAsync())
            .ReturnsAsync(new List<Feed> { feed1, feed2 });
        _mockDataService.Setup(x => x.GetFeedByNameAsync("feed1"))
            .ReturnsAsync(feed1);
        _mockDataService.Setup(x => x.GetFeedByNameAsync("feed2"))
            .ReturnsAsync(feed2);
        _mockDataService.Setup(x => x.GetFeedByNameAsync(It.IsNotIn("feed1", "feed2")))
            .ReturnsAsync((Feed?)null);
        _mockDataService.Setup(x => x.GetEpisodesAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<Episode>());

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
            _mockDataService.Object,
            _cache,
            _mockLogger.Object);
    }

    [Fact]
    public async Task GetFeedNamesAsync_ReturnsAllConfiguredFeeds()
    {
        // Arrange
        var service = CreateService();

        // Act
        var names = (await service.GetFeedNamesAsync()).ToList();

        // Assert
        Assert.Equal(2, names.Count);
        Assert.Contains("feed1", names);
        Assert.Contains("feed2", names);
    }

    [Fact]
    public async Task FeedExistsAsync_WithValidFeed_ReturnsTrue()
    {
        // Arrange
        var service = CreateService();

        // Act
        var exists = await service.FeedExistsAsync("feed1");

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task FeedExistsAsync_WithInvalidFeed_ReturnsFalse()
    {
        // Arrange
        var service = CreateService();

        // Act
        var exists = await service.FeedExistsAsync("nonexistent");

        // Assert
        Assert.False(exists);
    }

    [Fact]
    public async Task GetMediaFilePathAsync_WithPathTraversal_ReturnsNull()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.GetMediaFilePathAsync("feed1", "../../../etc/passwd");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetMediaFilePathAsync_WithNonExistentFeed_ReturnsNull()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.GetMediaFilePathAsync("nonexistent", "file.mp3");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetMediaFilePathAsync_WithNonExistentFile_ReturnsNull()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.GetMediaFilePathAsync("feed1", "nonexistent.mp3");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetMediaFilePathAsync_WithValidFile_ReturnsPath()
    {
        // Arrange
        var service = CreateService();
        var testFile = "valid.mp3";
        File.WriteAllBytes(Path.Combine(_testDirectory, testFile), new byte[] { 0xFF, 0xFB, 0x90 });

        // Act
        var result = await service.GetMediaFilePathAsync("feed1", testFile);

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
