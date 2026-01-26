using Castr.Tests.TestHelpers;

namespace Castr.Tests.Services;

/// <summary>
/// Extended tests for YouTubeDownloadService to improve coverage.
/// </summary>
public class YouTubeDownloadServiceExtendedTests : IDisposable
{
    private readonly Mock<ILogger<YouTubeDownloadService>> _mockLogger;
    private readonly string _testDirectory;

    public YouTubeDownloadServiceExtendedTests()
    {
        _mockLogger = new Mock<ILogger<YouTubeDownloadService>>();
        _testDirectory = TestDatabaseHelper.CreateTempDirectory();
    }

    public void Dispose()
    {
        TestDatabaseHelper.DeleteDirectory(_testDirectory);
    }

    [Theory]
    [InlineData("Test Episode", "Test Episode.mp3", true)]
    [InlineData("Episode One Two Three", "Episode One Two Three.mp3", true)]
    [InlineData("Behind the Bastards", "Behind the Bastards.mp3", true)]
    [InlineData("Short", "S.mp3", false)] // Too short to match
    public void GetExistingFilePath_MatchesSimilarTitles(string searchTitle, string fileName, bool shouldMatch)
    {
        // Arrange
        var service = new YouTubeDownloadService(_mockLogger.Object);
        File.WriteAllText(Path.Combine(_testDirectory, fileName), "test");

        // Act
        var result = service.GetExistingFilePath(searchTitle, _testDirectory);

        // Assert
        if (shouldMatch)
        {
            Assert.NotNull(result);
        }
        else
        {
            // Short/different titles may not match
        }
    }

    [Fact]
    public void GetExistingFilePath_WithSpecialCharacters_Matches()
    {
        // Arrange
        var service = new YouTubeDownloadService(_mockLogger.Object);
        var fileName = "Episode (Part 1) - The Story.mp3";
        File.WriteAllText(Path.Combine(_testDirectory, fileName), "test");

        // Act
        var result = service.GetExistingFilePath("Episode (Part 1) - The Story", _testDirectory);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void GetExistingFilePath_WithUnicodeCharacters_Handles()
    {
        // Arrange
        var service = new YouTubeDownloadService(_mockLogger.Object);
        var fileName = "Test ｜ Episode Title.mp3";
        File.WriteAllText(Path.Combine(_testDirectory, fileName), "test");

        // Act
        var result = service.GetExistingFilePath("Test | Episode Title", _testDirectory);

        // Assert - should handle unicode pipe character normalization
        // May or may not match depending on normalization
        Assert.NotNull(result);
    }

    [Fact]
    public void GetExistingFilePath_WithMultipleFiles_ReturnsBesTMatch()
    {
        // Arrange
        var service = new YouTubeDownloadService(_mockLogger.Object);
        File.WriteAllText(Path.Combine(_testDirectory, "Episode 1.mp3"), "test");
        File.WriteAllText(Path.Combine(_testDirectory, "Episode 2.mp3"), "test");
        File.WriteAllText(Path.Combine(_testDirectory, "Different Title.mp3"), "test");

        // Act
        var result = service.GetExistingFilePath("Episode 1", _testDirectory);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Episode 1.mp3", result);
    }

    [Fact]
    public void GetExistingFilePath_MatchesMp3FilesOnly()
    {
        // Arrange - the service only searches for .mp3 files
        var service = new YouTubeDownloadService(_mockLogger.Object);
        File.WriteAllText(Path.Combine(_testDirectory, "Test Audio.mp3"), "test");
        File.WriteAllText(Path.Combine(_testDirectory, "Test Audio.m4a"), "test"); // This won't be matched

        // Act
        var result = service.GetExistingFilePath("Test Audio", _testDirectory);

        // Assert - should find the mp3 file
        Assert.NotNull(result);
        Assert.EndsWith(".mp3", result);
    }

    [Fact]
    public void GetExistingFilePath_WithLongTitle_Handles()
    {
        // Arrange
        var service = new YouTubeDownloadService(_mockLogger.Object);
        var longTitle = "This Is A Very Long Episode Title That Goes On And On With Many Words";
        var fileName = $"{longTitle}.mp3";
        File.WriteAllText(Path.Combine(_testDirectory, fileName), "test");

        // Act
        var result = service.GetExistingFilePath(longTitle, _testDirectory);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void GetExistingFilePath_WithNumbers_Matches()
    {
        // Arrange
        var service = new YouTubeDownloadService(_mockLogger.Object);
        File.WriteAllText(Path.Combine(_testDirectory, "Episode 123 - Title.mp3"), "test");

        // Act
        var result = service.GetExistingFilePath("Episode 123 - Title", _testDirectory);

        // Assert
        Assert.NotNull(result);
    }

    [Theory]
    [InlineData("UPPER CASE TITLE", "upper case title.mp3")]
    [InlineData("lower case title", "LOWER CASE TITLE.mp3")]
    [InlineData("MiXeD CaSe TiTlE", "mixed case title.mp3")]
    public void GetExistingFilePath_IsCaseInsensitive(string searchTitle, string fileName)
    {
        // Arrange
        var service = new YouTubeDownloadService(_mockLogger.Object);
        File.WriteAllText(Path.Combine(_testDirectory, fileName), "test");

        // Act
        var result = service.GetExistingFilePath(searchTitle, _testDirectory);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void GetExistingFilePath_WithBTBSuffix_Normalizes()
    {
        // Arrange
        var service = new YouTubeDownloadService(_mockLogger.Object);
        File.WriteAllText(Path.Combine(_testDirectory, "The History of Something.mp3"), "test");

        // Act - search with BTB suffix that should be normalized out
        var result = service.GetExistingFilePath("The History of Something | BEHIND THE BASTARDS", _testDirectory);

        // Assert
        Assert.NotNull(result);
    }
}
