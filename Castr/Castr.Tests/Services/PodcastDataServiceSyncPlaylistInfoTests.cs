using Castr.Data;
using Castr.Data.Entities;
using Castr.Data.Repositories;
using Castr.Models;
using Castr.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Castr.Tests.Services;

/// <summary>
/// Tests for <see cref="PodcastDataService.SyncPlaylistInfoAsync"/> metadata writing, covering the
/// Clear &amp; Resync thumbnail/date regression: when a <see cref="PlaylistVideoInfo"/> carries a
/// thumbnail and upload date they must be written to new rows, and a non-null thumbnail must heal an
/// existing row whose thumbnail_url is currently null.
/// </summary>
public class PodcastDataServiceSyncPlaylistInfoTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly string _tempDir;
    private readonly CastrDbContext _context;
    private readonly PodcastDataService _service;

    public PodcastDataServiceSyncPlaylistInfoTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"castr_sync_test_{Guid.NewGuid()}.db");
        _tempDir = Path.Combine(Path.GetTempPath(), $"castr_sync_dir_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);

        var options = new DbContextOptionsBuilder<CastrDbContext>()
            .UseSqlite($"Data Source={_tempDbPath}")
            .Options;
        _context = new CastrDbContext(options);
        _context.Database.Migrate();

        var feedRepo = new FeedRepository(_context);
        var episodeRepo = new EpisodeRepository(_context);
        var downloadRepo = new DownloadRepository(_context, NullLogger<DownloadRepository>.Instance);
        var activityRepo = new ActivityRepository(_context);

        _service = new PodcastDataService(
            _context, feedRepo, episodeRepo, downloadRepo, activityRepo,
            NullLogger<PodcastDataService>.Instance);
    }

    public void Dispose()
    {
        _context.Dispose();
        if (File.Exists(_tempDbPath)) File.Delete(_tempDbPath);
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    private async Task<int> SeedFeedAsync()
    {
        var feed = new Feed
        {
            Name = "test",
            Title = "Test",
            Description = "desc",
            Directory = _tempDir
        };
        _context.Feeds.Add(feed);
        await _context.SaveChangesAsync();
        return feed.Id;
    }

    private string CreateFile(string name)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, "x");
        return name;
    }

    [Fact]
    public async Task SyncPlaylistInfoAsync_WritesThumbnailAndDate_OnNewEpisode()
    {
        var feedId = await SeedFeedAsync();
        CreateFile("Episode One.mp3");

        var uploadDate = new DateTime(2021, 5, 4, 0, 0, 0, DateTimeKind.Utc);
        var videos = new[]
        {
            new PlaylistVideoInfo
            {
                VideoId = "vid1",
                Title = "Episode One",
                ThumbnailUrl = "https://img/thumb1.jpg",
                UploadDate = uploadDate,
                PlaylistIndex = 1
            }
        };

        await _service.SyncPlaylistInfoAsync(feedId, videos, _tempDir);

        var episode = await _context.Episodes.SingleAsync(e => e.FeedId == feedId);
        Assert.Equal("https://img/thumb1.jpg", episode.ThumbnailUrl);
        Assert.Equal(uploadDate, episode.PublishDate);
    }

    [Fact]
    public async Task SyncPlaylistInfoAsync_HealsNullThumbnail_OnExistingEpisode()
    {
        var feedId = await SeedFeedAsync();
        var filename = CreateFile("Episode One.mp3");

        // Existing row from a prior broken sync: matched video but null thumbnail/date.
        _context.Episodes.Add(new Episode
        {
            FeedId = feedId,
            Filename = filename,
            VideoId = "vid1",
            YoutubeTitle = "Episode One",
            Title = "Episode One",
            ThumbnailUrl = null,
            PublishDate = null,
            DisplayOrder = 1,
            AddedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var videos = new[]
        {
            new PlaylistVideoInfo
            {
                VideoId = "vid1",
                Title = "Episode One",
                ThumbnailUrl = "https://img/healed.jpg",
                UploadDate = null, // dates not enriched on this path
                PlaylistIndex = 1
            }
        };

        await _service.SyncPlaylistInfoAsync(feedId, videos, _tempDir);

        var episode = await _context.Episodes.SingleAsync(e => e.FeedId == feedId);
        Assert.Equal("https://img/healed.jpg", episode.ThumbnailUrl);
    }
}
