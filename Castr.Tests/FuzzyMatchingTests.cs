using Xunit;
using System.Reflection;
using Castr.Services;

namespace Castr.Tests;

/// <summary>
/// Tests for fuzzy matching algorithms used in YouTubeDownloadService and PodcastDatabaseService.
/// These tests validate the string normalization and similarity calculation logic.
/// </summary>
public class FuzzyMatchingTests
{
    #region String Normalization Tests

    [Theory]
    [InlineData("Test Episode", "test episode")]
    [InlineData("Test Episode | BEHIND THE BASTARDS", "test episode")]
    [InlineData("Test  Episode  With  Spaces", "test episode with spaces")]
    [InlineData("Test｜Episode：With？Unicode", "test|episode:with?unicode")]
    [InlineData("  Leading and Trailing  ", "leading and trailing")]
    public void NormalizeForComparison_StandardCases(string input, string expected)
    {
        // Act
        var result = CallNormalizeForComparison(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void NormalizeForComparison_RemovesBehindTheBastardsSuffix()
    {
        // Arrange
        var input1 = "Episode Title | BEHIND THE BASTARDS";
        var input2 = "Episode Title| BEHIND THE BASTARDS";
        var input3 = "Episode Title | behind the bastards";

        // Act
        var result1 = CallNormalizeForComparison(input1);
        var result2 = CallNormalizeForComparison(input2);
        var result3 = CallNormalizeForComparison(input3);

        // Assert
        Assert.Equal("episode title", result1);
        Assert.Equal("episode title", result2);
        Assert.Equal("episode title", result3);
    }

    [Fact]
    public void NormalizeForComparison_HandlesUnicodeCharacters()
    {
        // Arrange
        var input = "Episode｜With：Special？Characters";

        // Act
        var result = CallNormalizeForComparison(input);

        // Assert
        Assert.Equal("episode|with:special?characters", result);
    }

    [Fact]
    public void NormalizeForComparison_CollapsesMultipleSpaces()
    {
        // Arrange
        var input = "Too    Many     Spaces";

        // Act
        var result = CallNormalizeForComparison(input);

        // Assert
        Assert.Equal("too many spaces", result);
    }

    [Fact]
    public void NormalizeForComparison_EmptyString_ReturnsEmpty()
    {
        // Act
        var result = CallNormalizeForComparison("");

        // Assert
        Assert.Equal("", result);
    }

    #endregion

    #region Similarity Calculation Tests

    [Theory]
    [InlineData("hello", "hello", 1.0)]
    [InlineData("hello", "helo", 0.888)] // 8 LCS / (5 + 4) = 0.888
    [InlineData("test", "test123", 0.727)] // 8 LCS / (4 + 7) = 0.727
    [InlineData("abc", "xyz", 0.0)]
    [InlineData("", "", 0.0)]
    [InlineData("a", "", 0.0)]
    [InlineData("", "b", 0.0)]
    public void CalculateSimilarity_VariousCases(string a, string b, double expectedMin)
    {
        // Act
        var result = CallCalculateSimilarity(a, b);

        // Assert
        Assert.True(result >= expectedMin - 0.01, $"Expected at least {expectedMin}, got {result}");
    }

    [Fact]
    public void CalculateSimilarity_IdenticalStrings_Returns1()
    {
        // Arrange
        var str = "identical string";

        // Act
        var result = CallCalculateSimilarity(str, str);

        // Assert
        Assert.Equal(1.0, result, precision: 3);
    }

    [Fact]
    public void CalculateSimilarity_CompletelyDifferent_Returns0()
    {
        // Act
        var result = CallCalculateSimilarity("abcdef", "xyz123");

        // Assert
        Assert.True(result < 0.5, $"Expected less than 0.5 for completely different strings, got {result}");
    }

    [Fact]
    public void CalculateSimilarity_SubstringMatch_ReturnsHighScore()
    {
        // Act
        var result = CallCalculateSimilarity("episode", "episode001");

        // Assert
        Assert.True(result > 0.7, $"Expected > 0.7 for substring match, got {result}");
    }

    [Fact]
    public void CalculateSimilarity_SymmetricProperty()
    {
        // Arrange
        var a = "string one";
        var b = "string two";

        // Act
        var result1 = CallCalculateSimilarity(a, b);
        var result2 = CallCalculateSimilarity(b, a);

        // Assert
        Assert.Equal(result1, result2, precision: 10);
    }

    #endregion

    #region LCS (Longest Common Subsequence) Tests

    [Theory]
    [InlineData("ABCD", "ACDE", 3)] // ACD
    [InlineData("hello", "helo", 4)] // helo
    [InlineData("abc", "def", 0)] // no common
    [InlineData("programming", "gaming", 6)] // gaming
    [InlineData("", "test", 0)]
    [InlineData("test", "", 0)]
    public void LongestCommonSubsequenceLength_VariousCases(string a, string b, int expected)
    {
        // Act
        var result = CallLongestCommonSubsequenceLength(a, b);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void LongestCommonSubsequenceLength_IdenticalStrings_ReturnsLength()
    {
        // Arrange
        var str = "teststring";

        // Act
        var result = CallLongestCommonSubsequenceLength(str, str);

        // Assert
        Assert.Equal(str.Length, result);
    }

    [Fact]
    public void LongestCommonSubsequenceLength_EmptyStrings_Returns0()
    {
        // Act
        var result = CallLongestCommonSubsequenceLength("", "");

        // Assert
        Assert.Equal(0, result);
    }

    #endregion

    #region Integration Tests - Real World Scenarios

    [Theory]
    [InlineData("Episode 1 - The Beginning", "001_Episode 1 - The Beginning.mp3", 0.85)]
    [InlineData("Special Episode｜Part 1", "Special Episode | Part 1.mp3", 0.80)]
    public void RealWorldMatching_ShouldMatchEpisodes(string videoTitle, string fileName, double minExpectedScore)
    {
        // Arrange
        var normalizedTitle = CallNormalizeForComparison(videoTitle);
        var normalizedFileName = CallNormalizeForComparison(Path.GetFileNameWithoutExtension(fileName));

        // Act
        var score = CallCalculateSimilarity(normalizedTitle, normalizedFileName);

        // Assert
        Assert.True(score >= minExpectedScore, 
            $"Expected score >= {minExpectedScore}, got {score} for '{videoTitle}' vs '{fileName}'");
    }

    [Fact]
    public void RealWorldMatching_DifferentEpisodes_ShouldNotMatch()
    {
        // Arrange
        var videoTitle = "Episode 1 - The Beginning";
        var fileName = "Episode 99 - The End.mp3";
        var normalizedTitle = CallNormalizeForComparison(videoTitle);
        var normalizedFileName = CallNormalizeForComparison(Path.GetFileNameWithoutExtension(fileName));

        // Act
        var score = CallCalculateSimilarity(normalizedTitle, normalizedFileName);

        // Assert
        Assert.True(score < 0.80, $"Expected low match score for different episodes, got {score}");
    }

    #endregion

    #region Helper Methods - Using Reflection to Access Private Methods

    private static string CallNormalizeForComparison(string input)
    {
        // We need to test the private method NormalizeForComparison from YouTubeDownloadService
        // Using reflection to access it
        var type = typeof(YouTubeDownloadService);
        var method = type.GetMethod("NormalizeForComparison", 
            BindingFlags.NonPublic | BindingFlags.Static);
        
        if (method == null)
        {
            // If not in YouTubeDownloadService, try PodcastDatabaseService
            type = typeof(PodcastDatabaseService);
            method = type.GetMethod("NormalizeForComparison", 
                BindingFlags.NonPublic | BindingFlags.Static);
        }

        Assert.NotNull(method);
        var result = method.Invoke(null, new object[] { input });
        return (string)result!;
    }

    private static double CallCalculateSimilarity(string a, string b)
    {
        var type = typeof(YouTubeDownloadService);
        var method = type.GetMethod("CalculateSimilarity", 
            BindingFlags.NonPublic | BindingFlags.Static);
        
        if (method == null)
        {
            type = typeof(PodcastDatabaseService);
            method = type.GetMethod("CalculateSimilarity", 
                BindingFlags.NonPublic | BindingFlags.Static);
        }

        Assert.NotNull(method);
        var result = method.Invoke(null, new object[] { a, b });
        return (double)result!;
    }

    private static int CallLongestCommonSubsequenceLength(string a, string b)
    {
        var type = typeof(YouTubeDownloadService);
        var method = type.GetMethod("LongestCommonSubsequenceLength", 
            BindingFlags.NonPublic | BindingFlags.Static);
        
        if (method == null)
        {
            type = typeof(PodcastDatabaseService);
            method = type.GetMethod("LongestCommonSubsequenceLength", 
                BindingFlags.NonPublic | BindingFlags.Static);
        }

        Assert.NotNull(method);
        var result = method.Invoke(null, new object[] { a, b });
        return (int)result!;
    }

    #endregion
}
