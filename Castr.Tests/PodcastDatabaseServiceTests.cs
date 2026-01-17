using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Castr.Services;
using Castr.Models;

namespace Castr.Tests;

/// <summary>
/// Tests for PodcastDatabaseService covering database operations,
/// episode tracking, and playlist synchronization.
/// </summary>
public class PodcastDatabaseServiceTests : IDisposable
{
    private readonly Mock<ILogger<PodcastDatabaseService>> _mockLogger;
    private readonly PodcastDatabaseService _service;
    private readonly string _testDbPath;
    private readonly string _testDirectory;

    public PodcastDatabaseServiceTests()
    {
        _mockLogger = new Mock<ILogger<PodcastDatabaseService>>();
        _testDirectory = Path.Combine(Path.GetTempPath(), "castr_db_test_" + Guid.NewGuid());
        Directory.CreateDirectory(_testDirectory);
        _testDbPath = Path.Combine(_testDirectory, "test.db");

        var config = new PodcastFeedsConfig
        {
            Feeds = new Dictionary<string, PodcastFeedConfig>
            {
                ["testfeed"] = new PodcastFeedConfig
                {
                    Title = "Test Feed",
                    Description = "Test Description",
                    Directory = _testDirectory,
                    DatabasePath = _testDbPath
                }
            }
        };

        var options = Options.Create(config);
        _service = new PodcastDatabaseService(options, _mockLogger.Object);
    }

    public void Dispose()
    {
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

    #region Database Initialization Tests

    [Fact]
    public async Task InitializeDatabaseAsync_CreatesDatabase()
    {
        // Act
        await _service.InitializeDatabaseAsync("testfeed");

        // Assert
        Assert.True(File.Exists(_testDbPath), "Database file should be created");
    }

    [Fact]
    public async Task InitializeDatabaseAsync_CalledTwice_DoesNotThrow()
    {
        // Act & Assert
        await _service.InitializeDatabaseAsync("testfeed");
        await _service.InitializeDatabaseAsync("testfeed"); // Should not throw
        
        Assert.True(File.Exists(_testDbPath));
    }

    [Fact]
    public async Task InitializeDatabaseAsync_CreatesDirectoryIfNotExists()
    {
        // Arrange
        var newDir = Path.Combine(Path.GetTempPath(), "castr_new_" + Guid.NewGuid());
        var newDbPath = Path.Combine(newDir, "podcast.db");
        
        var config = new PodcastFeedsConfig
        {
            Feeds = new Dictionary<string, PodcastFeedConfig>
            {
                ["newfeed"] = new PodcastFeedConfig
                {
                    Title = "New Feed",
                    Description = "Test",
                    Directory = newDir,
                    DatabasePath = newDbPath
                }
            }
        };
        var service = new PodcastDatabaseService(Options.Create(config), _mockLogger.Object);

        try
        {
            // Act
            await service.InitializeDatabaseAsync("newfeed");

            // Assert
            Assert.True(Directory.Exists(newDir), "Directory should be created");
            Assert.True(File.Exists(newDbPath), "Database should be created");
        }
        finally
        {
            if (Directory.Exists(newDir))
                Directory.Delete(newDir, true);
        }
    }

    #endregion

    #region Episode CRUD Tests

    [Fact]
    public async Task AddEpisodeAsync_AddsEpisode()
    {
        // Arrange
        await _service.InitializeDatabaseAsync("testfeed");
        var episode = new EpisodeRecord
        {
            Filename = "episode001.mp3",
            VideoId = "abc123",
            YoutubeTitle = "Episode 1",
            DisplayOrder = 1,
            AddedAt = DateTime.UtcNow
        };

        // Act
        await _service.AddEpisodeAsync("testfeed", episode);
        var episodes = await _service.GetEpisodesAsync("testfeed");

        // Assert
        Assert.Single(episodes);
        Assert.Equal("episode001.mp3", episodes[0].Filename);
        Assert.Equal("abc123", episodes[0].VideoId);
    }

    [Fact]
    public async Task AddEpisodesAsync_AddsMultipleEpisodes()
    {
        // Arrange
        await _service.InitializeDatabaseAsync("testfeed");
        var episodes = new[]
        {
            new EpisodeRecord
            {
                Filename = "episode001.mp3",
                VideoId = "abc123",
                YoutubeTitle = "Episode 1",
                DisplayOrder = 1,
                AddedAt = DateTime.UtcNow
            },
            new EpisodeRecord
            {
                Filename = "episode002.mp3",
                VideoId = "def456",
                YoutubeTitle = "Episode 2",
                DisplayOrder = 2,
                AddedAt = DateTime.UtcNow
            }
        };

        // Act
        await _service.AddEpisodesAsync("testfeed", episodes);
        var result = await _service.GetEpisodesAsync("testfeed");

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetEpisodesAsync_EmptyDatabase_ReturnsEmptyList()
    {
        // Arrange
        await _service.InitializeDatabaseAsync("testfeed");

        // Act
        var episodes = await _service.GetEpisodesAsync("testfeed");

        // Assert
        Assert.Empty(episodes);
    }

    [Fact]
    public async Task GetEpisodesAsync_ReturnsSortedByDisplayOrder()
    {
        // Arrange
        await _service.InitializeDatabaseAsync("testfeed");
        var episodes = new[]
        {
            new EpisodeRecord
            {
                Filename = "episode003.mp3",
                DisplayOrder = 3,
                AddedAt = DateTime.UtcNow
            },
            new EpisodeRecord
            {
                Filename = "episode001.mp3",
                DisplayOrder = 1,
                AddedAt = DateTime.UtcNow
            },
            new EpisodeRecord
            {
                Filename = "episode002.mp3",
                DisplayOrder = 2,
                AddedAt = DateTime.UtcNow
            }
        };

        // Act
        await _service.AddEpisodesAsync("testfeed", episodes);
        var result = await _service.GetEpisodesAsync("testfeed");

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("episode001.mp3", result[0].Filename);
        Assert.Equal("episode002.mp3", result[1].Filename);
        Assert.Equal("episode003.mp3", result[2].Filename);
    }

    [Fact]
    public async Task UpdateEpisodeAsync_UpdatesExistingEpisode()
    {
        // Arrange
        await _service.InitializeDatabaseAsync("testfeed");
        var episode = new EpisodeRecord
        {
            Filename = "episode001.mp3",
            VideoId = "old_id",
            DisplayOrder = 1,
            AddedAt = DateTime.UtcNow
        };
        await _service.AddEpisodeAsync("testfeed", episode);

        // Act
        episode.VideoId = "new_id";
        episode.YoutubeTitle = "Updated Title";
        await _service.UpdateEpisodeAsync("testfeed", episode);
        
        var result = await _service.GetEpisodesAsync("testfeed");

        // Assert
        Assert.Single(result);
        Assert.Equal("new_id", result[0].VideoId);
        Assert.Equal("Updated Title", result[0].YoutubeTitle);
    }

    #endregion

    #region Downloaded Video Tracking Tests

    [Fact]
    public async Task IsVideoDownloadedAsync_NewVideo_ReturnsFalse()
    {
        // Arrange
        await _service.InitializeDatabaseAsync("testfeed");

        // Act
        var result = await _service.IsVideoDownloadedAsync("testfeed", "abc123");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task MarkVideoDownloadedAsync_MarksVideo()
    {
        // Arrange
        await _service.InitializeDatabaseAsync("testfeed");

        // Act
        await _service.MarkVideoDownloadedAsync("testfeed", "abc123", "episode001.mp3");
        var isDownloaded = await _service.IsVideoDownloadedAsync("testfeed", "abc123");

        // Assert
        Assert.True(isDownloaded);
    }

    [Fact]
    public async Task GetDownloadedVideoIdsAsync_ReturnsAllIds()
    {
        // Arrange
        await _service.InitializeDatabaseAsync("testfeed");
        await _service.MarkVideoDownloadedAsync("testfeed", "abc123", "episode1.mp3");
        await _service.MarkVideoDownloadedAsync("testfeed", "def456", "episode2.mp3");

        // Act
        var ids = await _service.GetDownloadedVideoIdsAsync("testfeed");

        // Assert
        Assert.Equal(2, ids.Count);
        Assert.Contains("abc123", ids);
        Assert.Contains("def456", ids);
    }

    [Fact]
    public async Task MarkVideoDownloadedAsync_DuplicateId_DoesNotThrow()
    {
        // Arrange
        await _service.InitializeDatabaseAsync("testfeed");
        await _service.MarkVideoDownloadedAsync("testfeed", "abc123", "episode1.mp3");

        // Act & Assert - should not throw
        await _service.MarkVideoDownloadedAsync("testfeed", "abc123", "episode1.mp3");
        
        var isDownloaded = await _service.IsVideoDownloadedAsync("testfeed", "abc123");
        Assert.True(isDownloaded);
    }

    #endregion

    #region Directory Sync Tests

    [Fact]
    public async Task SyncDirectoryAsync_AddsNewFiles()
    {
        // Arrange
        await _service.InitializeDatabaseAsync("testfeed");
        File.WriteAllText(Path.Combine(_testDirectory, "episode001.mp3"), "test");
        File.WriteAllText(Path.Combine(_testDirectory, "episode002.mp3"), "test");

        // Act
        await _service.SyncDirectoryAsync("testfeed", _testDirectory, new[] { ".mp3" });
        var episodes = await _service.GetEpisodesAsync("testfeed");

        // Assert
        Assert.Equal(2, episodes.Count);
        Assert.Contains(episodes, e => e.Filename == "episode001.mp3");
        Assert.Contains(episodes, e => e.Filename == "episode002.mp3");
    }

    [Fact]
    public async Task SyncDirectoryAsync_IgnoresExistingFiles()
    {
        // Arrange
        await _service.InitializeDatabaseAsync("testfeed");
        File.WriteAllText(Path.Combine(_testDirectory, "episode001.mp3"), "test");
        
        var existingEpisode = new EpisodeRecord
        {
            Filename = "episode001.mp3",
            DisplayOrder = 1,
            AddedAt = DateTime.UtcNow
        };
        await _service.AddEpisodeAsync("testfeed", existingEpisode);

        // Act
        await _service.SyncDirectoryAsync("testfeed", _testDirectory, new[] { ".mp3" });
        var episodes = await _service.GetEpisodesAsync("testfeed");

        // Assert
        Assert.Single(episodes); // Should still be 1
    }

    [Fact]
    public async Task SyncDirectoryAsync_NonExistentDirectory_DoesNotThrow()
    {
        // Arrange
        await _service.InitializeDatabaseAsync("testfeed");
        var nonExistentDir = Path.Combine(_testDirectory, "nonexistent");

        // Act & Assert
        await _service.SyncDirectoryAsync("testfeed", nonExistentDir, new[] { ".mp3" });
        
        var episodes = await _service.GetEpisodesAsync("testfeed");
        Assert.Empty(episodes);
    }

    #endregion

    #region Playlist Sync Tests

    [Fact]
    public async Task SyncPlaylistInfoAsync_MatchesAndUpdatesFiles()
    {
        // Arrange
        await _service.InitializeDatabaseAsync("testfeed");
        
        // Create a file that should match
        File.WriteAllText(Path.Combine(_testDirectory, "Episode 001 - The Beginning.mp3"), "test");
        
        var videos = new[]
        {
            new PlaylistVideoInfo
            {
                VideoId = "abc123",
                Title = "Episode 1: The Beginning",
                Description = "First episode",
                PlaylistIndex = 1,
                UploadDate = DateTime.UtcNow.AddDays(-10)
            }
        };

        // Act
        await _service.SyncPlaylistInfoAsync("testfeed", videos, _testDirectory);
        var episodes = await _service.GetEpisodesAsync("testfeed");

        // Assert
        Assert.Single(episodes);
        Assert.Equal("Episode 001 - The Beginning.mp3", episodes[0].Filename);
        Assert.Equal("abc123", episodes[0].VideoId);
        Assert.Equal("Episode 1: The Beginning", episodes[0].YoutubeTitle);
    }

    [Fact]
    public async Task SyncPlaylistInfoAsync_NoMatchingFiles_SkipsVideos()
    {
        // Arrange
        await _service.InitializeDatabaseAsync("testfeed");
        
        var videos = new[]
        {
            new PlaylistVideoInfo
            {
                VideoId = "abc123",
                Title = "Completely Different Title",
                PlaylistIndex = 1
            }
        };

        // Act
        await _service.SyncPlaylistInfoAsync("testfeed", videos, _testDirectory);
        var episodes = await _service.GetEpisodesAsync("testfeed");

        // Assert
        Assert.Empty(episodes); // No matches, so nothing added
    }

    [Fact]
    public async Task SyncPlaylistInfoAsync_UpdatesExistingEpisode()
    {
        // Arrange
        await _service.InitializeDatabaseAsync("testfeed");
        
        var fileName = "Episode 001.mp3";
        File.WriteAllText(Path.Combine(_testDirectory, fileName), "test");
        
        // Add existing episode
        var existingEpisode = new EpisodeRecord
        {
            Filename = fileName,
            VideoId = "old_id",
            DisplayOrder = 999,
            AddedAt = DateTime.UtcNow
        };
        await _service.AddEpisodeAsync("testfeed", existingEpisode);

        var videos = new[]
        {
            new PlaylistVideoInfo
            {
                VideoId = "new_id",
                Title = "Episode 001",
                Description = "Updated description",
                PlaylistIndex = 1,
                UploadDate = DateTime.UtcNow
            }
        };

        // Act
        await _service.SyncPlaylistInfoAsync("testfeed", videos, _testDirectory);
        var episodes = await _service.GetEpisodesAsync("testfeed");

        // Assert
        Assert.Single(episodes);
        Assert.Equal("new_id", episodes[0].VideoId);
        Assert.Equal(1, episodes[0].DisplayOrder); // Should be updated to playlist index
    }

    [Fact]
    public async Task SyncPlaylistInfoAsync_EmptyDirectory_DoesNotThrow()
    {
        // Arrange
        await _service.InitializeDatabaseAsync("testfeed");
        
        var videos = new[]
        {
            new PlaylistVideoInfo
            {
                VideoId = "abc123",
                Title = "Episode 1",
                PlaylistIndex = 1
            }
        };

        // Act & Assert
        await _service.SyncPlaylistInfoAsync("testfeed", videos, _testDirectory);
        
        var episodes = await _service.GetEpisodesAsync("testfeed");
        Assert.Empty(episodes);
    }

    #endregion
}
