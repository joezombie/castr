using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Castr.Services;
using System.Reflection;

namespace Castr.Tests;

/// <summary>
/// Tests for YouTubeDownloadService focusing on file name sanitization,
/// fuzzy matching, and existing file detection.
/// </summary>
public class YouTubeDownloadServiceTests
{
    private readonly Mock<ILogger<YouTubeDownloadService>> _mockLogger;
    private readonly YouTubeDownloadService _service;

    public YouTubeDownloadServiceTests()
    {
        _mockLogger = new Mock<ILogger<YouTubeDownloadService>>();
        _service = new YouTubeDownloadService(_mockLogger.Object);
    }

    #region File Name Sanitization Tests

    [Theory]
    [InlineData("Simple Title", "Simple Title")]
    [InlineData("Title/With/Slashes", "TitleWithSlashes")]
    public void SanitizeFileName_RemovesInvalidCharacters(string input, string expected)
    {
        // Act
        var result = CallSanitizeFileName(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void SanitizeFileName_RemovesBackslashesOnWindows()
    {
        // Arrange
        var input = "Title\\With\\Backslashes";

        // Act
        var result = CallSanitizeFileName(input);

        // Assert
        if (OperatingSystem.IsWindows())
        {
            // Windows treats backslash as invalid
            Assert.Equal("TitleWithBackslashes", result);
        }
        else
        {
            // Linux/Mac allows backslash in filenames
            Assert.Equal(input, result);
        }
    }

    [Fact]
    public void SanitizeFileName_TruncatesLongNames()
    {
        // Arrange
        var longName = new string('a', 250);

        // Act
        var result = CallSanitizeFileName(longName);

        // Assert
        Assert.True(result.Length <= 200, $"Expected length <= 200, got {result.Length}");
    }

    [Fact]
    public void SanitizeFileName_TrimsWhitespace()
    {
        // Arrange
        var input = "  Title With Spaces  ";

        // Act
        var result = CallSanitizeFileName(input);

        // Assert
        Assert.Equal("Title With Spaces", result);
    }

    #endregion

    #region GetExistingFilePath Tests

    [Fact]
    public void GetExistingFilePath_ExactMatch_ReturnsPath()
    {
        // Arrange
        var testDir = CreateTestDirectory();
        var fileName = "episode001.mp3";
        var filePath = Path.Combine(testDir, fileName);
        File.WriteAllText(filePath, "test");

        try
        {
            // Act
            var result = _service.GetExistingFilePath("episode001", testDir);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(filePath, result);
        }
        finally
        {
            CleanupTestDirectory(testDir);
        }
    }

    [Fact]
    public void GetExistingFilePath_NonExistentDirectory_ReturnsNull()
    {
        // Arrange
        var nonExistentDir = Path.Combine(Path.GetTempPath(), "nonexistent_" + Guid.NewGuid());

        // Act
        var result = _service.GetExistingFilePath("episode001", nonExistentDir);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetExistingFilePath_EmptyDirectory_ReturnsNull()
    {
        // Arrange
        var testDir = CreateTestDirectory();

        try
        {
            // Act
            var result = _service.GetExistingFilePath("episode001", testDir);

            // Assert
            Assert.Null(result);
        }
        finally
        {
            CleanupTestDirectory(testDir);
        }
    }

    [Fact]
    public void GetExistingFilePath_FuzzyMatch_ReturnsPath()
    {
        // Arrange
        var testDir = CreateTestDirectory();
        var fileName = "001_Episode One - The Beginning.mp3";
        var filePath = Path.Combine(testDir, fileName);
        File.WriteAllText(filePath, "test");

        try
        {
            // Act - search with similar but not exact title
            var result = _service.GetExistingFilePath("Episode One: The Beginning", testDir);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(filePath, result);
        }
        finally
        {
            CleanupTestDirectory(testDir);
        }
    }

    [Fact]
    public void GetExistingFilePath_BelowThreshold_ReturnsNull()
    {
        // Arrange
        var testDir = CreateTestDirectory();
        var fileName = "completely_different_episode.mp3";
        var filePath = Path.Combine(testDir, fileName);
        File.WriteAllText(filePath, "test");

        try
        {
            // Act
            var result = _service.GetExistingFilePath("Unrelated Video Title", testDir);

            // Assert
            Assert.Null(result);
        }
        finally
        {
            CleanupTestDirectory(testDir);
        }
    }

    [Fact]
    public void GetExistingFilePath_MultipleMp3Files_ReturnsBestMatch()
    {
        // Arrange
        var testDir = CreateTestDirectory();
        File.WriteAllText(Path.Combine(testDir, "episode001.mp3"), "test");
        File.WriteAllText(Path.Combine(testDir, "episode002.mp3"), "test");
        var targetFile = Path.Combine(testDir, "episode001 - The First Episode.mp3");
        File.WriteAllText(targetFile, "test");

        try
        {
            // Act
            var result = _service.GetExistingFilePath("Episode 001: The First Episode", testDir);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("First Episode", result);
        }
        finally
        {
            CleanupTestDirectory(testDir);
        }
    }

    [Fact]
    public void GetExistingFilePath_IgnoresNonMp3Files()
    {
        // Arrange
        var testDir = CreateTestDirectory();
        File.WriteAllText(Path.Combine(testDir, "episode001.txt"), "test");
        File.WriteAllText(Path.Combine(testDir, "episode001.wav"), "test");

        try
        {
            // Act
            var result = _service.GetExistingFilePath("episode001", testDir);

            // Assert
            Assert.Null(result);
        }
        finally
        {
            CleanupTestDirectory(testDir);
        }
    }

    #endregion

    #region Helper Methods

    private static string CreateTestDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "castr_test_" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void CleanupTestDirectory(string dir)
    {
        if (Directory.Exists(dir))
        {
            try
            {
                Directory.Delete(dir, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    private static string CallSanitizeFileName(string input)
    {
        var type = typeof(YouTubeDownloadService);
        var method = type.GetMethod("SanitizeFileName", 
            BindingFlags.NonPublic | BindingFlags.Static);
        
        Assert.NotNull(method);
        var result = method.Invoke(null, new object[] { input });
        return (string)result!;
    }

    #endregion
}
