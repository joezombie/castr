using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Castr.Tests.TestHelpers;
using Castr.Data.Entities;

namespace Castr.Tests.Controllers;

public class FeedControllerTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly PodcastFeedService _feedService;
    private readonly Mock<ILogger<FeedController>> _mockLogger;
    private readonly FeedController _controller;

    public FeedControllerTests()
    {
        _testDirectory = TestDatabaseHelper.CreateTempDirectory();

        var config = new PodcastFeedsConfig
        {
            Feeds = new Dictionary<string, PodcastFeedConfig>
            {
                ["testfeed"] = new PodcastFeedConfig
                {
                    Title = "Test Feed",
                    Description = "A test podcast feed",
                    Directory = _testDirectory
                },
                ["anotherfeed"] = new PodcastFeedConfig
                {
                    Title = "Another Feed",
                    Description = "Another test feed",
                    Directory = _testDirectory
                }
            }
        };

        var mockConfig = new Mock<IOptions<PodcastFeedsConfig>>();
        mockConfig.Setup(x => x.Value).Returns(config);

        var mockDataService = new Mock<IPodcastDataService>();
        // Return null for feed lookup to simulate feed not in database (falls back to alphabetical order)
        mockDataService.Setup(x => x.GetFeedByNameAsync(It.IsAny<string>()))
            .ReturnsAsync((Feed?)null);

        var cache = new MemoryCache(new MemoryCacheOptions());
        var mockFeedLogger = new Mock<ILogger<PodcastFeedService>>();

        _feedService = new PodcastFeedService(
            mockConfig.Object,
            mockDataService.Object,
            cache,
            mockFeedLogger.Object);

        _mockLogger = new Mock<ILogger<FeedController>>();
        _controller = new FeedController(_feedService, _mockLogger.Object);

        // Setup HttpContext
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("localhost", 5000);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    public void Dispose()
    {
        TestDatabaseHelper.DeleteDirectory(_testDirectory);
    }

    [Fact]
    public void GetFeeds_ReturnsOkWithFeedNames()
    {
        // Act
        var result = _controller.GetFeeds();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task GetFeed_WithValidFeed_ReturnsContentResult()
    {
        // Act
        var result = await _controller.GetFeed("testfeed");

        // Assert
        var contentResult = Assert.IsType<ContentResult>(result);
        Assert.Equal("application/rss+xml; charset=utf-8", contentResult.ContentType);
        Assert.Contains("<rss", contentResult.Content);
        Assert.Contains("Test Feed", contentResult.Content);
    }

    [Fact]
    public async Task GetFeed_WithInvalidFeed_ReturnsNotFound()
    {
        // Act
        var result = await _controller.GetFeed("nonexistent");

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetFeed_WithNullFeedName_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetFeed(null!);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
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
    public async Task GetFeed_WithWhitespaceFeedName_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetFeed("   ");

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetFeed_WithTooLongFeedName_ReturnsBadRequest()
    {
        // Arrange
        var longName = new string('a', 101);

        // Act
        var result = await _controller.GetFeed(longName);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void GetMedia_WithPathTraversal_ReturnsBadRequest()
    {
        // Arrange
        var maliciousFileName = "../../../etc/passwd";

        // Act
        var result = _controller.GetMedia("testfeed", maliciousFileName);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void GetMedia_WithBackslashTraversal_ReturnsBadRequest()
    {
        // Arrange
        var maliciousFileName = "..\\..\\etc\\passwd";

        // Act
        var result = _controller.GetMedia("testfeed", maliciousFileName);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void GetMedia_WithForwardSlash_ReturnsBadRequest()
    {
        // Arrange
        var maliciousFileName = "path/to/file.mp3";

        // Act
        var result = _controller.GetMedia("testfeed", maliciousFileName);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void GetMedia_WithNonExistentFeed_ReturnsNotFound()
    {
        // Act
        var result = _controller.GetMedia("nonexistent", "episode.mp3");

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public void GetMedia_WithNonExistentFile_ReturnsNotFound()
    {
        // Act
        var result = _controller.GetMedia("testfeed", "nonexistent.mp3");

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public void GetMedia_WithNullFileName_ReturnsBadRequest()
    {
        // Act
        var result = _controller.GetMedia("testfeed", null!);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void GetMedia_WithEmptyFileName_ReturnsBadRequest()
    {
        // Act
        var result = _controller.GetMedia("testfeed", "");

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void GetMedia_WithTooLongFileName_ReturnsBadRequest()
    {
        // Arrange
        var longName = new string('a', 256);

        // Act
        var result = _controller.GetMedia("testfeed", longName);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void GetMedia_WithNullFeedName_ReturnsBadRequest()
    {
        // Act
        var result = _controller.GetMedia(null!, "episode.mp3");

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void GetMedia_WithTooLongFeedName_ReturnsBadRequest()
    {
        // Arrange
        var longName = new string('a', 101);

        // Act
        var result = _controller.GetMedia(longName, "episode.mp3");

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void GetMedia_WithValidFileReturnsPhysicalFileResult()
    {
        // Arrange - create a test file
        var testFileName = "test-episode.mp3";
        var testFilePath = Path.Combine(_testDirectory, testFileName);
        File.WriteAllBytes(testFilePath, new byte[] { 0xFF, 0xFB, 0x90, 0x00 }); // MP3 header bytes

        // Act
        var result = _controller.GetMedia("testfeed", testFileName);

        // Assert
        var fileResult = Assert.IsType<PhysicalFileResult>(result);
        Assert.Equal("audio/mpeg", fileResult.ContentType);
        Assert.True(fileResult.EnableRangeProcessing);
    }
}
