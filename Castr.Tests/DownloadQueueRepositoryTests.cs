using Xunit;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Castr.Data;
using Castr.Data.Entities;
using Castr.Data.Repositories;

namespace Castr.Tests;

/// <summary>
/// Tests for the DownloadQueue side of <see cref="DownloadRepository"/>: enqueue idempotency,
/// status/timestamp transitions, and retention-based cleanup. Uses a real SQLite (in-memory)
/// database so the unique index on (feed_id, video_id) is enforced.
/// </summary>
public class DownloadQueueRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly CastrDbContext _context;
    private readonly DownloadRepository _repository;
    private readonly int _feedId;

    public DownloadQueueRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<CastrDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new CastrDbContext(options);
        _context.Database.EnsureCreated();

        var feed = new Feed
        {
            Name = "test",
            Title = "Test Feed",
            Description = "desc",
            Directory = "/tmp"
        };
        _context.Feeds.Add(feed);
        _context.SaveChanges();
        _feedId = feed.Id;

        _repository = new DownloadRepository(_context, NullLogger<DownloadRepository>.Instance);
    }

    [Fact]
    public async Task AddToQueueAsync_IsIdempotentForSameFeedAndVideo()
    {
        var first = await _repository.AddToQueueAsync(_feedId, "vid1", "Title 1");
        var second = await _repository.AddToQueueAsync(_feedId, "vid1", "Title 1 again");

        Assert.Equal(first.Id, second.Id);
        var all = await _repository.GetQueueAsync(_feedId);
        Assert.Single(all);
        // The original row is returned unchanged (title from the first insert).
        Assert.Equal("Title 1", second.VideoTitle);
    }

    [Fact]
    public async Task UpdateQueueProgressAsync_SetsStartedAtOnFirstDownloading()
    {
        var item = await _repository.AddToQueueAsync(_feedId, "vid1", "T");
        Assert.Null(item.StartedAt);

        await _repository.UpdateQueueProgressAsync(item.Id, "downloading", 0, null);

        var reloaded = await _context.DownloadQueue.FindAsync(item.Id);
        Assert.NotNull(reloaded);
        Assert.Equal("downloading", reloaded!.Status);
        Assert.NotNull(reloaded.StartedAt);
        Assert.Null(reloaded.CompletedAt);
    }

    [Fact]
    public async Task UpdateQueueProgressAsync_SetsCompletedAtOnCompleted()
    {
        var item = await _repository.AddToQueueAsync(_feedId, "vid1", "T");
        await _repository.UpdateQueueProgressAsync(item.Id, "downloading", 0, null);
        await _repository.UpdateQueueProgressAsync(item.Id, "completed", 100, null);

        var reloaded = await _context.DownloadQueue.FindAsync(item.Id);
        Assert.NotNull(reloaded);
        Assert.Equal("completed", reloaded!.Status);
        Assert.Equal(100, reloaded.ProgressPercent);
        Assert.NotNull(reloaded.CompletedAt);
    }

    [Fact]
    public async Task UpdateQueueProgressAsync_SetsCompletedAtAndErrorOnFailed()
    {
        var item = await _repository.AddToQueueAsync(_feedId, "vid1", "T");
        await _repository.UpdateQueueProgressAsync(item.Id, "failed", 0, "boom");

        var reloaded = await _context.DownloadQueue.FindAsync(item.Id);
        Assert.NotNull(reloaded);
        Assert.Equal("failed", reloaded!.Status);
        Assert.Equal("boom", reloaded.ErrorMessage);
        Assert.NotNull(reloaded.CompletedAt);
    }

    [Fact]
    public async Task CleanupOldQueueItemsAsync_DeletesOnlyOldCompletedAndFailed()
    {
        var now = DateTime.UtcNow;

        // Old completed (should be deleted)
        _context.DownloadQueue.Add(new DownloadQueueItem
        {
            FeedId = _feedId, VideoId = "old-completed", Status = "completed",
            QueuedAt = now.AddDays(-30), CompletedAt = now.AddDays(-30)
        });
        // Recent completed (should be kept)
        _context.DownloadQueue.Add(new DownloadQueueItem
        {
            FeedId = _feedId, VideoId = "recent-completed", Status = "completed",
            QueuedAt = now.AddDays(-1), CompletedAt = now.AddDays(-1)
        });
        // Old failed (should be deleted)
        _context.DownloadQueue.Add(new DownloadQueueItem
        {
            FeedId = _feedId, VideoId = "old-failed", Status = "failed",
            QueuedAt = now.AddDays(-60), CompletedAt = now.AddDays(-60)
        });
        // Recent failed (should be kept)
        _context.DownloadQueue.Add(new DownloadQueueItem
        {
            FeedId = _feedId, VideoId = "recent-failed", Status = "failed",
            QueuedAt = now.AddDays(-5), CompletedAt = now.AddDays(-5)
        });
        // Queued / downloading are never cleaned regardless of age
        _context.DownloadQueue.Add(new DownloadQueueItem
        {
            FeedId = _feedId, VideoId = "queued", Status = "queued",
            QueuedAt = now.AddDays(-90)
        });
        _context.DownloadQueue.Add(new DownloadQueueItem
        {
            FeedId = _feedId, VideoId = "downloading", Status = "downloading",
            QueuedAt = now.AddDays(-90), StartedAt = now.AddDays(-90)
        });
        await _context.SaveChangesAsync();

        var deleted = await _repository.CleanupOldQueueItemsAsync(
            completedRetention: TimeSpan.FromDays(7),
            failedRetention: TimeSpan.FromDays(30));

        Assert.Equal(2, deleted);

        var remaining = (await _repository.GetQueueAsync(_feedId)).Select(q => q.VideoId).ToHashSet();
        Assert.Equal(
            new HashSet<string> { "recent-completed", "recent-failed", "queued", "downloading" },
            remaining);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
