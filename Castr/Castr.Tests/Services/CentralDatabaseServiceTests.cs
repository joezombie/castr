using Microsoft.Extensions.Configuration;
using Castr.Tests.TestHelpers;

namespace Castr.Tests.Services;

public class CentralDatabaseServiceTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly CentralDatabaseService _service;
    private readonly Mock<ILogger<CentralDatabaseService>> _mockLogger;

    public CentralDatabaseServiceTests()
    {
        _testDbPath = TestDatabaseHelper.CreateTempDatabase();
        _mockLogger = new Mock<ILogger<CentralDatabaseService>>();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PodcastFeeds:CentralDatabasePath"] = _testDbPath
            })
            .Build();

        _service = new CentralDatabaseService(configuration, _mockLogger.Object);
    }

    public void Dispose()
    {
        _service.Dispose();
        TestDatabaseHelper.DeleteDatabase(_testDbPath);
    }

    #region Database Initialization Tests

    [Fact]
    public async Task InitializeDatabaseAsync_CreatesSchema()
    {
        // Act
        await _service.InitializeDatabaseAsync();

        // Assert - verify tables exist by querying them
        var feeds = await _service.GetAllFeedsAsync();
        Assert.NotNull(feeds);
        Assert.Empty(feeds);
    }

    [Fact]
    public async Task InitializeDatabaseAsync_IsIdempotent()
    {
        // Act - initialize twice
        await _service.InitializeDatabaseAsync();
        await _service.InitializeDatabaseAsync();

        // Assert - should not throw
        var feeds = await _service.GetAllFeedsAsync();
        Assert.NotNull(feeds);
    }

    #endregion

    #region Feed Management Tests

    [Fact]
    public async Task AddFeedAsync_ReturnsFeedId()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var feed = new FeedRecord
        {
            Name = "testfeed",
            Title = "Test Feed",
            Description = "A test feed",
            Directory = "/test/path"
        };

        // Act
        var feedId = await _service.AddFeedAsync(feed);

        // Assert
        Assert.True(feedId > 0);
    }

    [Fact]
    public async Task GetFeedByNameAsync_ReturnsCorrectFeed()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var feed = new FeedRecord
        {
            Name = "myfeed",
            Title = "My Feed",
            Description = "Description",
            Directory = "/path"
        };
        await _service.AddFeedAsync(feed);

        // Act
        var result = await _service.GetFeedByNameAsync("myfeed");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("myfeed", result.Name);
        Assert.Equal("My Feed", result.Title);
    }

    [Fact]
    public async Task GetFeedByNameAsync_WithNonExistent_ReturnsNull()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();

        // Act
        var result = await _service.GetFeedByNameAsync("nonexistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetFeedByIdAsync_ReturnsCorrectFeed()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var feed = new FeedRecord
        {
            Name = "idfeed",
            Title = "ID Feed",
            Description = "Description",
            Directory = "/path"
        };
        var feedId = await _service.AddFeedAsync(feed);

        // Act
        var result = await _service.GetFeedByIdAsync(feedId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(feedId, result.Id);
        Assert.Equal("idfeed", result.Name);
    }

    [Fact]
    public async Task GetAllFeedsAsync_ReturnsAllFeeds()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        await _service.AddFeedAsync(new FeedRecord { Name = "feed1", Title = "Feed 1", Description = "D", Directory = "/p1" });
        await _service.AddFeedAsync(new FeedRecord { Name = "feed2", Title = "Feed 2", Description = "D", Directory = "/p2" });

        // Act
        var feeds = await _service.GetAllFeedsAsync();

        // Assert
        Assert.Equal(2, feeds.Count);
    }

    [Fact]
    public async Task UpdateFeedAsync_ModifiesFeed()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var feed = new FeedRecord
        {
            Name = "updatetest",
            Title = "Original Title",
            Description = "Original",
            Directory = "/path"
        };
        var feedId = await _service.AddFeedAsync(feed);

        // Act
        feed.Id = feedId;
        feed.Title = "Updated Title";
        await _service.UpdateFeedAsync(feed);

        // Assert
        var updated = await _service.GetFeedByIdAsync(feedId);
        Assert.NotNull(updated);
        Assert.Equal("Updated Title", updated.Title);
    }

    [Fact]
    public async Task DeleteFeedAsync_RemovesFeed()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var feed = new FeedRecord
        {
            Name = "todelete",
            Title = "To Delete",
            Description = "Will be deleted",
            Directory = "/path"
        };
        var feedId = await _service.AddFeedAsync(feed);

        // Act
        await _service.DeleteFeedAsync(feedId);

        // Assert
        var deleted = await _service.GetFeedByIdAsync(feedId);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task DeleteFeedAsync_CascadeDeletesRelatedRecords()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var feed = new FeedRecord { Name = "cascadetest", Title = "T", Description = "D", Directory = "/p" };
        var feedId = await _service.AddFeedAsync(feed);

        // Add related records
        await _service.AddEpisodeAsync(feedId, new EpisodeRecord { Filename = "test.mp3", DisplayOrder = 1 });
        await _service.MarkVideoDownloadedAsync(feedId, "vid1", "test.mp3");
        await _service.LogActivityAsync(feedId, "test", "test message");
        await _service.AddToDownloadQueueAsync(feedId, "vid2", "Title");

        // Act
        await _service.DeleteFeedAsync(feedId);

        // Assert - all related records should be deleted
        var episodes = await _service.GetEpisodesAsync(feedId);
        Assert.Empty(episodes);

        var downloaded = await _service.IsVideoDownloadedAsync(feedId, "vid1");
        Assert.False(downloaded);
    }

    #endregion

    #region Episode Management Tests

    [Fact]
    public async Task AddEpisodeAsync_AddsEpisode()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var feed = new FeedRecord { Name = "eptest", Title = "T", Description = "D", Directory = "/p" };
        var feedId = await _service.AddFeedAsync(feed);

        var episode = new EpisodeRecord
        {
            Filename = "episode1.mp3",
            DisplayOrder = 1
        };

        // Act
        await _service.AddEpisodeAsync(feedId, episode);

        // Assert
        var episodes = await _service.GetEpisodesAsync(feedId);
        Assert.Single(episodes);
        Assert.Equal("episode1.mp3", episodes[0].Filename);
    }

    [Fact]
    public async Task AddEpisodesAsync_AddsBatch()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var feed = new FeedRecord { Name = "batchtest", Title = "T", Description = "D", Directory = "/p" };
        var feedId = await _service.AddFeedAsync(feed);

        var episodes = new[]
        {
            new EpisodeRecord { Filename = "ep1.mp3", DisplayOrder = 1 },
            new EpisodeRecord { Filename = "ep2.mp3", DisplayOrder = 2 },
            new EpisodeRecord { Filename = "ep3.mp3", DisplayOrder = 3 }
        };

        // Act
        await _service.AddEpisodesAsync(feedId, episodes);

        // Assert
        var result = await _service.GetEpisodesAsync(feedId);
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task GetEpisodeByIdAsync_ReturnsCorrectEpisode()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var feed = new FeedRecord { Name = "epidtest", Title = "T", Description = "D", Directory = "/p" };
        var feedId = await _service.AddFeedAsync(feed);

        await _service.AddEpisodeAsync(feedId, new EpisodeRecord { Filename = "target.mp3", DisplayOrder = 1 });
        var episodes = await _service.GetEpisodesAsync(feedId);
        var episodeId = episodes[0].Id;

        // Act
        var result = await _service.GetEpisodeByIdAsync(episodeId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("target.mp3", result.Filename);
    }

    [Fact]
    public async Task GetEpisodeByFilenameAsync_ReturnsCorrectEpisode()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var feed = new FeedRecord { Name = "fntest", Title = "T", Description = "D", Directory = "/p" };
        var feedId = await _service.AddFeedAsync(feed);

        var episode = new EpisodeRecord { Filename = "specific.mp3", DisplayOrder = 1 };
        await _service.AddEpisodeAsync(feedId, episode);

        // Act
        var result = await _service.GetEpisodeByFilenameAsync(feedId, "specific.mp3");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("specific.mp3", result.Filename);
    }

    [Fact]
    public async Task GetEpisodeByFilenameAsync_ReturnsNullForNonExistent()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var feed = new FeedRecord { Name = "fntest2", Title = "T", Description = "D", Directory = "/p" };
        var feedId = await _service.AddFeedAsync(feed);

        // Act
        var result = await _service.GetEpisodeByFilenameAsync(feedId, "nonexistent.mp3");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateEpisodeAsync_UpdatesEpisodeMetadata()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var feed = new FeedRecord { Name = "epupdtest", Title = "T", Description = "D", Directory = "/p" };
        var feedId = await _service.AddFeedAsync(feed);

        var episode = new EpisodeRecord { Filename = "update.mp3", DisplayOrder = 1 };
        await _service.AddEpisodeAsync(feedId, episode);

        // Act
        episode.VideoId = "vid123";
        episode.YoutubeTitle = "Updated Title";
        episode.Description = "Updated description";
        await _service.UpdateEpisodeAsync(feedId, episode);

        // Assert
        var updated = await _service.GetEpisodeByFilenameAsync(feedId, "update.mp3");
        Assert.NotNull(updated);
        Assert.Equal("vid123", updated.VideoId);
        Assert.Equal("Updated Title", updated.YoutubeTitle);
    }

    #endregion

    #region Download Tracking Tests

    [Fact]
    public async Task IsVideoDownloadedAsync_ReturnsTrueForDownloaded()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var feed = new FeedRecord { Name = "dltest", Title = "T", Description = "D", Directory = "/p" };
        var feedId = await _service.AddFeedAsync(feed);
        await _service.MarkVideoDownloadedAsync(feedId, "video123", "video.mp3");

        // Act
        var result = await _service.IsVideoDownloadedAsync(feedId, "video123");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsVideoDownloadedAsync_ReturnsFalseForNotDownloaded()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var feed = new FeedRecord { Name = "notdl", Title = "T", Description = "D", Directory = "/p" };
        var feedId = await _service.AddFeedAsync(feed);

        // Act
        var result = await _service.IsVideoDownloadedAsync(feedId, "notdownloaded");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetDownloadedVideoIdsAsync_ReturnsAllIds()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var feed = new FeedRecord { Name = "idstest", Title = "T", Description = "D", Directory = "/p" };
        var feedId = await _service.AddFeedAsync(feed);
        await _service.MarkVideoDownloadedAsync(feedId, "v1", "f1.mp3");
        await _service.MarkVideoDownloadedAsync(feedId, "v2", "f2.mp3");

        // Act
        var ids = await _service.GetDownloadedVideoIdsAsync(feedId);

        // Assert
        Assert.Equal(2, ids.Count);
        Assert.Contains("v1", ids);
        Assert.Contains("v2", ids);
    }

    [Fact]
    public async Task RemoveDownloadedVideoAsync_RemovesVideo()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var feed = new FeedRecord { Name = "removedl", Title = "T", Description = "D", Directory = "/p" };
        var feedId = await _service.AddFeedAsync(feed);
        await _service.MarkVideoDownloadedAsync(feedId, "toremove", "file.mp3");

        // Act
        await _service.RemoveDownloadedVideoAsync(feedId, "toremove");

        // Assert
        var isDownloaded = await _service.IsVideoDownloadedAsync(feedId, "toremove");
        Assert.False(isDownloaded);
    }

    #endregion

    #region Activity Logging Tests

    [Fact]
    public async Task LogActivityAsync_CreatesActivityRecord()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var feed = new FeedRecord { Name = "acttest", Title = "T", Description = "D", Directory = "/p" };
        var feedId = await _service.AddFeedAsync(feed);

        // Act
        await _service.LogActivityAsync(feedId, "download", "Downloaded episode", "details here");

        // Assert
        var activities = await _service.GetRecentActivityAsync(feedId, 10);
        Assert.Single(activities);
        Assert.Equal("download", activities[0].ActivityType);
        Assert.Equal("Downloaded episode", activities[0].Message);
        Assert.Equal("details here", activities[0].Details);
    }

    [Fact]
    public async Task LogActivityAsync_WithNullFeedId_Works()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();

        // Act
        await _service.LogActivityAsync(null, "system", "System startup");

        // Assert
        var activities = await _service.GetRecentActivityAsync(null, 10);
        Assert.Single(activities);
        Assert.Null(activities[0].FeedId);
    }

    [Fact]
    public async Task GetRecentActivityAsync_ReturnsLimitedResults()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var feed = new FeedRecord { Name = "limitact", Title = "T", Description = "D", Directory = "/p" };
        var feedId = await _service.AddFeedAsync(feed);

        for (var i = 0; i < 20; i++)
        {
            await _service.LogActivityAsync(feedId, "test", $"Message {i}");
        }

        // Act
        var activities = await _service.GetRecentActivityAsync(feedId, 5);

        // Assert
        Assert.Equal(5, activities.Count);
    }

    #endregion

    #region Download Queue Tests

    [Fact]
    public async Task AddToDownloadQueueAsync_CreatesQueueItem()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var feed = new FeedRecord { Name = "qtest", Title = "T", Description = "D", Directory = "/p" };
        var feedId = await _service.AddFeedAsync(feed);

        // Act
        var item = await _service.AddToDownloadQueueAsync(feedId, "vid1", "Video Title");

        // Assert
        Assert.NotNull(item);
        Assert.Equal("vid1", item.VideoId);
        Assert.Equal("queued", item.Status);
        Assert.Equal(0, item.ProgressPercent);
    }

    [Fact]
    public async Task UpdateDownloadProgressAsync_UpdatesStatus()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var feed = new FeedRecord { Name = "progtest", Title = "T", Description = "D", Directory = "/p" };
        var feedId = await _service.AddFeedAsync(feed);
        var item = await _service.AddToDownloadQueueAsync(feedId, "vid2", "Title");

        // Act
        await _service.UpdateDownloadProgressAsync(item.Id, "downloading", 50, null);

        // Assert
        var updated = await _service.GetQueueItemAsync(feedId, "vid2");
        Assert.NotNull(updated);
        Assert.Equal("downloading", updated.Status);
        Assert.Equal(50, updated.ProgressPercent);
        Assert.NotNull(updated.StartedAt);
    }

    [Fact]
    public async Task UpdateDownloadProgressAsync_WithError_SetsErrorMessage()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var feed = new FeedRecord { Name = "errtest", Title = "T", Description = "D", Directory = "/p" };
        var feedId = await _service.AddFeedAsync(feed);
        var item = await _service.AddToDownloadQueueAsync(feedId, "vid3", "Title");

        // Act
        await _service.UpdateDownloadProgressAsync(item.Id, "failed", 0, "Download failed");

        // Assert
        var updated = await _service.GetQueueItemAsync(feedId, "vid3");
        Assert.NotNull(updated);
        Assert.Equal("failed", updated.Status);
        Assert.Equal("Download failed", updated.ErrorMessage);
        Assert.NotNull(updated.CompletedAt);
    }

    [Fact]
    public async Task GetDownloadQueueAsync_ReturnsAllItems()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var feed = new FeedRecord { Name = "qlisttest", Title = "T", Description = "D", Directory = "/p" };
        var feedId = await _service.AddFeedAsync(feed);
        await _service.AddToDownloadQueueAsync(feedId, "vid1", "Title 1");
        await _service.AddToDownloadQueueAsync(feedId, "vid2", "Title 2");

        // Act
        var queue = await _service.GetDownloadQueueAsync(feedId);

        // Assert
        Assert.Equal(2, queue.Count);
    }

    [Fact]
    public async Task RemoveFromDownloadQueueAsync_RemovesItem()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var feed = new FeedRecord { Name = "qremtest", Title = "T", Description = "D", Directory = "/p" };
        var feedId = await _service.AddFeedAsync(feed);
        var item = await _service.AddToDownloadQueueAsync(feedId, "toremove", "Title");

        // Act
        await _service.RemoveFromDownloadQueueAsync(item.Id);

        // Assert
        var result = await _service.GetQueueItemAsync(feedId, "toremove");
        Assert.Null(result);
    }

    #endregion

    #region Feed With YouTube Configuration Tests

    [Fact]
    public async Task AddFeedAsync_WithYouTubeConfig_PersistsCorrectly()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var feed = new FeedRecord
        {
            Name = "ytfeed",
            Title = "YouTube Feed",
            Description = "Feed with YouTube config",
            Directory = "/path",
            YouTubePlaylistUrl = "https://youtube.com/playlist?list=abc123",
            YouTubePollIntervalMinutes = 30,
            YouTubeEnabled = true,
            YouTubeMaxConcurrentDownloads = 2,
            YouTubeAudioQuality = "highest"
        };

        // Act
        var feedId = await _service.AddFeedAsync(feed);
        var result = await _service.GetFeedByIdAsync(feedId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("https://youtube.com/playlist?list=abc123", result.YouTubePlaylistUrl);
        Assert.Equal(30, result.YouTubePollIntervalMinutes);
        Assert.True(result.YouTubeEnabled);
        Assert.Equal(2, result.YouTubeMaxConcurrentDownloads);
        Assert.Equal("highest", result.YouTubeAudioQuality);
    }

    #endregion

    #region Sync Directory Tests

    [Fact]
    public async Task SyncDirectoryAsync_AddsNewFiles()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var testDir = TestDatabaseHelper.CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(testDir, "episode1.mp3"), "test");
            File.WriteAllText(Path.Combine(testDir, "episode2.mp3"), "test");

            var feed = new FeedRecord { Name = "synctest", Title = "T", Description = "D", Directory = testDir };
            var feedId = await _service.AddFeedAsync(feed);

            // Act
            await _service.SyncDirectoryAsync(feedId, testDir, new[] { ".mp3" });

            // Assert
            var episodes = await _service.GetEpisodesAsync(feedId);
            Assert.Equal(2, episodes.Count);
        }
        finally
        {
            TestDatabaseHelper.DeleteDirectory(testDir);
        }
    }

    [Fact]
    public async Task SyncDirectoryAsync_SkipsExistingFiles()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var testDir = TestDatabaseHelper.CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(testDir, "existing.mp3"), "test");

            var feed = new FeedRecord { Name = "skiptest", Title = "T", Description = "D", Directory = testDir };
            var feedId = await _service.AddFeedAsync(feed);

            // Add existing episode
            await _service.AddEpisodeAsync(feedId, new EpisodeRecord { Filename = "existing.mp3", DisplayOrder = 1 });

            // Act
            await _service.SyncDirectoryAsync(feedId, testDir, new[] { ".mp3" });

            // Assert
            var episodes = await _service.GetEpisodesAsync(feedId);
            Assert.Single(episodes);
        }
        finally
        {
            TestDatabaseHelper.DeleteDirectory(testDir);
        }
    }

    [Fact]
    public async Task SyncDirectoryAsync_WithNonExistentDirectory_DoesNothing()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var feed = new FeedRecord { Name = "nodirtest", Title = "T", Description = "D", Directory = "/nonexistent/path" };
        var feedId = await _service.AddFeedAsync(feed);

        // Act - should not throw
        await _service.SyncDirectoryAsync(feedId, "/nonexistent/path", new[] { ".mp3" });

        // Assert
        var episodes = await _service.GetEpisodesAsync(feedId);
        Assert.Empty(episodes);
    }

    #endregion
}
