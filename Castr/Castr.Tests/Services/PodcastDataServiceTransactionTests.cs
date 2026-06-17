using Castr.Data;
using Castr.Data.Entities;
using Castr.Data.Repositories;
using Castr.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Castr.Tests.Services;

/// <summary>
/// Transaction/rollback tests for <see cref="PodcastDataService.ClearFeedEpisodeDataAsync"/>.
/// These use SQLite rather than the InMemory provider because rollback requires a real relational
/// transaction (the InMemory provider does not support transactions).
/// </summary>
public class PodcastDataServiceTransactionTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly CastrDbContext _context;
    private readonly FeedRepository _feedRepo;
    private readonly EpisodeRepository _episodeRepo;
    private readonly ActivityRepository _activityRepo;

    public PodcastDataServiceTransactionTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"castr_tx_test_{Guid.NewGuid()}.db");
        var options = new DbContextOptionsBuilder<CastrDbContext>()
            .UseSqlite($"Data Source={_tempDbPath}")
            .Options;
        _context = new CastrDbContext(options);
        _context.Database.Migrate();

        _feedRepo = new FeedRepository(_context);
        _episodeRepo = new EpisodeRepository(_context);
        _activityRepo = new ActivityRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        if (File.Exists(_tempDbPath))
            File.Delete(_tempDbPath);
    }

    [Fact]
    public async Task ClearFeedEpisodeDataAsync_RollsBack_WhenSecondDeleteFails()
    {
        // Arrange: real episode repo (deletes succeed) + a download repo mock that throws,
        // simulating a mid-operation failure after episodes have already been deleted.
        var feedId = await _feedRepo.AddAsync(new Feed { Name = "rollback", Title = "T", Description = "D", Directory = "/d" });
        await _episodeRepo.AddAsync(new Episode { FeedId = feedId, Filename = "e1.mp3", DisplayOrder = 1 });
        await _episodeRepo.AddAsync(new Episode { FeedId = feedId, Filename = "e2.mp3", DisplayOrder = 2 });

        var downloadRepoMock = new Mock<IDownloadRepository>();
        downloadRepoMock
            .Setup(r => r.DeleteDownloadedVideosByFeedIdAsync(feedId))
            .ThrowsAsync(new InvalidOperationException("simulated mid-operation failure"));

        var service = new PodcastDataService(
            _context,
            _feedRepo,
            _episodeRepo,
            downloadRepoMock.Object,
            _activityRepo,
            NullLogger<PodcastDataService>.Instance);

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ClearFeedEpisodeDataAsync(feedId));

        // Assert: the episode delete was rolled back, so both episodes are still present,
        // and no activity log was committed.
        using var verifyContext = new CastrDbContext(
            new DbContextOptionsBuilder<CastrDbContext>().UseSqlite($"Data Source={_tempDbPath}").Options);
        var remainingEpisodes = await verifyContext.Episodes.Where(e => e.FeedId == feedId).ToListAsync();
        Assert.Equal(2, remainingEpisodes.Count);
        var logs = await verifyContext.ActivityLogs.Where(a => a.FeedId == feedId && a.ActivityType == "clear_resync").ToListAsync();
        Assert.Empty(logs);
    }

    [Fact]
    public async Task ClearFeedEpisodeDataAsync_ClearsEpisodesAndTracking_AndLogsActivity()
    {
        // Arrange
        var feedId = await _feedRepo.AddAsync(new Feed { Name = "clear", Title = "T", Description = "D", Directory = "/d" });
        await _episodeRepo.AddAsync(new Episode { FeedId = feedId, Filename = "e1.mp3", DisplayOrder = 1 });
        await _episodeRepo.AddAsync(new Episode { FeedId = feedId, Filename = "e2.mp3", DisplayOrder = 2 });
        var downloadRepo = new DownloadRepository(_context, NullLogger<DownloadRepository>.Instance);
        await downloadRepo.MarkVideoDownloadedAsync(feedId, "v1", "e1.mp3");
        await downloadRepo.MarkVideoDownloadedAsync(feedId, "v2", "e2.mp3");

        var service = CreateService(downloadRepo);

        // Act
        var result = await service.ClearFeedEpisodeDataAsync(feedId);

        // Assert
        Assert.Equal(2, result.EpisodesCleared);
        Assert.Equal(2, result.TrackingRowsCleared);
        Assert.Empty(await _episodeRepo.GetByFeedIdAsync(feedId));
        Assert.Empty(await downloadRepo.GetDownloadedVideoIdsAsync(feedId));

        var activities = await _activityRepo.GetRecentAsync(feedId);
        Assert.Single(activities);
        Assert.Equal("clear_resync", activities[0].ActivityType);
    }

    [Fact]
    public async Task ClearFeedEpisodeDataAsync_DoesNotAffectOtherFeeds()
    {
        // Arrange
        var targetFeedId = await _feedRepo.AddAsync(new Feed { Name = "target", Title = "T", Description = "D", Directory = "/d" });
        var otherFeedId = await _feedRepo.AddAsync(new Feed { Name = "other", Title = "O", Description = "D", Directory = "/o" });
        await _episodeRepo.AddAsync(new Episode { FeedId = targetFeedId, Filename = "t.mp3", DisplayOrder = 1 });
        await _episodeRepo.AddAsync(new Episode { FeedId = otherFeedId, Filename = "o.mp3", DisplayOrder = 1 });
        var downloadRepo = new DownloadRepository(_context, NullLogger<DownloadRepository>.Instance);
        await downloadRepo.MarkVideoDownloadedAsync(targetFeedId, "tv", "t.mp3");
        await downloadRepo.MarkVideoDownloadedAsync(otherFeedId, "ov", "o.mp3");

        var service = CreateService(downloadRepo);

        // Act
        await service.ClearFeedEpisodeDataAsync(targetFeedId);

        // Assert - other feed untouched
        Assert.Single(await _episodeRepo.GetByFeedIdAsync(otherFeedId));
        Assert.Single(await downloadRepo.GetDownloadedVideoIdsAsync(otherFeedId));
    }

    [Fact]
    public async Task ClearFeedEpisodeDataAsync_ReturnsZeroCounts_WhenNothingToClear()
    {
        // Arrange
        var feedId = await _feedRepo.AddAsync(new Feed { Name = "empty", Title = "T", Description = "D", Directory = "/d" });
        var downloadRepo = new DownloadRepository(_context, NullLogger<DownloadRepository>.Instance);
        var service = CreateService(downloadRepo);

        // Act
        var result = await service.ClearFeedEpisodeDataAsync(feedId);

        // Assert
        Assert.Equal(0, result.EpisodesCleared);
        Assert.Equal(0, result.TrackingRowsCleared);
        // Activity is still logged for auditability
        var activities = await _activityRepo.GetRecentAsync(feedId);
        Assert.Single(activities);
        Assert.Equal("clear_resync", activities[0].ActivityType);
    }

    private PodcastDataService CreateService(DownloadRepository downloadRepo) => new(
        _context,
        _feedRepo,
        _episodeRepo,
        downloadRepo,
        _activityRepo,
        NullLogger<PodcastDataService>.Instance);

    [Fact]
    public async Task ClearFeedEpisodeDataAsync_CommitsAllChanges_OnSuccessWithRelationalProvider()
    {
        // Arrange
        var feedId = await _feedRepo.AddAsync(new Feed { Name = "commit", Title = "T", Description = "D", Directory = "/d" });
        await _episodeRepo.AddAsync(new Episode { FeedId = feedId, Filename = "e1.mp3", DisplayOrder = 1 });
        var downloadRepo = new DownloadRepository(_context, NullLogger<DownloadRepository>.Instance);
        await downloadRepo.MarkVideoDownloadedAsync(feedId, "v1", "e1.mp3");

        var service = new PodcastDataService(
            _context,
            _feedRepo,
            _episodeRepo,
            downloadRepo,
            _activityRepo,
            NullLogger<PodcastDataService>.Instance);

        // Act
        var result = await service.ClearFeedEpisodeDataAsync(feedId);

        // Assert: changes are committed and visible from a fresh context
        Assert.Equal(1, result.EpisodesCleared);
        Assert.Equal(1, result.TrackingRowsCleared);

        using var verifyContext = new CastrDbContext(
            new DbContextOptionsBuilder<CastrDbContext>().UseSqlite($"Data Source={_tempDbPath}").Options);
        Assert.Empty(await verifyContext.Episodes.Where(e => e.FeedId == feedId).ToListAsync());
        Assert.Empty(await verifyContext.DownloadedVideos.Where(d => d.FeedId == feedId).ToListAsync());
        var logs = await verifyContext.ActivityLogs.Where(a => a.FeedId == feedId && a.ActivityType == "clear_resync").ToListAsync();
        Assert.Single(logs);
    }
}
