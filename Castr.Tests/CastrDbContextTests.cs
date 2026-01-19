using Xunit;
using Microsoft.EntityFrameworkCore;
using Castr.Data;
using Castr.Data.Entities;

namespace Castr.Tests;

/// <summary>
/// Tests for CastrDbContext covering entity configurations,
/// relationships, and basic CRUD operations.
/// </summary>
public class CastrDbContextTests : IDisposable
{
    private readonly CastrDbContext _context;
    private readonly string _testDbPath;

    public CastrDbContextTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_castr_{Guid.NewGuid()}.db");
        
        var options = new DbContextOptionsBuilder<CastrDbContext>()
            .UseSqlite($"Data Source={_testDbPath}")
            .Options;
            
        _context = new CastrDbContext(options);
        _context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _context.Dispose();
        if (File.Exists(_testDbPath))
        {
            try
            {
                File.Delete(_testDbPath);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public void DbContext_CanBeInstantiated()
    {
        // Arrange & Act
        // Context is created in constructor

        // Assert
        Assert.NotNull(_context);
        Assert.NotNull(_context.Feeds);
        Assert.NotNull(_context.Episodes);
        Assert.NotNull(_context.DownloadedVideos);
        Assert.NotNull(_context.ActivityLogs);
        Assert.NotNull(_context.DownloadQueue);
    }

    [Fact]
    public async Task DbContext_CanCreateDatabase()
    {
        // Arrange & Act
        var canConnect = await _context.Database.CanConnectAsync();

        // Assert
        Assert.True(canConnect);
    }

    [Fact]
    public async Task Feed_CanBeAddedAndRetrieved()
    {
        // Arrange
        var feed = new Feed
        {
            Name = "test-feed",
            Title = "Test Feed",
            Description = "Test Description",
            Directory = "/test/path",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsActive = true
        };

        // Act
        _context.Feeds.Add(feed);
        await _context.SaveChangesAsync();

        // Assert
        var retrievedFeed = await _context.Feeds.FirstOrDefaultAsync(f => f.Name == "test-feed");
        Assert.NotNull(retrievedFeed);
        Assert.Equal("Test Feed", retrievedFeed.Title);
        Assert.Equal("Test Description", retrievedFeed.Description);
        Assert.True(retrievedFeed.IsActive);
    }

    [Fact]
    public async Task Feed_NameIsUnique()
    {
        // Arrange
        var feed1 = new Feed
        {
            Name = "duplicate-feed",
            Title = "Feed 1",
            Description = "Description 1",
            Directory = "/test/path1",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var feed2 = new Feed
        {
            Name = "duplicate-feed", // Same name
            Title = "Feed 2",
            Description = "Description 2",
            Directory = "/test/path2",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsActive = true
        };

        // Act & Assert
        _context.Feeds.Add(feed1);
        await _context.SaveChangesAsync();

        _context.Feeds.Add(feed2);
        await Assert.ThrowsAsync<DbUpdateException>(() => _context.SaveChangesAsync());
    }

    [Fact]
    public async Task Episode_CanBeAddedWithFeedRelationship()
    {
        // Arrange
        var feed = new Feed
        {
            Name = "test-feed",
            Title = "Test Feed",
            Description = "Test Description",
            Directory = "/test/path",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.Feeds.Add(feed);
        await _context.SaveChangesAsync();

        var episode = new Episode
        {
            FeedId = feed.Id,
            Filename = "test-episode.mp3",
            DisplayOrder = 1,
            AddedAt = DateTime.UtcNow
        };

        // Act
        _context.Episodes.Add(episode);
        await _context.SaveChangesAsync();

        // Assert
        var retrievedEpisode = await _context.Episodes
            .Include(e => e.Feed)
            .FirstOrDefaultAsync(e => e.Filename == "test-episode.mp3");
            
        Assert.NotNull(retrievedEpisode);
        Assert.Equal(feed.Id, retrievedEpisode.FeedId);
        Assert.NotNull(retrievedEpisode.Feed);
        Assert.Equal("Test Feed", retrievedEpisode.Feed.Title);
    }

    [Fact]
    public async Task DownloadedVideo_UniqueConstraintOnFeedAndVideoId()
    {
        // Arrange
        var feed = new Feed
        {
            Name = "test-feed",
            Title = "Test Feed",
            Description = "Test Description",
            Directory = "/test/path",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.Feeds.Add(feed);
        await _context.SaveChangesAsync();

        var video1 = new DownloadedVideo
        {
            FeedId = feed.Id,
            VideoId = "test-video-123",
            DownloadedAt = DateTime.UtcNow
        };

        var video2 = new DownloadedVideo
        {
            FeedId = feed.Id,
            VideoId = "test-video-123", // Same video ID
            DownloadedAt = DateTime.UtcNow
        };

        // Act & Assert
        _context.DownloadedVideos.Add(video1);
        await _context.SaveChangesAsync();

        _context.DownloadedVideos.Add(video2);
        await Assert.ThrowsAsync<DbUpdateException>(() => _context.SaveChangesAsync());
    }

    [Fact]
    public async Task ActivityLog_CanBeAddedWithOptionalFeed()
    {
        // Arrange
        var feed = new Feed
        {
            Name = "test-feed",
            Title = "Test Feed",
            Description = "Test Description",
            Directory = "/test/path",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.Feeds.Add(feed);
        await _context.SaveChangesAsync();

        var logWithFeed = new ActivityLog
        {
            FeedId = feed.Id,
            ActivityType = "download",
            Message = "Downloaded video",
            CreatedAt = DateTime.UtcNow
        };

        var logWithoutFeed = new ActivityLog
        {
            FeedId = null, // System-wide log
            ActivityType = "startup",
            Message = "System started",
            CreatedAt = DateTime.UtcNow
        };

        // Act
        _context.ActivityLogs.Add(logWithFeed);
        _context.ActivityLogs.Add(logWithoutFeed);
        await _context.SaveChangesAsync();

        // Assert
        var logs = await _context.ActivityLogs.ToListAsync();
        Assert.Equal(2, logs.Count);
        Assert.Contains(logs, l => l.FeedId == feed.Id);
        Assert.Contains(logs, l => l.FeedId == null);
    }

    [Fact]
    public async Task DownloadQueueItem_CanBeAddedAndRetrieved()
    {
        // Arrange
        var feed = new Feed
        {
            Name = "test-feed",
            Title = "Test Feed",
            Description = "Test Description",
            Directory = "/test/path",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.Feeds.Add(feed);
        await _context.SaveChangesAsync();

        var queueItem = new DownloadQueueItem
        {
            FeedId = feed.Id,
            VideoId = "test-video-456",
            VideoTitle = "Test Video",
            Status = "queued",
            ProgressPercent = 0,
            QueuedAt = DateTime.UtcNow
        };

        // Act
        _context.DownloadQueue.Add(queueItem);
        await _context.SaveChangesAsync();

        // Assert
        var retrievedItem = await _context.DownloadQueue
            .Include(q => q.Feed)
            .FirstOrDefaultAsync(q => q.VideoId == "test-video-456");
            
        Assert.NotNull(retrievedItem);
        Assert.Equal("queued", retrievedItem.Status);
        Assert.Equal(0, retrievedItem.ProgressPercent);
        Assert.NotNull(retrievedItem.Feed);
        Assert.Equal("Test Feed", retrievedItem.Feed.Title);
    }

    [Fact]
    public async Task Feed_CascadeDeleteRemovesRelatedEntities()
    {
        // Arrange
        var feed = new Feed
        {
            Name = "test-feed",
            Title = "Test Feed",
            Description = "Test Description",
            Directory = "/test/path",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.Feeds.Add(feed);
        await _context.SaveChangesAsync();

        var episode = new Episode
        {
            FeedId = feed.Id,
            Filename = "test-episode.mp3",
            DisplayOrder = 1,
            AddedAt = DateTime.UtcNow
        };

        var downloadedVideo = new DownloadedVideo
        {
            FeedId = feed.Id,
            VideoId = "test-video-789",
            DownloadedAt = DateTime.UtcNow
        };

        _context.Episodes.Add(episode);
        _context.DownloadedVideos.Add(downloadedVideo);
        await _context.SaveChangesAsync();

        // Act
        _context.Feeds.Remove(feed);
        await _context.SaveChangesAsync();

        // Assert
        var episodes = await _context.Episodes.ToListAsync();
        var videos = await _context.DownloadedVideos.ToListAsync();
        
        Assert.Empty(episodes); // Should be deleted
        Assert.Empty(videos); // Should be deleted
    }
}
