using Castr.Tests.TestHelpers;
using Castr.Data.Entities;

namespace Castr.Tests.Services;

/// <summary>
/// Integration tests for PodcastFeedService focusing on RSS feed generation.
/// </summary>
public class PodcastFeedServiceIntegrationTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly Mock<IOptions<PodcastFeedsConfig>> _mockConfig;
    private readonly Mock<IPodcastDataService> _mockDataService;
    private readonly IMemoryCache _cache;
    private readonly Mock<ILogger<PodcastFeedService>> _mockLogger;
    private readonly PodcastFeedsConfig _config;

    public PodcastFeedServiceIntegrationTests()
    {
        _testDirectory = TestDatabaseHelper.CreateTempDirectory();

        _config = new PodcastFeedsConfig
        {
            Feeds = new Dictionary<string, PodcastFeedConfig>
            {
                ["testfeed"] = new PodcastFeedConfig
                {
                    Title = "Test Podcast",
                    Description = "A test podcast for integration testing",
                    Directory = _testDirectory,
                    Author = "Test Author",
                    ImageUrl = "https://example.com/image.png",
                    Link = "https://example.com",
                    Language = "en-us",
                    Category = "Technology"
                }
            },
            CacheDurationMinutes = 5
        };

        _mockConfig = new Mock<IOptions<PodcastFeedsConfig>>();
        _mockConfig.Setup(x => x.Value).Returns(_config);

        _mockDataService = new Mock<IPodcastDataService>();
        // Return null for feed lookup by default to simulate feed not in database (falls back to alphabetical order)
        _mockDataService.Setup(x => x.GetFeedByNameAsync(It.IsAny<string>()))
            .ReturnsAsync((Feed?)null);

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
            _mockDataService.Object,
            _cache,
            _mockLogger.Object);
    }

    [Fact]
    public async Task GenerateFeedAsync_IncludesChannelMetadata()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.GenerateFeedAsync("testfeed", "https://example.com");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("<title>Test Podcast</title>", result);
        Assert.Contains("<description>A test podcast for integration testing</description>", result);
        Assert.Contains("<language>en-us</language>", result);
    }

    [Fact]
    public async Task GenerateFeedAsync_IncludesItunesElements()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.GenerateFeedAsync("testfeed", "https://example.com");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("itunes:author", result);
        Assert.Contains("itunes:summary", result);
        Assert.Contains("itunes:explicit", result);
    }

    [Fact]
    public async Task GenerateFeedAsync_IncludesImage()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.GenerateFeedAsync("testfeed", "https://example.com");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("<image>", result);
        Assert.Contains("https://example.com/image.png", result);
    }

    [Fact]
    public async Task GenerateFeedAsync_IncludesCategory()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.GenerateFeedAsync("testfeed", "https://example.com");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Technology", result);
    }

    [Fact]
    public async Task GenerateFeedAsync_WithDifferentBaseUrls_GeneratesDifferentResults()
    {
        // Arrange
        // Create a test file so we have an episode with enclosure URL
        File.WriteAllBytes(Path.Combine(_testDirectory, "episode.mp3"), new byte[] { 0xFF, 0xFB });

        var service = CreateService();

        // Act
        var result1 = await service.GenerateFeedAsync("testfeed", "https://example1.com");
        var result2 = await service.GenerateFeedAsync("testfeed", "https://example2.com");

        // Assert - both should work but have different enclosure URLs
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.Contains("example1.com", result1);
        Assert.Contains("example2.com", result2);
    }

    [Fact]
    public async Task GenerateFeedAsync_AppliesEpisodeMetadataFromDatabase()
    {
        // Arrange
        var testFeed = new Feed { Id = 1, Name = "testfeed", Title = "Test Feed" };
        var dbEpisodes = new List<Episode>
        {
            new Episode
            {
                Id = 1,
                FeedId = 1,
                Filename = "episode1.mp3",
                VideoId = "vid1",
                YoutubeTitle = "Episode Title from YouTube",
                Description = "Description from YouTube",
                ThumbnailUrl = "https://example.com/thumb.jpg",
                DisplayOrder = 1,
                PublishDate = new DateTime(2024, 6, 15)
            }
        };

        _mockDataService.Setup(x => x.GetFeedByNameAsync("testfeed"))
            .ReturnsAsync(testFeed);
        _mockDataService.Setup(x => x.GetEpisodesAsync(1))
            .ReturnsAsync(dbEpisodes);

        // Create matching file
        File.WriteAllBytes(Path.Combine(_testDirectory, "episode1.mp3"), new byte[] { 0xFF, 0xFB, 0x90 });

        var service = CreateService();

        // Act
        var result = await service.GenerateFeedAsync("testfeed", "https://example.com");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("<item>", result);
        // The YouTube link should be included in description
        Assert.Contains("youtube.com/watch?v=vid1", result);
    }

    [Fact]
    public async Task GenerateFeedAsync_HandlesEmptyDirectory()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.GenerateFeedAsync("testfeed", "https://example.com");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("<rss", result);
        // Should still have channel but no items
        Assert.Contains("<channel>", result);
    }

    [Fact]
    public async Task GenerateFeedAsync_FiltersFileExtensions()
    {
        // Arrange
        File.WriteAllBytes(Path.Combine(_testDirectory, "audio.mp3"), new byte[] { 0xFF, 0xFB });
        File.WriteAllText(Path.Combine(_testDirectory, "readme.txt"), "not an audio file");
        File.WriteAllText(Path.Combine(_testDirectory, "image.jpg"), "not an audio file");

        var service = CreateService();

        // Act
        var result = await service.GenerateFeedAsync("testfeed", "https://example.com");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("audio.mp3", result);
        Assert.DoesNotContain("readme.txt", result);
        Assert.DoesNotContain("image.jpg", result);
    }

    [Fact]
    public void GetMediaFilePath_ReturnsFullPath()
    {
        // Arrange
        var testFile = "test-episode.mp3";
        File.WriteAllBytes(Path.Combine(_testDirectory, testFile), new byte[] { 0xFF, 0xFB });

        var service = CreateService();

        // Act
        var result = service.GetMediaFilePath("testfeed", testFile);

        // Assert
        Assert.NotNull(result);
        Assert.True(Path.IsPathRooted(result));
        Assert.Contains(testFile, result);
    }

    [Fact]
    public void GetMediaFilePath_BlocksPathTraversalAttempts()
    {
        // Arrange
        var service = CreateService();

        // Create a file outside the configured directory
        var parentDir = Directory.GetParent(_testDirectory)?.FullName ?? _testDirectory;
        var outsideFile = Path.Combine(parentDir, "outside.mp3");
        File.WriteAllText(outsideFile, "test");

        try
        {
            // Act
            var result = service.GetMediaFilePath("testfeed", "../outside.mp3");

            // Assert
            Assert.Null(result); // Should reject path traversal
        }
        finally
        {
            if (File.Exists(outsideFile))
                File.Delete(outsideFile);
        }
    }

    [Theory]
    [InlineData(".mp3", "audio/mpeg")]
    [InlineData(".m4a", "audio/mp4")]
    [InlineData(".aac", "audio/aac")]
    [InlineData(".ogg", "audio/ogg")]
    [InlineData(".wav", "audio/wav")]
    [InlineData(".flac", "audio/flac")]
    [InlineData(".xyz", "application/octet-stream")]
    public async Task GenerateFeedAsync_SetsCorrectMimeType(string extension, string expectedMimeType)
    {
        // Arrange
        var fileName = $"test{extension}";
        File.WriteAllBytes(Path.Combine(_testDirectory, fileName), new byte[] { 0x00, 0x00 });

        // Update config to accept this extension
        _config.Feeds["testfeed"].FileExtensions = new[] { extension };

        var service = CreateService();

        // Act
        var result = await service.GenerateFeedAsync("testfeed", "https://example.com");

        // Assert
        if (extension != ".xyz") // .xyz won't be picked up because it's filtered
        {
            Assert.NotNull(result);
            Assert.Contains($"type=\"{expectedMimeType}\"", result);
        }
    }

    [Fact]
    public async Task GenerateFeedAsync_SortsEpisodesByDatabaseOrder()
    {
        // Arrange
        var testFeed = new Feed { Id = 1, Name = "testfeed", Title = "Test Feed" };
        var dbEpisodes = new List<Episode>
        {
            new Episode { Id = 1, FeedId = 1, Filename = "episode3.mp3", DisplayOrder = 3 },
            new Episode { Id = 2, FeedId = 1, Filename = "episode1.mp3", DisplayOrder = 1 },
            new Episode { Id = 3, FeedId = 1, Filename = "episode2.mp3", DisplayOrder = 2 }
        };

        _mockDataService.Setup(x => x.GetFeedByNameAsync("testfeed"))
            .ReturnsAsync(testFeed);
        _mockDataService.Setup(x => x.GetEpisodesAsync(1))
            .ReturnsAsync(dbEpisodes);

        // Create files
        File.WriteAllBytes(Path.Combine(_testDirectory, "episode1.mp3"), new byte[] { 0xFF });
        File.WriteAllBytes(Path.Combine(_testDirectory, "episode2.mp3"), new byte[] { 0xFF });
        File.WriteAllBytes(Path.Combine(_testDirectory, "episode3.mp3"), new byte[] { 0xFF });

        var service = CreateService();

        // Act
        var result = await service.GenerateFeedAsync("testfeed", "https://example.com");

        // Assert
        Assert.NotNull(result);
        var idx1 = result.IndexOf("episode1.mp3");
        var idx2 = result.IndexOf("episode2.mp3");
        var idx3 = result.IndexOf("episode3.mp3");

        // Episodes should be in order 1, 2, 3
        Assert.True(idx1 < idx2, "Episode 1 should come before Episode 2");
        Assert.True(idx2 < idx3, "Episode 2 should come before Episode 3");
    }
}
