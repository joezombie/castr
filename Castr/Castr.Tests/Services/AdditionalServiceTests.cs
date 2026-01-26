using Microsoft.Extensions.Configuration;
using Castr.Tests.TestHelpers;

namespace Castr.Tests.Services;

/// <summary>
/// Additional service tests to increase coverage.
/// </summary>
public class AdditionalServiceTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly string _testDirectory;
    private readonly CentralDatabaseService _service;
    private readonly Mock<ILogger<CentralDatabaseService>> _mockLogger;

    public AdditionalServiceTests()
    {
        _testDbPath = TestDatabaseHelper.CreateTempDatabase();
        _testDirectory = TestDatabaseHelper.CreateTempDirectory();
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
        TestDatabaseHelper.DeleteDirectory(_testDirectory);
    }

    #region Episode Edge Cases

    [Fact]
    public async Task AddEpisodesAsync_WithEmptyList_DoesNothing()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var feed = new FeedRecord { Name = "emptylist", Title = "T", Description = "D", Directory = "/p" };
        var feedId = await _service.AddFeedAsync(feed);

        // Act - should not throw
        await _service.AddEpisodesAsync(feedId, new List<EpisodeRecord>());

        // Assert
        var episodes = await _service.GetEpisodesAsync(feedId);
        Assert.Empty(episodes);
    }

    [Fact]
    public async Task GetEpisodeByIdAsync_WithNonExistent_ReturnsNull()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();

        // Act
        var result = await _service.GetEpisodeByIdAsync(99999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetEpisodesAsync_ReturnsOrderedByDisplayOrder()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var feed = new FeedRecord { Name = "ordertest2", Title = "T", Description = "D", Directory = "/p" };
        var feedId = await _service.AddFeedAsync(feed);

        // Add episodes in random order
        await _service.AddEpisodeAsync(feedId, new EpisodeRecord { Filename = "ep3.mp3", DisplayOrder = 30 });
        await _service.AddEpisodeAsync(feedId, new EpisodeRecord { Filename = "ep1.mp3", DisplayOrder = 10 });
        await _service.AddEpisodeAsync(feedId, new EpisodeRecord { Filename = "ep2.mp3", DisplayOrder = 20 });

        // Act
        var episodes = await _service.GetEpisodesAsync(feedId);

        // Assert - should be ordered by display_order
        Assert.Equal(3, episodes.Count);
        Assert.Equal("ep1.mp3", episodes[0].Filename);
        Assert.Equal("ep2.mp3", episodes[1].Filename);
        Assert.Equal("ep3.mp3", episodes[2].Filename);
    }

    [Fact]
    public async Task AddEpisodeAsync_WithAllFields_PersistsCorrectly()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var feed = new FeedRecord { Name = "allfields", Title = "T", Description = "D", Directory = "/p" };
        var feedId = await _service.AddFeedAsync(feed);

        var publishDate = new DateTime(2024, 6, 15);
        var episode = new EpisodeRecord
        {
            Filename = "complete.mp3",
            VideoId = "abc123xyz",
            YoutubeTitle = "Full YouTube Title",
            Description = "Full episode description with lots of details",
            ThumbnailUrl = "https://example.com/thumbnails/large.jpg",
            DisplayOrder = 42,
            AddedAt = DateTime.UtcNow,
            PublishDate = publishDate,
            MatchScore = 0.9567
        };

        // Act
        await _service.AddEpisodeAsync(feedId, episode);

        // Assert
        var retrieved = await _service.GetEpisodeByFilenameAsync(feedId, "complete.mp3");
        Assert.NotNull(retrieved);
        Assert.Equal("abc123xyz", retrieved.VideoId);
        Assert.Equal("Full YouTube Title", retrieved.YoutubeTitle);
        Assert.Equal("Full episode description with lots of details", retrieved.Description);
        Assert.Equal("https://example.com/thumbnails/large.jpg", retrieved.ThumbnailUrl);
        Assert.Equal(42, retrieved.DisplayOrder);
        Assert.Equal(publishDate, retrieved.PublishDate);
        Assert.NotNull(retrieved.MatchScore);
        Assert.Equal(0.9567, retrieved.MatchScore.Value, 4);
    }

    #endregion

    #region Download Queue Edge Cases

    [Fact]
    public async Task GetDownloadQueueAsync_WithNoFeedId_ReturnsAllItems()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var feed1 = new FeedRecord { Name = "queue1", Title = "T1", Description = "D", Directory = "/p1" };
        var feed2 = new FeedRecord { Name = "queue2", Title = "T2", Description = "D", Directory = "/p2" };
        var feedId1 = await _service.AddFeedAsync(feed1);
        var feedId2 = await _service.AddFeedAsync(feed2);

        await _service.AddToDownloadQueueAsync(feedId1, "vid1", "Title 1");
        await _service.AddToDownloadQueueAsync(feedId2, "vid2", "Title 2");

        // Act - no feedId filter
        var queue = await _service.GetDownloadQueueAsync(null);

        // Assert
        Assert.Equal(2, queue.Count);
    }

    [Fact]
    public async Task GetQueueItemAsync_WithNonExistent_ReturnsNull()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var feed = new FeedRecord { Name = "qnone", Title = "T", Description = "D", Directory = "/p" };
        var feedId = await _service.AddFeedAsync(feed);

        // Act
        var result = await _service.GetQueueItemAsync(feedId, "nonexistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateDownloadProgressAsync_WithCompletedStatus_SetsCompletedAt()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var feed = new FeedRecord { Name = "complete", Title = "T", Description = "D", Directory = "/p" };
        var feedId = await _service.AddFeedAsync(feed);
        var item = await _service.AddToDownloadQueueAsync(feedId, "completing", "Title");

        // Act
        await _service.UpdateDownloadProgressAsync(item.Id, "completed", 100, null);

        // Assert
        var updated = await _service.GetQueueItemAsync(feedId, "completing");
        Assert.NotNull(updated);
        Assert.Equal("completed", updated.Status);
        Assert.Equal(100, updated.ProgressPercent);
        Assert.NotNull(updated.CompletedAt);
    }

    #endregion

    #region Activity Log Edge Cases

    [Fact]
    public async Task GetRecentActivityAsync_DefaultsTo100()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();

        // Add 150 activities
        for (int i = 0; i < 150; i++)
        {
            await _service.LogActivityAsync(null, "test", $"Message {i}");
        }

        // Act - no count specified, should default to 100
        var activities = await _service.GetRecentActivityAsync();

        // Assert
        Assert.Equal(100, activities.Count);
    }

    [Fact]
    public async Task GetRecentActivityAsync_ReturnsNewestFirst()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();

        await _service.LogActivityAsync(null, "first", "First message");
        await Task.Delay(10); // Ensure different timestamps
        await _service.LogActivityAsync(null, "second", "Second message");

        // Act
        var activities = await _service.GetRecentActivityAsync(null, 10);

        // Assert
        Assert.Equal(2, activities.Count);
        Assert.Equal("Second message", activities[0].Message); // Newest first
        Assert.Equal("First message", activities[1].Message);
    }

    [Fact]
    public async Task ClearActivityLogAsync_DeletesAllEntries()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        await _service.LogActivityAsync(null, "test", "Message 1");
        await _service.LogActivityAsync(null, "test", "Message 2");
        await _service.LogActivityAsync(null, "test", "Message 3");

        // Act
        await _service.ClearActivityLogAsync();

        // Assert
        var activities = await _service.GetRecentActivityAsync();
        Assert.Empty(activities);
    }

    #endregion

    #region Feed Edge Cases

    [Fact]
    public async Task GetFeedByIdAsync_WithNonExistent_ReturnsNull()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();

        // Act
        var result = await _service.GetFeedByIdAsync(99999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllFeedsAsync_ReturnsOrderedByName()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        await _service.AddFeedAsync(new FeedRecord { Name = "zoo", Title = "Z", Description = "D", Directory = "/z" });
        await _service.AddFeedAsync(new FeedRecord { Name = "apple", Title = "A", Description = "D", Directory = "/a" });
        await _service.AddFeedAsync(new FeedRecord { Name = "middle", Title = "M", Description = "D", Directory = "/m" });

        // Act
        var feeds = await _service.GetAllFeedsAsync();

        // Assert
        Assert.Equal(3, feeds.Count);
        Assert.Equal("apple", feeds[0].Name);
        Assert.Equal("middle", feeds[1].Name);
        Assert.Equal("zoo", feeds[2].Name);
    }

    [Fact]
    public async Task UpdateFeedAsync_UpdatesAllFields()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();
        var feed = new FeedRecord
        {
            Name = "updateall",
            Title = "Original",
            Description = "Original Desc",
            Directory = "/original"
        };
        var feedId = await _service.AddFeedAsync(feed);

        // Act
        feed.Id = feedId;
        feed.Title = "Updated Title";
        feed.Description = "Updated Description";
        feed.Author = "New Author";
        feed.ImageUrl = "https://new.com/image.png";
        feed.YouTubeEnabled = true;
        feed.YouTubePlaylistUrl = "https://youtube.com/new";
        await _service.UpdateFeedAsync(feed);

        // Assert
        var updated = await _service.GetFeedByIdAsync(feedId);
        Assert.NotNull(updated);
        Assert.Equal("Updated Title", updated.Title);
        Assert.Equal("Updated Description", updated.Description);
        Assert.Equal("New Author", updated.Author);
        Assert.True(updated.YouTubeEnabled);
    }

    #endregion

    #region Directory Sync Edge Cases

    [Fact]
    public async Task SyncDirectoryAsync_WithMultipleExtensions_FindsAll()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();

        File.WriteAllText(Path.Combine(_testDirectory, "audio1.mp3"), "test");
        File.WriteAllText(Path.Combine(_testDirectory, "audio2.m4a"), "test");
        File.WriteAllText(Path.Combine(_testDirectory, "audio3.ogg"), "test");

        var feed = new FeedRecord { Name = "multiext", Title = "T", Description = "D", Directory = _testDirectory };
        var feedId = await _service.AddFeedAsync(feed);

        // Act
        await _service.SyncDirectoryAsync(feedId, _testDirectory, new[] { ".mp3", ".m4a", ".ogg" });

        // Assert
        var episodes = await _service.GetEpisodesAsync(feedId);
        Assert.Equal(3, episodes.Count);
    }

    [Fact]
    public async Task SyncDirectoryAsync_AddsNewFilesWithPrependOrder()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();

        var feed = new FeedRecord { Name = "prepend", Title = "T", Description = "D", Directory = _testDirectory };
        var feedId = await _service.AddFeedAsync(feed);

        // Add existing episode with display order 10
        await _service.AddEpisodeAsync(feedId, new EpisodeRecord { Filename = "existing.mp3", DisplayOrder = 10 });

        // Create new file
        File.WriteAllText(Path.Combine(_testDirectory, "new.mp3"), "test");

        // Act
        await _service.SyncDirectoryAsync(feedId, _testDirectory, new[] { ".mp3" });

        // Assert - new file should have lower display order (prepended)
        var newEp = await _service.GetEpisodeByFilenameAsync(feedId, "new.mp3");
        Assert.NotNull(newEp);
        Assert.True(newEp.DisplayOrder < 10, "New episode should be prepended (lower order)");
    }

    #endregion
}
