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
    public async Task GetMedia_WithSubfolderPath_ServesFile()
    {
        // Arrange - create a test file in a subdirectory
        var subDir = Path.Combine(_testDirectory, "season1");
        Directory.CreateDirectory(subDir);
        var testFileName = "episode.mp3";
        File.WriteAllBytes(Path.Combine(subDir, testFileName), new byte[] { 0xFF, 0xFB, 0x90, 0x00 });

        // Act
        var result = await _controller.GetMedia("testfeed", "season1/episode.mp3");

        // Assert
        var fileResult = Assert.IsType<PhysicalFileResult>(result);
        Assert.Equal("audio/mpeg", fileResult.ContentType);
        Assert.True(fileResult.EnableRangeProcessing);
    }

    [Fact]
    public async Task GetMedia_WithDoubleDotInSubfolderPath_ReturnsBadRequest()
    {
        // Arrange
        var maliciousPath = "season1/../../../etc/passwd";

        // Act
        var result = await _controller.GetMedia("testfeed", maliciousPath);

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
    public async Task GetMedia_WithTooLongFilePath_ReturnsBadRequest()
    {
        // Arrange
        var longName = new string('a', 501);

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

    #region GetArtwork Tests

    [Fact]
    public async Task GetArtwork_WithNullFeedName_ReturnsBadRequest()
    {
        var result = await _controller.GetArtwork(null!, "episode.mp3");
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetArtwork_WithEmptyFeedName_ReturnsBadRequest()
    {
        var result = await _controller.GetArtwork("", "episode.mp3");
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetArtwork_WithTooLongFeedName_ReturnsBadRequest()
    {
        var result = await _controller.GetArtwork(new string('a', 101), "episode.mp3");
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetArtwork_WithNullFilePath_ReturnsBadRequest()
    {
        var result = await _controller.GetArtwork("testfeed", null!);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetArtwork_WithEmptyFilePath_ReturnsBadRequest()
    {
        var result = await _controller.GetArtwork("testfeed", "");
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetArtwork_WithTooLongFilePath_ReturnsBadRequest()
    {
        var result = await _controller.GetArtwork("testfeed", new string('a', 501));
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetArtwork_WithPathTraversal_ReturnsBadRequest()
    {
        var result = await _controller.GetArtwork("testfeed", "../../../etc/passwd");
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetArtwork_WithBackslashTraversal_ReturnsBadRequest()
    {
        var result = await _controller.GetArtwork("testfeed", "..\\..\\etc\\passwd");
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetArtwork_WithNonExistentFeed_ReturnsNotFound()
    {
        var result = await _controller.GetArtwork("nonexistent", "episode.mp3");
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetArtwork_WithNonExistentFile_ReturnsNotFound()
    {
        var result = await _controller.GetArtwork("testfeed", "nonexistent.mp3");
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetArtwork_WithCorruptFile_ReturnsNotFound()
    {
        // Arrange - plain text content causes TagLib to throw CorruptFileException
        File.WriteAllText(Path.Combine(_testDirectory, "noart.mp3"), "not a real mp3");

        // Act
        var result = await _controller.GetArtwork("testfeed", "noart.mp3");

        // Assert - CorruptFileException caught, returns NotFound
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetArtwork_WithDoubleDotInSubfolderPath_ReturnsBadRequest()
    {
        var result = await _controller.GetArtwork("testfeed", "season1/../../../etc/passwd");
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetArtwork_WithWhitespaceFeedName_ReturnsBadRequest()
    {
        var result = await _controller.GetArtwork("   ", "episode.mp3");
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetArtwork_WithWhitespaceFilePath_ReturnsBadRequest()
    {
        var result = await _controller.GetArtwork("testfeed", "   ");
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetArtwork_WithEmbeddedArt_ReturnsFileResult()
    {
        // Arrange - create a minimal ID3v2 tagged file with embedded JPEG art
        var filePath = Path.Combine(_testDirectory, "withArt.mp3");
        File.WriteAllBytes(filePath, CreateMinimalId3v2WithEmbeddedJpeg());

        // Act
        var result = await _controller.GetArtwork("testfeed", "withArt.mp3");

        // Assert
        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("image/jpeg", fileResult.ContentType);
        Assert.NotEmpty(fileResult.FileContents);
    }

    [Fact]
    public async Task GetArtwork_WithEmbeddedArtAndEmptyMimeType_ReturnsFileResultWithJpegFallback()
    {
        // Arrange - APIC frame with empty MIME type; controller should fall back to "image/jpeg"
        var filePath = Path.Combine(_testDirectory, "noMimeArt.mp3");
        File.WriteAllBytes(filePath, CreateMinimalId3v2WithEmbeddedJpegNoMime());

        // Act
        var result = await _controller.GetArtwork("testfeed", "noMimeArt.mp3");

        // Assert - MIME fallback to image/jpeg
        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("image/jpeg", fileResult.ContentType);
    }

    /// <summary>
    /// Creates a minimal valid ID3v2.3 file with an embedded JPEG image in an APIC frame,
    /// followed by a minimal MPEG audio frame so TagLib# can parse it without ReadStyle.None.
    /// </summary>
    private static byte[] CreateMinimalId3v2WithEmbeddedJpeg()
    {
        // APIC content: encoding(1) + "image/jpeg\0"(11) + pic_type(1) + desc\0(1) + minimal_JPEG(4) = 18 bytes
        var id3Tag = new byte[]
        {
            // ID3v2.3 header (10 bytes)
            0x49, 0x44, 0x33,       // "ID3"
            0x03, 0x00,             // version 2.3, revision 0
            0x00,                   // no flags
            0x00, 0x00, 0x00, 0x1C, // synchsafe size = 28 (total APIC frame size)
            // APIC frame header (10 bytes)
            0x41, 0x50, 0x49, 0x43, // "APIC"
            0x00, 0x00, 0x00, 0x12, // frame size = 18
            0x00, 0x00,             // no flags
            // APIC content (18 bytes)
            0x00,                                                               // Latin-1 encoding
            0x69, 0x6D, 0x61, 0x67, 0x65, 0x2F, 0x6A, 0x70, 0x65, 0x67, 0x00, // "image/jpeg\0"
            0x03,                   // picture type: Front Cover
            0x00,                   // empty description (null terminator)
            0xFF, 0xD8, 0xFF, 0xD9  // minimal valid JPEG (SOI marker + EOI marker)
        };
        // Append a minimal MPEG1/Layer3/128kbps/44100Hz frame (417 bytes) so TagLib# can
        // parse the audio properties without throwing CorruptFileException.
        var mpegFrame = new byte[417];
        mpegFrame[0] = 0xFF; mpegFrame[1] = 0xFB; mpegFrame[2] = 0x90; mpegFrame[3] = 0x00;
        var result = new byte[id3Tag.Length + mpegFrame.Length];
        id3Tag.CopyTo(result, 0);
        mpegFrame.CopyTo(result, id3Tag.Length);
        return result;
    }

    /// <summary>
    /// Creates a minimal ID3v2.3 file with an APIC frame that has an empty MIME type string,
    /// followed by a minimal MPEG audio frame so TagLib# can parse it without ReadStyle.None.
    /// This exercises the fallback to "image/jpeg" in GetArtwork.
    /// </summary>
    private static byte[] CreateMinimalId3v2WithEmbeddedJpegNoMime()
    {
        // APIC content: encoding(1) + empty_MIME\0(1) + pic_type(1) + desc\0(1) + minimal_JPEG(4) = 8 bytes
        var id3Tag = new byte[]
        {
            // ID3v2.3 header (10 bytes)
            0x49, 0x44, 0x33,       // "ID3"
            0x03, 0x00,             // version 2.3, revision 0
            0x00,                   // no flags
            0x00, 0x00, 0x00, 0x12, // synchsafe size = 18 (total APIC frame size)
            // APIC frame header (10 bytes)
            0x41, 0x50, 0x49, 0x43, // "APIC"
            0x00, 0x00, 0x00, 0x08, // frame size = 8
            0x00, 0x00,             // no flags
            // APIC content (8 bytes)
            0x00,                   // Latin-1 encoding
            0x00,                   // empty MIME type (just null terminator)
            0x03,                   // picture type: Front Cover
            0x00,                   // empty description (null terminator)
            0xFF, 0xD8, 0xFF, 0xD9  // minimal valid JPEG
        };
        // Append a minimal MPEG1/Layer3/128kbps/44100Hz frame (417 bytes) so TagLib# can
        // parse the audio properties without throwing CorruptFileException.
        var mpegFrame = new byte[417];
        mpegFrame[0] = 0xFF; mpegFrame[1] = 0xFB; mpegFrame[2] = 0x90; mpegFrame[3] = 0x00;
        var result = new byte[id3Tag.Length + mpegFrame.Length];
        id3Tag.CopyTo(result, 0);
        mpegFrame.CopyTo(result, id3Tag.Length);
        return result;
    }

    #endregion
}
