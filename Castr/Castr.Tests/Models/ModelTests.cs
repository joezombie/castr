namespace Castr.Tests.Models;

public class ModelTests
{
    #region FeedRecord Tests

    [Fact]
    public void FeedRecord_WithRequiredProperties_InitializesCorrectly()
    {
        // Act
        var feed = new FeedRecord
        {
            Name = "test",
            Title = "Test",
            Description = "Desc",
            Directory = "/path"
        };

        // Assert
        Assert.Equal(0, feed.Id);
        Assert.Equal("test", feed.Name);
        // IsActive defaults to false for new instances (set to true in service)
        Assert.False(feed.YouTubeEnabled);
    }

    [Fact]
    public void FeedRecord_CanSetAllProperties()
    {
        // Arrange & Act
        var feed = new FeedRecord
        {
            Id = 1,
            Name = "testfeed",
            Title = "Test Feed",
            Description = "Description",
            Directory = "/path",
            Author = "Author",
            ImageUrl = "https://example.com/image.png",
            Link = "https://example.com",
            Language = "en-us",
            Category = "Technology",
            FileExtensions = ".mp3,.m4a",
            YouTubePlaylistUrl = "https://youtube.com/playlist",
            YouTubePollIntervalMinutes = 60,
            YouTubeEnabled = true,
            YouTubeMaxConcurrentDownloads = 2,
            YouTubeAudioQuality = "highest",
            IsActive = true
        };

        // Assert
        Assert.Equal(1, feed.Id);
        Assert.Equal("testfeed", feed.Name);
        Assert.Equal("Test Feed", feed.Title);
        Assert.True(feed.YouTubeEnabled);
    }

    [Fact]
    public void FeedRecord_OptionalPropertiesDefaultToNull()
    {
        // Act
        var feed = new FeedRecord
        {
            Name = "test",
            Title = "Test",
            Description = "Desc",
            Directory = "/path"
        };

        // Assert
        Assert.Null(feed.Author);
        Assert.Null(feed.ImageUrl);
        Assert.Null(feed.Link);
        Assert.Null(feed.YouTubePlaylistUrl);
    }

    #endregion

    #region DownloadQueueItem Tests

    [Fact]
    public void DownloadQueueItem_WithRequiredProperties_InitializesCorrectly()
    {
        // Act
        var item = new DownloadQueueItem
        {
            VideoId = "vid123",
            Status = "queued"
        };

        // Assert
        Assert.Equal("queued", item.Status);
        Assert.Equal(0, item.ProgressPercent);
        Assert.Null(item.ErrorMessage);
        Assert.Null(item.StartedAt);
        Assert.Null(item.CompletedAt);
    }

    [Fact]
    public void DownloadQueueItem_CanSetAllProperties()
    {
        // Arrange & Act
        var now = DateTime.UtcNow;
        var item = new DownloadQueueItem
        {
            Id = 1,
            FeedId = 2,
            VideoId = "vid123",
            VideoTitle = "Video Title",
            Status = "downloading",
            ProgressPercent = 50,
            ErrorMessage = null,
            QueuedAt = now,
            StartedAt = now,
            CompletedAt = null
        };

        // Assert
        Assert.Equal(1, item.Id);
        Assert.Equal(2, item.FeedId);
        Assert.Equal("vid123", item.VideoId);
        Assert.Equal("downloading", item.Status);
        Assert.Equal(50, item.ProgressPercent);
    }

    [Fact]
    public void DownloadQueueItem_CanSetErrorMessage()
    {
        // Act
        var item = new DownloadQueueItem
        {
            VideoId = "vid",
            Status = "failed",
            ErrorMessage = "Download failed due to network error"
        };

        // Assert
        Assert.Equal("failed", item.Status);
        Assert.Equal("Download failed due to network error", item.ErrorMessage);
    }

    #endregion

    #region ActivityLogRecord Tests

    [Fact]
    public void ActivityLogRecord_Initialization()
    {
        // Act
        var activity = new ActivityLogRecord
        {
            Id = 1,
            FeedId = 1,
            ActivityType = "download",
            Message = "Test message",
            Details = "Some details",
            CreatedAt = DateTime.UtcNow
        };

        // Assert
        Assert.Equal(1, activity.FeedId);
        Assert.Equal("download", activity.ActivityType);
        Assert.Equal("Test message", activity.Message);
        Assert.Equal("Some details", activity.Details);
    }

    [Fact]
    public void ActivityLogRecord_FeedId_CanBeNull()
    {
        // Act
        var activity = new ActivityLogRecord
        {
            FeedId = null,
            ActivityType = "system",
            Message = "System event"
        };

        // Assert
        Assert.Null(activity.FeedId);
    }

    #endregion

    #region Feed Entity Tests

    [Fact]
    public void Feed_DefaultFileExtensions()
    {
        // Act
        var feed = new Castr.Data.Entities.Feed
        {
            Name = "test",
            Title = "Test",
            Description = "Desc",
            Directory = "/path"
        };

        // Assert - FileExtensions has default value
        Assert.NotNull(feed.FileExtensions);
        Assert.Contains(".mp3", feed.FileExtensions);
    }

    [Fact]
    public void Feed_DefaultCacheDurationMinutes()
    {
        // Act
        var feed = new Castr.Data.Entities.Feed
        {
            Name = "test",
            Title = "Test",
            Description = "Desc",
            Directory = "/path"
        };

        // Assert
        Assert.Equal(5, feed.CacheDurationMinutes);
    }

    [Fact]
    public void Feed_CanSetFileExtensionsArray()
    {
        // Arrange & Act
        var feed = new Castr.Data.Entities.Feed
        {
            Name = "test",
            Title = "Test",
            Description = "Desc",
            Directory = "/path",
            FileExtensions = [".mp3", ".m4a"]
        };

        // Assert
        Assert.Equal(2, feed.FileExtensions.Length);
        Assert.Contains(".mp3", feed.FileExtensions);
        Assert.Contains(".m4a", feed.FileExtensions);
    }

    #endregion

    #region PlaylistVideoInfo Tests

    [Fact]
    public void PlaylistVideoInfo_Initialization()
    {
        // Act
        var video = new PlaylistVideoInfo
        {
            VideoId = "abc123",
            Title = "Video Title",
            Description = "Description",
            ThumbnailUrl = "https://example.com/thumb.jpg",
            UploadDate = new DateTime(2024, 1, 15),
            PlaylistIndex = 5
        };

        // Assert
        Assert.Equal("abc123", video.VideoId);
        Assert.Equal("Video Title", video.Title);
        Assert.Equal(5, video.PlaylistIndex);
        Assert.Equal(new DateTime(2024, 1, 15), video.UploadDate);
    }

    [Fact]
    public void PlaylistVideoInfo_OptionalFieldsCanBeNull()
    {
        // Act
        var video = new PlaylistVideoInfo
        {
            VideoId = "xyz",
            Title = "Title",
            PlaylistIndex = 0
        };

        // Assert
        Assert.Null(video.Description);
        Assert.Null(video.ThumbnailUrl);
        Assert.Null(video.UploadDate);
    }

    #endregion

    #region VideoDetails Tests

    [Fact]
    public void VideoDetails_Initialization()
    {
        // Act
        var details = new VideoDetails
        {
            VideoId = "xyz789",
            Title = "Detail Title",
            Description = "Full description",
            ThumbnailUrl = "https://example.com/thumb.jpg",
            UploadDate = new DateTime(2024, 1, 15)
        };

        // Assert
        Assert.Equal("xyz789", details.VideoId);
        Assert.Equal(new DateTime(2024, 1, 15), details.UploadDate);
    }

    [Fact]
    public void VideoDetails_OptionalFieldsCanBeNull()
    {
        // Act
        var details = new VideoDetails
        {
            VideoId = "vid",
            Title = "Title"
        };

        // Assert
        Assert.Null(details.Description);
        Assert.Null(details.ThumbnailUrl);
        Assert.Null(details.UploadDate);
    }

    #endregion

}
