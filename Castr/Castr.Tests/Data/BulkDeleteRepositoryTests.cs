using Castr.Data;
using Castr.Data.Entities;
using Castr.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Castr.Tests.Data;

/// <summary>
/// Tests for the bulk delete repository methods. These use SQLite (not the InMemory provider)
/// because <c>ExecuteDeleteAsync</c> issues a relational DELETE statement that the InMemory
/// provider does not support.
/// </summary>
public class BulkDeleteRepositoryTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly CastrDbContext _context;
    private readonly FeedRepository _feedRepo;
    private readonly EpisodeRepository _episodeRepo;
    private readonly DownloadRepository _downloadRepo;

    public BulkDeleteRepositoryTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"castr_bulkdelete_test_{Guid.NewGuid()}.db");
        var options = new DbContextOptionsBuilder<CastrDbContext>()
            .UseSqlite($"Data Source={_tempDbPath}")
            .Options;
        _context = new CastrDbContext(options);
        _context.Database.Migrate();

        _feedRepo = new FeedRepository(_context);
        _episodeRepo = new EpisodeRepository(_context);
        _downloadRepo = new DownloadRepository(_context, new NullLogger<DownloadRepository>());
    }

    public void Dispose()
    {
        _context.Dispose();
        if (File.Exists(_tempDbPath))
            File.Delete(_tempDbPath);
    }

    [Fact]
    public async Task DeleteByFeedIdAsync_RemovesOnlyTargetFeedEpisodes()
    {
        // Arrange
        var targetFeedId = await _feedRepo.AddAsync(new Feed { Name = "target", Title = "T", Description = "D", Directory = "/d" });
        var otherFeedId = await _feedRepo.AddAsync(new Feed { Name = "other", Title = "O", Description = "D", Directory = "/o" });

        await _episodeRepo.AddAsync(new Episode { FeedId = targetFeedId, Filename = "t1.mp3", DisplayOrder = 1 });
        await _episodeRepo.AddAsync(new Episode { FeedId = targetFeedId, Filename = "t2.mp3", DisplayOrder = 2 });
        await _episodeRepo.AddAsync(new Episode { FeedId = otherFeedId, Filename = "o1.mp3", DisplayOrder = 1 });

        // Act
        var deleted = await _episodeRepo.DeleteByFeedIdAsync(targetFeedId);

        // Assert
        Assert.Equal(2, deleted);
        Assert.Empty(await _episodeRepo.GetByFeedIdAsync(targetFeedId));
        var otherEpisodes = await _episodeRepo.GetByFeedIdAsync(otherFeedId);
        Assert.Single(otherEpisodes);
        Assert.Equal("o1.mp3", otherEpisodes[0].Filename);
    }

    [Fact]
    public async Task DeleteByFeedIdAsync_ReturnsZeroWhenNoEpisodes()
    {
        // Arrange
        var feedId = await _feedRepo.AddAsync(new Feed { Name = "empty", Title = "T", Description = "D", Directory = "/d" });

        // Act
        var deleted = await _episodeRepo.DeleteByFeedIdAsync(feedId);

        // Assert
        Assert.Equal(0, deleted);
    }

    [Fact]
    public async Task DeleteDownloadedVideosByFeedIdAsync_RemovesOnlyTargetFeedRows()
    {
        // Arrange
        var targetFeedId = await _feedRepo.AddAsync(new Feed { Name = "target", Title = "T", Description = "D", Directory = "/d" });
        var otherFeedId = await _feedRepo.AddAsync(new Feed { Name = "other", Title = "O", Description = "D", Directory = "/o" });

        await _downloadRepo.MarkVideoDownloadedAsync(targetFeedId, "v1", "t1.mp3");
        await _downloadRepo.MarkVideoDownloadedAsync(targetFeedId, "v2", "t2.mp3");
        await _downloadRepo.MarkVideoDownloadedAsync(otherFeedId, "v3", "o1.mp3");

        // Act
        var deleted = await _downloadRepo.DeleteDownloadedVideosByFeedIdAsync(targetFeedId);

        // Assert
        Assert.Equal(2, deleted);
        Assert.Empty(await _downloadRepo.GetDownloadedVideoIdsAsync(targetFeedId));
        var otherIds = await _downloadRepo.GetDownloadedVideoIdsAsync(otherFeedId);
        Assert.Single(otherIds);
        Assert.Contains("v3", otherIds);
    }

    [Fact]
    public async Task DeleteDownloadedVideosByFeedIdAsync_ReturnsZeroWhenNoRows()
    {
        // Arrange
        var feedId = await _feedRepo.AddAsync(new Feed { Name = "empty", Title = "T", Description = "D", Directory = "/d" });

        // Act
        var deleted = await _downloadRepo.DeleteDownloadedVideosByFeedIdAsync(feedId);

        // Assert
        Assert.Equal(0, deleted);
    }
}
