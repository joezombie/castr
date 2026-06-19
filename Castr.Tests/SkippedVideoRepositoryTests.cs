using Xunit;
using Moq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Castr.Data;
using Castr.Data.Entities;
using Castr.Data.Repositories;

namespace Castr.Tests;

/// <summary>
/// Tests for SkippedVideoRepository, focused on the bulk MarkVideosSkippedAsync path. Uses a real
/// SQLite (in-memory) database so the unique index on (feed_id, video_id) is enforced, exercising the
/// per-row fallback on a constraint race.
/// </summary>
public class SkippedVideoRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly CastrDbContext _context;
    private readonly SkippedVideoRepository _repository;
    private readonly int _feedId;

    public SkippedVideoRepositoryTests()
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

        _repository = new SkippedVideoRepository(_context, Mock.Of<ILogger<SkippedVideoRepository>>());
    }

    [Fact]
    public async Task MarkVideosSkippedAsync_InsertsAllRowsInBulk()
    {
        var skips = new[]
        {
            ("vid1", "keyword"),
            ("vid2", "keyword"),
            ("vid3", "keyword"),
        };

        var count = await _repository.MarkVideosSkippedAsync(_feedId, skips, "hash-a");

        Assert.Equal(3, count);
        var ids = await _repository.GetSkippedVideoIdsAsync(_feedId);
        Assert.Equal(new HashSet<string> { "vid1", "vid2", "vid3" }, ids);
    }

    [Fact]
    public async Task MarkVideosSkippedAsync_EmptyInput_ReturnsZero()
    {
        var count = await _repository.MarkVideosSkippedAsync(_feedId, Array.Empty<(string, string)>(), "hash-a");
        Assert.Equal(0, count);
        Assert.Empty(await _repository.GetSkippedVideoIdsAsync(_feedId));
    }

    [Fact]
    public async Task MarkVideosSkippedAsync_DuplicateInBatch_FallsBackAndStillRecordsAll()
    {
        // Pre-seed one row so the bulk insert hits the unique (feed_id, video_id) constraint, forcing
        // the per-row idempotent fallback. All requested videos must still end up recorded.
        await _repository.MarkVideoSkippedAsync(_feedId, "vid1", "keyword", "hash-old");

        var skips = new[]
        {
            ("vid1", "keyword"),
            ("vid2", "keyword"),
        };

        var count = await _repository.MarkVideosSkippedAsync(_feedId, skips, "hash-new");

        Assert.Equal(2, count);
        var ids = await _repository.GetSkippedVideoIdsAsync(_feedId);
        Assert.Equal(new HashSet<string> { "vid1", "vid2" }, ids);

        // The fallback upsert refreshes the existing row's hash to the new value.
        var vid1 = await _context.SkippedVideos.SingleAsync(s => s.FeedId == _feedId && s.VideoId == "vid1");
        Assert.Equal("hash-new", vid1.FilterHash);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
