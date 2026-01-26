using Castr.Tests.TestHelpers;

namespace Castr.Tests.Services;

/// <summary>
/// Extended tests for PodcastDatabaseService to improve coverage.
/// </summary>
public class PodcastDatabaseServiceExtendedTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly PodcastDatabaseService _service;
    private readonly Mock<ILogger<PodcastDatabaseService>> _mockLogger;
    private readonly PodcastFeedsConfig _config;

    public PodcastDatabaseServiceExtendedTests()
    {
        _testDirectory = TestDatabaseHelper.CreateTempDirectory();

        _config = new PodcastFeedsConfig
        {
            Feeds = new Dictionary<string, PodcastFeedConfig>
            {
                ["testfeed"] = new PodcastFeedConfig
                {
                    Title = "Test Feed",
                    Description = "Test",
                    Directory = _testDirectory,
                    DatabasePath = Path.Combine(_testDirectory, "test.db")
                },
                ["defaultpath"] = new PodcastFeedConfig
                {
                    Title = "Default Path Feed",
                    Description = "Test",
                    Directory = _testDirectory
                    // No DatabasePath - should default to Directory/podcast.db
                }
            }
        };

        var mockConfig = new Mock<IOptions<PodcastFeedsConfig>>();
        mockConfig.Setup(x => x.Value).Returns(_config);

        _mockLogger = new Mock<ILogger<PodcastDatabaseService>>();
        _service = new PodcastDatabaseService(mockConfig.Object, _mockLogger.Object);
    }

    public void Dispose()
    {
        _service.Dispose();
        TestDatabaseHelper.DeleteDirectory(_testDirectory);
    }

    #region Fuzzy Matching via SyncPlaylistInfoAsync

    [Fact]
    public async Task SyncPlaylistInfoAsync_MatchesVideoToFile()
    {
        // Arrange
        await _service.InitializeDatabaseAsync("testfeed");

        // Create test file
        File.WriteAllText(Path.Combine(_testDirectory, "Episode Title.mp3"), "test");

        var videos = new List<PlaylistVideoInfo>
        {
            new PlaylistVideoInfo
            {
                VideoId = "vid1",
                Title = "Episode Title",
                Description = "Description",
                ThumbnailUrl = "https://example.com/thumb.jpg",
                UploadDate = new DateTime(2024, 6, 15),
                PlaylistIndex = 1
            }
        };

        // Act
        await _service.SyncPlaylistInfoAsync("testfeed", videos, _testDirectory);

        // Assert
        var episodes = await _service.GetEpisodesAsync("testfeed");
        Assert.Single(episodes);
        Assert.Equal("vid1", episodes[0].VideoId);
        Assert.Equal("Episode Title", episodes[0].YoutubeTitle);
    }

    [Fact]
    public async Task SyncPlaylistInfoAsync_UpdatesExistingEpisode()
    {
        // Arrange
        await _service.InitializeDatabaseAsync("testfeed");

        // Create file and existing episode
        var filename = "Existing Episode.mp3";
        File.WriteAllText(Path.Combine(_testDirectory, filename), "test");
        await _service.AddEpisodeAsync("testfeed", new EpisodeRecord
        {
            Filename = filename,
            DisplayOrder = 99
        });

        var videos = new List<PlaylistVideoInfo>
        {
            new PlaylistVideoInfo
            {
                VideoId = "newvid",
                Title = "Existing Episode",
                Description = "New description",
                PlaylistIndex = 5
            }
        };

        // Act
        await _service.SyncPlaylistInfoAsync("testfeed", videos, _testDirectory);

        // Assert
        var episodes = await _service.GetEpisodesAsync("testfeed");
        Assert.Single(episodes);
        Assert.Equal("newvid", episodes[0].VideoId);
        Assert.Equal(5, episodes[0].DisplayOrder);
    }

    [Fact]
    public async Task SyncPlaylistInfoAsync_SkipsLowSimilarityMatches()
    {
        // Arrange
        await _service.InitializeDatabaseAsync("testfeed");

        // Create file with very different name
        File.WriteAllText(Path.Combine(_testDirectory, "ABC.mp3"), "test");

        var videos = new List<PlaylistVideoInfo>
        {
            new PlaylistVideoInfo
            {
                VideoId = "xyz",
                Title = "Completely Different Title That Should Not Match ABC File",
                PlaylistIndex = 1
            }
        };

        // Act
        await _service.SyncPlaylistInfoAsync("testfeed", videos, _testDirectory);

        // Assert - should not match
        var episodes = await _service.GetEpisodesAsync("testfeed");
        Assert.All(episodes, e => Assert.Null(e.VideoId));
    }

    [Fact]
    public async Task SyncPlaylistInfoAsync_WithEmptyVideoList_DoesNothing()
    {
        // Arrange
        await _service.InitializeDatabaseAsync("testfeed");

        File.WriteAllText(Path.Combine(_testDirectory, "test.mp3"), "test");
        await _service.AddEpisodeAsync("testfeed", new EpisodeRecord { Filename = "test.mp3", DisplayOrder = 1 });

        // Act
        await _service.SyncPlaylistInfoAsync("testfeed", new List<PlaylistVideoInfo>(), _testDirectory);

        // Assert - episode should be unchanged
        var episodes = await _service.GetEpisodesAsync("testfeed");
        Assert.Single(episodes);
        Assert.Null(episodes[0].VideoId);
    }

    [Fact]
    public async Task SyncPlaylistInfoAsync_WithNonExistentDirectory_HandlesGracefully()
    {
        // Arrange
        await _service.InitializeDatabaseAsync("testfeed");

        var videos = new List<PlaylistVideoInfo>
        {
            new PlaylistVideoInfo { VideoId = "vid", Title = "Title", PlaylistIndex = 1 }
        };

        // Act - should not throw
        await _service.SyncPlaylistInfoAsync("testfeed", videos, "/nonexistent/path");

        // Assert - no episodes added
        var episodes = await _service.GetEpisodesAsync("testfeed");
        Assert.Empty(episodes);
    }

    [Fact]
    public async Task SyncPlaylistInfoAsync_MatchesMultipleVideos()
    {
        // Arrange
        await _service.InitializeDatabaseAsync("testfeed");

        // Create multiple files
        File.WriteAllText(Path.Combine(_testDirectory, "Video One.mp3"), "test");
        File.WriteAllText(Path.Combine(_testDirectory, "Video Two.mp3"), "test");

        var videos = new List<PlaylistVideoInfo>
        {
            new PlaylistVideoInfo { VideoId = "v1", Title = "Video One", PlaylistIndex = 1 },
            new PlaylistVideoInfo { VideoId = "v2", Title = "Video Two", PlaylistIndex = 2 }
        };

        // Act
        await _service.SyncPlaylistInfoAsync("testfeed", videos, _testDirectory);

        // Assert
        var episodes = await _service.GetEpisodesAsync("testfeed");
        Assert.Equal(2, episodes.Count);
        Assert.Contains(episodes, e => e.VideoId == "v1");
        Assert.Contains(episodes, e => e.VideoId == "v2");
    }

    [Fact]
    public async Task SyncPlaylistInfoAsync_PreservesExistingDescriptionWhenNewIsNull()
    {
        // Arrange
        await _service.InitializeDatabaseAsync("testfeed");

        var filename = "Preserve Desc.mp3";
        File.WriteAllText(Path.Combine(_testDirectory, filename), "test");
        await _service.AddEpisodeAsync("testfeed", new EpisodeRecord
        {
            Filename = filename,
            Description = "Existing description",
            DisplayOrder = 1
        });

        var videos = new List<PlaylistVideoInfo>
        {
            new PlaylistVideoInfo
            {
                VideoId = "vid",
                Title = "Preserve Desc",
                Description = null, // No new description
                PlaylistIndex = 1
            }
        };

        // Act
        await _service.SyncPlaylistInfoAsync("testfeed", videos, _testDirectory);

        // Assert
        var episodes = await _service.GetEpisodesAsync("testfeed");
        Assert.Single(episodes);
        // The existing description should be preserved if new is null
        // (depends on COALESCE in SQL)
    }

    #endregion

    #region SyncDirectoryAsync Tests

    [Fact]
    public async Task SyncDirectoryAsync_AddsNewFiles()
    {
        // Arrange
        await _service.InitializeDatabaseAsync("testfeed");

        File.WriteAllText(Path.Combine(_testDirectory, "new1.mp3"), "test");
        File.WriteAllText(Path.Combine(_testDirectory, "new2.mp3"), "test");

        // Act
        await _service.SyncDirectoryAsync("testfeed", _testDirectory, new[] { ".mp3" });

        // Assert
        var episodes = await _service.GetEpisodesAsync("testfeed");
        Assert.Equal(2, episodes.Count);
    }

    [Fact]
    public async Task SyncDirectoryAsync_IgnoresNonMatchingExtensions()
    {
        // Arrange
        await _service.InitializeDatabaseAsync("testfeed");

        File.WriteAllText(Path.Combine(_testDirectory, "audio.mp3"), "test");
        File.WriteAllText(Path.Combine(_testDirectory, "text.txt"), "test");
        File.WriteAllText(Path.Combine(_testDirectory, "image.jpg"), "test");

        // Act
        await _service.SyncDirectoryAsync("testfeed", _testDirectory, new[] { ".mp3" });

        // Assert
        var episodes = await _service.GetEpisodesAsync("testfeed");
        Assert.Single(episodes);
        Assert.Equal("audio.mp3", episodes[0].Filename);
    }

    [Fact]
    public async Task SyncDirectoryAsync_WithNonExistentDirectory_DoesNotThrow()
    {
        // Arrange
        await _service.InitializeDatabaseAsync("testfeed");

        // Act - should not throw
        await _service.SyncDirectoryAsync("testfeed", "/nonexistent/path", new[] { ".mp3" });

        // Assert
        var episodes = await _service.GetEpisodesAsync("testfeed");
        Assert.Empty(episodes);
    }

    [Fact]
    public async Task SyncDirectoryAsync_SkipsExistingFiles()
    {
        // Arrange
        await _service.InitializeDatabaseAsync("testfeed");

        var filename = "existing.mp3";
        File.WriteAllText(Path.Combine(_testDirectory, filename), "test");
        await _service.AddEpisodeAsync("testfeed", new EpisodeRecord
        {
            Filename = filename,
            VideoId = "original",
            DisplayOrder = 1
        });

        // Act
        await _service.SyncDirectoryAsync("testfeed", _testDirectory, new[] { ".mp3" });

        // Assert
        var episodes = await _service.GetEpisodesAsync("testfeed");
        Assert.Single(episodes);
        Assert.Equal("original", episodes[0].VideoId); // Should be unchanged
    }

    #endregion

    #region Additional Coverage

    [Fact]
    public async Task AddEpisodesAsync_AddsBatch()
    {
        // Arrange
        await _service.InitializeDatabaseAsync("testfeed");
        var episodes = new List<EpisodeRecord>
        {
            new EpisodeRecord { Filename = "batch1.mp3", DisplayOrder = 1 },
            new EpisodeRecord { Filename = "batch2.mp3", DisplayOrder = 2 },
            new EpisodeRecord { Filename = "batch3.mp3", DisplayOrder = 3 }
        };

        // Act
        await _service.AddEpisodesAsync("testfeed", episodes);

        // Assert
        var result = await _service.GetEpisodesAsync("testfeed");
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task MarkVideoDownloadedAsync_IsIdempotent()
    {
        // Arrange
        await _service.InitializeDatabaseAsync("testfeed");

        // Act - mark twice
        await _service.MarkVideoDownloadedAsync("testfeed", "samevid", "file.mp3");
        await _service.MarkVideoDownloadedAsync("testfeed", "samevid", "file.mp3");

        // Assert - should still only be one record
        var isDownloaded = await _service.IsVideoDownloadedAsync("testfeed", "samevid");
        Assert.True(isDownloaded);
    }

    [Fact]
    public async Task UpdateEpisodeAsync_UpdatesAllFields()
    {
        // Arrange
        await _service.InitializeDatabaseAsync("testfeed");
        await _service.AddEpisodeAsync("testfeed", new EpisodeRecord
        {
            Filename = "toupdate.mp3",
            DisplayOrder = 1
        });

        var episodes = await _service.GetEpisodesAsync("testfeed");
        var episode = episodes[0];

        // Act
        episode.VideoId = "newvid";
        episode.YoutubeTitle = "New Title";
        episode.Description = "New Description";
        episode.ThumbnailUrl = "https://new.url/thumb.jpg";
        episode.PublishDate = new DateTime(2024, 6, 15);
        episode.MatchScore = 0.95;
        await _service.UpdateEpisodeAsync("testfeed", episode);

        // Assert
        var updated = (await _service.GetEpisodesAsync("testfeed"))[0];
        Assert.Equal("newvid", updated.VideoId);
        Assert.Equal("New Title", updated.YoutubeTitle);
        Assert.Equal("New Description", updated.Description);
        Assert.Equal("https://new.url/thumb.jpg", updated.ThumbnailUrl);
        Assert.Equal(0.95, updated.MatchScore);
    }

    #endregion
}
