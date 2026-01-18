using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Castr.Services;
using Castr.Models;

namespace Castr.Tests;

/// <summary>
/// Tests for CentralDatabaseService covering database operations,
/// feed management, episode tracking, and migration.
/// </summary>
public class CentralDatabaseServiceTests : IDisposable
{
    private readonly Mock<ILogger<CentralDatabaseService>> _mockLogger;
    private readonly Mock<IConfiguration> _mockConfig;
    private readonly CentralDatabaseService _service;
    private readonly string _testDbPath;
    private readonly string _testDirectory;

    public CentralDatabaseServiceTests()
    {
        _mockLogger = new Mock<ILogger<CentralDatabaseService>>();
        _testDirectory = Path.Combine(Path.GetTempPath(), "castr_central_db_test_" + Guid.NewGuid());
        Directory.CreateDirectory(_testDirectory);
        _testDbPath = Path.Combine(_testDirectory, "test_central.db");

        _mockConfig = new Mock<IConfiguration>();
        _mockConfig.Setup(c => c["PodcastFeeds:CentralDatabasePath"]).Returns(_testDbPath);

        _service = new CentralDatabaseService(_mockConfig.Object, _mockLogger.Object);
    }

    public void Dispose()
    {
        _service.Dispose();
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
        await _service.InitializeDatabaseAsync();

        // Assert
        Assert.True(File.Exists(_testDbPath), "Database file should be created");
    }

    [Fact]
    public async Task InitializeDatabaseAsync_CalledTwice_DoesNotThrow()
    {
        // Act & Assert
        await _service.InitializeDatabaseAsync();
        await _service.InitializeDatabaseAsync(); // Should not throw
        
        Assert.True(File.Exists(_testDbPath));
    }

    #endregion

    #region Feed Management Tests

    [Fact]
    public async Task AddFeedAsync_CreatesFeedSuccessfully()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var feed = new FeedRecord
        {
            Name = "test-feed",
            Title = "Test Feed",
            Description = "Test Description",
            Directory = "/test/directory",
            Language = "en-us",
            IsActive = true
        };

        // Act
        var feedId = await _service.AddFeedAsync(feed);

        // Assert
        Assert.True(feedId > 0, "Feed ID should be positive");
        
        var retrievedFeed = await _service.GetFeedByIdAsync(feedId);
        Assert.NotNull(retrievedFeed);
        Assert.Equal("test-feed", retrievedFeed.Name);
        Assert.Equal("Test Feed", retrievedFeed.Title);
    }

    [Fact]
    public async Task GetFeedByNameAsync_ReturnsCorrectFeed()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var feed = new FeedRecord
        {
            Name = "unique-feed",
            Title = "Unique Feed",
            Description = "Description",
            Directory = "/test",
            IsActive = true
        };
        await _service.AddFeedAsync(feed);

        // Act
        var result = await _service.GetFeedByNameAsync("unique-feed");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("unique-feed", result.Name);
        Assert.Equal("Unique Feed", result.Title);
    }

    [Fact]
    public async Task GetAllFeedsAsync_ReturnsAllFeeds()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var feed1 = new FeedRecord
        {
            Name = "feed1",
            Title = "Feed 1",
            Description = "Desc",
            Directory = "/test1",
            IsActive = true
        };
        var feed2 = new FeedRecord
        {
            Name = "feed2",
            Title = "Feed 2",
            Description = "Desc",
            Directory = "/test2",
            IsActive = true
        };

        await _service.AddFeedAsync(feed1);
        await _service.AddFeedAsync(feed2);

        // Act
        var feeds = await _service.GetAllFeedsAsync();

        // Assert
        Assert.Equal(2, feeds.Count);
        Assert.Contains(feeds, f => f.Name == "feed1");
        Assert.Contains(feeds, f => f.Name == "feed2");
    }

    [Fact]
    public async Task UpdateFeedAsync_UpdatesFeedSuccessfully()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var feed = new FeedRecord
        {
            Name = "update-feed",
            Title = "Original Title",
            Description = "Original Description",
            Directory = "/test",
            IsActive = true
        };
        var feedId = await _service.AddFeedAsync(feed);

        // Act
        feed.Id = feedId;
        feed.Title = "Updated Title";
        feed.Description = "Updated Description";
        await _service.UpdateFeedAsync(feed);

        // Assert
        var updatedFeed = await _service.GetFeedByIdAsync(feedId);
        Assert.NotNull(updatedFeed);
        Assert.Equal("Updated Title", updatedFeed.Title);
        Assert.Equal("Updated Description", updatedFeed.Description);
    }

    [Fact]
    public async Task DeleteFeedAsync_DeletesFeedAndRelatedData()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var feed = new FeedRecord
        {
            Name = "delete-feed",
            Title = "Delete Feed",
            Description = "Description",
            Directory = "/test",
            IsActive = true
        };
        var feedId = await _service.AddFeedAsync(feed);

        // Add an episode to ensure cascading delete
        var episode = new EpisodeRecord
        {
            Filename = "test.mp3",
            DisplayOrder = 1,
            AddedAt = DateTime.UtcNow
        };
        await _service.AddEpisodeAsync(feedId, episode);

        // Act
        await _service.DeleteFeedAsync(feedId);

        // Assert
        var deletedFeed = await _service.GetFeedByIdAsync(feedId);
        Assert.Null(deletedFeed);

        var episodes = await _service.GetEpisodesAsync(feedId);
        Assert.Empty(episodes);
    }

    #endregion

    #region Episode Management Tests

    [Fact]
    public async Task AddEpisodeAsync_AddsEpisodeSuccessfully()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var feedId = await CreateTestFeed("episode-feed");
        var episode = new EpisodeRecord
        {
            Filename = "episode1.mp3",
            VideoId = "video123",
            YoutubeTitle = "Episode 1",
            DisplayOrder = 1,
            AddedAt = DateTime.UtcNow
        };

        // Act
        await _service.AddEpisodeAsync(feedId, episode);

        // Assert
        var episodes = await _service.GetEpisodesAsync(feedId);
        Assert.Single(episodes);
        Assert.Equal("episode1.mp3", episodes[0].Filename);
        Assert.Equal("video123", episodes[0].VideoId);
    }

    [Fact]
    public async Task GetEpisodeByFilenameAsync_ReturnsCorrectEpisode()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var feedId = await CreateTestFeed("filename-feed");
        var episode = new EpisodeRecord
        {
            Filename = "specific.mp3",
            DisplayOrder = 1,
            AddedAt = DateTime.UtcNow
        };
        await _service.AddEpisodeAsync(feedId, episode);

        // Act
        var result = await _service.GetEpisodeByFilenameAsync(feedId, "specific.mp3");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("specific.mp3", result.Filename);
    }

    [Fact]
    public async Task UpdateEpisodeAsync_UpdatesEpisodeSuccessfully()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var feedId = await CreateTestFeed("update-episode-feed");
        var episode = new EpisodeRecord
        {
            Filename = "update.mp3",
            VideoId = "old-video",
            DisplayOrder = 1,
            AddedAt = DateTime.UtcNow
        };
        await _service.AddEpisodeAsync(feedId, episode);

        // Act
        episode.VideoId = "new-video";
        episode.YoutubeTitle = "New Title";
        await _service.UpdateEpisodeAsync(feedId, episode);

        // Assert
        var updated = await _service.GetEpisodeByFilenameAsync(feedId, "update.mp3");
        Assert.NotNull(updated);
        Assert.Equal("new-video", updated.VideoId);
        Assert.Equal("New Title", updated.YoutubeTitle);
    }

    [Fact]
    public async Task AddEpisodesAsync_AddsMultipleEpisodes()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var feedId = await CreateTestFeed("multi-episode-feed");
        var episodes = new List<EpisodeRecord>
        {
            new() { Filename = "ep1.mp3", DisplayOrder = 1, AddedAt = DateTime.UtcNow },
            new() { Filename = "ep2.mp3", DisplayOrder = 2, AddedAt = DateTime.UtcNow },
            new() { Filename = "ep3.mp3", DisplayOrder = 3, AddedAt = DateTime.UtcNow }
        };

        // Act
        await _service.AddEpisodesAsync(feedId, episodes);

        // Assert
        var result = await _service.GetEpisodesAsync(feedId);
        Assert.Equal(3, result.Count);
    }

    #endregion

    #region Download Tracking Tests

    [Fact]
    public async Task MarkVideoDownloadedAsync_MarksVideoAsDownloaded()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var feedId = await CreateTestFeed("download-feed");

        // Act
        await _service.MarkVideoDownloadedAsync(feedId, "video123", "file.mp3");

        // Assert
        var isDownloaded = await _service.IsVideoDownloadedAsync(feedId, "video123");
        Assert.True(isDownloaded);
    }

    [Fact]
    public async Task GetDownloadedVideoIdsAsync_ReturnsAllDownloadedVideos()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var feedId = await CreateTestFeed("multi-download-feed");
        await _service.MarkVideoDownloadedAsync(feedId, "video1", "file1.mp3");
        await _service.MarkVideoDownloadedAsync(feedId, "video2", "file2.mp3");

        // Act
        var videoIds = await _service.GetDownloadedVideoIdsAsync(feedId);

        // Assert
        Assert.Equal(2, videoIds.Count);
        Assert.Contains("video1", videoIds);
        Assert.Contains("video2", videoIds);
    }

    [Fact]
    public async Task IsVideoDownloadedAsync_ReturnsFalseForNonDownloadedVideo()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var feedId = await CreateTestFeed("check-feed");

        // Act
        var isDownloaded = await _service.IsVideoDownloadedAsync(feedId, "nonexistent");

        // Assert
        Assert.False(isDownloaded);
    }

    #endregion

    #region Activity Logging Tests

    [Fact]
    public async Task LogActivityAsync_LogsActivitySuccessfully()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var feedId = await CreateTestFeed("activity-feed");

        // Act
        await _service.LogActivityAsync(feedId, "download", "Downloaded video", "details here");

        // Assert
        var activities = await _service.GetRecentActivityAsync(feedId, 10);
        Assert.Single(activities);
        Assert.Equal("download", activities[0].ActivityType);
        Assert.Equal("Downloaded video", activities[0].Message);
    }

    [Fact]
    public async Task GetRecentActivityAsync_ReturnsActivitiesForFeed()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var feedId1 = await CreateTestFeed("feed1");
        var feedId2 = await CreateTestFeed("feed2");

        await _service.LogActivityAsync(feedId1, "sync", "Synced feed 1");
        await _service.LogActivityAsync(feedId2, "sync", "Synced feed 2");
        await _service.LogActivityAsync(feedId1, "download", "Downloaded video");

        // Act
        var activities = await _service.GetRecentActivityAsync(feedId1, 100);

        // Assert
        Assert.Equal(2, activities.Count);
        Assert.All(activities, a => Assert.Equal(feedId1, a.FeedId));
    }

    [Fact]
    public async Task GetRecentActivityAsync_WithoutFeedId_ReturnsAllActivities()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var feedId1 = await CreateTestFeed("feed1");
        var feedId2 = await CreateTestFeed("feed2");

        await _service.LogActivityAsync(feedId1, "sync", "Activity 1");
        await _service.LogActivityAsync(feedId2, "sync", "Activity 2");
        await _service.LogActivityAsync(null, "system", "System activity");

        // Act
        var activities = await _service.GetRecentActivityAsync(null, 100);

        // Assert
        Assert.Equal(3, activities.Count);
    }

    #endregion

    #region Download Queue Tests

    [Fact]
    public async Task AddToDownloadQueueAsync_AddsItemToQueue()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var feedId = await CreateTestFeed("queue-feed");

        // Act
        var item = await _service.AddToDownloadQueueAsync(feedId, "video123", "Test Video");

        // Assert
        Assert.True(item.Id > 0);
        Assert.Equal(feedId, item.FeedId);
        Assert.Equal("video123", item.VideoId);
        Assert.Equal("Test Video", item.VideoTitle);
        Assert.Equal("queued", item.Status);
    }

    [Fact]
    public async Task UpdateDownloadProgressAsync_UpdatesProgress()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var feedId = await CreateTestFeed("progress-feed");
        var item = await _service.AddToDownloadQueueAsync(feedId, "video123", "Test Video");

        // Act
        await _service.UpdateDownloadProgressAsync(item.Id, "downloading", 50);

        // Assert
        var updatedItem = await _service.GetQueueItemAsync(feedId, "video123");
        Assert.NotNull(updatedItem);
        Assert.Equal("downloading", updatedItem.Status);
        Assert.Equal(50, updatedItem.ProgressPercent);
    }

    [Fact]
    public async Task GetDownloadQueueAsync_ReturnsQueueForFeed()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var feedId1 = await CreateTestFeed("feed1");
        var feedId2 = await CreateTestFeed("feed2");

        await _service.AddToDownloadQueueAsync(feedId1, "video1", "Video 1");
        await _service.AddToDownloadQueueAsync(feedId1, "video2", "Video 2");
        await _service.AddToDownloadQueueAsync(feedId2, "video3", "Video 3");

        // Act
        var queue = await _service.GetDownloadQueueAsync(feedId1);

        // Assert
        Assert.Equal(2, queue.Count);
        Assert.All(queue, item => Assert.Equal(feedId1, item.FeedId));
    }

    [Fact]
    public async Task RemoveFromDownloadQueueAsync_RemovesItem()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var feedId = await CreateTestFeed("remove-feed");
        var item = await _service.AddToDownloadQueueAsync(feedId, "video123", "Test Video");

        // Act
        await _service.RemoveFromDownloadQueueAsync(item.Id);

        // Assert
        var removedItem = await _service.GetQueueItemAsync(feedId, "video123");
        Assert.Null(removedItem);
    }

    #endregion

    #region Migration Tests

    [Fact]
    public async Task MigrateFromPerFeedDatabasesAsync_MigratesFeedConfiguration()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var feeds = new Dictionary<string, PodcastFeedConfig>
        {
            ["test-feed"] = new PodcastFeedConfig
            {
                Title = "Test Podcast",
                Description = "Test Description",
                Directory = _testDirectory,
                Author = "Test Author",
                Language = "en-us",
                YouTube = new YouTubePlaylistConfig
                {
                    PlaylistUrl = "https://youtube.com/playlist?list=test",
                    PollIntervalMinutes = 60,
                    Enabled = true,
                    MaxConcurrentDownloads = 1,
                    AudioQuality = "highest"
                }
            }
        };

        // Act
        await _service.MigrateFromPerFeedDatabasesAsync(feeds);

        // Assert
        var migratedFeed = await _service.GetFeedByNameAsync("test-feed");
        Assert.NotNull(migratedFeed);
        Assert.Equal("Test Podcast", migratedFeed.Title);
        Assert.Equal("Test Description", migratedFeed.Description);
        Assert.Equal("Test Author", migratedFeed.Author);
        Assert.True(migratedFeed.YouTubeEnabled);
        Assert.Equal("https://youtube.com/playlist?list=test", migratedFeed.YouTubePlaylistUrl);
    }

    [Fact]
    public async Task MigrateFromPerFeedDatabasesAsync_SkipsExistingFeeds()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        
        // Add feed first
        var feed = new FeedRecord
        {
            Name = "existing-feed",
            Title = "Original Title",
            Description = "Original Description",
            Directory = "/test",
            IsActive = true
        };
        await _service.AddFeedAsync(feed);

        // Prepare migration data with same feed name but different title
        var feeds = new Dictionary<string, PodcastFeedConfig>
        {
            ["existing-feed"] = new PodcastFeedConfig
            {
                Title = "New Title",
                Description = "New Description",
                Directory = "/test"
            }
        };

        // Act
        await _service.MigrateFromPerFeedDatabasesAsync(feeds);

        // Assert
        var existingFeed = await _service.GetFeedByNameAsync("existing-feed");
        Assert.NotNull(existingFeed);
        Assert.Equal("Original Title", existingFeed.Title); // Should not be updated
    }

    #endregion

    #region Helper Methods

    private async Task<int> CreateTestFeed(string name)
    {
        var feed = new FeedRecord
        {
            Name = name,
            Title = $"{name} Title",
            Description = "Description",
            Directory = "/test",
            IsActive = true
        };
        return await _service.AddFeedAsync(feed);
    }

    #endregion
}
