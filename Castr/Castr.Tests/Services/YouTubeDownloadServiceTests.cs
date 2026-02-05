using Castr.Tests.TestHelpers;

namespace Castr.Tests.Services;

public class YouTubeDownloadServiceTests : IDisposable
{
    private readonly Mock<ILogger<YouTubeDownloadService>> _mockLogger;
    private readonly string _testDirectory;

    public YouTubeDownloadServiceTests()
    {
        _mockLogger = new Mock<ILogger<YouTubeDownloadService>>();
        _testDirectory = TestDatabaseHelper.CreateTempDirectory();
    }

    public void Dispose()
    {
        TestDatabaseHelper.DeleteDirectory(_testDirectory);
    }

    [Fact]
    public void GetExistingFilePath_WithExactMatch_ReturnsPath()
    {
        // Arrange
        var service = new YouTubeDownloadService(_mockLogger.Object);
        var fileName = "Test Episode Title.mp3";
        var filePath = Path.Combine(_testDirectory, fileName);
        File.WriteAllText(filePath, "test content");

        // Act
        var result = service.GetExistingFilePath("Test Episode Title", _testDirectory);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(filePath, result);
    }

    [Fact]
    public void GetExistingFilePath_WithSimilarMatch_ReturnsPath()
    {
        // Arrange
        var service = new YouTubeDownloadService(_mockLogger.Object);
        var fileName = "My Podcast - Episode 123.mp3";
        var filePath = Path.Combine(_testDirectory, fileName);
        File.WriteAllText(filePath, "test content");

        // Act - search with slightly different title
        var result = service.GetExistingFilePath("My Podcast Episode 123", _testDirectory);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void GetExistingFilePath_WithNoMatch_ReturnsNull()
    {
        // Arrange
        var service = new YouTubeDownloadService(_mockLogger.Object);
        File.WriteAllText(Path.Combine(_testDirectory, "Completely Different.mp3"), "test");

        // Act
        var result = service.GetExistingFilePath("No Match Here At All Whatsoever", _testDirectory);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetExistingFilePath_WithEmptyDirectory_ReturnsNull()
    {
        // Arrange
        var service = new YouTubeDownloadService(_mockLogger.Object);

        // Act
        var result = service.GetExistingFilePath("Some Title", _testDirectory);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetExistingFilePath_WithNonExistentDirectory_ReturnsNull()
    {
        // Arrange
        var service = new YouTubeDownloadService(_mockLogger.Object);
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        // Act
        var result = service.GetExistingFilePath("Some Title", nonExistentPath);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetExistingFilePath_MatchesMultipleFormats()
    {
        // Arrange
        var service = new YouTubeDownloadService(_mockLogger.Object);
        File.WriteAllText(Path.Combine(_testDirectory, "Episode Title.mp3"), "test");
        File.WriteAllText(Path.Combine(_testDirectory, "Another Title.m4a"), "test");

        // Act - should find mp3
        var result = service.GetExistingFilePath("Episode Title", _testDirectory);

        // Assert
        Assert.NotNull(result);
        Assert.EndsWith(".mp3", result);
    }

    [Fact]
    public void GetExistingFilePath_HandlesSpecialCharacters()
    {
        // Arrange
        var service = new YouTubeDownloadService(_mockLogger.Object);
        var fileName = "Episode - Part 1 (2024).mp3";
        File.WriteAllText(Path.Combine(_testDirectory, fileName), "test");

        // Act
        var result = service.GetExistingFilePath("Episode - Part 1 (2024)", _testDirectory);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void GetExistingFilePath_IsCaseInsensitive()
    {
        // Arrange
        var service = new YouTubeDownloadService(_mockLogger.Object);
        File.WriteAllText(Path.Combine(_testDirectory, "UPPERCASE TITLE.mp3"), "test");

        // Act
        var result = service.GetExistingFilePath("uppercase title", _testDirectory);

        // Assert
        Assert.NotNull(result);
    }
}
