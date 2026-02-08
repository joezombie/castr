using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Castr.Tests.TestHelpers;
using Castr.Data.Entities;

namespace Castr.Tests.Controllers;

public class FeedControllerTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly Mock<IPodcastDataService> _mockDataService;
    private readonly PodcastFeedService _feedService;
    private readonly Mock<ILogger<FeedController>> _mockLogger;
    private readonly FeedController _controller;

    public FeedControllerTests()
    {
        _testDirectory = TestDatabaseHelper.CreateTempDirectory();

        var testFeed = new Feed
        {
            Id = 1,
            Name = "testfeed",
            Title = "Test Feed",
            Description = "A test podcast feed",
            Directory = _testDirectory,
            FileExtensions = [".mp3"],
            CacheDurationMinutes = 5
        };
        var anotherFeed = new Feed
        {
            Id = 2,
            Name = "anotherfeed",
            Title = "Another Feed",
            Description = "Another test feed",
            Directory = _testDirectory,
            FileExtensions = [".mp3"],
            CacheDurationMinutes = 5
        };

        _mockDataService = new Mock<IPodcastDataService>();
        _mockDataService.Setup(x => x.GetAllFeedsAsync())
            .ReturnsAsync(new List<Feed> { testFeed, anotherFeed });
        _mockDataService.Setup(x => x.GetFeedByNameAsync("testfeed"))
            .ReturnsAsync(testFeed);
        _mockDataService.Setup(x => x.GetFeedByNameAsync("anotherfeed"))
            .ReturnsAsync(anotherFeed);
        _mockDataService.Setup(x => x.GetFeedByNameAsync(It.IsNotIn("testfeed", "anotherfeed")))
            .ReturnsAsync((Feed?)null);
        _mockDataService.Setup(x => x.GetEpisodesAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<Episode>());

        var cache = new MemoryCache(new MemoryCacheOptions());
        var mockFeedLogger = new Mock<ILogger<PodcastFeedService>>();

        _feedService = new PodcastFeedService(
            _mockDataService.Object,
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
    public async Task GetFeeds_ReturnsOkWithFeedNames()
    {
        // Act
        var result = await _controller.GetFeeds();

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
    public async Task GetMedia_WithPathTraversal_ReturnsBadRequest()
    {
        // Arrange
        var maliciousFileName = "../../../etc/passwd";

        // Act
        var result = await _controller.GetMedia("testfeed", maliciousFileName);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetMedia_WithBackslashTraversal_ReturnsBadRequest()
    {
        // Arrange
        var maliciousFileName = "..\\..\\etc\\passwd";

        // Act
        var result = await _controller.GetMedia("testfeed", maliciousFileName);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetMedia_WithForwardSlash_ReturnsBadRequest()
    {
        // Arrange
        var maliciousFileName = "path/to/file.mp3";

        // Act
        var result = await _controller.GetMedia("testfeed", maliciousFileName);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetMedia_WithNonExistentFeed_ReturnsNotFound()
    {
        // Act
        var result = await _controller.GetMedia("nonexistent", "episode.mp3");

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetMedia_WithNonExistentFile_ReturnsNotFound()
    {
        // Act
        var result = await _controller.GetMedia("testfeed", "nonexistent.mp3");

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetMedia_WithNullFileName_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetMedia("testfeed", null!);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetMedia_WithEmptyFileName_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetMedia("testfeed", "");

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetMedia_WithTooLongFileName_ReturnsBadRequest()
    {
        // Arrange
        var longName = new string('a', 256);

        // Act
        var result = await _controller.GetMedia("testfeed", longName);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetMedia_WithNullFeedName_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetMedia(null!, "episode.mp3");

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetMedia_WithTooLongFeedName_ReturnsBadRequest()
    {
        // Arrange
        var longName = new string('a', 101);

        // Act
        var result = await _controller.GetMedia(longName, "episode.mp3");

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetMedia_WithValidFileReturnsPhysicalFileResult()
    {
        // Arrange - create a test file
        var testFileName = "test-episode.mp3";
        var testFilePath = Path.Combine(_testDirectory, testFileName);
        File.WriteAllBytes(testFilePath, new byte[] { 0xFF, 0xFB, 0x90, 0x00 }); // MP3 header bytes

        // Act
        var result = await _controller.GetMedia("testfeed", testFileName);

        // Assert
        var fileResult = Assert.IsType<PhysicalFileResult>(result);
        Assert.Equal("audio/mpeg", fileResult.ContentType);
        Assert.True(fileResult.EnableRangeProcessing);
    }
}
