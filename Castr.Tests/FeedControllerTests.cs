using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Castr.Controllers;
using Castr.Services;

namespace Castr.Tests;

public class FeedControllerTests
{
    private readonly Mock<PodcastFeedService> _mockFeedService;
    private readonly Mock<ILogger<FeedController>> _mockLogger;
    private readonly FeedController _controller;

    public FeedControllerTests()
    {
        _mockFeedService = new Mock<PodcastFeedService>();
        _mockLogger = new Mock<ILogger<FeedController>>();
        _controller = new FeedController(_mockFeedService.Object, _mockLogger.Object);
        
        // Setup HttpContext for base URL generation
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        _controller.HttpContext.Request.Scheme = "http";
        _controller.HttpContext.Request.Host = new HostString("localhost:5000");
    }

    #region GetFeeds Tests

    [Fact]
    public void GetFeeds_ReturnsOkWithFeedList()
    {
        // Arrange
        var feedNames = new[] { "btb", "techfeed" };
        _mockFeedService.Setup(s => s.GetFeedNames()).Returns(feedNames);

        // Act
        var result = _controller.GetFeeds();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public void GetFeeds_ReturnsEmptyList_WhenNoFeeds()
    {
        // Arrange
        _mockFeedService.Setup(s => s.GetFeedNames()).Returns(Array.Empty<string>());

        // Act
        var result = _controller.GetFeeds();

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
        var xmlContent = "<?xml version=\"1.0\"?><rss><channel><title>Test</title></channel></rss>";
        _mockFeedService.Setup(s => s.FeedExists(feedName)).Returns(true);
        _mockFeedService.Setup(s => s.GenerateFeedAsync(feedName, It.IsAny<string>()))
            .ReturnsAsync(xmlContent);

        // Act
        var result = await _controller.GetFeed(feedName);

        // Assert
        var contentResult = Assert.IsType<ContentResult>(result);
        Assert.Equal("application/rss+xml; charset=utf-8", contentResult.ContentType);
        Assert.Equal(xmlContent, contentResult.Content);
    }

    [Fact]
    public async Task GetFeed_WithInvalidFeedName_ReturnsNotFound()
    {
        // Arrange
        var feedName = "nonexistent";
        _mockFeedService.Setup(s => s.FeedExists(feedName)).Returns(false);

        // Act
        var result = await _controller.GetFeed(feedName);

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

    [Fact]
    public async Task GetFeed_WhenGenerationFails_ReturnsNotFound()
    {
        // Arrange
        var feedName = "btb";
        _mockFeedService.Setup(s => s.FeedExists(feedName)).Returns(true);
        _mockFeedService.Setup(s => s.GenerateFeedAsync(feedName, It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        // Act
        var result = await _controller.GetFeed(feedName);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
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
        var testDir = Path.Combine(Path.GetTempPath(), "castr_test_" + Guid.NewGuid());
        Directory.CreateDirectory(testDir);
        var testFile = Path.Combine(testDir, fileName);
        File.WriteAllText(testFile, "test content");
        
        _mockFeedService.Setup(s => s.GetMediaFilePath(feedName, fileName))
            .Returns(testFile);

        try
        {
            // Act
            var result = _controller.GetMedia(feedName, fileName);

            // Assert
            var fileResult = Assert.IsType<PhysicalFileResult>(result);
            Assert.Equal("audio/mpeg", fileResult.ContentType);
            Assert.Equal(testFile, fileResult.FileName);
            Assert.True(fileResult.EnableRangeProcessing);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(testDir))
                Directory.Delete(testDir, true);
        }
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
        _mockFeedService.Setup(s => s.GetMediaFilePath(feedName, fileName))
            .Returns((string?)null);

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
        var testDir = Path.Combine(Path.GetTempPath(), "castr_test_" + Guid.NewGuid());
        Directory.CreateDirectory(testDir);
        var testFile = Path.Combine(testDir, fileName);
        File.WriteAllText(testFile, "test content");
        
        _mockFeedService.Setup(s => s.GetMediaFilePath(feedName, fileName))
            .Returns(testFile);

        try
        {
            // Act
            var result = _controller.GetMedia(feedName, fileName);

            // Assert
            var fileResult = Assert.IsType<PhysicalFileResult>(result);
            Assert.Equal(expectedMimeType, fileResult.ContentType);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(testDir))
                Directory.Delete(testDir, true);
        }
    }

    #endregion
}
