using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Castr.Controllers;
using Castr.Services;
using Castr.Models;

namespace Castr.Tests;

public class FeedControllerTests : IDisposable
{
    private readonly Mock<IPodcastDatabaseService> _mockDatabase;
    private readonly Mock<ILogger<FeedController>> _mockLogger;
    private readonly Mock<ILogger<PodcastFeedService>> _mockFeedServiceLogger;
    private readonly PodcastFeedService _feedService;
    private readonly FeedController _controller;
    private readonly string _testDirectory;

    public FeedControllerTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "castr_controller_test_" + Guid.NewGuid());
        Directory.CreateDirectory(_testDirectory);

        var config = new PodcastFeedsConfig
        {
            Feeds = new Dictionary<string, PodcastFeedConfig>
            {
                ["btb"] = new PodcastFeedConfig
                {
                    Title = "Behind The Bastards",
                    Description = "Test Feed",
                    Directory = _testDirectory
                }
            }
        };

        _mockDatabase = new Mock<IPodcastDatabaseService>();
        _mockFeedServiceLogger = new Mock<ILogger<PodcastFeedService>>();
        _mockLogger = new Mock<ILogger<FeedController>>();
        
        // Create real PodcastFeedService with mocked dependencies
        _feedService = new PodcastFeedService(
            Options.Create(config),
            _mockDatabase.Object,
            _mockFeedServiceLogger.Object
        );
        
        _controller = new FeedController(_feedService, _mockLogger.Object);
        
        // Setup HttpContext for base URL generation
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        _controller.HttpContext.Request.Scheme = "http";
        _controller.HttpContext.Request.Host = new HostString("localhost:5000");
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

    #region GetFeeds Tests

    [Fact]
    public void GetFeeds_ReturnsOkWithFeedList()
    {
        // Act
        var result = _controller.GetFeeds();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public void GetFeeds_ReturnsEmptyList_WhenNoFeeds()
    {
        // Arrange - create controller with empty config
        var emptyConfig = new PodcastFeedsConfig { Feeds = new Dictionary<string, PodcastFeedConfig>() };
        var emptyFeedService = new PodcastFeedService(
            Options.Create(emptyConfig),
            _mockDatabase.Object,
            _mockFeedServiceLogger.Object
        );
        var emptyController = new FeedController(emptyFeedService, _mockLogger.Object);

        // Act
        var result = emptyController.GetFeeds();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    #endregion

    #region GetFeed Tests

    [Fact]
    public async Task GetFeed_WithValidFeedName_ReturnsRssXml()
    {
        // Arrange
        var feedName = "btb";
        _mockDatabase.Setup(d => d.GetEpisodesAsync(feedName))
            .ReturnsAsync(new List<EpisodeRecord>());

        // Act
        var result = await _controller.GetFeed(feedName);

        // Assert
        var contentResult = Assert.IsType<ContentResult>(result);
        Assert.Equal("application/rss+xml; charset=utf-8", contentResult.ContentType);
        Assert.NotNull(contentResult.Content);
        Assert.Contains("<rss", contentResult.Content);
    }

    [Fact]
    public async Task GetFeed_WithInvalidFeedName_ReturnsNotFound()
    {
        // Act
        var result = await _controller.GetFeed("nonexistent");

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.NotNull(notFoundResult.Value);
    }

    [Fact]
    public async Task GetFeed_WithEmptyFeedName_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetFeed("");

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetFeed_WithTooLongFeedName_ReturnsBadRequest()
    {
        // Arrange
        var longName = new string('a', 101); // 101 characters

        // Act
        var result = await _controller.GetFeed(longName);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetFeed_WithWhitespaceFeedName_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetFeed("   ");

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    #endregion

    #region GetMedia Tests

    [Fact]
    public void GetMedia_WithValidFile_ReturnsPhysicalFile()
    {
        // Arrange
        var feedName = "btb";
        var fileName = "episode001.mp3";
        
        // Create a temporary file for testing
        var testFile = Path.Combine(_testDirectory, fileName);
        File.WriteAllText(testFile, "test content");

        // Act
        var result = _controller.GetMedia(feedName, fileName);

        // Assert
        var fileResult = Assert.IsType<PhysicalFileResult>(result);
        Assert.Equal("audio/mpeg", fileResult.ContentType);
        Assert.True(fileResult.EnableRangeProcessing);
    }

    [Fact]
    public void GetMedia_WithPathTraversal_ReturnsBadRequest()
    {
        // Arrange
        var feedName = "btb";
        var fileName = "../../../etc/passwd";

        // Act
        var result = _controller.GetMedia(feedName, fileName);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequestResult.Value);
    }

    [Fact]
    public void GetMedia_WithBackslashPathTraversal_ReturnsBadRequest()
    {
        // Arrange
        var feedName = "btb";
        var fileName = "..\\..\\..\\Windows\\System32\\config\\SAM";

        // Act
        var result = _controller.GetMedia(feedName, fileName);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void GetMedia_WithForwardSlash_ReturnsBadRequest()
    {
        // Arrange
        var feedName = "btb";
        var fileName = "subdir/episode.mp3";

        // Act
        var result = _controller.GetMedia(feedName, fileName);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void GetMedia_WithEmptyFileName_ReturnsBadRequest()
    {
        // Arrange
        var feedName = "btb";

        // Act
        var result = _controller.GetMedia(feedName, "");

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void GetMedia_WithTooLongFileName_ReturnsBadRequest()
    {
        // Arrange
        var feedName = "btb";
        var fileName = new string('a', 256); // 256 characters

        // Act
        var result = _controller.GetMedia(feedName, fileName);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void GetMedia_WithEmptyFeedName_ReturnsBadRequest()
    {
        // Act
        var result = _controller.GetMedia("", "episode.mp3");

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void GetMedia_WhenFileNotFound_ReturnsNotFound()
    {
        // Arrange
        var feedName = "btb";
        var fileName = "nonexistent.mp3";

        // Act
        var result = _controller.GetMedia(feedName, fileName);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Theory]
    [InlineData("test.mp3", "audio/mpeg")]
    [InlineData("test.m4a", "audio/mp4")]
    [InlineData("test.aac", "audio/aac")]
    [InlineData("test.ogg", "audio/ogg")]
    [InlineData("test.wav", "audio/wav")]
    [InlineData("test.flac", "audio/flac")]
    [InlineData("test.unknown", "application/octet-stream")]
    public void GetMedia_ReturnCorrectMimeType(string fileName, string expectedMimeType)
    {
        // Arrange
        var feedName = "btb";
        var testFile = Path.Combine(_testDirectory, fileName);
        File.WriteAllText(testFile, "test content");

        // Act
        var result = _controller.GetMedia(feedName, fileName);

        // Assert
        var fileResult = Assert.IsType<PhysicalFileResult>(result);
        Assert.Equal(expectedMimeType, fileResult.ContentType);
    }

    #endregion
}
