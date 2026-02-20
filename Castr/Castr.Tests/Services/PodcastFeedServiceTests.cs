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
    public async Task GetMediaFilePathAsync_WithRelativePath_ResolvesWithinDirectory()
    {
        // Arrange
        var service = CreateService();
        var subDir = Path.Combine(_testDirectory, "season1");
        Directory.CreateDirectory(subDir);
        var testFile = "episode.mp3";
        File.WriteAllBytes(Path.Combine(subDir, testFile), new byte[] { 0xFF, 0xFB, 0x90 });

        // Act
        var result = await service.GetMediaFilePathAsync("feed1", "season1/episode.mp3");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("season1", result);
        Assert.Contains("episode.mp3", result);
    }

    [Fact]
    public async Task GenerateFeedAsync_WithSubfolderEpisode_EncodesPathSegmentsIndividually()
    {
        // Arrange
        var subDir = Path.Combine(_testDirectory, "season 1");
        Directory.CreateDirectory(subDir);
        var testFile = "my episode.mp3";
        File.WriteAllBytes(Path.Combine(subDir, testFile), new byte[] { 0xFF, 0xFB, 0x90, 0x00 });

        _mockDataService.Setup(x => x.GetEpisodesAsync(1))
            .ReturnsAsync(new List<Episode>
            {
                new Episode { Id = 1, FeedId = 1, Filename = "season 1/my episode.mp3", Title = "My Episode", DisplayOrder = 1, FileSize = 4, DurationSeconds = 60 }
            });

        var service = CreateService();

        // Act
        var result = await service.GenerateFeedAsync("feed1", "https://example.com");

        // Assert
        Assert.NotNull(result);
        // Each segment should be encoded individually: "season%201/my%20episode.mp3" not "season%201%2Fmy%20episode.mp3"
        Assert.Contains("season%201/my%20episode.mp3", result);
    }

    [Fact]
    public async Task GenerateFeedAsync_WithFeedImageUrl_UsesFeedImage()
    {
        // Arrange
        var feedWithImage = new Feed
        {
            Id = 3,
            Name = "feed3",
            Title = "Feed 3",
            Description = "Description 3",
            Directory = _testDirectory,
            FileExtensions = [".mp3"],
            CacheDurationMinutes = 5,
            ImageUrl = "https://example.com/feed-image.jpg"
        };
        _mockDataService.Setup(x => x.GetFeedByNameAsync("feed3")).ReturnsAsync(feedWithImage);
        _mockDataService.Setup(x => x.GetEpisodesAsync(3)).ReturnsAsync(new List<Episode>());

        var service = CreateService();

        // Act
        var result = await service.GenerateFeedAsync("feed3", "https://example.com");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("https://example.com/feed-image.jpg", result);
    }

    [Fact]
    public async Task GenerateFeedAsync_WithNoFeedImageUrl_FallsBackToLatestEpisodeThumbnail()
    {
        // Arrange
        var testFile = "episode1.mp3";
        File.WriteAllBytes(Path.Combine(_testDirectory, testFile), new byte[] { 0xFF, 0xFB, 0x90, 0x00 });

        _mockDataService.Setup(x => x.GetEpisodesAsync(1))
            .ReturnsAsync(new List<Episode>
            {
                new Episode { Id = 1, FeedId = 1, Filename = testFile, Title = "Episode 1", DisplayOrder = 1, FileSize = 4, DurationSeconds = 60, ThumbnailUrl = "https://example.com/thumb.jpg" }
            });

        var service = CreateService();

        // Act
        var result = await service.GenerateFeedAsync("feed1", "https://example.com");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("https://example.com/thumb.jpg", result);
    }

    [Fact]
    public async Task GenerateFeedAsync_WithNoFeedImageUrl_UsesLatestEpisodeThumbnailWhenMultipleExist()
    {
        // Arrange
        var file1 = "episode1.mp3";
        var file2 = "episode2.mp3";
        File.WriteAllBytes(Path.Combine(_testDirectory, file1), new byte[] { 0xFF, 0xFB, 0x90, 0x00 });
        File.WriteAllBytes(Path.Combine(_testDirectory, file2), new byte[] { 0xFF, 0xFB, 0x90, 0x00 });

        var olderDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var newerDate = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        _mockDataService.Setup(x => x.GetEpisodesAsync(1))
            .ReturnsAsync(new List<Episode>
            {
                // Episode 2 has older PublishDate but higher DisplayOrder (simulates newest-first playlist)
                new Episode { Id = 1, FeedId = 1, Filename = file1, Title = "Episode 1", DisplayOrder = 2, PublishDate = newerDate, FileSize = 4, DurationSeconds = 60, ThumbnailUrl = "https://example.com/thumb1.jpg" },
                new Episode { Id = 2, FeedId = 1, Filename = file2, Title = "Episode 2", DisplayOrder = 1, PublishDate = olderDate, FileSize = 4, DurationSeconds = 60, ThumbnailUrl = "https://example.com/thumb2.jpg" }
            });

        var service = CreateService();

        // Act
        var result = await service.GenerateFeedAsync("feed1", "https://example.com");

        // Assert - should use the episode with the most recent PublishDate, not highest DisplayOrder
        Assert.NotNull(result);
        var doc = System.Xml.Linq.XDocument.Parse(result);
        var channelImageUrl = doc.Descendants("channel").First().Element("image")?.Element("url")?.Value;
        Assert.Equal("https://example.com/thumb1.jpg", channelImageUrl);
    }

    [Fact]
    public async Task GenerateFeedAsync_WithNoFeedImageAndNoThumbnailUrl_FallsBackToEmbeddedArt()
    {
        // Arrange
        var testFile = "episode1.mp3";
        File.WriteAllBytes(Path.Combine(_testDirectory, testFile), new byte[] { 0xFF, 0xFB, 0x90, 0x00 });

        _mockDataService.Setup(x => x.GetEpisodesAsync(1))
            .ReturnsAsync(new List<Episode>
            {
                new Episode { Id = 1, FeedId = 1, Filename = testFile, Title = "Episode 1", DisplayOrder = 1, FileSize = 4, DurationSeconds = 60, HasEmbeddedArt = true }
            });

        var service = CreateService();

        // Act
        var result = await service.GenerateFeedAsync("feed1", "https://example.com");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("/feed/feed1/artwork/episode1.mp3", result);
    }

    [Fact]
    public async Task GenerateFeedAsync_WithEpisodes_IncludesItems()
    {
        // Arrange
        var testFile = "episode1.mp3";
        File.WriteAllBytes(Path.Combine(_testDirectory, testFile), new byte[] { 0xFF, 0xFB, 0x90, 0x00 });

        _mockDataService.Setup(x => x.GetEpisodesAsync(1))
            .ReturnsAsync(new List<Episode>
            {
                new Episode { Id = 1, FeedId = 1, Filename = testFile, Title = "Episode 1", DisplayOrder = 1, FileSize = 4, DurationSeconds = 60 }
            });

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
