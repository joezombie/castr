using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Castr.Controllers;
using Castr.Services;
using Castr.Data.Entities;

namespace Castr.Tests;

public class FeedControllerTests : IDisposable
{
    private readonly Mock<IPodcastDataService> _mockDataService;
    private readonly Mock<ILogger<FeedController>> _mockLogger;
    private readonly Mock<ILogger<PodcastFeedService>> _mockFeedServiceLogger;
    private readonly IMemoryCache _cache;
    private readonly PodcastFeedService _feedService;
    private readonly FeedController _controller;
    private readonly string _testDirectory;

    public FeedControllerTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "castr_controller_test_" + Guid.NewGuid());
        Directory.CreateDirectory(_testDirectory);

        var testFeed = new Feed
        {
            Id = 1,
            Name = "mypodcast",
            Title = "My Podcast",
            Description = "Test Feed",
            Directory = _testDirectory,
            FileExtensions = [".mp3"],
            CacheDurationMinutes = 5
        };

        _mockDataService = new Mock<IPodcastDataService>();
        _mockDataService.Setup(x => x.GetAllFeedsAsync())
            .ReturnsAsync(new List<Feed> { testFeed });
        _mockDataService.Setup(x => x.GetFeedByNameAsync("mypodcast"))
            .ReturnsAsync(testFeed);
        _mockDataService.Setup(x => x.GetFeedByNameAsync(It.IsNotIn("mypodcast")))
            .ReturnsAsync((Feed?)null);
        _mockDataService.Setup(x => x.GetEpisodesAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<Episode>());

        _mockFeedServiceLogger = new Mock<ILogger<PodcastFeedService>>();
        _mockLogger = new Mock<ILogger<FeedController>>();
        _cache = new MemoryCache(new MemoryCacheOptions());

        _feedService = new PodcastFeedService(
            _mockDataService.Object,
            _cache,
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

    #region GetFeeds Tests

    [Fact]
    public async Task GetFeeds_ReturnsOkWithFeedList()
    {
        // Act
        var result = await _controller.GetFeeds();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task GetFeeds_ReturnsEmptyList_WhenNoFeeds()
    {
        // Arrange - create controller with empty data service
        var emptyDataService = new Mock<IPodcastDataService>();
        emptyDataService.Setup(x => x.GetAllFeedsAsync()).ReturnsAsync(new List<Feed>());
        emptyDataService.Setup(x => x.GetFeedByNameAsync(It.IsAny<string>())).ReturnsAsync((Feed?)null);

        var emptyFeedService = new PodcastFeedService(
            emptyDataService.Object,
            _cache,
            _mockFeedServiceLogger.Object
        );
        var emptyController = new FeedController(emptyFeedService, _mockLogger.Object);
        emptyController.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        emptyController.HttpContext.Request.Scheme = "http";
        emptyController.HttpContext.Request.Host = new HostString("localhost:5000");

        // Act
        var result = await emptyController.GetFeeds();

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
        var feedName = "mypodcast";

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
    public async Task GetMedia_WithValidFile_ReturnsPhysicalFile()
    {
        // Arrange
        var feedName = "mypodcast";
        var fileName = "episode001.mp3";

        // Create a temporary file for testing
        var testFile = Path.Combine(_testDirectory, fileName);
        File.WriteAllText(testFile, "test content");

        // Act
        var result = await _controller.GetMedia(feedName, fileName);

        // Assert
        var fileResult = Assert.IsType<PhysicalFileResult>(result);
        Assert.Equal("audio/mpeg", fileResult.ContentType);
        Assert.True(fileResult.EnableRangeProcessing);
    }

    [Fact]
    public async Task GetMedia_WithPathTraversal_ReturnsBadRequest()
    {
        // Arrange
        var feedName = "mypodcast";
        var fileName = "../../../etc/passwd";

        // Act
        var result = await _controller.GetMedia(feedName, fileName);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequestResult.Value);
    }

    [Fact]
    public async Task GetMedia_WithBackslashPathTraversal_ReturnsBadRequest()
    {
        // Arrange
        var feedName = "mypodcast";
        var fileName = "..\\..\\..\\Windows\\System32\\config\\SAM";

        // Act
        var result = await _controller.GetMedia(feedName, fileName);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetMedia_WithSubfolderPath_ReturnsNotFound_WhenFileDoesNotExist()
    {
        // Arrange - forward slashes are allowed for subfolder paths
        var feedName = "mypodcast";
        var filePath = "subdir/episode.mp3";

        // Act
        var result = await _controller.GetMedia(feedName, filePath);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetMedia_WithEmptyFileName_ReturnsBadRequest()
    {
        // Arrange
        var feedName = "mypodcast";

        // Act
        var result = await _controller.GetMedia(feedName, "");

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetMedia_WithTooLongFilePath_ReturnsBadRequest()
    {
        // Arrange
        var feedName = "mypodcast";
        var filePath = new string('a', 501); // exceeds 500 char limit

        // Act
        var result = await _controller.GetMedia(feedName, filePath);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetMedia_WithEmptyFeedName_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetMedia("", "episode.mp3");

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetMedia_WhenFileNotFound_ReturnsNotFound()
    {
        // Arrange
        var feedName = "mypodcast";
        var fileName = "nonexistent.mp3";

        // Act
        var result = await _controller.GetMedia(feedName, fileName);

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
    public async Task GetMedia_ReturnCorrectMimeType(string fileName, string expectedMimeType)
    {
        // Arrange
        var feedName = "mypodcast";
        var testFile = Path.Combine(_testDirectory, fileName);
        File.WriteAllText(testFile, "test content");

        // Act
        var result = await _controller.GetMedia(feedName, fileName);

        // Assert
        var fileResult = Assert.IsType<PhysicalFileResult>(result);
        Assert.Equal(expectedMimeType, fileResult.ContentType);
    }

    #endregion
}
