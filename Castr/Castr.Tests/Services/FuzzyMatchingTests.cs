using System.Reflection;
using Microsoft.Extensions.Configuration;
using Castr.Tests.TestHelpers;

namespace Castr.Tests.Services;

/// <summary>
/// Tests for fuzzy matching functionality used in playlist synchronization.
/// These methods are internal/private, so we test via integration or reflection.
/// </summary>
public class FuzzyMatchingTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly CentralDatabaseService _service;
    private readonly Mock<ILogger<CentralDatabaseService>> _mockLogger;
    private readonly string _testDirectory;

    public FuzzyMatchingTests()
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

    #region SyncPlaylistInfoAsync Integration Tests

    [Fact]
    public async Task SyncPlaylistInfoAsync_WithMatchingFiles_UpdatesEpisodes()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();

        // Create test feed
        var feed = new FeedRecord
        {
            Name = "synctest",
            Title = "Sync Test",
            Description = "Test",
            Directory = _testDirectory
        };
        var feedId = await _service.AddFeedAsync(feed);

        // Create test file that should match
        var testFileName = "Episode 123 - The Story.mp3";
        File.WriteAllText(Path.Combine(_testDirectory, testFileName), "test");

        // Add existing episode
        await _service.AddEpisodeAsync(feedId, new EpisodeRecord
        {
            Filename = testFileName,
            DisplayOrder = 1
        });

        // Create playlist video that should match
        var videos = new List<PlaylistVideoInfo>
        {
            new PlaylistVideoInfo
            {
                VideoId = "abc123",
                Title = "Episode 123 - The Story",
                Description = "Episode description",
                ThumbnailUrl = "https://example.com/thumb.jpg",
                UploadDate = new DateTime(2024, 6, 15),
                PlaylistIndex = 1
            }
        };

        // Act
        await _service.SyncPlaylistInfoAsync(feedId, videos, _testDirectory);

        // Assert
        var episode = await _service.GetEpisodeByFilenameAsync(feedId, testFileName);
        Assert.NotNull(episode);
        Assert.Equal("abc123", episode.VideoId);
        Assert.Equal("Episode 123 - The Story", episode.YoutubeTitle);
    }

    [Fact]
    public async Task SyncPlaylistInfoAsync_WithNoMatchingFiles_SkipsVideo()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();

        var feed = new FeedRecord
        {
            Name = "nomatch",
            Title = "No Match Test",
            Description = "Test",
            Directory = _testDirectory
        };
        var feedId = await _service.AddFeedAsync(feed);

        // Create file with very different name
        File.WriteAllText(Path.Combine(_testDirectory, "Completely Different Name.mp3"), "test");

        var videos = new List<PlaylistVideoInfo>
        {
            new PlaylistVideoInfo
            {
                VideoId = "xyz789",
                Title = "Totally Unrelated Video Title That Won't Match",
                PlaylistIndex = 1
            }
        };

        // Act
        await _service.SyncPlaylistInfoAsync(feedId, videos, _testDirectory);

        // Assert - no episode should have the video ID
        var episodes = await _service.GetEpisodesAsync(feedId);
        Assert.All(episodes, e => Assert.Null(e.VideoId));
    }

    [Fact]
    public async Task SyncPlaylistInfoAsync_WithEmptyDirectory_DoesNotThrow()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();

        var feed = new FeedRecord
        {
            Name = "emptydir",
            Title = "Empty Dir Test",
            Description = "Test",
            Directory = _testDirectory
        };
        var feedId = await _service.AddFeedAsync(feed);

        var videos = new List<PlaylistVideoInfo>
        {
            new PlaylistVideoInfo
            {
                VideoId = "vid1",
                Title = "Video 1",
                PlaylistIndex = 1
            }
        };

        // Act & Assert - should not throw
        await _service.SyncPlaylistInfoAsync(feedId, videos, _testDirectory);
    }

    [Fact]
    public async Task SyncPlaylistInfoAsync_WithMultipleVideos_MatchesInOrder()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();

        var feed = new FeedRecord
        {
            Name = "multitest",
            Title = "Multi Test",
            Description = "Test",
            Directory = _testDirectory
        };
        var feedId = await _service.AddFeedAsync(feed);

        // Create test files
        File.WriteAllText(Path.Combine(_testDirectory, "Episode One.mp3"), "test");
        File.WriteAllText(Path.Combine(_testDirectory, "Episode Two.mp3"), "test");
        File.WriteAllText(Path.Combine(_testDirectory, "Episode Three.mp3"), "test");

        var videos = new List<PlaylistVideoInfo>
        {
            new PlaylistVideoInfo { VideoId = "v1", Title = "Episode One", PlaylistIndex = 1 },
            new PlaylistVideoInfo { VideoId = "v2", Title = "Episode Two", PlaylistIndex = 2 },
            new PlaylistVideoInfo { VideoId = "v3", Title = "Episode Three", PlaylistIndex = 3 }
        };

        // Act
        await _service.SyncPlaylistInfoAsync(feedId, videos, _testDirectory);

        // Assert
        var ep1 = await _service.GetEpisodeByFilenameAsync(feedId, "Episode One.mp3");
        var ep2 = await _service.GetEpisodeByFilenameAsync(feedId, "Episode Two.mp3");
        var ep3 = await _service.GetEpisodeByFilenameAsync(feedId, "Episode Three.mp3");

        Assert.NotNull(ep1);
        Assert.Equal("v1", ep1.VideoId);
        Assert.NotNull(ep2);
        Assert.Equal("v2", ep2.VideoId);
        Assert.NotNull(ep3);
        Assert.Equal("v3", ep3.VideoId);
    }

    [Fact]
    public async Task SyncPlaylistInfoAsync_WithEmptyVideoList_DoesNotThrow()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();

        var feed = new FeedRecord
        {
            Name = "emptyvideos",
            Title = "Empty Videos Test",
            Description = "Test",
            Directory = _testDirectory
        };
        var feedId = await _service.AddFeedAsync(feed);

        var videos = new List<PlaylistVideoInfo>();

        // Act & Assert - should not throw
        await _service.SyncPlaylistInfoAsync(feedId, videos, _testDirectory);
    }

    [Fact]
    public async Task SyncPlaylistInfoAsync_UpdatesDisplayOrder()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();

        var feed = new FeedRecord
        {
            Name = "ordertest",
            Title = "Order Test",
            Description = "Test",
            Directory = _testDirectory
        };
        var feedId = await _service.AddFeedAsync(feed);

        // Create file and existing episode
        File.WriteAllText(Path.Combine(_testDirectory, "Test Episode.mp3"), "test");
        await _service.AddEpisodeAsync(feedId, new EpisodeRecord
        {
            Filename = "Test Episode.mp3",
            DisplayOrder = 99
        });

        var videos = new List<PlaylistVideoInfo>
        {
            new PlaylistVideoInfo
            {
                VideoId = "vid1",
                Title = "Test Episode",
                PlaylistIndex = 5
            }
        };

        // Act
        await _service.SyncPlaylistInfoAsync(feedId, videos, _testDirectory);

        // Assert
        var episode = await _service.GetEpisodeByFilenameAsync(feedId, "Test Episode.mp3");
        Assert.NotNull(episode);
        Assert.Equal(5, episode.DisplayOrder);
    }

    [Fact]
    public async Task SyncPlaylistInfoAsync_SetsMatchScore()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();

        var feed = new FeedRecord
        {
            Name = "scoretest",
            Title = "Score Test",
            Description = "Test",
            Directory = _testDirectory
        };
        var feedId = await _service.AddFeedAsync(feed);

        File.WriteAllText(Path.Combine(_testDirectory, "Exact Title Match.mp3"), "test");

        var videos = new List<PlaylistVideoInfo>
        {
            new PlaylistVideoInfo
            {
                VideoId = "vid1",
                Title = "Exact Title Match",
                PlaylistIndex = 1
            }
        };

        // Act
        await _service.SyncPlaylistInfoAsync(feedId, videos, _testDirectory);

        // Assert
        var episode = await _service.GetEpisodeByFilenameAsync(feedId, "Exact Title Match.mp3");
        Assert.NotNull(episode);
        Assert.NotNull(episode.MatchScore);
        Assert.True(episode.MatchScore > 0.9, $"Expected match score > 0.9, got {episode.MatchScore}");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task SyncPlaylistInfoAsync_HandlesSpecialCharactersInTitle()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();

        var feed = new FeedRecord
        {
            Name = "specialchars",
            Title = "Special Chars",
            Description = "Test",
            Directory = _testDirectory
        };
        var feedId = await _service.AddFeedAsync(feed);

        // Create file with special characters
        var fileName = "Episode - Part 1 (2024).mp3";
        File.WriteAllText(Path.Combine(_testDirectory, fileName), "test");

        var videos = new List<PlaylistVideoInfo>
        {
            new PlaylistVideoInfo
            {
                VideoId = "vid1",
                Title = "Episode - Part 1 (2024)",
                PlaylistIndex = 1
            }
        };

        // Act
        await _service.SyncPlaylistInfoAsync(feedId, videos, _testDirectory);

        // Assert
        var episode = await _service.GetEpisodeByFilenameAsync(feedId, fileName);
        Assert.NotNull(episode);
        Assert.Equal("vid1", episode.VideoId);
    }

    [Fact]
    public async Task SyncPlaylistInfoAsync_HandlesBehindTheBastardsFormat()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();

        var feed = new FeedRecord
        {
            Name = "btbformat",
            Title = "BTB Format",
            Description = "Test",
            Directory = _testDirectory
        };
        var feedId = await _service.AddFeedAsync(feed);

        // Create file matching BTB format
        var fileName = "The Villain Who Built McDonald's.mp3";
        File.WriteAllText(Path.Combine(_testDirectory, fileName), "test");

        var videos = new List<PlaylistVideoInfo>
        {
            new PlaylistVideoInfo
            {
                VideoId = "btbvid1",
                Title = "The Villain Who Built McDonald's | BEHIND THE BASTARDS",
                PlaylistIndex = 1
            }
        };

        // Act
        await _service.SyncPlaylistInfoAsync(feedId, videos, _testDirectory);

        // Assert
        var episode = await _service.GetEpisodeByFilenameAsync(feedId, fileName);
        Assert.NotNull(episode);
        Assert.Equal("btbvid1", episode.VideoId);
    }

    [Fact]
    public async Task SyncPlaylistInfoAsync_PreservesExistingMetadataWhenUpdating()
    {
        // Arrange
        await _service.InitializeDatabaseAsync();

        var feed = new FeedRecord
        {
            Name = "preserve",
            Title = "Preserve Test",
            Description = "Test",
            Directory = _testDirectory
        };
        var feedId = await _service.AddFeedAsync(feed);

        var fileName = "Preserve Test.mp3";
        File.WriteAllText(Path.Combine(_testDirectory, fileName), "test");

        // Add episode with existing metadata
        await _service.AddEpisodeAsync(feedId, new EpisodeRecord
        {
            Filename = fileName,
            DisplayOrder = 1,
            Description = "Existing description",
            ThumbnailUrl = "https://existing.com/thumb.jpg"
        });

        // Sync with video that has no description/thumbnail
        var videos = new List<PlaylistVideoInfo>
        {
            new PlaylistVideoInfo
            {
                VideoId = "vid1",
                Title = "Preserve Test",
                Description = null,
                ThumbnailUrl = null,
                PlaylistIndex = 2
            }
        };

        // Act
        await _service.SyncPlaylistInfoAsync(feedId, videos, _testDirectory);

        // Assert - existing metadata should be preserved (null in video = keep existing)
        var episode = await _service.GetEpisodeByFilenameAsync(feedId, fileName);
        Assert.NotNull(episode);
        Assert.Equal("vid1", episode.VideoId);
    }

    #endregion
}
