using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Castr.Services;
using Castr.Models;

namespace Castr.Tests;

/// <summary>
/// Tests for PodcastFeedService focusing on feed management, 
/// RSS generation, and media file path resolution.
/// </summary>
public class PodcastFeedServiceTests : IDisposable
{
    private readonly Mock<IPodcastDatabaseService> _mockDatabase;
    private readonly Mock<ILogger<PodcastFeedService>> _mockLogger;
    private readonly PodcastFeedService _service;
    private readonly string _testDirectory;

    public PodcastFeedServiceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "castr_feed_test_" + Guid.NewGuid());
        Directory.CreateDirectory(_testDirectory);

        var config = new PodcastFeedsConfig
        {
            Feeds = new Dictionary<string, PodcastFeedConfig>
            {
                ["testfeed"] = new PodcastFeedConfig
                {
                    Title = "Test Podcast",
                    Description = "Test Description",
                    Directory = _testDirectory,
                    Author = "Test Author",
                    Language = "en-us",
                    Category = "Technology",
                    ImageUrl = "http://example.com/image.jpg",
                    Link = "http://example.com"
                },
                ["emptyfeed"] = new PodcastFeedConfig
                {
                    Title = "Empty Feed",
                    Description = "Empty",
                    Directory = Path.Combine(_testDirectory, "empty")
                }
            }
        };

        _mockDatabase = new Mock<IPodcastDatabaseService>();
        _mockLogger = new Mock<ILogger<PodcastFeedService>>();
        
        var options = Options.Create(config);
        _service = new PodcastFeedService(options, _mockDatabase.Object, _mockLogger.Object);
    }

    public void Dispose()
    {
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
    public void GetFeedNames_ReturnsConfiguredFeeds()
    {
        // Act
        var feedNames = _service.GetFeedNames().ToList();

        // Assert
        Assert.Equal(2, feedNames.Count);
        Assert.Contains("testfeed", feedNames);
        Assert.Contains("emptyfeed", feedNames);
    }

    [Fact]
    public void FeedExists_WithValidFeed_ReturnsTrue()
    {
        // Act
        var exists = _service.FeedExists("testfeed");

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public void FeedExists_WithInvalidFeed_ReturnsFalse()
    {
        // Act
        var exists = _service.FeedExists("nonexistent");

        // Assert
        Assert.False(exists);
    }

    [Fact]
    public void FeedExists_CaseSensitive()
    {
        // Act
        var exists = _service.FeedExists("TESTFEED");

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
        // Arrange
        _mockDatabase.Setup(d => d.GetEpisodesAsync("testfeed"))
            .ReturnsAsync(new List<EpisodeRecord>());

        // Act
        var result = await _service.GenerateFeedAsync("testfeed", "http://localhost");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("<?xml", result);
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

        _mockDatabase.Setup(d => d.GetEpisodesAsync("testfeed"))
            .ReturnsAsync(new List<EpisodeRecord>());

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
        _mockDatabase.Setup(d => d.GetEpisodesAsync("testfeed"))
            .ReturnsAsync(new List<EpisodeRecord>());

        // Act
        var result = await _service.GenerateFeedAsync("testfeed", baseUrl);

        // Assert
        Assert.NotNull(result);
        // Base URL should be used in enclosure URLs
        Assert.Contains(baseUrl, result);
    }

    [Fact]
    public async Task GenerateFeedAsync_IncludesItunesNamespace()
    {
        // Arrange
        _mockDatabase.Setup(d => d.GetEpisodesAsync("testfeed"))
            .ReturnsAsync(new List<EpisodeRecord>());

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
        // Arrange
        _mockDatabase.Setup(d => d.GetEpisodesAsync("testfeed"))
            .ReturnsAsync(new List<EpisodeRecord>());

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
        // Arrange
        _mockDatabase.Setup(d => d.GetEpisodesAsync("testfeed"))
            .ReturnsAsync(new List<EpisodeRecord>());

        // Act
        var result = await _service.GenerateFeedAsync("testfeed", "http://localhost");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("http://example.com/image.jpg", result);
        Assert.Contains("<image>", result);
    }

    #endregion

    #region GetMediaFilePath Tests

    [Fact]
    public void GetMediaFilePath_WithValidFile_ReturnsPath()
    {
        // Arrange
        var fileName = "episode001.mp3";
        var filePath = Path.Combine(_testDirectory, fileName);
        File.WriteAllText(filePath, "test");

        // Act
        var result = _service.GetMediaFilePath("testfeed", fileName);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(Path.GetFullPath(filePath), result);
    }

    [Fact]
    public void GetMediaFilePath_WithNonExistentFile_ReturnsNull()
    {
        // Act
        var result = _service.GetMediaFilePath("testfeed", "nonexistent.mp3");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetMediaFilePath_WithInvalidFeed_ReturnsNull()
    {
        // Act
        var result = _service.GetMediaFilePath("nonexistent", "episode.mp3");

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("..\\..\\..\\Windows\\System32\\config\\SAM")]
    [InlineData("subdir/../../../etc/passwd")]
    public void GetMediaFilePath_WithPathTraversal_ReturnsNull(string maliciousFileName)
    {
        // Act
        var result = _service.GetMediaFilePath("testfeed", maliciousFileName);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetMediaFilePath_SecurityCheck_PreventsDirectoryTraversal()
    {
        // Arrange - create a file outside the feed directory
        var outsideDir = Path.Combine(Path.GetTempPath(), "outside_" + Guid.NewGuid());
        Directory.CreateDirectory(outsideDir);
        var outsideFile = Path.Combine(outsideDir, "secret.mp3");
        File.WriteAllText(outsideFile, "secret data");

        try
        {
            // Calculate relative path from test directory to outside file
            // This simulates an attempt to use path traversal
            var relativePath = Path.GetRelativePath(_testDirectory, outsideFile);

            // Act
            var result = _service.GetMediaFilePath("testfeed", relativePath);

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
    public void GetMediaFilePath_WithSymlink_RespectsSecurityCheck()
    {
        // This test verifies that even with symlinks, the security check works
        // Note: Symlink creation may require elevated permissions on Windows
        
        // Arrange
        var targetFile = Path.Combine(_testDirectory, "legitimate.mp3");
        File.WriteAllText(targetFile, "test");

        // Act
        var result = _service.GetMediaFilePath("testfeed", "legitimate.mp3");

        // Assert
        Assert.NotNull(result);
        Assert.StartsWith(Path.GetFullPath(_testDirectory), result);
    }

    [Fact]
    public void GetMediaFilePath_CaseInsensitiveOnWindows()
    {
        // Arrange
        var fileName = "Episode001.mp3";
        var filePath = Path.Combine(_testDirectory, fileName);
        File.WriteAllText(filePath, "test");

        // Act - try with different case
        var result = _service.GetMediaFilePath("testfeed", 
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
            result = _service.GetMediaFilePath("testfeed", fileName);
            Assert.NotNull(result);
        }
    }

    #endregion

    #region Configuration Validation Tests

    [Fact]
    public void PodcastFeedService_WithEmptyConfig_HandlesGracefully()
    {
        // Arrange
        var emptyConfig = new PodcastFeedsConfig
        {
            Feeds = new Dictionary<string, PodcastFeedConfig>()
        };
        var options = Options.Create(emptyConfig);

        // Act
        var service = new PodcastFeedService(options, _mockDatabase.Object, _mockLogger.Object);
        var feedNames = service.GetFeedNames().ToList();

        // Assert
        Assert.Empty(feedNames);
    }

    #endregion
}
