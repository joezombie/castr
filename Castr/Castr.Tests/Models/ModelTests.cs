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

    #region EpisodeRecord Tests

    [Fact]
    public void EpisodeRecord_WithRequiredProperties_InitializesCorrectly()
    {
        // Act
        var episode = new EpisodeRecord
        {
            Filename = "test.mp3"
        };

        // Assert
        Assert.Equal(0, episode.Id);
        Assert.Equal(0, episode.DisplayOrder);
        Assert.Null(episode.VideoId);
        Assert.Null(episode.YoutubeTitle);
        Assert.Null(episode.Description);
        Assert.Null(episode.ThumbnailUrl);
        Assert.Null(episode.PublishDate);
        Assert.Null(episode.MatchScore);
    }

    [Fact]
    public void EpisodeRecord_CanSetAllProperties()
    {
        // Arrange & Act
        var episode = new EpisodeRecord
        {
            Id = 1,
            Filename = "episode.mp3",
            VideoId = "abc123",
            YoutubeTitle = "YouTube Title",
            Description = "Description",
            ThumbnailUrl = "https://example.com/thumb.jpg",
            DisplayOrder = 5,
            AddedAt = DateTime.UtcNow,
            PublishDate = DateTime.UtcNow,
            MatchScore = 0.95
        };

        // Assert
        Assert.Equal(1, episode.Id);
        Assert.Equal("episode.mp3", episode.Filename);
        Assert.Equal("abc123", episode.VideoId);
        Assert.Equal(0.95, episode.MatchScore);
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

    #region PodcastFeedConfig Tests

    [Fact]
    public void PodcastFeedConfig_DefaultFileExtensions()
    {
        // Act
        var config = new PodcastFeedConfig
        {
            Title = "Test",
            Description = "Desc",
            Directory = "/path"
        };

        // Assert - FileExtensions has default value
        Assert.NotNull(config.FileExtensions);
        Assert.Contains(".mp3", config.FileExtensions);
    }

    [Fact]
    public void PodcastFeedConfig_DefaultLanguage()
    {
        // Act
        var config = new PodcastFeedConfig
        {
            Title = "Test",
            Description = "Desc",
            Directory = "/path"
        };

        // Assert
        Assert.Equal("en-us", config.Language);
    }

    [Fact]
    public void PodcastFeedConfig_CanSetAllProperties()
    {
        // Arrange & Act
        var config = new PodcastFeedConfig
        {
            Title = "My Podcast",
            Description = "Description",
            Directory = "/podcasts/my-podcast",
            Author = "John Doe",
            ImageUrl = "https://example.com/image.png",
            Link = "https://example.com",
            Language = "en-us",
            Category = "Technology",
            FileExtensions = new[] { ".mp3", ".m4a" },
            DatabasePath = "/data/podcast.db",
            YouTube = new YouTubePlaylistConfig
            {
                PlaylistUrl = "https://youtube.com/playlist",
                Enabled = true
            }
        };

        // Assert
        Assert.Equal("My Podcast", config.Title);
        Assert.Equal(2, config.FileExtensions?.Length);
        Assert.NotNull(config.YouTube);
    }

    #endregion

    #region YouTubePlaylistConfig Tests

    [Fact]
    public void YouTubePlaylistConfig_DefaultValues()
    {
        // Act
        var config = new YouTubePlaylistConfig
        {
            PlaylistUrl = "https://youtube.com/playlist"
        };

        // Assert
        Assert.Equal(60, config.PollIntervalMinutes);
        Assert.True(config.Enabled);
        Assert.Equal(1, config.MaxConcurrentDownloads);
        Assert.Equal("highest", config.AudioQuality);
    }

    [Fact]
    public void YouTubePlaylistConfig_CanOverrideDefaults()
    {
        // Arrange & Act
        var config = new YouTubePlaylistConfig
        {
            PlaylistUrl = "https://youtube.com/playlist",
            PollIntervalMinutes = 30,
            Enabled = false,
            MaxConcurrentDownloads = 3,
            AudioQuality = "lowest"
        };

        // Assert
        Assert.Equal(30, config.PollIntervalMinutes);
        Assert.False(config.Enabled);
        Assert.Equal(3, config.MaxConcurrentDownloads);
        Assert.Equal("lowest", config.AudioQuality);
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

    #region PodcastFeedsConfig Tests

    [Fact]
    public void PodcastFeedsConfig_DefaultCacheDuration()
    {
        // Act
        var config = new PodcastFeedsConfig();

        // Assert
        Assert.Equal(5, config.CacheDurationMinutes);
    }

    [Fact]
    public void PodcastFeedsConfig_CanAddFeeds()
    {
        // Arrange & Act
        var config = new PodcastFeedsConfig
        {
            CacheDurationMinutes = 10,
            Feeds = new Dictionary<string, PodcastFeedConfig>
            {
                ["feed1"] = new PodcastFeedConfig { Title = "F1", Description = "D1", Directory = "/p1" },
                ["feed2"] = new PodcastFeedConfig { Title = "F2", Description = "D2", Directory = "/p2" }
            }
        };

        // Assert
        Assert.Equal(10, config.CacheDurationMinutes);
        Assert.Equal(2, config.Feeds.Count);
    }

    [Fact]
    public void PodcastFeedsConfig_DefaultFeedsIsEmpty()
    {
        // Act
        var config = new PodcastFeedsConfig();

        // Assert
        Assert.NotNull(config.Feeds);
        Assert.Empty(config.Feeds);
    }

    #endregion
}
