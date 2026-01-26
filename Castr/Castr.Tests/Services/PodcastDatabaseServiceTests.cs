using Castr.Tests.TestHelpers;

namespace Castr.Tests.Services;

public class PodcastDatabaseServiceTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly PodcastDatabaseService _service;
    private readonly Mock<ILogger<PodcastDatabaseService>> _mockLogger;
    private readonly PodcastFeedsConfig _config;

    public PodcastDatabaseServiceTests()
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
        TestDatabaseHelper.DeleteDirectory(_testDirectory);
    }

    [Fact]
    public async Task InitializeDatabaseAsync_CreatesDatabase()
    {
        // Act
        await _service.InitializeDatabaseAsync("testfeed");

        // Assert
        var dbPath = Path.Combine(_testDirectory, "test.db");
        Assert.True(File.Exists(dbPath));
    }

    [Fact]
    public async Task GetEpisodesAsync_ReturnsEmptyForNewDatabase()
    {
        // Arrange
        await _service.InitializeDatabaseAsync("testfeed");

        // Act
        var episodes = await _service.GetEpisodesAsync("testfeed");

        // Assert
        Assert.Empty(episodes);
    }

    [Fact]
    public async Task AddEpisodeAsync_AddsEpisode()
    {
        // Arrange
        await _service.InitializeDatabaseAsync("testfeed");
        var episode = new EpisodeRecord
        {
            Filename = "test.mp3",
            DisplayOrder = 1
        };

        // Act
        await _service.AddEpisodeAsync("testfeed", episode);

        // Assert
        var episodes = await _service.GetEpisodesAsync("testfeed");
        Assert.Single(episodes);
        Assert.Equal("test.mp3", episodes[0].Filename);
    }

    [Fact]
    public async Task AddEpisodesAsync_AddsBatch()
    {
        // Arrange
        await _service.InitializeDatabaseAsync("testfeed");
        var episodes = new[]
        {
            new EpisodeRecord { Filename = "ep1.mp3", DisplayOrder = 1 },
            new EpisodeRecord { Filename = "ep2.mp3", DisplayOrder = 2 }
        };

        // Act
        await _service.AddEpisodesAsync("testfeed", episodes);

        // Assert
        var result = await _service.GetEpisodesAsync("testfeed");
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task MarkVideoDownloadedAsync_MarksVideo()
    {
        // Arrange
        await _service.InitializeDatabaseAsync("testfeed");

        // Act
        await _service.MarkVideoDownloadedAsync("testfeed", "vid123", "video.mp3");

        // Assert
        var isDownloaded = await _service.IsVideoDownloadedAsync("testfeed", "vid123");
        Assert.True(isDownloaded);
    }

    [Fact]
    public async Task IsVideoDownloadedAsync_ReturnsFalseForUnknownVideo()
    {
        // Arrange
        await _service.InitializeDatabaseAsync("testfeed");

        // Act
        var isDownloaded = await _service.IsVideoDownloadedAsync("testfeed", "unknown");

        // Assert
        Assert.False(isDownloaded);
    }

    [Fact]
    public async Task GetDownloadedVideoIdsAsync_ReturnsAllDownloadedIds()
    {
        // Arrange
        await _service.InitializeDatabaseAsync("testfeed");
        await _service.MarkVideoDownloadedAsync("testfeed", "v1", "f1.mp3");
        await _service.MarkVideoDownloadedAsync("testfeed", "v2", "f2.mp3");

        // Act
        var ids = await _service.GetDownloadedVideoIdsAsync("testfeed");

        // Assert
        Assert.Equal(2, ids.Count);
        Assert.Contains("v1", ids);
        Assert.Contains("v2", ids);
    }

    [Fact]
    public async Task UpdateEpisodeAsync_UpdatesExistingEpisode()
    {
        // Arrange
        await _service.InitializeDatabaseAsync("testfeed");
        var episode = new EpisodeRecord
        {
            Filename = "update.mp3",
            DisplayOrder = 1,
            YoutubeTitle = "Original Title"
        };
        await _service.AddEpisodeAsync("testfeed", episode);

        // Act
        var episodes = await _service.GetEpisodesAsync("testfeed");
        var toUpdate = episodes[0];
        toUpdate.YoutubeTitle = "Updated Title";
        await _service.UpdateEpisodeAsync("testfeed", toUpdate);

        // Assert
        var updated = await _service.GetEpisodesAsync("testfeed");
        Assert.Equal("Updated Title", updated[0].YoutubeTitle);
    }

    [Fact]
    public async Task SyncDirectoryAsync_AddsNewFilesToDatabase()
    {
        // Arrange
        await _service.InitializeDatabaseAsync("testfeed");
        File.WriteAllText(Path.Combine(_testDirectory, "new-episode.mp3"), "test");

        // Act
        await _service.SyncDirectoryAsync("testfeed", _testDirectory, new[] { ".mp3" });

        // Assert
        var episodes = await _service.GetEpisodesAsync("testfeed");
        Assert.Single(episodes);
        Assert.Equal("new-episode.mp3", episodes[0].Filename);
    }

    [Fact]
    public async Task SyncDirectoryAsync_SkipsExistingFiles()
    {
        // Arrange
        await _service.InitializeDatabaseAsync("testfeed");
        await _service.AddEpisodeAsync("testfeed", new EpisodeRecord { Filename = "existing.mp3", DisplayOrder = 1 });
        File.WriteAllText(Path.Combine(_testDirectory, "existing.mp3"), "test");

        // Act
        await _service.SyncDirectoryAsync("testfeed", _testDirectory, new[] { ".mp3" });

        // Assert
        var episodes = await _service.GetEpisodesAsync("testfeed");
        Assert.Single(episodes);
    }

    [Fact]
    public async Task AddEpisodeAsync_WithYouTubeMetadata_PersistsCorrectly()
    {
        // Arrange
        await _service.InitializeDatabaseAsync("testfeed");
        var episode = new EpisodeRecord
        {
            Filename = "youtube-episode.mp3",
            VideoId = "abc123",
            YoutubeTitle = "YouTube Video Title",
            Description = "Video description",
            ThumbnailUrl = "https://example.com/thumb.jpg",
            DisplayOrder = 1,
            PublishDate = new DateTime(2024, 6, 15),
            MatchScore = 0.92
        };

        // Act
        await _service.AddEpisodeAsync("testfeed", episode);

        // Assert
        var episodes = await _service.GetEpisodesAsync("testfeed");
        Assert.Single(episodes);
        Assert.Equal("abc123", episodes[0].VideoId);
        Assert.Equal("YouTube Video Title", episodes[0].YoutubeTitle);
        Assert.Equal(0.92, episodes[0].MatchScore);
    }
}
